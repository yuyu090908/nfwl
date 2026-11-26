import { z } from "zod";
import { route, ApiError } from "@/lib/server/api";
import { db } from "@/lib/server/db";
import { evaluateLineup, getRoster } from "@/lib/server/club";
import { getGameConfig } from "@/lib/server/game-config";
import { audit } from "@/lib/server/audit";

const body = z.object({
  starters: z.array(z.string().uuid()).length(5),
  bench: z.array(z.string().uuid()).max(3).default([]),
});

export const PUT = route({ name: "squad.save", body, limit: 30 }, async ({ user }, b) => {
  const club = await db.club.findUnique({ where: { ownerId: user.id } });
  if (!club) throw new ApiError(409, "NO_CLUB");
  const cfg = await getGameConfig();
  const roster = await getRoster(club.id);
  const lineup = evaluateLineup(roster, b.starters, cfg);
  if (!lineup.ok) throw new ApiError(400, "INVALID_SQUAD", lineup.errors.join(" / "));
  const bench = b.bench.filter((id) => !b.starters.includes(id) && roster.some((p) => p.id === id));
  const squad = await db.squad.findFirst({ where: { clubId: club.id, isActive: true } });
  if (squad) await db.squad.update({ where: { id: squad.id }, data: { starters: b.starters, bench } });
  else await db.squad.create({ data: { clubId: club.id, formationId: "2-1-1", starters: b.starters, bench } });
  if (club.onboardingStep < 3) await db.club.update({ where: { id: club.id }, data: { onboardingStep: 3 } });
  await audit("squad.save", { userId: user.id, target: club.id, data: { starters: b.starters } });
  return {
    ok: true,
    slots: lineup.slots.map((s) => ({ slot: s!.slot, fit: s!.fit, ovrInSlot: s!.ovrInSlot, changes: s!.changes })),
  };
});
