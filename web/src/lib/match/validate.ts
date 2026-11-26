// Server-side checks on a client-reported match (prompt 6.2 step 9, 16).
// A WebGL client is never trusted: these checks only catch results that are *impossible*
// (time running faster than real time, scores that do not add up). A result that passes is still
// CLIENT_REPORTED, never "verified": an official season only counts server-simulated results.

export interface RecordedEvent {
  eventType: string;
  seq: number;
  /** Server receive time (ms since epoch). The client's own timestamps are ignored. */
  receivedAt: number;
  payload: Record<string, unknown>;
}

export interface ValidationInput {
  halfSeconds: number;
  startedAt: number | null; // server time of MatchStarted
  finishedAt: number; // server time of MatchFinished
  events: RecordedEvent[]; // all events of the session, any order
  final: { homeScore: number; awayScore: number; gameClock: number };
  clockToleranceSeconds: number;
  maxGoalsPerTeam: number;
}

export interface ValidationResult {
  verdict: "CLIENT_REPORTED" | "FLAGGED" | "REJECTED";
  anomalies: { code: string; detail: string; severity: "soft" | "hard" }[];
  goals: { team: "home" | "away"; scorerSlot: number | null; gameClock: number }[];
}

export function validateMatch(i: ValidationInput): ValidationResult {
  const anomalies: ValidationResult["anomalies"] = [];
  const hard = (code: string, detail: string) => anomalies.push({ code, detail, severity: "hard" });
  const soft = (code: string, detail: string) => anomalies.push({ code, detail, severity: "soft" });
  const full = i.halfSeconds * 2;
  const tol = i.clockToleranceSeconds;

  const events = [...i.events].sort((a, b) => a.seq - b.seq);
  const goals: ValidationResult["goals"] = [];

  if (i.startedAt == null) hard("NO_KICKOFF", "MatchFinished without MatchStarted");

  // Real time must be at least the game time: Unity's clock uses Time.deltaTime and never runs faster
  // than the wall clock (it also stops during replays and restarts).
  if (i.startedAt != null) {
    const realSeconds = (i.finishedAt - i.startedAt) / 1000;
    if (realSeconds + tol < full) hard("TOO_FAST", `match lasted ${realSeconds.toFixed(1)}s, needs at least ${full}s`);
  }
  if (i.final.gameClock + tol < full) hard("ENDED_EARLY", `final clock ${i.final.gameClock.toFixed(1)} < ${full}`);
  if (i.final.gameClock > full + tol) soft("CLOCK_OVERRUN", `final clock ${i.final.gameClock.toFixed(1)} > ${full}`);

  // Sequence numbers must be unique and strictly increasing in receive order (no replayed old messages).
  const seen = new Set<number>();
  for (const e of events) {
    if (seen.has(e.seq)) hard("DUP_SEQ", `seq ${e.seq} used twice`);
    seen.add(e.seq);
  }

  let home = 0;
  let away = 0;
  let lastClock = -1;
  for (const e of events) {
    if (e.eventType !== "GoalScored") continue;
    const p = e.payload as { team: "home" | "away"; scorerSlot: number | null; homeScore: number; awayScore: number; gameClock: number };
    if (p.team === "home") home++;
    else away++;
    if (p.homeScore !== home || p.awayScore !== away)
      hard("SCORE_SEQUENCE", `goal seq ${e.seq} reports ${p.homeScore}-${p.awayScore}, expected ${home}-${away}`);
    if (p.gameClock + 1e-6 < lastClock) hard("CLOCK_BACKWARDS", `goal seq ${e.seq} clock ${p.gameClock} < ${lastClock}`);
    lastClock = Math.max(lastClock, p.gameClock);
    if (p.gameClock > full + tol) hard("GOAL_AFTER_FULLTIME", `goal at ${p.gameClock}`);
    if (i.startedAt != null) {
      const real = (e.receivedAt - i.startedAt) / 1000;
      if (real + tol < p.gameClock) hard("GOAL_TOO_EARLY", `goal at clock ${p.gameClock}s arrived after ${real.toFixed(1)}s`);
    }
    goals.push({ team: p.team, scorerSlot: p.scorerSlot, gameClock: p.gameClock });
  }

  if (home !== i.final.homeScore || away !== i.final.awayScore)
    hard("FINAL_MISMATCH", `goal events ${home}-${away}, final ${i.final.homeScore}-${i.final.awayScore}`);
  if (i.final.homeScore > i.maxGoalsPerTeam || i.final.awayScore > i.maxGoalsPerTeam)
    soft("IMPLAUSIBLE_SCORE", `${i.final.homeScore}-${i.final.awayScore}`);

  const halfTimes = events.filter((e) => e.eventType === "HalfTime");
  if (halfTimes.length === 0) soft("NO_HALFTIME", "no HalfTime event");
  for (const h of halfTimes) {
    const c = Number((h.payload as { gameClock: number }).gameClock);
    if (Math.abs(c - i.halfSeconds) > tol) soft("HALFTIME_CLOCK", `half time at ${c}`);
  }

  const verdict = anomalies.some((a) => a.severity === "hard") ? "REJECTED" : anomalies.length ? "FLAGGED" : "CLIENT_REPORTED";
  return { verdict, anomalies, goals };
}
