import { z } from "zod";
import { route, ApiError } from "@/lib/server/api";
import { db } from "@/lib/server/db";
import { getGameConfig } from "@/lib/server/game-config";
import { achieve } from "@/lib/server/match-session";

const body = z.object({ matchId: z.string().uuid(), pick: z.enum(["HOME", "DRAW", "AWAY"]) });

/** Free Pick: free, no stake, no payout. Correct picks earn PICK_SCORE (titles/badges only). */
export const POST = route({ name: "predictions.pick", body, limit: 30 }, async ({ user }, b) => {
  const cfg = await getGameConfig();
  const m = await db.match.findUnique({ where: { id: b.matchId } });
  if (!m || m.mode !== "LEAGUE_SIM") throw new ApiError(404, "MATCH_NOT_FOUND");
  const closesAt = (m.kickoffAt?.getTime() ?? 0) - cfg.predictions.closeMinutesBeforeKickoff * 60_000;
  if (m.status !== "SCHEDULED" || Date.now() >= closesAt) throw new ApiError(409, "PICKS_CLOSED", "受付は終了しました");
  const own = await db.club.findUnique({ where: { ownerId: user.id } });
  if (own && (own.id === m.homeClubId || own.id === m.awayClubId)) throw new ApiError(409, "OWN_MATCH", "自分のクラブの試合は予想できません");
  await db.predictionEntry.upsert({
    where: { userId_matchId: { userId: user.id, matchId: m.id } },
    update: { pick: b.pick },
    create: { userId: user.id, matchId: m.id, pick: b.pick },
  });
  await db.$transaction((tx) => achieve(tx, user.id, "first-pick"));
  return { ok: true };
});
