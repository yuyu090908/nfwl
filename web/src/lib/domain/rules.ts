// Pure game rules shared by server, client and tests.
// Numbers that exist in the Unity build (unity/NFWL/Assets/Scripts/NFWLRules.cs, Core/PlayerData.cs)
// are copied exactly so a player looks the same on the web and in the match.
// Numbers marked PROPOSED come from the design docs and are overridable through GameConfig.

export const POSITIONS = ["GK", "DF", "MF", "FW"] as const;
export type Position = (typeof POSITIONS)[number];

export const RARITIES = ["Common", "Rare", "Epic", "Mythic", "Legendary"] as const;
export type Rarity = (typeof RARITIES)[number];

export const CRYSTAL_TYPES = ["ICE", "SAPPHIRE", "AMETHYST", "EMERALD", "GOLD", "VIOLET"] as const;
export type CrystalType = (typeof CRYSTAL_TYPES)[number];

/** The 10 attributes of the Unity match engine, in Unity's order. */
export const ATTRIBUTE_KEYS = [
  "speed",
  "acceleration",
  "agility",
  "ballControl",
  "shortPass",
  "longPass",
  "shotPower",
  "finishing",
  "tackling",
  "goalkeeping",
] as const;
export type AttributeKey = (typeof ATTRIBUTE_KEYS)[number];
export type Attributes = Record<AttributeKey, number>;

export const ATTRIBUTE_LABELS: Record<AttributeKey, string> = {
  speed: "Speed",
  acceleration: "Acceleration",
  agility: "Agility",
  ballControl: "Ball Control",
  shortPass: "Short Pass",
  longPass: "Long Pass",
  shotPower: "Shot Power",
  finishing: "Finishing",
  tackling: "Tackling",
  goalkeeping: "Goalkeeping",
};

/** Unity clamps every attribute to 40..99 (PlayerAttributes [Range(40, 99)]). */
export const ATTR_MIN = 40;
export const ATTR_MAX = 99;

export const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));

/**
 * C#'s Mathf.RoundToInt uses banker's rounding (round half to even).
 * Using Math.round here would make OVR differ by 1 from the in-game card for x.5 values.
 */
export function roundHalfEven(x: number): number {
  const f = Math.floor(x);
  const diff = x - f;
  if (Math.abs(diff - 0.5) < 1e-9) return f % 2 === 0 ? f : f + 1;
  return Math.round(x);
}

/** Same formula as PlayerAttributes.Overall(Role) in NFWLRules.cs. */
export function overall(a: Attributes, role: Position): number {
  if (role === "GK") return roundHalfEven(a.goalkeeping * 0.7 + a.agility * 0.15 + a.longPass * 0.15);
  const v =
    (a.speed + a.acceleration + a.agility + a.ballControl + a.shortPass + a.longPass + a.shotPower + a.finishing + a.tackling) /
    9;
  return roundHalfEven(v);
}

// ------------------------------------------------------------------ rarity

/** Unity Skills.Slots(): Common 0, Rare 0, Epic 1, Mythic 2, Legendary 3. */
export function unitySkillSlots(r: Rarity): number {
  const i = RARITIES.indexOf(r);
  return i >= 2 ? i - 1 : 0;
}

export const RARITY_COLORS: Record<Rarity, { name: string; hex: string }> = {
  Common: { name: "Silver", hex: "#C9D1DE" },
  Rare: { name: "Blue", hex: "#44D9FF" },
  Epic: { name: "Purple", hex: "#9258FF" },
  Mythic: { name: "Red", hex: "#FF465F" },
  Legendary: { name: "Gold", hex: "#F3C65A" },
};

// ------------------------------------------------------------------ deterministic RNG

/** mulberry32: small seeded PRNG so generation is reproducible from a stored seed. */
export function rng(seed: number) {
  let t = seed >>> 0;
  const next = () => {
    t += 0x6d2b79f5;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
  };
  return {
    next,
    /** Like C# Random.Next(min, maxExclusive). */
    int: (min: number, maxExclusive: number) => min + Math.floor(next() * (maxExclusive - min)),
    pick: <T,>(arr: readonly T[]) => arr[Math.floor(next() * arr.length)],
  };
}
export type Rng = ReturnType<typeof rng>;

/** Same shape as PlayerAttributes.Random(baseLevel): base + Next(-8, 9), clamped 40..99. */
export function randomAttributes(baseLevel: number, r: Rng, cap = ATTR_MAX): Attributes {
  const a = {} as Attributes;
  for (const k of ATTRIBUTE_KEYS) a[k] = clamp(baseLevel + r.int(-8, 9), ATTR_MIN, Math.min(cap, ATTR_MAX));
  return a;
}

/**
 * Starter player (Full Spec 2.4): Common, Lv1, OVR within [minOvr, maxOvr].
 * Keepers get Goalkeeping = base + 8 like the Unity generator, then everything is shifted so the
 * OVR for the main position lands inside the band. Never exceeds the rarity cap.
 */
export function starterAttributes(
  position: Position,
  r: Rng,
  opts: { minOvr: number; maxOvr: number; cap: number },
): Attributes {
  const target = r.int(opts.minOvr, opts.maxOvr + 1);
  const a = randomAttributes(target, r, opts.cap);
  if (position === "GK") a.goalkeeping = clamp(target + 8, ATTR_MIN, opts.cap);
  else a.goalkeeping = clamp(ATTR_MIN + r.int(0, 8), ATTR_MIN, opts.cap);
  for (let guard = 0; guard < 40; guard++) {
    const ovr = overall(a, position);
    if (ovr >= opts.minOvr && ovr <= opts.maxOvr) break;
    const step = ovr < opts.minOvr ? 1 : -1;
    const keys: AttributeKey[] =
      position === "GK" ? ["goalkeeping", "agility", "longPass"] : ATTRIBUTE_KEYS.filter((k) => k !== "goalkeeping");
    for (const k of keys) a[k] = clamp(a[k] + step, ATTR_MIN, opts.cap);
  }
  return a;
}

// ------------------------------------------------------------------ positions

/** Abilities that suffer when a player is placed off position (Full Spec 6.4). */
export const RELATED_ATTRIBUTES: Record<Position, AttributeKey[]> = {
  GK: ["goalkeeping"],
  DF: ["tackling", "agility"],
  MF: ["shortPass", "longPass", "ballControl"],
  FW: ["finishing", "shotPower", "ballControl"],
};

export type Fit = "main" | "sub" | "off" | "fpInGoal";

export function positionFit(main: Position, subs: readonly Position[], slot: Position): Fit {
  if (slot === main) return "main";
  if (slot === "GK") return "fpInGoal";
  if (subs.includes(slot)) return "sub";
  return "off";
}

export interface PenaltyRules {
  sub: number; // -3
  off: number; // -8
  fpGoalkeeping: number; // 30 (Unity floor is 40, so the match receives 40)
}

export const DEFAULT_PENALTIES: PenaltyRules = { sub: 3, off: 8, fpGoalkeeping: 30 };

/** Attributes as they will be used in a slot, plus what changed (for the penalty badge). */
export function applyPositionPenalty(
  a: Attributes,
  main: Position,
  subs: readonly Position[],
  slot: Position,
  rules: PenaltyRules = DEFAULT_PENALTIES,
): { attributes: Attributes; fit: Fit; changes: Partial<Record<AttributeKey, number>>; displayGoalkeeping?: number } {
  const fit = positionFit(main, subs, slot);
  const out = { ...a };
  const changes: Partial<Record<AttributeKey, number>> = {};
  if (fit === "sub" || fit === "off") {
    const d = fit === "sub" ? rules.sub : rules.off;
    for (const k of RELATED_ATTRIBUTES[slot]) {
      const v = clamp(a[k] - d, ATTR_MIN, ATTR_MAX);
      if (v !== a[k]) changes[k] = v - a[k];
      out[k] = v;
    }
  }
  if (fit === "fpInGoal") {
    const v = clamp(rules.fpGoalkeeping, ATTR_MIN, ATTR_MAX);
    if (v !== a.goalkeeping) changes.goalkeeping = v - a.goalkeeping;
    out.goalkeeping = v;
    return { attributes: out, fit, changes, displayGoalkeeping: rules.fpGoalkeeping };
  }
  return { attributes: out, fit, changes };
}

// ------------------------------------------------------------------ levels and XP (Full Spec 6.5 / 6.6)

export const MAX_LEVEL = 60;

/** XP needed to go from `level` to level+1: 100 + 25 * (level - 1). */
export const xpToNext = (level: number) => 100 + 25 * (level - 1);

/** Adds XP, levelling up until the cap. Excess XP at the cap is kept (up to the next requirement). */
export function addXp(level: number, xp: number, gained: number, levelCap: number) {
  let l = level;
  let x = xp + Math.max(0, gained);
  let levelsGained = 0;
  while (l < Math.min(levelCap, MAX_LEVEL) && x >= xpToNext(l)) {
    x -= xpToNext(l);
    l++;
    levelsGained++;
  }
  if (l >= Math.min(levelCap, MAX_LEVEL)) x = Math.min(x, xpToNext(l) - 1);
  return { level: l, xp: x, levelsGained };
}

export interface XpInput {
  minutes: number;
  result: "W" | "D" | "L";
  goals: number;
  assists: number;
  saves: number;
  cleanSheet: boolean;
  position: Position;
}

export interface XpRules {
  perMinute: number;
  win: number;
  draw: number;
  goal: number;
  assist: number;
  save: number;
  cleanSheet: number;
  perMatchCap: number;
  modeMultiplier: Record<string, number>;
}

export const DEFAULT_XP: XpRules = {
  perMinute: 5,
  win: 50,
  draw: 20,
  goal: 30,
  assist: 20,
  save: 10,
  cleanSheet: 30,
  perMatchCap: 400,
  modeMultiplier: { PRACTICE_CPU: 0.5, FRIENDLY: 0.5, LEAGUE_SIM: 1 },
};

export function matchXp(i: XpInput, mode: string, rules: XpRules = DEFAULT_XP): number {
  let xp = Math.round(i.minutes * rules.perMinute);
  if (i.minutes >= 5) xp += i.result === "W" ? rules.win : i.result === "D" ? rules.draw : 0;
  xp += i.goals * rules.goal + i.assists * rules.assist;
  if (i.position === "GK") xp += i.saves * rules.save;
  if (i.cleanSheet && i.minutes >= 10 && (i.position === "GK" || i.position === "DF")) xp += rules.cleanSheet;
  xp = Math.min(xp, rules.perMatchCap);
  return Math.floor(xp * (rules.modeMultiplier[mode] ?? 1));
}

// ------------------------------------------------------------------ league

export interface PointsRules {
  win: number;
  draw: number;
  loss: number;
}
export const DEFAULT_POINTS: PointsRules = { win: 3, draw: 1, loss: 0 };

export interface StandingRow {
  clubId: string;
  played: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  points: number;
}

/** Tie-break order is configurable; default: points, goal difference, goals for, wins, then id. */
export function sortStandings<T extends StandingRow>(rows: T[], order: string[] = ["points", "gd", "gf", "wins"]): T[] {
  const key = (r: T, k: string) =>
    k === "gd" ? r.goalsFor - r.goalsAgainst : k === "gf" ? r.goalsFor : (r as unknown as Record<string, number>)[k] ?? 0;
  return [...rows].sort((a, b) => {
    for (const k of order) {
      const d = key(b, k) - key(a, k);
      if (d !== 0) return d;
    }
    return a.clubId < b.clubId ? -1 : a.clubId > b.clubId ? 1 : 0;
  });
}

/** Win probability shown on Free Pick cards: a logistic on the squads' average OVR. Labelled as an AI estimate. */
export function estimateOutcome(homeOvr: number, awayOvr: number) {
  const d = homeOvr - awayOvr + 1.5; // small home edge
  const pHomeNoDraw = 1 / (1 + Math.exp(-d / 6));
  const draw = clamp(0.26 - Math.abs(d) * 0.008, 0.12, 0.26);
  const home = (1 - draw) * pHomeNoDraw;
  const away = 1 - draw - home;
  return { home: Math.round(home * 100), draw: Math.round(draw * 100), away: Math.round(away * 100) };
}
