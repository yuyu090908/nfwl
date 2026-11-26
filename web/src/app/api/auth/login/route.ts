import { z } from "zod";
import { db } from "@/lib/server/db";
import { route, ApiError } from "@/lib/server/api";
import { dummyVerify, verifyPassword } from "@/lib/server/password";
import { createSession } from "@/lib/server/session";
import { audit } from "@/lib/server/audit";
import { rateLimit } from "@/lib/server/rate-limit";

const body = z.object({ email: z.string().trim().toLowerCase().email(), password: z.string().min(1).max(200) });

export const POST = route({ name: "auth.login", auth: false, body, limit: 20 }, async ({ req, ip }, b) => {
  // Per-account limit on top of the per-IP one, against password spraying.
  if (!rateLimit(`login-acct:${b.email}`, 10, 15 * 60_000).ok) throw new ApiError(429, "RATE_LIMITED");
  const acct = await db.account.findUnique({
    where: { provider_providerAccountId: { provider: "EMAIL", providerAccountId: b.email } },
    include: { user: { include: { club: true } } },
  });
  if (!acct?.passwordHash) {
    await dummyVerify();
    throw new ApiError(401, "BAD_CREDENTIALS", "メールアドレスまたはパスワードが違います");
  }
  if (!(await verifyPassword(b.password, acct.passwordHash)) || acct.user.disabledAt) {
    await audit("auth.login_failed", { userId: acct.userId, ip, severity: "warn" });
    throw new ApiError(401, "BAD_CREDENTIALS", "メールアドレスまたはパスワードが違います");
  }
  await createSession(acct.userId, { userAgent: req.headers.get("user-agent"), ip });
  await audit("auth.login", { userId: acct.userId, ip });
  return { ok: true, next: acct.user.club ? "/club" : "/onboarding" };
});
