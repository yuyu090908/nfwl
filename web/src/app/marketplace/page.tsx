import type { Metadata } from "next";
import Link from "next/link";
import { searchListings } from "@/lib/server/market";
import { db } from "@/lib/server/db";
import { EmptyState, Notice, PageHeader } from "@/components/ui/misc";
import { PlayerCard } from "@/components/game/player-card";
import { MarketFilters } from "@/components/game/market-filters";
import { Badge, DemoBadge } from "@/components/ui/badge";
import type { Rarity } from "@/lib/domain/rules";

export const metadata: Metadata = { title: "Marketplace" };

const num = (v: unknown) => (typeof v === "string" && v ? Number(v) : undefined);

export default async function MarketplacePage({ searchParams }: PageProps<"/marketplace">) {
  const raw = await searchParams;
  const sp = Object.fromEntries(Object.entries(raw).map(([k, v]) => [k, typeof v === "string" ? v : undefined]));
  const [list, skills] = await Promise.all([
    searchListings({ rarity: sp.rarity, position: sp.position, crystal: sp.crystal, skill: sp.skill, minOvr: num(sp.minOvr), minLevel: num(sp.minLevel), maxPrice: num(sp.maxPrice), sort: sp.sort, type: "FIXED_PRICE" }),
    db.skill.findMany({ select: { id: true, name: true } }),
  ]);
  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="NFT" title="Marketplace" actions={<Link href="/auctions" className="text-sm text-crystal hover:underline">Auctions →</Link>}>
        NFT選手の出品・購入。クラブ選手（初期配布など非NFT）は出品できません。
      </PageHeader>
      <div className="mb-6">
        <Notice tone="warning" title="売買はまだ有効になっていません">
          使用チェーンとスマートコントラクトの仕様が確定し、法令確認が終わるまで、出品・購入・入札はできません。表示中の出品はデータ構造と画面の確認用です。
        </Notice>
      </div>
      <MarketFilters sp={sp} skills={skills} action="/marketplace" />
      <div className="mt-6">
        {list.length === 0 ? (
          <EmptyState title="出品はありません">本番売買が始まると、ここにNFT選手が並びます。</EmptyState>
        ) : (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
            {list.map((l) => (
              <PlayerCard
                key={l.id}
                href={`/marketplace/${l.id}`}
                p={{ id: l.player.id, name: l.player.name, position: l.player.position, rarity: l.player.rarity as Rarity, ovr: l.ovr, level: l.player.level, crystalType: l.player.crystalType, skills: l.player.skills.map((s) => s.skill.name), nft: !!l.player.nft }}
                footer={
                  <div className="mt-1.5 flex items-center justify-between gap-1 text-xs">
                    <span className="font-display font-bold text-gold">{l.price ? `${l.price.toString()} ${l.currency}` : "—"}</span>
                    <span className="flex gap-1">{l.isDemo && <DemoBadge />}<Badge tone={l.status === "SOLD" ? "neutral" : "success"}>{l.status}</Badge></span>
                  </div>
                }
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
