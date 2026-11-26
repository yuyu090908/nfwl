import { z } from "zod";
import { db } from "@/lib/server/db";
import { route, ApiError } from "@/lib/server/api";
import { hashPassword } from "@/lib/server/password";
import { createSession } from "@/lib/server/session";
import { audit } from "@/lib/server/audit";

const body = z.object({
  email: z.string().trim().toLowerCase().email().max(254),
  password: z.string().min(10, "10文字以上").max(200),
  displayName: z.string().trim().min(2).max(32).regex(/^[^<>]+$/),
  birthYear: z.number().int().min(1900).max(new Date().getFullYear()).optional(),
  acceptTerms: z.literal(true),
});

export const POST = route({ name: "auth.register", auth: false, body, limit: 10 }, async ({ req, ip }, b) => {
  const exists = await db.account.findUnique({ where: { provider_providerAccountId: { provider: "EMAIL", providerAccountId: b.email } } });
  if (exists) throw new ApiError(409, "EMAIL_TAKEN", "このメールアドレスは登録済みです");
  const passwordHash = await hashPassword(b.password);
  const user = await db.user.create({
    data: {
      email: b.email,
      displayName: b.displayName,
      birthYear: b.birthYear,
      accounts: { create: { provider: "EMAIL", providerAccountId: b.email, passwordHash } },
    },
  });
  await createSession(user.id, { userAgent: req.headers.get("user-agent"), ip });
  await audit("auth.register", { userId: user.id, ip });
  return { ok: true, next: "/onboarding" };
});
