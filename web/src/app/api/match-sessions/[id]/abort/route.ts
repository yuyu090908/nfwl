import { route, ApiError } from "@/lib/server/api";
import { db } from "@/lib/server/db";
import { abortSession } from "@/lib/server/match-session";

export const POST = route<{ id: string }>({ name: "match.abort", limit: 20 }, async ({ user, params }) => {
  const s = await db.matchSession.findUnique({ where: { id: params.id } });
  if (!s || s.userId !== user.id) throw new ApiError(404, "SESSION_NOT_FOUND");
  await abortSession(s.id, "user");
  return { ok: true };
});
