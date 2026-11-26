import { z } from "zod";
import { route } from "@/lib/server/api";
import { db } from "@/lib/server/db";

const body = z.object({
  displayName: z.string().trim().min(2).max(32).regex(/^[^<>]+$/).optional(),
  settings: z
    .object({
      graphics: z.enum(["auto", "low", "medium", "high", "ultra"]).optional(),
      muted: z.boolean().optional(),
      reducedMotion: z.boolean().optional(),
      showWalletPublicly: z.boolean().optional(),
    })
    .optional(),
});

export const PUT = route({ name: "settings.save", body, limit: 30 }, async ({ user }, b) => {
  const u = await db.user.findUniqueOrThrow({ where: { id: user.id } });
  await db.user.update({
    where: { id: user.id },
    data: {
      displayName: b.displayName ?? u.displayName,
      settings: { ...(u.settings as object), ...(b.settings ?? {}) },
    },
  });
  return { ok: true };
});
