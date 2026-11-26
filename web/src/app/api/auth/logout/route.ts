import { route } from "@/lib/server/api";
import { destroySession } from "@/lib/server/session";

export const POST = route({ name: "auth.logout", auth: false }, async () => {
  await destroySession();
  return { ok: true };
});
