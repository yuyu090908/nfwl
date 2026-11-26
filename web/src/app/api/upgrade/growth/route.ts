import { z } from "zod";
import { route } from "@/lib/server/api";
import { allocateGrowth } from "@/lib/server/upgrade";
import { ATTRIBUTE_KEYS } from "@/lib/domain/rules";

const alloc = z.object(Object.fromEntries(ATTRIBUTE_KEYS.map((k) => [k, z.number().int().min(0).max(60).optional()])));
const body = z.object({ playerId: z.string().uuid(), alloc, idempotencyKey: z.string().min(8).max(64) });

export const POST = route({ name: "upgrade.growth", body, limit: 30 }, async ({ user }, b) => {
  const r = await allocateGrowth(user.id, b.playerId, b.alloc, b.idempotencyKey);
  return { ok: true, replay: r.replay, after: r.upgrade.after };
});
