"use client";
// DEVELOPMENT ONLY. Plays the role of the Unity build when no WebGL build is deployed, so the whole
// pipeline (bridge -> API -> validation -> history) can be exercised. It sends the same messages the
// real game sends, in real time (a 10 s half takes 10 s). It is not a football match and the page
// labels it "DEV SIMULATOR". The server treats its results exactly like any other client report.

import { BRIDGE_SCHEMA_VERSION, type UnityMatchConfig } from "./protocol";
import { messageId } from "./web-bridge";

export function runSimulator(cfg: UnityMatchConfig, send: (json: string) => void, opts: { goals?: [number, number] } = {}) {
  let seq = 0;
  let paused = false;
  let clock = 0;
  let last = performance.now();
  let home = 0;
  let away = 0;
  let half = 1;
  let stopped = false;
  const full = cfg.halfLengthSeconds * 2;
  const [hg, ag] = opts.goals ?? [1 + Math.floor(Math.random() * 3), Math.floor(Math.random() * 3)];
  const goalTimes = [
    ...Array.from({ length: hg }, () => ({ t: 0.1 + Math.random() * 0.8, team: "home" as const })),
    ...Array.from({ length: ag }, () => ({ t: 0.1 + Math.random() * 0.8, team: "away" as const })),
  ]
    .map((g) => ({ ...g, t: g.t * full }))
    .sort((a, b) => a.t - b.t);

  const emit = (eventType: string, payload: object) =>
    send(JSON.stringify({ schemaVersion: BRIDGE_SCHEMA_VERSION, sessionId: cfg.sessionId, messageId: messageId(), eventType, seq: ++seq, payload }));

  emit("GameReady", { unityVersion: "simulator", buildVersion: "dev-simulator", platform: "WebGL" });
  emit("GameStarted", { homeName: cfg.homeName, awayName: cfg.awayName });
  setTimeout(() => {
    emit("MatchStarted", { half: 1, gameClock: 0 });
    last = performance.now();
    const tick = () => {
      if (stopped) return;
      const now = performance.now();
      if (!paused) clock += (now - last) / 1000;
      last = now;
      while (goalTimes.length && goalTimes[0].t <= clock) {
        const g = goalTimes.shift()!;
        if (g.team === "home") home++;
        else away++;
        emit("GoalScored", { team: g.team, scorerSlot: g.team === "home" ? 4 : 3, homeScore: home, awayScore: away, half, gameClock: +g.t.toFixed(2) });
      }
      if (half === 1 && clock >= cfg.halfLengthSeconds) {
        half = 2;
        emit("HalfTime", { homeScore: home, awayScore: away, gameClock: cfg.halfLengthSeconds });
      }
      if (clock >= full) {
        stopped = true;
        emit("MatchFinished", { homeScore: home, awayScore: away, gameClock: full });
        return;
      }
      setTimeout(tick, 200);
    };
    tick();
  }, 300);

  return {
    SendMessage(_o: string, _m: string, value?: string | number) {
      try {
        const c = JSON.parse(String(value));
        if (c.command === "pause" && !paused) {
          paused = true;
          emit("GamePaused", { reason: "web", gameClock: +clock.toFixed(2) });
        } else if (c.command === "resume" && paused) {
          paused = false;
          last = performance.now();
          emit("GameResumed", { gameClock: +clock.toFixed(2) });
        }
      } catch {}
    },
    stop() {
      stopped = true;
    },
    clock: () => clock,
  };
}
