import "server-only";
import { randomInt } from "node:crypto";
import { db, type Tx } from "./db";
import { getGameConfig } from "./game-config";
import { audit } from "./audit";
import { simulateMatch } from "@/lib/match/sim";
import { overall, type Position } from "@/lib/domain/rules";
import { attrsOf, FORMATION_ROLES } from "./club";
import { grant } from "./ledger";

export async function activeSeason(tx: Tx | typeof db = db) {
  return tx.season.findFirst({ where: { status: "ACTIVE" }, orderBy: { number: "desc" } });
}

/** Division for an RP value (Master Doc 4.1). Division 0 is never reached by RP. */
export function divisionForRp(rp: number): number {
  if (rp >= 900) return 1;
  return Math.max(1, 10 - Math.floor(Math.max(0, rp) / 100));
}

/**
 * Applies one finished match to standings, RP and division. Called once per match (guarded by Match.appliedAt).
 * `provisional` = counted in a beta season from a browser-reported result; RP never moves on provisional results.
 */
export async function applyToStandings(
  tx: Tx,
  m: { id: string; seasonId: number | null; homeClubId: string | null; awayClubId: string | null; homeScore: number; awayScore: number },
  provisional: boolean,
) {
  if (!m.seasonId) return;
  const cfg = await getGameConfig();
  const sides = [
    { clubId: m.homeClubId, gf: m.homeScore, ga: m.awayScore },
    { clubId: m.awayClubId, gf: m.awayScore, ga: m.homeScore },
  ];
  for (const s of sides) {
    if (!s.clubId) continue; // CPU side
    const club = await tx.club.findUniqueOrThrow({ where: { id: s.clubId } });
    const res = s.gf > s.ga ? "W" : s.gf === s.ga ? "D" : "L";
    const pts = res === "W" ? cfg.league.points.win : res === "D" ? cfg.league.points.draw : cfg.league.points.loss;
    await tx.leagueStanding.upsert({
      where: { seasonId_clubId: { seasonId: m.seasonId, clubId: s.clubId } },
      create: {
        seasonId: m.seasonId,
        clubId: s.clubId,
        divisionId: club.divisionId,
        played: 1,
        wins: res === "W" ? 1 : 0,
        draws: res === "D" ? 1 : 0,
        losses: res === "L" ? 1 : 0,
        goalsFor: s.gf,
        goalsAgainst: s.ga,
        points: pts,
        provisional: provisional ? 1 : 0,
      },
      update: {
        played: { increment: 1 },
        wins: { increment: res === "W" ? 1 : 0 },
        draws: { increment: res === "D" ? 1 : 0 },
        losses: { increment: res === "L" ? 1 : 0 },
        goalsFor: { increment: s.gf },
        goalsAgainst: { increment: s.ga },
        points: { increment: pts },
        provisional: { increment: provisional ? 1 : 0 },
      },
    });
    if (!provisional) {
      const delta = res === "W" ? cfg.league.rp.win : res === "D" ? cfg.league.rp.draw : cfg.league.rp.loss;
      const rp = Math.max(0, club.rp + delta);
      const divisionId = club.divisionId === 0 ? 0 : divisionForRp(rp);
      await tx.club.update({ where: { id: club.id }, data: { rp, divisionId } });
      if (divisionId !== club.divisionId) {
        await tx.leagueStanding.update({ where: { seasonId_clubId: { seasonId: m.seasonId, clubId: club.id } }, data: { divisionId } });
        await audit("league.division_change", { userId: club.ownerId, target: club.id, data: { from: club.divisionId, to: divisionId, rp } }, tx);
      }
    }
  }
}

/** Starting five OVR per slot of a club's active squad (with position penalties applied). */
export async function squadStrength(tx: Tx | typeof db, clubId: string): Promise<number[] | null> {
  const squad = await tx.squad.findFirst({ where: { clubId, isActive: true } });
  if (!squad || squad.starters.length !== 5) return null;
  const players = await tx.player.findMany({ where: { id: { in: squad.starters } }, include: { attributes: true } });
  return squad.starters.map((id, i) => {
    const p = players.find((x) => x.id === id);
    return p?.attributes ? overall(attrsOf(p.attributes), FORMATION_ROLES[i] as Position) : 50;
  });
}

/**
 * Creates the next round of league fixtures: clubs in the same division are paired at random,
 * kickoff `hoursAhead` from now. Only real clubs (demo clubs only pair with demo clubs).
 */
export async function scheduleFixtures(hoursAhead = 2) {
  const season = await activeSeason();
  if (!season) return { created: 0 };
  const kickoffAt = new Date(Date.now() + hoursAhead * 3600_000);
  let created = 0;
  for (let div = 1; div <= 10; div++) {
    for (const isDemo of [false, true]) {
      const busy = await db.match.findMany({
        where: { seasonId: season.id, mode: "LEAGUE_SIM", status: "SCHEDULED" },
        select: { homeClubId: true, awayClubId: true },
      });
      const busyIds = new Set(busy.flatMap((b) => [b.homeClubId, b.awayClubId]));
      const clubs = (await db.club.findMany({ where: { divisionId: div, isDemo }, select: { id: true } })).filter((c) => !busyIds.has(c.id));
      for (let i = clubs.length - 1; i > 0; i--) {
        const j = randomInt(0, i + 1);
        [clubs[i], clubs[j]] = [clubs[j], clubs[i]];
      }
      for (let i = 0; i + 1 < clubs.length; i += 2) {
        await db.match.create({
          data: {
            seasonId: season.id,
            mode: "LEAGUE_SIM",
            status: "SCHEDULED",
            homeClubId: clubs[i].id,
            awayClubId: clubs[i + 1].id,
            kickoffAt,
            seed: randomInt(0, 2 ** 31 - 1),
          },
        });
        created++;
      }
    }
  }
  return { created };
}

/** Plays every due LEAGUE_SIM fixture on the server, applies standings and settles Free Picks. */
export async function resolveDueFixtures(now = new Date()) {
  const due = await db.match.findMany({ where: { mode: "LEAGUE_SIM", status: "SCHEDULED", kickoffAt: { lte: now } }, take: 200 });
  let resolved = 0;
  for (const m of due) {
    await db.$transaction(async (tx) => {
      const fresh = await tx.match.findUnique({ where: { id: m.id } });
      if (!fresh || fresh.status !== "SCHEDULED") return;
      const home = await squadStrength(tx, m.homeClubId!);
      const away = await squadStrength(tx, m.awayClubId!);
      if (!home || !away) {
        await tx.match.update({ where: { id: m.id }, data: { status: "VOID", verification: "REJECTED", anomalies: [{ code: "NO_SQUAD" }] } });
        await settlePredictions(tx, m.id, null);
        return;
      }
      const sim = simulateMatch({ ovr: home }, { ovr: away }, m.seed);
      await tx.match.update({
        where: { id: m.id },
        data: {
          status: "FINISHED",
          verification: "SERVER_SIMULATED",
          homeScore: sim.homeScore,
          awayScore: sim.awayScore,
          finishedAt: now,
          appliedAt: now,
          events: {
            create: sim.goals.map((g, i) => ({
              messageId: `sim-${i}`,
              eventType: "GoalScored",
              seq: i + 1,
              gameClock: g.gameClock,
              payload: { ...g },
            })),
          },
        },
      });
      await applyToStandings(tx, { ...m, homeScore: sim.homeScore, awayScore: sim.awayScore }, false);
      await settlePredictions(tx, m.id, sim.homeScore > sim.awayScore ? "HOME" : sim.homeScore < sim.awayScore ? "AWAY" : "DRAW");
      resolved++;
    });
  }
  return { resolved };
}

/** Free Pick settlement: correct picks earn PICK_SCORE (no value). VOID matches void all picks. */
export async function settlePredictions(tx: Tx, matchId: string, outcome: "HOME" | "DRAW" | "AWAY" | null) {
  const cfg = await getGameConfig();
  const entries = await tx.predictionEntry.findMany({ where: { matchId, status: "OPEN" } });
  for (const e of entries) {
    const won = outcome !== null && e.pick === outcome;
    await tx.predictionEntry.update({
      where: { id: e.id },
      data: { status: outcome === null ? "VOID" : won ? "WON" : "LOST", score: won ? cfg.predictions.scoreCorrect : 0, settledAt: new Date() },
    });
    if (won) await grant(tx, e.userId, "PICK_SCORE", cfg.predictions.scoreCorrect, `pick:${e.id}`, "FREE_PICK_CORRECT");
  }
}
