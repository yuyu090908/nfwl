import { describe, expect, it } from "vitest";
import { parseBridgeMessage } from "@/lib/bridge/protocol";

const env = (eventType: string, payload: unknown, extra: object = {}) => ({
  schemaVersion: 1, sessionId: "0b7f6fe4-0a5e-4098-9c1e-e86b583cf7c8", messageId: "abcdef0123456789", eventType, seq: 3, payload, ...extra,
});

describe("bridge protocol", () => {
  it("accepts every event type with a valid payload", () => {
    const ok = [
      env("GameReady", { unityVersion: "6000.6.9f1", buildVersion: "v0.7", platform: "WebGLPlayer" }),
      env("GameStarted", { homeName: "A", awayName: "B" }),
      env("MatchStarted", { half: 1, gameClock: 0 }),
      env("GoalScored", { team: "home", scorerSlot: null, homeScore: 1, awayScore: 0, half: 1, gameClock: 12.5 }),
      env("HalfTime", { homeScore: 1, awayScore: 0, gameClock: 300 }),
      env("MatchFinished", { homeScore: 1, awayScore: 0, gameClock: 600 }),
      env("PlayerSwitched", { slot: 3, gameClock: 10 }),
      env("GamePaused", { reason: "web", gameClock: 10 }),
      env("GameResumed", { gameClock: 10 }),
      env("GameError", { code: "Exception", message: "boom" }),
    ];
    for (const m of ok) expect(parseBridgeMessage(m).ok, JSON.stringify(m)).toBe(true);
  });

  it("rejects wrong versions, unknown events, bad ids and bad payloads", () => {
    expect(parseBridgeMessage(env("GameReady", {}, { schemaVersion: 2 })).ok).toBe(false);
    expect(parseBridgeMessage(env("Teleport", {})).ok).toBe(false);
    expect(parseBridgeMessage(env("GameResumed", { gameClock: 1 }, { messageId: "<script>" })).ok).toBe(false);
    expect(parseBridgeMessage(env("GoalScored", { team: "home", scorerSlot: 9, homeScore: 1, awayScore: 0, half: 1, gameClock: 1 })).ok).toBe(false);
    expect(parseBridgeMessage(env("MatchFinished", { homeScore: -1, awayScore: 0, gameClock: 600 })).ok).toBe(false);
    expect(parseBridgeMessage(env("GameResumed", { gameClock: 1 }, { sessionId: "not-a-uuid" })).ok).toBe(false);
  });
});
