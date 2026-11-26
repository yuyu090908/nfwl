// Operator-tunable game settings. Defaults come from the design docs; every number here can be
// overridden by the GameConfig row "game" in the database (deep-merged), so balance changes need
// no deploy. Items marked PROPOSED in the docs are kept PROPOSED here too.

import { DEFAULT_PENALTIES, DEFAULT_POINTS, DEFAULT_XP, type Rarity } from "./rules";

export interface RarityRule {
  /** Attribute cap (Full Spec 6.7). */
  attributeCap: number;
  /** Base level used when the server generates a player of this rarity (Unity: 70 + rarity*4 for the demo home team).
   *  null = not defined yet; the server refuses to generate such players (prompt 8.2 for Legendary). */
  generationBaseLevel: number | null;
  skillSlots: number;
}

export interface GameConfig {
  starter: { count: number; positions: Record<string, number>; minOvr: number; maxOvr: number; pool: string; points: number };
  rarity: Record<Rarity, RarityRule>;
  penalties: typeof DEFAULT_PENALTIES;
  xp: typeof DEFAULT_XP & { practiceDailyLimit: number };
  levelCaps: { steps: number[]; cost: number[] };
  league: {
    points: typeof DEFAULT_POINTS;
    tieBreak: string[];
    rp: { win: number; draw: number; loss: number };
    promotionSpots: number;
    relegationSpots: number;
    minMatchesForPromotion: number;
    championsLeagueSlots: number;
    seasonDays: number;
  };
  match: {
    /** Allowed half lengths in seconds for practice (Full Spec 4: 6/10/20 minute matches). */
    practiceHalfSeconds: number[];
    /** Extra seconds allowed on top of the game clock (loading, replays, pauses are added separately). */
    clockToleranceSeconds: number;
    maxGoalsPerTeam: number;
    sessionTtlMinutes: number;
  };
  predictions: { showEstimate: boolean; scoreCorrect: number; closeMinutesBeforeKickoff: number };
}

export const DEFAULT_CONFIG: GameConfig = {
  starter: { count: 8, positions: { GK: 2, DF: 2, MF: 2, FW: 2 }, minOvr: 55, maxOvr: 60, pool: "starter-common", points: 500 },
  rarity: {
    Common: { attributeCap: 72, generationBaseLevel: 70, skillSlots: 0 },
    Rare: { attributeCap: 78, generationBaseLevel: 74, skillSlots: 0 },
    Epic: { attributeCap: 84, generationBaseLevel: 78, skillSlots: 1 },
    Mythic: { attributeCap: 90, generationBaseLevel: 82, skillSlots: 2 },
    // Unity has no generation rule for Legendary; left undefined on purpose.
    Legendary: { attributeCap: 95, generationBaseLevel: null, skillSlots: 3 },
  },
  penalties: DEFAULT_PENALTIES,
  xp: { ...DEFAULT_XP, practiceDailyLimit: 5 },
  // PROPOSED: caps reached by breakthrough. cost[i] = NFWL Points to raise the cap from steps[i] to steps[i+1].
  levelCaps: { steps: [10, 20, 30, 40, 50, 60], cost: [200, 400, 700, 1000, 1500] },
  league: {
    points: DEFAULT_POINTS,
    tieBreak: ["points", "gd", "gf", "wins"],
    rp: { win: 25, draw: 5, loss: -20 },
    promotionSpots: 3,
    relegationSpots: 3,
    minMatchesForPromotion: 5,
    championsLeagueSlots: 32,
    seasonDays: 30,
  },
  match: {
    practiceHalfSeconds: [180, 300, 600],
    clockToleranceSeconds: 3,
    maxGoalsPerTeam: 30,
    sessionTtlMinutes: 120,
  },
  predictions: { showEstimate: true, scoreCorrect: 10, closeMinutesBeforeKickoff: 5 },
};

/** Test/dev only: a 10 second half so the full pipeline can be exercised quickly. Never in production. */
export function practiceHalfOptions(cfg: GameConfig): number[] {
  const extra = process.env.NODE_ENV !== "production" && process.env.NFWL_ALLOW_SHORT_MATCHES === "1" ? [10] : [];
  return [...extra, ...cfg.match.practiceHalfSeconds];
}

type Obj = Record<string, unknown>;
export function deepMerge<T>(base: T, over: unknown): T {
  if (over === null || typeof over !== "object" || Array.isArray(over)) return (over ?? base) as T;
  const out: Obj = { ...(base as Obj) };
  for (const [k, v] of Object.entries(over as Obj)) {
    const b = (base as Obj)[k];
    out[k] = b && typeof b === "object" && !Array.isArray(b) ? deepMerge(b, v) : v;
  }
  return out as T;
}
