// Unity <-> Web bridge protocol (prompt 6.3). Shared by the browser bridge, the API and the tests.
// The C# side lives in unity/NFWL/Assets/Scripts/Web/WebBridge.cs and must stay in sync with this file.

import { z } from "zod";

export const BRIDGE_SCHEMA_VERSION = 1;

export const EVENT_TYPES = [
  "GameReady",
  "GameStarted",
  "MatchStarted",
  "GoalScored",
  "HalfTime",
  "MatchFinished",
  "PlayerSwitched",
  "GamePaused",
  "GameResumed",
  "GameError",
] as const;
export type BridgeEventType = (typeof EVENT_TYPES)[number];

const side = z.enum(["home", "away"]);
const clock = z.number().finite().min(0).max(24 * 3600);
const score = z.number().int().min(0).max(999);

export const payloadSchemas = {
  GameReady: z.object({ unityVersion: z.string().max(64), buildVersion: z.string().max(128), platform: z.string().max(32) }),
  GameStarted: z.object({ homeName: z.string().max(64), awayName: z.string().max(64) }),
  MatchStarted: z.object({ half: z.number().int().min(1).max(2), gameClock: clock }),
  GoalScored: z.object({
    team: side,
    scorerSlot: z.number().int().min(0).max(4).nullable(),
    homeScore: score,
    awayScore: score,
    half: z.number().int().min(1).max(2),
    gameClock: clock,
  }),
  HalfTime: z.object({ homeScore: score, awayScore: score, gameClock: clock }),
  MatchFinished: z.object({ homeScore: score, awayScore: score, gameClock: clock }),
  PlayerSwitched: z.object({ slot: z.number().int().min(0).max(4), gameClock: clock }),
  GamePaused: z.object({ reason: z.string().max(64), gameClock: clock }),
  GameResumed: z.object({ gameClock: clock }),
  GameError: z.object({ code: z.string().max(64), message: z.string().max(500) }),
} satisfies Record<BridgeEventType, z.ZodType>;

export const envelopeSchema = z.object({
  schemaVersion: z.literal(BRIDGE_SCHEMA_VERSION),
  sessionId: z.string().uuid(),
  messageId: z.string().min(8).max(64).regex(/^[A-Za-z0-9_-]+$/),
  eventType: z.enum(EVENT_TYPES),
  /** Monotonic counter from Unity, starting at 1. */
  seq: z.number().int().min(1).max(1_000_000),
  payload: z.unknown(),
});

export type BridgeEnvelope = z.infer<typeof envelopeSchema>;
export type BridgeMessage = {
  [K in BridgeEventType]: Omit<BridgeEnvelope, "eventType" | "payload"> & { eventType: K; payload: z.infer<(typeof payloadSchemas)[K]> };
}[BridgeEventType];

/** Validates envelope + payload. Returns a typed message or the list of problems. */
export function parseBridgeMessage(raw: unknown): { ok: true; message: BridgeMessage } | { ok: false; error: string } {
  const env = envelopeSchema.safeParse(raw);
  if (!env.success) return { ok: false, error: env.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ") };
  const p = payloadSchemas[env.data.eventType].safeParse(env.data.payload);
  if (!p.success) return { ok: false, error: `payload: ${p.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ")}` };
  return { ok: true, message: { ...env.data, payload: p.data } as BridgeMessage };
}

// ------------------------------------------------------------------ web -> Unity

/** Commands the page can send into Unity (SendMessage("NFWLWebBridge", "OnWebCommand", json)). */
export type WebCommand =
  | { command: "pause" }
  | { command: "resume" }
  | { command: "setMuted"; muted: boolean }
  | { command: "setQuality"; level: 0 | 1 | 2 | 3 };

/** One player as Unity's JsonPlayerSource reads it ("our own shape", see Core/PlayerData.cs). */
export interface UnityPlayer {
  tokenId: string; // in-game player UUID (not an NFT token id, not a secret)
  name: string;
  rarity: string;
  element: string;
  number: number;
  attributes: Record<string, number | string>;
  skills: string[];
}

/**
 * Everything Unity receives before the match. No auth token, no wallet, no email:
 * the page keeps the session cookie; Unity only gets ids and match data (prompt 6.4).
 */
export interface UnityMatchConfig {
  schemaVersion: number;
  sessionId: string;
  userId: string;
  matchId: string;
  mode: string;
  halfLengthSeconds: number;
  formation: string;
  homeName: string;
  homeShort: string;
  awayName: string;
  awayShort: string;
  home: UnityPlayer[];
  away: UnityPlayer[];
  cosmetics: { homeKit: string; awayKit: string; clubColors: string[] };
}
