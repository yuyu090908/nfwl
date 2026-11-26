import "server-only";
import { randomInt } from "node:crypto";
import { Prisma } from "@/generated/prisma/client";
import { BRIDGE_SCHEMA_VERSION, type BridgeMessage, type UnityMatchConfig, type UnityPlayer } from "@/lib/bridge/protocol";
import { addXp, matchXp, overall, randomAttributes, rng, CRYSTAL_TYPES, type Position } from "@/lib/domain/rules";
import { practiceHalfOptions } from "@/lib/domain/config";
import { validateMatch } from "@/lib/match/validate";
import { ApiError } from "./api";
import { audit } from "./audit";
import { evaluateLineup, getRoster, FORMATION_ROLES } from "./club";
import { db, type Tx } from "./db";
import { getGameConfig } from "./game-config";
import { activeSeason, applyToStandings } from "./league";

const CPU_NAMES = ["Orrin Vale", "Sel Dravik", "Kova Lune", "Iber Strand", "Tolu Nax", "Pyre Ostin", "Halden Ro", "Quill Mear", "Vash Ender", "Rook Selby"];
const DIFFICULTY_LEVEL = { beginner: 56, standard: 63, hard: 70 } as const;
export type Difficulty = keyof typeof DIFFICULTY_LEVEL;

const UNITY_KEYS: Record<string, string> = {
  speed: "Speed", acceleration: "Acceleration", agility: "Agility", ballControl: "BallControl", shortPass: "ShortPass",
  longPass: "LongPass", shotPower: "ShotPower", finishing: "Finishing", tackling: "Tackling", goalkeeping: "Goalkeeping",
};

/**
 * Starts a Practice vs CPU match: freezes the user's lineup (penalties applied, so Unity gets exactly
 * the values shown on the web), generates the CPU side on the server, and returns the config for Unity.
 */
export async function createPracticeSession(userId: string, halfSeconds: number, difficulty: Difficulty) {
  const cfg = await getGameConfig();
  if (!practiceHalfOptions(cfg).includes(halfSeconds)) throw new ApiError(400, "BAD_HALF_LENGTH");
  const club = await db.club.findUnique({ where: { ownerId: userId } });
  if (!club) throw new ApiError(409, "NO_CLUB");
  const squad = await db.squad.findFirst({ where: { clubId: club.id, isActive: true } });
  if (!squad) throw new ApiError(409, "NO_SQUAD");
  const roster = await getRoster(club.id);
  const lineup = evaluateLineup(roster, squad.starters, cfg);
  if (!lineup.ok) throw new ApiError(409, "INVALID_SQUAD", lineup.errors.join(" / "));

  // One live session at a time: older unfinished ones are aborted.
  const stale = await db.matchSession.findMany({ where: { userId, status: { in: ["CREATED", "LOADING", "IN_PLAY"] } } });
  for (const s of stale) await abortSession(s.id, "replaced");

  const seed = randomInt(0, 2 ** 31 - 1);
  const r = rng(seed);
  const level = DIFFICULTY_LEVEL[difficulty];
  const skillMap = new Map((await db.skill.findMany()).map((s) => [s.id, s.unitySkillId]));

  const home: UnityPlayer[] = lineup.slots.map((s, i) => {
    const p = s!.player;
    return {
      tokenId: p.id,
      name: p.name.toUpperCase(),
      rarity: p.rarity,
      element: p.crystalType,
      number: [1, 4, 5, 8, 10][i],
      attributes: {
        ...Object.fromEntries(Object.entries(s!.attributes).map(([k, v]) => [UNITY_KEYS[k], v])),
        PreferredFoot: p.preferredFoot,
        WeakFoot: p.weakFoot,
      },
      // Only skills the Unity build implements; Unity also clamps to its own slot count.
      skills: p.skills.map((x) => skillMap.get(x.skillId)).filter((x): x is string => !!x),
    };
  });
  const away: UnityPlayer[] = FORMATION_ROLES.map((role, i) => {
    const a = randomAttributes(level, r);
    if (role === "GK") a.goalkeeping = Math.min(99, level + 8);
    return {
      tokenId: `cpu-${i}`,
      name: r.pick(CPU_NAMES).toUpperCase(),
      rarity: "Common",
      element: r.pick(CRYSTAL_TYPES),
      number: [1, 3, 6, 7, 9][i],
      attributes: Object.fromEntries(Object.entries(a).map(([k, v]) => [UNITY_KEYS[k], v])),
      skills: [],
    };
  });

  const season = await activeSeason();
  const match = await db.match.create({
    data: {
      seasonId: season?.id ?? null,
      mode: "PRACTICE_CPU",
      status: "IN_PROGRESS",
      homeClubId: club.id,
      cpuOpponent: { name: `CPU ${difficulty.toUpperCase()}`, difficulty, level },
      kickoffAt: new Date(),
      seed,
    },
  });
  const sessionId = crypto.randomUUID();
  const config: UnityMatchConfig = {
    schemaVersion: BRIDGE_SCHEMA_VERSION,
    sessionId,
    userId,
    matchId: match.id,
    mode: "PRACTICE_CPU",
    halfLengthSeconds: halfSeconds,
    formation: "2-1-1",
    homeName: club.name.toUpperCase(),
    homeShort: club.shortName,
    awayName: `CPU ${difficulty.toUpperCase()}`,
    awayShort: "CPU",
    home,
    away,
    cosmetics: { homeKit: "home", awayKit: "away", clubColors: (club.colors as string[]) ?? [] },
  };
  await db.matchSession.create({
    data: {
      id: sessionId,
      matchId: match.id,
      userId,
      clientConfig: config as unknown as Prisma.InputJsonValue,
      halfSeconds,
      expiresAt: new Date(Date.now() + cfg.match.sessionTtlMinutes * 60_000),
    },
  });
  await audit("match.session_create", { userId, target: match.id, data: { sessionId, halfSeconds, difficulty } });
  return config;
}

export async function abortSession(sessionId: string, reason: string) {
  await db.$transaction(async (tx) => {
    const s = await tx.matchSession.findUnique({ where: { id: sessionId } });
    if (!s || ["FINISHED", "ABORTED", "EXPIRED", "REJECTED"].includes(s.status)) return;
    await tx.matchSession.update({ where: { id: sessionId }, data: { status: "ABORTED" } });
    await tx.match.update({ where: { id: s.matchId }, data: { status: "VOID", anomalies: [{ code: "ABORTED", detail: reason }] } });
  });
}

type IngestResult = { accepted: true; duplicate?: boolean; status: string; result?: unknown };

/** Stores one bridge message (idempotent by messageId) and advances the session state machine. */
export async function ingestEvent(userId: string, msg: BridgeMessage): Promise<IngestResult> {
  const cfg = await getGameConfig();
  return db.$transaction(
    async (tx) => {
      await tx.$queryRaw`SELECT id FROM "MatchSession" WHERE id = ${msg.sessionId}::uuid FOR UPDATE`;
      const s = await tx.matchSession.findUnique({ where: { id: msg.sessionId } });
      if (!s || s.userId !== userId) throw new ApiError(404, "SESSION_NOT_FOUND");

      const dup = await tx.matchEvent.findUnique({ where: { matchId_messageId: { matchId: s.matchId, messageId: msg.messageId } } });
      if (dup) return { accepted: true, duplicate: true, status: s.status };

      if (["FINISHED", "ABORTED", "EXPIRED", "REJECTED"].includes(s.status)) throw new ApiError(409, "SESSION_CLOSED");
      if (s.expiresAt < new Date()) {
        await tx.matchSession.update({ where: { id: s.id }, data: { status: "EXPIRED" } });
        await tx.match.update({ where: { id: s.matchId }, data: { status: "VOID" } });
        throw new ApiError(409, "SESSION_EXPIRED");
      }
      if (msg.seq <= s.lastSeq) throw new ApiError(409, "OUT_OF_ORDER");

      const needsPlay = ["GoalScored", "HalfTime", "PlayerSwitched", "MatchFinished"].includes(msg.eventType);
      if (needsPlay && s.status !== "IN_PLAY") throw new ApiError(409, "NOT_IN_PLAY");

      const now = new Date();
      const gameClock = "gameClock" in msg.payload ? (msg.payload as { gameClock: number }).gameClock : null;
      await tx.matchEvent.create({
        data: {
          receivedAt: now, // server clock: the client's own timing is never used
          matchId: s.matchId,
          messageId: msg.messageId,
          eventType: msg.eventType,
          seq: msg.seq,
          gameClock,
          payload: msg.payload as Prisma.InputJsonValue,
        },
      });

      const data: Prisma.MatchSessionUpdateInput = { lastSeq: msg.seq };
      switch (msg.eventType) {
        case "GameReady":
          if (s.status === "CREATED") data.status = "LOADING";
          data.loadedAt = s.loadedAt ?? now;
          break;
        case "MatchStarted":
          if (s.status === "CREATED" || s.status === "LOADING") {
            data.status = "IN_PLAY";
            data.startedAt = now;
          }
          break;
        case "GameError":
          await audit("match.client_error", { userId, target: s.matchId, severity: "warn", data: msg.payload }, tx);
          break;
        case "MatchFinished": {
          await tx.matchSession.update({ where: { id: s.id }, data: { ...data, status: "FINISHED", finishedAt: now } });
          const result = await finalize(tx, s.id, now, msg.payload, cfg);
          return { accepted: true, status: "FINISHED", result };
        }
      }
      const updated = await tx.matchSession.update({ where: { id: s.id }, data });
      return { accepted: true, status: updated.status };
    },
    { timeout: 20_000 },
  );
}

async function finalize(
  tx: Tx,
  sessionId: string,
  finishedAt: Date,
  final: { homeScore: number; awayScore: number; gameClock: number },
  cfg: Awaited<ReturnType<typeof getGameConfig>>,
) {
  const s = await tx.matchSession.findUniqueOrThrow({ where: { id: sessionId }, include: { match: { include: { season: true } } } });
  const events = await tx.matchEvent.findMany({ where: { matchId: s.matchId } });
  const v = validateMatch({
    halfSeconds: s.halfSeconds,
    startedAt: s.startedAt?.getTime() ?? null,
    finishedAt: finishedAt.getTime(),
    events: events.map((e) => ({ eventType: e.eventType, seq: e.seq ?? 0, receivedAt: e.receivedAt.getTime(), payload: e.payload as Record<string, unknown> })),
    final,
    clockToleranceSeconds: cfg.match.clockToleranceSeconds,
    maxGoalsPerTeam: cfg.match.maxGoalsPerTeam,
  });

  const m = s.match;
  const config = s.clientConfig as unknown as UnityMatchConfig;
  if (v.verdict === "REJECTED") {
    await tx.match.update({
      where: { id: m.id },
      data: { status: "REJECTED", verification: "REJECTED", homeScore: final.homeScore, awayScore: final.awayScore, finishedAt, anomalies: v.anomalies },
    });
    await tx.matchSession.update({ where: { id: sessionId }, data: { status: "REJECTED" } });
    await audit("match.result_rejected", { userId: s.userId, target: m.id, severity: "alert", data: { anomalies: v.anomalies, final } }, tx);
    return { verdict: v.verdict, anomalies: v.anomalies, xp: [] };
  }

  await tx.match.update({
    where: { id: m.id },
    data: {
      status: "FINISHED",
      verification: v.verdict,
      homeScore: final.homeScore,
      awayScore: final.awayScore,
      finishedAt,
      anomalies: v.anomalies,
    },
  });

  // Per-slot statistics for both sides.
  const minutes = Math.round((s.halfSeconds * 2) / 60);
  const goalsBy = (side: "home" | "away", slot: number) => v.goals.filter((g) => g.team === side && g.scorerSlot === slot).length;
  for (const side of ["home", "away"] as const) {
    const players = side === "home" ? config.home : config.away;
    for (let i = 0; i < players.length; i++) {
      await tx.matchStatistic.create({
        data: {
          matchId: m.id,
          side,
          slot: i,
          playerId: side === "home" ? players[i].tokenId : null,
          clubId: side === "home" ? m.homeClubId : null,
          goals: goalsBy(side, i),
          minutes,
        },
      });
    }
  }

  // Apply once: XP (non-economic), club XP, achievements, provisional standings in a beta season.
  const xp: { playerId: string; amount: number; levelTo: number }[] = [];
  let xpNote: string | null = null;
  if (v.verdict === "CLIENT_REPORTED" && !m.appliedAt) {
    const res = final.homeScore > final.awayScore ? "W" : final.homeScore === final.awayScore ? "D" : "L";
    const dayStart = new Date();
    dayStart.setUTCHours(0, 0, 0, 0);
    const todays = await tx.match.count({
      where: { homeClubId: m.homeClubId, mode: "PRACTICE_CPU", appliedAt: { gte: dayStart } },
    });
    if (todays >= cfg.xp.practiceDailyLimit) xpNote = `本日のCPU戦XP上限（${cfg.xp.practiceDailyLimit}試合）に達しています`;
    const club = await tx.club.findUniqueOrThrow({ where: { id: m.homeClubId! } });
    for (let i = 0; i < config.home.length && !xpNote; i++) {
      const pid = config.home[i].tokenId;
      const player = await tx.player.findUnique({ where: { id: pid } });
      if (!player) continue;
      const amount = matchXp(
        {
          minutes,
          result: res,
          goals: goalsBy("home", i),
          assists: 0,
          saves: 0,
          cleanSheet: final.awayScore === 0,
          position: FORMATION_ROLES[i] as Position,
        },
        m.mode,
        cfg.xp,
      );
      const next = addXp(player.level, player.xp, amount, player.levelCap);
      await tx.playerExperience.create({
        data: { playerId: pid, matchId: m.id, source: "match", amount, levelFrom: player.level, levelTo: next.level },
      });
      await tx.player.update({
        where: { id: pid },
        data: { level: next.level, xp: next.xp, growthPoints: { increment: next.levelsGained * 3 } },
      });
      await tx.matchStatistic.update({ where: { matchId_side_slot: { matchId: m.id, side: "home", slot: i } }, data: { xpAwarded: amount } });
      xp.push({ playerId: pid, amount, levelTo: next.level });
      if (next.level >= 10) await achieve(tx, club.ownerId, "level-10");
    }
    const clubXp = res === "W" ? 30 : res === "D" ? 15 : 10;
    await tx.rewardTransaction.create({
      data: { clubId: club.id, kind: "CLUB_XP", amount: clubXp, reason: "match", sourceRef: m.id, idempotencyKey: `clubxp:${m.id}` },
    });
    const newClubXp = club.clubXp + clubXp;
    await tx.club.update({ where: { id: club.id }, data: { clubXp: newClubXp, level: Math.floor(newClubXp / 200) + 1 } });
    await achieve(tx, club.ownerId, "first-match");
    if (res === "W") await achieve(tx, club.ownerId, "first-win");
    if (v.goals.some((g) => g.team === "home" && g.scorerSlot !== null && goalsBy("home", g.scorerSlot) >= 3)) await achieve(tx, club.ownerId, "hat-trick");

    // League: an official season never takes browser results. A beta season counts them as provisional.
    const counted = m.season && m.season.status === "ACTIVE" && !m.season.official;
    if (counted) {
      await applyToStandings(tx, { ...m, homeScore: final.homeScore, awayScore: final.awayScore }, true);
    }
    await tx.match.update({ where: { id: m.id }, data: { appliedAt: new Date() } });
  }
  await audit("match.finish", { userId: s.userId, target: m.id, data: { verdict: v.verdict, final, anomalies: v.anomalies } }, tx);
  return { verdict: v.verdict, anomalies: v.anomalies, xp, xpNote };
}

export async function achieve(tx: Tx, userId: string, achievementId: string) {
  await tx.userAchievement.upsert({
    where: { userId_achievementId: { userId, achievementId } },
    update: {},
    create: { userId, achievementId },
  });
}

export function clubOvr(slotOvrs: number[]) {
  return Math.round(slotOvrs.reduce((a, b) => a + b, 0) / Math.max(1, slotOvrs.length));
}
export { overall };
