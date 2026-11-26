import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { db } from "@/lib/server/db";
import { attrsOf } from "@/lib/server/club";
import { AttributeBars } from "@/components/game/attribute-bars";
import { PlayerCard } from "@/components/game/player-card";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge, DemoBadge } from "@/components/ui/badge";
import { Notice } from "@/components/ui/misc";
import { Button } from "@/components/ui/button";
import { overall, type Position, type Rarity } from "@/lib/domain/rules";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "NFT販売" };

export default async function ListingPage({ params }: PageProps<"/marketplace/[id]">) {
  const { id } = await params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) notFound();
  const l = await db.marketplaceListing.findUnique({
    where: { id },
    include: { seller: { select: { displayName: true } }, player: { include: { attributes: true, skills: { include: { skill: true } }, nft: true } }, bids: { orderBy: { amount: "desc" }, take: 10, include: { bidder: { select: { displayName: true } } } } },
  });
  if (!l) notFound();
  const a = attrsOf(l.player.attributes!);
  const ovr = overall(a, l.player.position as Position);
  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <Link href={l.type === "AUCTION" ? "/auctions" : "/marketplace"} className="text-sm text-crystal hover:underline">← 戻る</Link>
      <div className="mt-4 grid gap-8 lg:grid-cols-[320px_1fr]">
        <PlayerCard p={{ id: l.player.id, name: l.player.name, position: l.player.position, rarity: l.player.rarity as Rarity, ovr, level: l.player.level, crystalType: l.player.crystalType, skills: l.player.skills.map((s) => s.skill.name), nft: !!l.player.nft }} />
        <div className="flex flex-col gap-5">
          <div className="flex flex-wrap items-center gap-2">{l.isDemo && <DemoBadge />}<Badge>{l.type === "AUCTION" ? "Auction" : "Fixed price"}</Badge><Badge tone="success">{l.status}</Badge></div>
          <h1 className="font-display text-4xl font-bold uppercase">{l.player.name}</h1>
          <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <div><dt className="text-ink-3">Price</dt><dd className="font-display text-xl font-bold text-gold">{l.price ? `${l.price} ${l.currency}` : l.startPrice ? `${l.startPrice} ${l.currency}` : "—"}</dd></div>
            <div><dt className="text-ink-3">Seller</dt><dd>{l.seller.displayName}</dd></div>
            <div><dt className="text-ink-3">Listed</dt><dd>{fmtDate(l.createdAt)}</dd></div>
            {l.type === "AUCTION" && <div><dt className="text-ink-3">Ends</dt><dd>{fmtDate(l.endsAt)}</dd></div>}
          </dl>
          <Notice tone="warning">本番の売買は無効です。所有権確認・署名検証・決済はチェーン確定後に有効化します。</Notice>
          <Button variant="gold" disabled aria-disabled title="売買は無効です">{l.type === "AUCTION" ? "Place bid" : "Buy now"}（準備中）</Button>
          <Card><CardHeader><CardTitle>Attributes</CardTitle></CardHeader><CardBody><AttributeBars attrs={a} /></CardBody></Card>
          {l.type === "AUCTION" && (
            <Card>
              <CardHeader><CardTitle>Bid history</CardTitle></CardHeader>
              <CardBody>{l.bids.length === 0 ? <p className="text-sm text-ink-2">入札はありません。</p> : <ol className="text-sm">{l.bids.map((b) => <li key={b.id} className="flex justify-between border-b border-line py-1.5"><span>{b.bidder.displayName}</span><span className="font-display">{b.amount.toString()}</span></li>)}</ol>}</CardBody>
            </Card>
          )}
          <Card>
            <CardHeader><CardTitle>Ownership</CardTitle></CardHeader>
            <CardBody className="text-sm">{l.player.nft ? <>Chain {l.player.nft.chainId} · Token {l.player.nft.tokenId}<div className="font-mono text-xs text-ink-2">{l.player.nft.contract}</div></> : "NFT情報なし（デモ）"}</CardBody>
          </Card>
        </div>
      </div>
    </div>
  );
}
