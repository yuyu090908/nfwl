import { generateSiweNonce } from "viem/siwe";
import { db } from "@/lib/server/db";
import { route } from "@/lib/server/api";

/** One-time nonce for Sign-In with Ethereum (EIP-4361). Valid 10 minutes, usable once. */
export const POST = route({ name: "auth.wallet.nonce", auth: false, limit: 20 }, async () => {
  const nonce = generateSiweNonce();
  await db.authNonce.create({ data: { nonce, purpose: "siwe", expiresAt: new Date(Date.now() + 10 * 60_000) } });
  return { nonce };
});
