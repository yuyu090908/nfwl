import type { Metadata } from "next";
import { divisionTable } from "@/lib/server/queries";
import { EmptyState, Notice, PageHeader } from "@/components/ui/misc";
import { StandingsTable } from "@/components/game/standings-table";

export const metadata: Metadata = { title: "Champions League" };

export default async function ChampionsPage() {
  const { rows, cfg } = await divisionTable(1);
  const slots = cfg.league.championsLeagueSlots;
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Division 0" title="Champions League">
        Division 1 の上位 {slots} クラブが出場資格を得ます。資格は成績だけで決まり、NFTの所有や購入とは関係ありません。
      </PageHeader>
      <Notice title="大会の進行について">グループステージ・決勝トーナメントの運営機能は未実装です。現在はDivision 1の出場圏（上位{slots}）のみ表示しています。</Notice>
      <div className="mt-6">
        {rows.length === 0 ? (
          <EmptyState title="まだ Division 1 のクラブはありません">Division 1（900 RP以上）に到達したクラブがここに表示されます。</EmptyState>
        ) : (
          <StandingsTable division={1} promotion={slots} relegation={0}
            rows={rows.slice(0, slots).map((r) => ({ clubId: r.clubId, name: r.club.name, isDemo: r.club.isDemo, played: r.played, wins: r.wins, draws: r.draws, losses: r.losses, goalsFor: r.goalsFor, goalsAgainst: r.goalsAgainst, points: r.points, provisional: r.provisional }))} />
        )}
      </div>
    </div>
  );
}
