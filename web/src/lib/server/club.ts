import "server-only";
import { randomInt } from "node:crypto";
import { ApiError } from "./api";
import { audit } from "./audit";
import { db, type Tx } from "./db";
import { getGameConfig } from "./game-config";
import { grant } from "./ledger";
import { activeSeason } from "./league";
import {
  applyPositionPenalty,
  overall,
  rng,
  starterAttributes,
  type Attributes,
  type Position,
} from "@/lib/domain/rules";

export const SUB_POSITIONS: Record<Position, Position[]> = { GK: [], DF: ["MF"], MF: ["DF", "FW"], FW: ["MF"] };

export function attrsOf(a: {
  speed: number; acceleration: number; agility: number; ballControl: number; shortPass: number;
  longPass: number; shotPower: number; finishing: number; tackling: number; goalkeeping: number;
}): Attributes {
  const { speed, acceleration, agility, ballControl, shortPass, longPass, shotPower, finishing, tackling, goalkeeping } = a;
  return { speed, acceleration, agility, ballControl, shortPass, longPass, shotPower, finishing, tackling, goalkeeping };
}

export interface CreateClubInput {
  name: string;
  shortName: string;
  logo: { shape: string; symbol: string };
  colors: [string, string, string];
}

/**
 * Club creation + starter allocation in one transaction (prompt 14, Full Spec 2.4):
 * Division 10 / RP 0, 8 Common players drawn at random from the operator's starter pool
 * (2 per position, so 5-a-side always has a GK, DFs, MF and FW), a default 2-1-1 squad and
 * the 500 NFWL Points starter grant. Users cannot choose or generate their starters.
 */
export async function createClubWithStarters(userId: string, input: CreateClubInput, ip?: string) {
  const cfg = await getGameConfig();
  const seed = randomInt(0, 2 ** 31 - 1);
  const r = rng(seed);

  return db.$transaction(async (tx) => {
    const existing = await tx.club.findUnique({ where: { ownerId: userId } });
    if (existing) throw new ApiError(409, "CLUB_EXISTS");

    const club = await tx.club.create({
      data: {
        ownerId: userId,
        name: input.name,
        shortName: input.shortName,
        logo: input.logo,
        colors: input.colors,
        divisionId: 10,
        onboardingStep: 1,
      },
    });

    const players = await allocateStarters(tx, club.id, r, cfg);
    const byPos = (p: Position) => players.filter((x) => x.position === p).sort((a, b) => b.ovr - a.ovr);
    const starters = [byPos("GK")[0], byPos("DF")[0], byPos("DF")[1], byPos("MF")[0], byPos("FW")[0]].map((p) => p.id);
    const bench = players.filter((p) => !starters.includes(p.id)).slice(0, 3).map((p) => p.id);
    await tx.squad.create({ data: { clubId: club.id, formationId: "2-1-1", starters, bench } });

    await grant(tx, userId, "NFWL_POINTS", cfg.starter.points, `starter-points:${userId}`, "STARTER_GRANT", "Stage 1 NFWL Points (not transferable, not cashable)");

    const season = await activeSeason(tx);
    if (season) await tx.leagueStanding.create({ data: { seasonId: season.id, divisionId: 10, clubId: club.id } });

    await tx.userAchievement.upsert({
      where: { userId_achievementId: { userId, achievementId: "first-club" } },
      update: {},
      create: { userId, achievementId: "first-club" },
    });
    await audit("club.create", { userId, target: club.id, data: { seed, players: players.map((p) => p.id) }, ip }, tx);
    return { club, players };
  });
}

async function allocateStarters(tx: Tx, clubId: string, r: ReturnType<typeof rng>, cfg: Awaited<ReturnType<typeof getGameConfig>>) {
  const out: { id: string; position: Position; ovr: number }[] = [];
  for (const [pos, count] of Object.entries(cfg.starter.positions) as [Position, number][]) {
    const pool = await tx.playerTemplate.findMany({ where: { pool: cfg.starter.pool, position: pos, active: true } });
    if (pool.length < count) throw new Error(`starter pool has only ${pool.length} ${pos} templates`);
    // Fisher-Yates on a copy, then take `count` distinct templates.
    const p = [...pool];
    for (let i = p.length - 1; i > 0; i--) {
      const j = r.int(0, i + 1);
      [p[i], p[j]] = [p[j], p[i]];
    }
    for (const t of p.slice(0, count)) {
      const a = starterAttributes(pos, r, { minOvr: cfg.starter.minOvr, maxOvr: cfg.starter.maxOvr, cap: cfg.rarity.Common.attributeCap });
      const player = await tx.player.create({
        data: {
          templateId: t.id,
          name: t.name,
          position: pos,
          subPositions: SUB_POSITIONS[pos],
          rarity: "Common",
          crystalType: t.crystalType,
          preferredFoot: r.next() < 0.25 ? "Left" : "Right",
          weakFoot: 2 + r.int(0, 3),
          level: 1,
          levelCap: cfg.levelCaps.steps[0],
          origin: "STARTER",
          appearance: t.appearance ?? {},
          attributes: { create: a },
          ownership: { create: { clubId } },
        },
      });
      out.push({ id: player.id, position: pos, ovr: overall(a, pos) });
    }
  }
  return out;
}

export const playerInclude = {
  attributes: true,
  skills: { include: { skill: true }, orderBy: { slot: "asc" as const } },
  nft: true,
} as const;

/** Roster of a club with attributes and OVR. */
export async function getRoster(clubId: string) {
  const rows = await db.clubPlayer.findMany({
    where: { clubId, active: true },
    include: { player: { include: playerInclude } },
    orderBy: { joinedAt: "asc" },
  });
  return rows.map((r) => {
    const p = r.player;
    const a = attrsOf(p.attributes!);
    return { ...p, attrs: a, ovr: overall(a, p.position as Position), locked: !!(r.lockedUntil && r.lockedUntil > new Date()) };
  });
}
export type RosterPlayer = Awaited<ReturnType<typeof getRoster>>[number];

export const FORMATION_ROLES: Position[] = ["GK", "DF", "DF", "MF", "FW"];

/** Validates a lineup against the roster. Returns per-slot penalty info for the UI and Unity. */
export function evaluateLineup(roster: RosterPlayer[], starters: string[], cfg: Awaited<ReturnType<typeof getGameConfig>>) {
  const errors: string[] = [];
  if (starters.length !== 5) errors.push("スタメンは5人必要です");
  if (new Set(starters).size !== starters.length) errors.push("同じ選手が重複しています");
  const slots = starters.map((id, i) => {
    const p = roster.find((x) => x.id === id);
    if (!p) {
      errors.push("クラブに所属していない選手が含まれています");
      return null;
    }
    if (p.locked) errors.push(`${p.name} は取引ロック中です`);
    const role = FORMATION_ROLES[i];
    const pen = applyPositionPenalty(p.attrs, p.position as Position, p.subPositions as Position[], role, cfg.penalties);
    return { slot: i, role, player: p, ...pen, ovrInSlot: overall(pen.attributes, role) };
  });
  if (!slots.some((s) => s && s.player.position === "GK")) errors.push("GKの選手を最低1人入れてください");
  return { ok: errors.length === 0, errors, slots };
}
