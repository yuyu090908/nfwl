import type { Metadata } from "next";
import Link from "next/link";
import { searchListings } from "@/lib/server/market";
import { EmptyState, Notice, PageHeader } from "@/components/ui/misc";
import { Badge, DemoBadge } from "@/components/ui/badge";
import { PlayerCard } from "@/components/game/player-card";
import type { Rarity } from "@/lib/domain/rules";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "Auctions" };

export default async function AuctionsPage() {
  const list = await searchListings({ type: "AUCTION", sort: "recent" });
  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="NFT" title="Auctions" actions={<Link href="/marketplace" className="text-sm text-crystal hover:underline">← Marketplace</Link>}>
        開始価格・現在の最高入札・最低入札単位・終了時刻・入札履歴を表示します。
      </PageHeader>
      <div className="mb-6"><Notice tone="warning" title="入札は無効です">入札資金の確保（エスクロー）と決済失敗時の復旧は、スマートコントラクト確定後に実装します。</Notice></div>
      {list.length === 0 ? (
        <EmptyState title="開催中のオークションはありません" />
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {list.map((l) => (
            <PlayerCard key={l.id} href={`/marketplace/${l.id}`}
              p={{ id: l.player.id, name: l.player.name, position: l.player.position, rarity: l.player.rarity as Rarity, ovr: l.ovr, level: l.player.level, crystalType: l.player.crystalType, nft: !!l.player.nft }}
              footer={
                <div className="mt-1.5 space-y-0.5 text-[11px] text-ink-2">
                  <div className="flex justify-between"><span>Start</span><span>{l.startPrice?.toString() ?? "—"}</span></div>
                  <div className="flex justify-between"><span>Top bid</span><span className="text-gold">{l.bids[0]?.amount.toString() ?? "—"}</span></div>
                  <div className="flex justify-between"><span>Min +</span><span>{l.minIncrement?.toString() ?? "—"}</span></div>
                  <div className="flex justify-between"><span>Ends</span><span>{fmtDate(l.endsAt)}</span></div>
                  <div className="flex gap-1">{l.isDemo && <DemoBadge />}<Badge>{l.status}</Badge></div>
                </div>
              } />
          ))}
        </div>
      )}
    </div>
  );
}
