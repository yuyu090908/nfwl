import { describe, expect, it } from "vitest";
import {
  addXp, applyPositionPenalty, matchXp, overall, randomAttributes, rng, roundHalfEven, sortStandings, starterAttributes,
  unitySkillSlots, xpToNext, MAX_LEVEL, ATTRIBUTE_KEYS, estimateOutcome, type Attributes,
} from "@/lib/domain/rules";

const flat = (v: number): Attributes => Object.fromEntries(ATTRIBUTE_KEYS.map((k) => [k, v])) as Attributes;

describe("Unity compatibility", () => {
  it("rounds half to even like Mathf.RoundToInt", () => {
    expect(roundHalfEven(2.5)).toBe(2);
    expect(roundHalfEven(3.5)).toBe(4);
    expect(roundHalfEven(70.4)).toBe(70);
    expect(roundHalfEven(70.6)).toBe(71);
  });

  it("OVR uses the PlayerAttributes.Overall formula", () => {
    const a = flat(70);
    expect(overall(a, "FW")).toBe(70);
    // GK: GK*0.7 + Agility*0.15 + LongPass*0.15
    expect(overall({ ...a, goalkeeping: 80, agility: 60, longPass: 60 }, "GK")).toBe(74);
    // Outfield: mean of the 9 non-GK attributes (goalkeeping ignored)
    expect(overall({ ...a, goalkeeping: 40, speed: 79 }, "MF")).toBe(71);
  });

  it("skill slots match Skills.Slots()", () => {
    expect(["Common", "Rare", "Epic", "Mythic", "Legendary"].map((r) => unitySkillSlots(r as never))).toEqual([0, 0, 1, 2, 3]);
  });

  it("random attributes stay in 40..99 and base +- 8", () => {
    const r = rng(42);
    for (let i = 0; i < 200; i++) {
      const a = randomAttributes(45, r);
      for (const k of ATTRIBUTE_KEYS) {
        expect(a[k]).toBeGreaterThanOrEqual(40);
        expect(a[k]).toBeLessThanOrEqual(53);
      }
    }
  });
});

describe("starter players (Full Spec 2.4)", () => {
  it("land in OVR 55..60 and never exceed the Common cap", () => {
    for (let seed = 1; seed < 400; seed++) {
      const r = rng(seed);
      for (const pos of ["GK", "DF", "MF", "FW"] as const) {
        const a = starterAttributes(pos, r, { minOvr: 55, maxOvr: 60, cap: 72 });
        const o = overall(a, pos);
        expect(o).toBeGreaterThanOrEqual(55);
        expect(o).toBeLessThanOrEqual(60);
        for (const k of ATTRIBUTE_KEYS) expect(a[k]).toBeLessThanOrEqual(72);
      }
    }
  });
  it("is deterministic for a seed", () => {
    expect(starterAttributes("FW", rng(7), { minOvr: 55, maxOvr: 60, cap: 72 })).toEqual(starterAttributes("FW", rng(7), { minOvr: 55, maxOvr: 60, cap: 72 }));
  });
});

describe("position penalty (Full Spec 6.4)", () => {
  const a = flat(70);
  it("none on the main position", () => {
    expect(applyPositionPenalty(a, "FW", ["MF"], "FW").fit).toBe("main");
  });
  it("-3 on a sub position, -8 off position, only related attributes", () => {
    const sub = applyPositionPenalty(a, "FW", ["MF"], "MF");
    expect(sub.fit).toBe("sub");
    expect(sub.attributes.shortPass).toBe(67);
    expect(sub.attributes.speed).toBe(70);
    const off = applyPositionPenalty(a, "FW", ["MF"], "DF");
    expect(off.attributes.tackling).toBe(62);
  });
  it("outfield player in goal: GK treated as 30, sent as Unity's floor 40", () => {
    const g = applyPositionPenalty(a, "FW", [], "GK");
    expect(g.fit).toBe("fpInGoal");
    expect(g.attributes.goalkeeping).toBe(40);
    expect(g.displayGoalkeeping).toBe(30);
  });
});

describe("XP (Full Spec 6.5/6.6)", () => {
  it("total XP from Lv1 to Lv60 is 48,675", () => {
    let total = 0;
    for (let l = 1; l < MAX_LEVEL; l++) total += xpToNext(l);
    expect(total).toBe(48675);
  });
  it("caps at 400 per match and halves for practice", () => {
    const big = { minutes: 20, result: "W" as const, goals: 10, assists: 0, saves: 0, cleanSheet: true, position: "FW" as const };
    expect(matchXp(big, "LEAGUE_SIM")).toBe(400);
    expect(matchXp(big, "PRACTICE_CPU")).toBe(200);
    expect(matchXp({ ...big, goals: 0, result: "D", cleanSheet: false }, "LEAGUE_SIM")).toBe(120);
  });
  it("levels up and stops at the cap", () => {
    expect(addXp(1, 0, 100, 10)).toEqual({ level: 2, xp: 0, levelsGained: 1 });
    const capped = addXp(9, 0, 100000, 10);
    expect(capped.level).toBe(10);
    expect(capped.xp).toBeLessThan(xpToNext(10));
  });
});

describe("league", () => {
  it("sorts by points, goal difference, goals for", () => {
    const rows = [
      { clubId: "a", played: 3, wins: 2, draws: 0, losses: 1, goalsFor: 5, goalsAgainst: 4, points: 6 },
      { clubId: "b", played: 3, wins: 2, draws: 0, losses: 1, goalsFor: 6, goalsAgainst: 3, points: 6 },
      { clubId: "c", played: 3, wins: 3, draws: 0, losses: 0, goalsFor: 3, goalsAgainst: 0, points: 9 },
    ];
    expect(sortStandings(rows).map((r) => r.clubId)).toEqual(["c", "b", "a"]);
  });
  it("estimate sums to ~100 and favours the stronger side", () => {
    const e = estimateOutcome(70, 60);
    expect(e.home + e.draw + e.away).toBeGreaterThanOrEqual(99);
    expect(e.home).toBeGreaterThan(e.away);
  });
});
