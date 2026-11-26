import { z } from "zod";
import { route } from "@/lib/server/api";
import { breakthrough } from "@/lib/server/upgrade";

const body = z.object({ playerId: z.string().uuid(), idempotencyKey: z.string().min(8).max(64) });

export const POST = route({ name: "upgrade.breakthrough", body, limit: 20 }, async ({ user }, b) => {
  const r = await breakthrough(user.id, b.playerId, b.idempotencyKey);
  return { ok: true, replay: r.replay, after: r.upgrade.after };
});
