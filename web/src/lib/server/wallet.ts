import "server-only";
import { verifyMessage } from "viem";
import { parseSiweMessage, validateSiweMessage } from "viem/siwe";

/**
 * Chain-agnostic wallet layer. The chain for NFTs is not decided yet (Master Doc 0.3), so
 * everything else talks to this interface. EVM (EIP-4361 sign-in) is the first adapter;
 * another chain adds another adapter without touching accounts, clubs or the market.
 */
export interface WalletAdapter {
  id: string;
  verify(message: string, signature: string, expectedHost: string): Promise<
    { ok: true; address: string; chainId: number; nonce: string } | { ok: false; error: string }
  >;
  /** Whether `address` currently owns `tokenId` on-chain. Not implemented until a chain and contract exist. */
  ownerOf?(contract: string, tokenId: string): Promise<string | null>;
}

export const evmWallet: WalletAdapter = {
  id: "evm",
  async verify(message, signature, expectedHost) {
    let parsed;
    try {
      parsed = parseSiweMessage(message);
    } catch {
      return { ok: false, error: "malformed message" };
    }
    if (!parsed.address || !parsed.nonce || !parsed.chainId) return { ok: false, error: "missing fields" };
    const domain = expectedHost.split(":")[0] === parsed.domain?.split(":")[0];
    if (!domain) return { ok: false, error: "domain mismatch" };
    if (!validateSiweMessage({ message: parsed, domain: parsed.domain, nonce: parsed.nonce, time: new Date() }))
      return { ok: false, error: "expired or invalid message" };
    if (parsed.issuedAt && Date.now() - parsed.issuedAt.getTime() > 10 * 60_000) return { ok: false, error: "message too old" };
    const good = await verifyMessage({ address: parsed.address, message, signature: signature as `0x${string}` }).catch(() => false);
    if (!good) return { ok: false, error: "signature does not match" };
    return { ok: true, address: parsed.address, chainId: parsed.chainId, nonce: parsed.nonce };
  },
};
