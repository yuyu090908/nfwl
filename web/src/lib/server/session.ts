import "server-only";
import { createHash, randomBytes } from "node:crypto";
import { cookies } from "next/headers";
import { cache } from "react";
import { db } from "./db";

export const SESSION_COOKIE = "nfwl_session";
const SESSION_DAYS = 30;

const sha256 = (s: string) => createHash("sha256").update(s).digest("hex");

/** Creates a server-side session and sets an HttpOnly, SameSite=Lax cookie. The token is never stored in clear. */
export async function createSession(userId: string, meta: { userAgent?: string | null; ip?: string | null } = {}) {
  const token = randomBytes(32).toString("base64url");
  const expiresAt = new Date(Date.now() + SESSION_DAYS * 86400_000);
  await db.session.create({
    data: { userId, tokenHash: sha256(token), expiresAt, userAgent: meta.userAgent?.slice(0, 300), ip: meta.ip ?? null },
  });
  (await cookies()).set(SESSION_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: expiresAt,
  });
}

export async function destroySession() {
  const jar = await cookies();
  const token = jar.get(SESSION_COOKIE)?.value;
  if (token) await db.session.deleteMany({ where: { tokenHash: sha256(token) } });
  jar.delete(SESSION_COOKIE);
}

export interface CurrentUser {
  id: string;
  displayName: string;
  email: string | null;
  role: string;
  clubId: string | null;
}

/** The signed-in user, or null. Cached per request. Slides the session's lastSeenAt at most once a minute. */
export const getCurrentUser = cache(async (): Promise<CurrentUser | null> => {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!token) return null;
  const s = await db.session.findUnique({
    where: { tokenHash: sha256(token) },
    include: { user: { include: { club: { select: { id: true } } } } },
  });
  if (!s || s.expiresAt < new Date() || s.user.disabledAt) return null;
  if (Date.now() - s.lastSeenAt.getTime() > 60_000)
    await db.session.update({ where: { id: s.id }, data: { lastSeenAt: new Date() } }).catch(() => {});
  return { id: s.user.id, displayName: s.user.displayName, email: s.user.email, role: s.user.role, clubId: s.user.club?.id ?? null };
});
