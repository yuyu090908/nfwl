import type { Metadata } from "next";
import Link from "next/link";
import { divisionTable } from "@/lib/server/queries";
import { getCurrentUser } from "@/lib/server/session";
import { db } from "@/lib/server/db";
import { EmptyState, PageHeader, StatTile } from "@/components/ui/misc";
import { Badge } from "@/components/ui/badge";
import { LiveRefresh } from "@/components/game/live-refresh";
import { StandingsTable } from "@/components/game/standings-table";
import { cn, daysUntil } from "@/lib/utils";

export const metadata: Metadata = { title: "World League" };

export default async function LeaguePage({ searchParams }: PageProps<"/league">) {
  const sp = await searchParams;
  const user = await getCurrentUser();
  const my = user ? await db.club.findUnique({ where: { ownerId: user.id } }) : null;
  const division = Math.min(10, Math.max(1, Number(sp.division) || my?.divisionId || 10));
  const showDemo = sp.demo === "1";
  const { season, rows, cfg } = await divisionTable(division, showDemo);
  const days = season ? daysUntil(season.endsAt) : 0;

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="World League" title={`Division ${division}`} actions={<LiveRefresh division={division} />}>
        勝利 {cfg.league.points.win} / 引分 {cfg.league.points.draw} / 敗北 {cfg.league.points.loss} ポイント（運営設定で変更可）。昇格・降格はRPで決まり、Division 1 上位 {cfg.league.championsLeagueSlots} クラブがChampions Leagueの出場資格を得ます。
      </PageHeader>
      {season && (
        <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatTile label="Season" value={`#${season.number}`} accent />
          <StatTile label="Remaining" value={`${days} days`} />
          <StatTile label="Clubs here" value={rows.length || "—"} />
          <StatTile label="Status" value={season.official ? "Official" : "Beta"} hint={season.official ? "サーバー検証済みの結果のみ" : "ブラウザ報告の試合は暫定集計"} />
        </div>
      )}
      <nav aria-label="Division" className="mb-4 flex flex-wrap gap-1.5">
        {Array.from({ length: 10 }, (_, i) => i + 1).map((d) => (
          <Link key={d} href={`/league?division=${d}${showDemo ? "&demo=1" : ""}`} aria-current={d === division ? "page" : undefined}
            className={cn("rounded-lg border px-3 py-1.5 font-display text-xs font-semibold", d === division ? "border-gold bg-gold/10 text-gold" : "border-line text-ink-2 hover:text-ink")}>D{d}</Link>
        ))}
        <Link href={`/league?division=${division}${showDemo ? "" : "&demo=1"}`} className="ml-auto text-xs text-ink-3 hover:text-ink">{showDemo ? "デモクラブを隠す" : "デモクラブも表示"}</Link>
      </nav>
      {!season ? (
        <EmptyState title="シーズンが始まっていません" />
      ) : rows.length === 0 ? (
        <EmptyState title="このDivisionにはまだクラブがありません">新規クラブは Division 10 から始まります。</EmptyState>
      ) : (
        <StandingsTable
          division={division}
          promotion={cfg.league.promotionSpots}
          relegation={cfg.league.relegationSpots}
          myClubId={my?.id}
          rows={rows.map((r) => ({ clubId: r.clubId, name: r.club.name, isDemo: r.club.isDemo, played: r.played, wins: r.wins, draws: r.draws, losses: r.losses, goalsFor: r.goalsFor, goalsAgainst: r.goalsAgainst, points: r.points, provisional: r.provisional }))}
        />
      )}
      <div className="mt-6 flex flex-wrap gap-3 text-sm">
        <Link href="/league/divisions" className="text-crystal hover:underline">Division一覧</Link>
        <Link href="/league/champions" className="text-crystal hover:underline">Champions League</Link>
        <Badge>同点時: {cfg.league.tieBreak.join(" → ")}</Badge>
      </div>
    </div>
  );
}
