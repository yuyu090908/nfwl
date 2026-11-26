import { NextResponse, type NextRequest } from "next/server";
import { generateCodeVerifier, generateState, Google } from "arctic";
import { oauthClient, type OAuthProvider } from "@/lib/server/oauth";

export async function GET(_req: NextRequest, { params }: { params: Promise<{ provider: string }> }) {
  const { provider } = await params;
  const client = oauthClient(provider as OAuthProvider);
  if (!client) return NextResponse.json({ error: "PROVIDER_NOT_CONFIGURED" }, { status: 404 });
  const state = generateState();
  const verifier = generateCodeVerifier();
  const url =
    client instanceof Google
      ? client.createAuthorizationURL(state, verifier, ["openid", "profile", "email"])
      : client.createAuthorizationURL(state, verifier, ["identify", "email"]);
  const res = NextResponse.redirect(url);
  const opts = { httpOnly: true, secure: process.env.NODE_ENV === "production", sameSite: "lax" as const, path: "/", maxAge: 600 };
  res.cookies.set("nfwl_oauth_state", state, opts);
  res.cookies.set("nfwl_oauth_verifier", verifier, opts);
  return res;
}
