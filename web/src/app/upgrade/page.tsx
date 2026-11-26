import type { Metadata } from "next";
import Link from "next/link";
import { requireClub } from "@/lib/server/queries";
import { getRoster } from "@/lib/server/club";
import { getGameConfig } from "@/lib/server/game-config";
import { balanceOf } from "@/lib/server/ledger";
import { nextBreakthrough } from "@/lib/server/upgrade";
import { PageHeader } from "@/components/ui/misc";
import { UpgradePanel } from "@/components/club/upgrade-panel";
import { CrystaliaPortrait } from "@/components/game/crystalia-portrait";
import { xpToNext, type Position, type Rarity } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "選手育成" };

export default async function UpgradePage({ searchParams }: PageProps<"/upgrade">) {
  const { user, club } = await requireClub("/upgrade");
  const sp = await searchParams;
  const [roster, cfg, balance] = await Promise.all([getRoster(club.id), getGameConfig(), balanceOf(user.id, "NFWL_POINTS")]);
  const sel = roster.find((p) => p.id === sp.player) ?? roster[0];
  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Academy" title="Player upgrade">
        試合でXPを獲得してレベルアップし、成長ポイントで能力を伸ばします。能力はレアリティごとの上限を超えられません。上限解放には NFWL Points（ゲーム内専用・譲渡/換金不可）を使います。
      </PageHeader>
      <div className="grid gap-6 lg:grid-cols-[260px_1fr]">
        <nav aria-label="選手を選択" className="flex gap-2 overflow-x-auto lg:flex-col lg:overflow-visible">
          {roster.map((p) => (
            <Link key={p.id} href={`/upgrade?player=${p.id}`} aria-current={p.id === sel?.id ? "page" : undefined}
              className={cn("flex min-w-[180px] items-center gap-2 rounded-xl border p-2 text-sm", p.id === sel?.id ? "border-gold bg-gold/10" : "border-line hover:border-crystal/40")}>
              <CrystaliaPortrait crystal={p.crystalType} seed={p.id} className="h-9 w-9 shrink-0" />
              <span className="min-w-0"><span className="block truncate font-semibold">{p.name}</span><span className="text-xs text-ink-2">{p.position} · Lv {p.level} · {p.growthPoints} GP</span></span>
            </Link>
          ))}
        </nav>
        {sel && (
          <div>
            <h2 className="mb-4 font-display text-2xl font-bold uppercase">{sel.name}</h2>
            <UpgradePanel
              key={sel.id + sel.level + sel.growthPoints + sel.levelCap}
              balance={balance}
              p={{
                id: sel.id, name: sel.name, position: sel.position as Position, level: sel.level, levelCap: sel.levelCap, xp: sel.xp,
                xpToNext: xpToNext(sel.level), growthPoints: sel.growthPoints, attrs: sel.attrs,
                cap: cfg.rarity[sel.rarity as Rarity].attributeCap, breakthrough: nextBreakthrough(sel.levelCap, cfg),
              }}
            />
          </div>
        )}
      </div>
    </div>
  );
}
