import type { Metadata } from "next";
import Link from "next/link";
import { requireClub } from "@/lib/server/queries";
import { getRoster } from "@/lib/server/club";
import { PlayerCard } from "@/components/game/player-card";
import { EmptyState, PageHeader } from "@/components/ui/misc";
import { POSITIONS, RARITIES, type Rarity } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "保有選手" };

export default async function PlayersPage({ searchParams }: PageProps<"/players">) {
  const { club } = await requireClub("/players");
  const sp = await searchParams;
  const pos = typeof sp.position === "string" ? sp.position : "";
  const rar = typeof sp.rarity === "string" ? sp.rarity : "";
  const sort = typeof sp.sort === "string" ? sp.sort : "ovr";
  let list = await getRoster(club.id);
  if (pos) list = list.filter((p) => p.position === pos);
  if (rar) list = list.filter((p) => p.rarity === rar);
  list.sort((a, b) => (sort === "level" ? b.level - a.level : sort === "name" ? a.name.localeCompare(b.name) : b.ovr - a.ovr));
  const q = (k: string, v: string) => {
    const o = { position: pos, rarity: rar, sort, [k]: v };
    return "?" + new URLSearchParams(Object.entries(o).filter(([, x]) => x) as [string, string][]).toString();
  };
  const chip = (active: boolean) => cn("rounded-lg border px-3 py-1.5 text-xs font-semibold uppercase tracking-wider", active ? "border-gold bg-gold/10 text-gold" : "border-line text-ink-2 hover:text-ink");

  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow={club.name} title="Players">クラブ選手（非NFT）とNFT選手を区別して表示します。NFT発行済みの選手には NFT と表示されます。</PageHeader>
      <div className="mb-6 flex flex-col gap-3">
        <div className="flex flex-wrap gap-2" role="group" aria-label="ポジション">
          <Link href={q("position", "")} className={chip(!pos)}>All</Link>
          {POSITIONS.map((p) => <Link key={p} href={q("position", p)} className={chip(pos === p)}>{p}</Link>)}
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="レアリティ">
          <Link href={q("rarity", "")} className={chip(!rar)}>All rarities</Link>
          {RARITIES.map((r) => <Link key={r} href={q("rarity", r)} className={chip(rar === r)}>{r}</Link>)}
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="並び替え">
          {[["ovr", "OVR"], ["level", "Level"], ["name", "Name"]].map(([k, l]) => <Link key={k} href={q("sort", k)} className={chip(sort === k)}>Sort: {l}</Link>)}
        </div>
      </div>
      {list.length === 0 ? (
        <EmptyState title="該当する選手はいません" />
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {list.map((p) => (
            <PlayerCard key={p.id} href={`/players/${p.id}`} p={{ id: p.id, name: p.name, position: p.position, rarity: p.rarity as Rarity, ovr: p.ovr, level: p.level, crystalType: p.crystalType, skills: p.skills.map((s) => s.skill.name), nft: !!p.nft }} />
          ))}
        </div>
      )}
    </div>
  );
}
