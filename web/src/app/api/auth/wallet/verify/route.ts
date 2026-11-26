import { z } from "zod";
import { db } from "@/lib/server/db";
import { route, ApiError } from "@/lib/server/api";
import { createSession } from "@/lib/server/session";
import { audit } from "@/lib/server/audit";
import { evmWallet } from "@/lib/server/wallet";

const body = z.object({ message: z.string().min(20).max(2000), signature: z.string().regex(/^0x[0-9a-fA-F]+$/).max(1000) });

/**
 * Verifies a signed SIWE message. Signed in: links the wallet to the account.
 * Signed out: signs in with the wallet (creates an account on first use; a club is created in onboarding).
 * Only the public address is stored. Private keys never reach the server or Unity.
 */
export const POST = route({ name: "auth.wallet.verify", auth: false, body, limit: 20 }, async ({ req, user, ip }, b) => {
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host") ?? "";
  const res = await evmWallet.verify(b.message, b.signature as `0x${string}`, host);
  if (!res.ok) throw new ApiError(401, "BAD_SIGNATURE", res.error);
  const used = await db.authNonce.updateMany({
    where: { nonce: res.nonce, purpose: "siwe", usedAt: null, expiresAt: { gt: new Date() } },
    data: { usedAt: new Date() },
  });
  if (used.count !== 1) throw new ApiError(401, "NONCE_INVALID");

  const providerAccountId = `${res.chainId}:${res.address.toLowerCase()}`;
  const existing = await db.account.findUnique({ where: { provider_providerAccountId: { provider: "WALLET_EVM", providerAccountId } } });
  if (user) {
    if (existing && existing.userId !== user.id) throw new ApiError(409, "WALLET_LINKED_ELSEWHERE");
    if (!existing) await db.account.create({ data: { userId: user.id, provider: "WALLET_EVM", providerAccountId } });
    await audit("wallet.link", { userId: user.id, ip, data: { address: res.address, chainId: res.chainId } });
    return { ok: true, linked: true };
  }
  let userId = existing?.userId;
  if (!userId) {
    const u = await db.user.create({
      data: {
        displayName: `Manager-${res.address.slice(2, 6)}`,
        accounts: { create: { provider: "WALLET_EVM", providerAccountId } },
      },
    });
    userId = u.id;
    await audit("auth.register_wallet", { userId, ip });
  }
  await createSession(userId, { userAgent: req.headers.get("user-agent"), ip });
  const club = await db.club.findUnique({ where: { ownerId: userId } });
  return { ok: true, next: club ? "/club" : "/onboarding" };
});
