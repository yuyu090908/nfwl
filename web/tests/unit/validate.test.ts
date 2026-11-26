import { describe, expect, it } from "vitest";
import { validateMatch, type RecordedEvent } from "@/lib/match/validate";
import { simulateMatch } from "@/lib/match/sim";

const T0 = 1_000_000;
const half = 300;
const goal = (seq: number, team: "home" | "away", h: number, a: number, clock: number, realOffset = clock + 1): RecordedEvent => ({
  eventType: "GoalScored", seq, receivedAt: T0 + realOffset * 1000, payload: { team, scorerSlot: 4, homeScore: h, awayScore: a, gameClock: clock, half: clock < half ? 1 : 2 },
});
const base = (events: RecordedEvent[], final = { homeScore: 2, awayScore: 1, gameClock: 600 }, realSeconds = 640) => ({
  halfSeconds: half,
  startedAt: T0,
  finishedAt: T0 + realSeconds * 1000,
  events: [{ eventType: "MatchStarted", seq: 1, receivedAt: T0, payload: { half: 1, gameClock: 0 } }, { eventType: "HalfTime", seq: 50, receivedAt: T0 + 310_000, payload: { homeScore: 1, awayScore: 0, gameClock: 300 } }, ...events],
  final,
  clockToleranceSeconds: 3,
  maxGoalsPerTeam: 30,
});

describe("validateMatch", () => {
  const good = [goal(2, "home", 1, 0, 120), goal(60, "away", 1, 1, 400), goal(61, "home", 2, 1, 500)];

  it("accepts a consistent match", () => {
    const r = validateMatch(base(good));
    expect(r.verdict).toBe("CLIENT_REPORTED");
    expect(r.goals).toHaveLength(3);
  });

  it("rejects a match that finished faster than the game clock allows", () => {
    const r = validateMatch(base(good, undefined, 120));
    expect(r.verdict).toBe("REJECTED");
    expect(r.anomalies.map((a) => a.code)).toContain("TOO_FAST");
  });

  it("rejects a final score that does not match the goal events", () => {
    const r = validateMatch(base(good, { homeScore: 9, awayScore: 1, gameClock: 600 }));
    expect(r.anomalies.map((a) => a.code)).toContain("FINAL_MISMATCH");
    expect(r.verdict).toBe("REJECTED");
  });

  it("rejects skipped scores in the goal sequence", () => {
    const r = validateMatch(base([goal(2, "home", 2, 0, 120), goal(3, "away", 2, 1, 400)]));
    expect(r.anomalies.map((a) => a.code)).toContain("SCORE_SEQUENCE");
  });

  it("rejects a goal that arrived before that much real time had passed", () => {
    const r = validateMatch(base([goal(2, "home", 1, 0, 500, 30), goal(3, "away", 1, 1, 510), goal(4, "home", 2, 1, 520)]));
    expect(r.anomalies.map((a) => a.code)).toContain("GOAL_TOO_EARLY");
  });

  it("rejects a match that ended before full time", () => {
    const r = validateMatch(base(good, { homeScore: 2, awayScore: 1, gameClock: 400 }));
    expect(r.anomalies.map((a) => a.code)).toContain("ENDED_EARLY");
  });

  it("rejects reused sequence numbers and missing kickoff", () => {
    const b = base([goal(2, "home", 1, 0, 120), goal(2, "away", 1, 1, 400), goal(61, "home", 2, 1, 500)]);
    expect(validateMatch(b).anomalies.map((a) => a.code)).toContain("DUP_SEQ");
    expect(validateMatch({ ...base(good), startedAt: null }).anomalies.map((a) => a.code)).toContain("NO_KICKOFF");
  });

  it("flags (does not reject) a missing half time", () => {
    const b = base(good);
    b.events = b.events.filter((e) => e.eventType !== "HalfTime");
    expect(validateMatch(b).verdict).toBe("FLAGGED");
  });
});

describe("server simulation", () => {
  it("is deterministic and consistent", () => {
    const a = simulateMatch({ ovr: [60, 60, 60, 60, 60] }, { ovr: [58, 58, 58, 58, 58] }, 123);
    const b = simulateMatch({ ovr: [60, 60, 60, 60, 60] }, { ovr: [58, 58, 58, 58, 58] }, 123);
    expect(a).toEqual(b);
    expect(a.goals.filter((g) => g.team === "home")).toHaveLength(a.homeScore);
    expect(a.goals.filter((g) => g.team === "away")).toHaveLength(a.awayScore);
  });
  it("stronger squads score more on average", () => {
    let strong = 0, weak = 0;
    for (let s = 0; s < 500; s++) {
      const r = simulateMatch({ ovr: [75, 75, 75, 75, 75] }, { ovr: [55, 55, 55, 55, 55] }, s);
      strong += r.homeScore;
      weak += r.awayScore;
    }
    expect(strong).toBeGreaterThan(weak * 1.5);
  });
});
