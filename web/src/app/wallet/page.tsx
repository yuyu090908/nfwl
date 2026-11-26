import type { Metadata } from "next";
import { requireUser } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { balanceOf } from "@/lib/server/ledger";
import { WalletButton } from "@/components/auth/wallet-button";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Notice, PageHeader, StatTile } from "@/components/ui/misc";
import { Badge } from "@/components/ui/badge";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "ウォレット" };

export default async function WalletPage() {
  const user = await requireUser("/wallet");
  const [wallets, points, pick, txs] = await Promise.all([
    db.account.findMany({ where: { userId: user.id, provider: "WALLET_EVM" } }),
    balanceOf(user.id, "NFWL_POINTS"),
    balanceOf(user.id, "PICK_SCORE"),
    db.tokenLedgerEntry.findMany({ where: { account: { userId: user.id } }, include: { transaction: true }, orderBy: { createdAt: "desc" }, take: 30 }),
  ]);
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Account" title="Wallet">外部ウォレットの連携と、ゲーム内ポイントの台帳です。</PageHeader>
      <div className="grid grid-cols-2 gap-3">
        <StatTile label="NFWL Points" value={points} hint="Stage 1・オフチェーン。購入/譲渡/換金不可" accent />
        <StatTile label="Pick score" value={pick} hint="予想スコア。価値なし" />
      </div>
      <div className="mt-6 grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Linked wallets</CardTitle></CardHeader>
          <CardBody className="flex flex-col gap-3">
            {wallets.length === 0 ? <p className="text-sm text-ink-2">連携しているウォレットはありません。</p> : wallets.map((w) => {
              const [chain, addr] = w.providerAccountId.split(":");
              return <div key={w.id} className="flex items-center justify-between rounded-lg bg-bg-2 px-3 py-2 text-sm"><span className="font-mono">{addr.slice(0, 6)}…{addr.slice(-4)}</span><Badge>EVM chain {chain}</Badge></div>;
            })}
            <WalletButton mode="link" />
            <p className="text-xs text-ink-3">署名でアドレスの所有を確認するだけです。取引やガス代は発生せず、秘密鍵がサーバーやゲームに送られることはありません。NFTのチェーンは未確定のため、他のチェーンにも対応できる構造にしています。</p>
          </CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Ledger</CardTitle></CardHeader>
          <CardBody>
            {txs.length === 0 ? <p className="text-sm text-ink-2">取引はありません。</p> : (
              <ul className="divide-y divide-line text-sm">
                {txs.map((e) => (
                  <li key={e.id} className="flex items-center justify-between py-2">
                    <span><span className="block">{e.transaction.kind}</span><span className="text-[11px] text-ink-3">{e.transaction.currency} · {fmtDate(e.createdAt)}</span></span>
                    <span className={`font-display ${e.amount >= 0 ? "text-success" : "text-danger"}`}>{e.amount >= 0 ? "+" : ""}{e.amount}</span>
                  </li>
                ))}
              </ul>
            )}
          </CardBody>
        </Card>
      </div>
      <div className="mt-6"><Notice>オンチェーンのNFWL Token（Stage 2）は、法務意見書・規制対応・提供地域・コントラクト監査が揃うまで導入しません。Stage 1 のポイントを将来トークンに交換する約束もしていません。</Notice></div>
    </div>
  );
}
