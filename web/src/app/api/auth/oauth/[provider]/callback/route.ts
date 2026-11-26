import { NextResponse, type NextRequest } from "next/server";
import { oauthClient, profileFrom, type OAuthProvider } from "@/lib/server/oauth";
import { db } from "@/lib/server/db";
import { createSession, getCurrentUser } from "@/lib/server/session";
import { audit } from "@/lib/server/audit";
import { clientIp } from "@/lib/server/api";

export async function GET(req: NextRequest, { params }: { params: Promise<{ provider: string }> }) {
  const { provider } = await params;
  const p = provider as OAuthProvider;
  const client = oauthClient(p);
  if (!client) return NextResponse.json({ error: "PROVIDER_NOT_CONFIGURED" }, { status: 404 });
  const code = req.nextUrl.searchParams.get("code");
  const state = req.nextUrl.searchParams.get("state");
  const saved = req.cookies.get("nfwl_oauth_state")?.value;
  const verifier = req.cookies.get("nfwl_oauth_verifier")?.value;
  const fail = (why: string) => NextResponse.redirect(new URL(`/login?error=${why}`, req.url));
  if (!code || !state || !saved || state !== saved || !verifier) return fail("oauth_state");
  let profile;
  try {
    const tokens = await client.validateAuthorizationCode(code, verifier);
    profile = await profileFrom(p, tokens);
  } catch {
    return fail("oauth_exchange");
  }
  const provEnum = p === "google" ? "GOOGLE" : "DISCORD";
  const existing = await db.account.findUnique({ where: { provider_providerAccountId: { provider: provEnum, providerAccountId: profile.id } } });
  const current = await getCurrentUser();
  let userId = existing?.userId ?? current?.id;
  if (!userId) {
    const u = await db.user.create({ data: { displayName: profile.name.slice(0, 32), email: null } });
    userId = u.id;
  }
  if (!existing) await db.account.create({ data: { userId, provider: provEnum, providerAccountId: profile.id } });
  await createSession(userId, { userAgent: req.headers.get("user-agent"), ip: clientIp(req) });
  await audit("auth.oauth", { userId, ip: clientIp(req), data: { provider: p } });
  const club = await db.club.findUnique({ where: { ownerId: userId } });
  const res = NextResponse.redirect(new URL(club ? "/club" : "/onboarding", req.url));
  res.cookies.delete("nfwl_oauth_state");
  res.cookies.delete("nfwl_oauth_verifier");
  return res;
}
