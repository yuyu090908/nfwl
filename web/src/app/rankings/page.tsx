import type { Metadata } from "next";
import Link from "next/link";
import { db } from "@/lib/server/db";
import { EmptyState, PageHeader } from "@/components/ui/misc";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Rankings" };

const TABS = [
  ["world", "World"], ["division", "Division"], ["club", "Club level"], ["scorers", "Top scorers"],
  ["assists", "Top assists"], ["keepers", "Best goalkeepers"], ["winrate", "Win rate"], ["champions", "Season champions"],
] as const;

type Row = { key: string; name: string; sub?: string; value: string | number; href?: string };

async function load(tab: string): Promise<{ rows: Row[]; note?: string }> {
  const real = { isDemo: false };
  switch (tab) {
    case "world": {
      const cs = await db.club.findMany({ where: real, orderBy: [{ rp: "desc" }, { mmr: "desc" }, { createdAt: "asc" }], take: 100 });
      return { rows: cs.map((c) => ({ key: c.id, name: c.name, sub: `Division ${c.divisionId}`, value: `${c.rp} RP` })) };
    }
    case "division": {
      const cs = await db.club.findMany({ where: real, orderBy: [{ divisionId: "asc" }, { rp: "desc" }], take: 100 });
      return { rows: cs.map((c) => ({ key: c.id, name: c.name, sub: `Division ${c.divisionId}`, value: `${c.rp} RP`, href: `/league?division=${c.divisionId}` })) };
    }
    case "club": {
      const cs = await db.club.findMany({ where: real, orderBy: [{ level: "desc" }, { clubXp: "desc" }], take: 100 });
      return { rows: cs.map((c) => ({ key: c.id, name: c.name, sub: `${c.clubXp} club XP`, value: `Lv ${c.level}` })) };
    }
    case "scorers": {
      const g = await db.matchStatistic.groupBy({
        by: ["playerId"],
        where: { playerId: { not: null }, goals: { gt: 0 }, match: { status: "FINISHED", verification: { in: ["CLIENT_REPORTED", "SERVER_SIMULATED"] } } },
        _sum: { goals: true },
        orderBy: { _sum: { goals: "desc" } },
        take: 50,
      });
      const ps = await db.player.findMany({ where: { id: { in: g.map((x) => x.playerId!) } }, include: { ownership: { where: { active: true }, include: { club: true } } } });
      return {
        rows: g.map((x) => {
          const p = ps.find((y) => y.id === x.playerId);
          return { key: x.playerId!, name: p?.name ?? "—", sub: p?.ownership[0]?.club.name, value: `${x._sum.goals} G`, href: `/players/${x.playerId}` };
        }),
        note: "AI Match（ブラウザ報告・検証通過）を含みます。",
      };
    }
    case "assists":
      return { rows: [], note: "アシストは現在の試合エンジン（Unity v0.7）がまだ記録していないため、集計できません。" };
    case "keepers":
      return { rows: [], note: "セーブ数は現在の試合エンジンからまだ送られていないため、集計できません。" };
    case "winrate": {
      const cs = await db.leagueStanding.findMany({ where: { played: { gte: 5 }, club: real }, include: { club: true } });
      const rows = cs
        .map((s) => ({ key: s.id, name: s.club.name, sub: `${s.played} 試合`, rate: s.wins / s.played }))
        .sort((a, b) => b.rate - a.rate)
        .slice(0, 100)
        .map((r) => ({ ...r, value: `${Math.round(r.rate * 100)}%` }));
      return { rows, note: "今シーズン5試合以上のクラブが対象です。" };
    }
    case "champions":
      return { rows: [], note: "シーズン終了処理が行われると、各Divisionの優勝クラブがここに記録されます。" };
  }
  return { rows: [] };
}

export default async function RankingsPage({ searchParams }: PageProps<"/rankings">) {
  const sp = await searchParams;
  const tab = TABS.some(([k]) => k === sp.tab) ? String(sp.tab) : "world";
  const { rows, note } = await load(tab);
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Global" title="Rankings">デモクラブは含みません。</PageHeader>
      <nav aria-label="ランキングの種類" className="mb-6 flex gap-1.5 overflow-x-auto pb-1">
        {TABS.map(([k, l]) => (
          <Link key={k} href={`/rankings?tab=${k}`} aria-current={tab === k ? "page" : undefined}
            className={cn("whitespace-nowrap rounded-lg border px-3 py-1.5 text-xs font-semibold uppercase tracking-wider", tab === k ? "border-gold bg-gold/10 text-gold" : "border-line text-ink-2 hover:text-ink")}>{l}</Link>
        ))}
      </nav>
      {note && <p className="mb-3 text-xs text-ink-3">{note}</p>}
      {rows.length === 0 ? (
        <EmptyState title="まだデータがありません" />
      ) : (
        <ol className="divide-y divide-line overflow-hidden rounded-2xl border border-line">
          {rows.map((r, i) => (
            <li key={r.key} className="flex items-center gap-4 bg-card/50 px-4 py-3">
              <span className={cn("w-8 font-display text-lg font-bold", i < 3 ? "text-gold" : "text-ink-2")}>{i + 1}</span>
              <span className="min-w-0 flex-1">
                {r.href ? <Link href={r.href} className="block truncate font-medium hover:text-crystal">{r.name}</Link> : <span className="block truncate font-medium">{r.name}</span>}
                {r.sub && <span className="text-xs text-ink-3">{r.sub}</span>}
              </span>
              <span className="font-display font-semibold">{r.value}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
