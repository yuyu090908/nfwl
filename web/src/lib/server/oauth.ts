import "server-only";
import { Discord, Google, decodeIdToken, type OAuth2Tokens } from "arctic";

// Social login. A provider is active only when its client id/secret are set in the environment;
// without them the button is shown as "not configured" and these routes answer 404.
export type OAuthProvider = "google" | "discord";

function base() {
  return (process.env.APP_URL ?? "http://localhost:3000").replace(/\/$/, "");
}

export function oauthClient(p: OAuthProvider) {
  const redirect = `${base()}/api/auth/oauth/${p}/callback`;
  if (p === "google" && process.env.GOOGLE_CLIENT_ID && process.env.GOOGLE_CLIENT_SECRET)
    return new Google(process.env.GOOGLE_CLIENT_ID, process.env.GOOGLE_CLIENT_SECRET, redirect);
  if (p === "discord" && process.env.DISCORD_CLIENT_ID && process.env.DISCORD_CLIENT_SECRET)
    return new Discord(process.env.DISCORD_CLIENT_ID, process.env.DISCORD_CLIENT_SECRET, redirect);
  return null;
}

export function enabledProviders(): OAuthProvider[] {
  return (["google", "discord"] as const).filter((p) => oauthClient(p) !== null);
}

export async function profileFrom(p: OAuthProvider, tokens: OAuth2Tokens) {
  if (p === "google") {
    const c = decodeIdToken(tokens.idToken()) as { sub: string; email?: string; email_verified?: boolean; name?: string };
    return { id: c.sub, email: c.email_verified ? c.email ?? null : null, name: c.name ?? "Manager" };
  }
  const r = await fetch("https://discord.com/api/users/@me", { headers: { Authorization: `Bearer ${tokens.accessToken()}` } });
  if (!r.ok) throw new Error("discord profile failed");
  const u = (await r.json()) as { id: string; email?: string; verified?: boolean; global_name?: string; username: string };
  return { id: u.id, email: u.verified ? u.email ?? null : null, name: u.global_name ?? u.username };
}
