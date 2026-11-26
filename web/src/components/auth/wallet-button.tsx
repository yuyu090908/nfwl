"use client";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { createSiweMessage } from "viem/siwe";
import { api, ClientApiError } from "@/lib/client-api";
import { Notice } from "@/components/ui/misc";

type Eth = { request: (a: { method: string; params?: unknown[] }) => Promise<unknown> };

/**
 * Sign-In with Ethereum through any EIP-1193 wallet (window.ethereum). The wallet signs a
 * human-readable message; no transaction, no gas, no private key ever leaves the wallet.
 */
export function WalletButton({ mode }: { mode: "login" | "link" }) {
  const router = useRouter();
  const [msg, setMsg] = useState<{ tone: "info" | "danger"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  return (
    <div className="flex flex-col gap-2">
      <button
        type="button"
        disabled={busy}
        onClick={async () => {
          const eth = (window as unknown as { ethereum?: Eth }).ethereum;
          if (!eth) {
            setMsg({ tone: "info", text: "ブラウザにウォレット（MetaMask など）が見つかりません。" });
            return;
          }
          setBusy(true);
          setMsg(null);
          try {
            const [address] = (await eth.request({ method: "eth_requestAccounts" })) as string[];
            const chainHex = (await eth.request({ method: "eth_chainId" })) as string;
            const { nonce } = await api<{ nonce: string }>("/api/auth/wallet/nonce", { method: "POST" });
            const message = createSiweMessage({
              domain: window.location.host,
              address: address as `0x${string}`,
              statement: "Sign in to NFWL. This request will not trigger a blockchain transaction or cost any gas.",
              uri: window.location.origin,
              version: "1",
              chainId: parseInt(chainHex, 16),
              nonce,
              issuedAt: new Date(),
              expirationTime: new Date(Date.now() + 10 * 60_000),
            });
            const signature = await eth.request({ method: "personal_sign", params: [message, address] });
            const r = await api<{ next?: string; linked?: boolean }>("/api/auth/wallet/verify", { body: { message, signature } });
            if (r.linked) {
              setMsg({ tone: "info", text: "ウォレットを連携しました。" });
              router.refresh();
            } else {
              router.push(r.next ?? "/club");
              router.refresh();
            }
          } catch (e) {
            setMsg({ tone: "danger", text: e instanceof ClientApiError ? e.message : "署名がキャンセルされたか、失敗しました。" });
          } finally {
            setBusy(false);
          }
        }}
        className="flex h-11 items-center justify-center gap-2 rounded-xl border border-epic/50 bg-epic/10 text-sm font-medium text-[#c7b0ff] hover:bg-epic/20 disabled:opacity-50"
      >
        {busy ? "署名を待っています…" : mode === "login" ? "ウォレットで続ける（EVM）" : "ウォレットを連携（EVM）"}
      </button>
      {msg && <Notice tone={msg.tone}>{msg.text}</Notice>}
    </div>
  );
}
