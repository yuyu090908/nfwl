import { route, ApiError } from "@/lib/server/api";
import { parseBridgeMessage } from "@/lib/bridge/protocol";
import { ingestEvent } from "@/lib/server/match-session";
import { z } from "zod";

/** Receives one Unity bridge message relayed by the page. Type-checked, de-duplicated by messageId. */
export const POST = route<{ id: string }, z.ZodUnknown>(
  { name: "match.event", body: z.unknown(), limit: 240 },
  async ({ user, params }, raw) => {
    const parsed = parseBridgeMessage(raw);
    if (!parsed.ok) throw new ApiError(400, "BAD_MESSAGE", parsed.error);
    if (parsed.message.sessionId !== params.id) throw new ApiError(400, "SESSION_MISMATCH");
    return ingestEvent(user.id, parsed.message);
  },
);
