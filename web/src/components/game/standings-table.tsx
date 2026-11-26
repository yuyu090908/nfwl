import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

export interface StandingView {
  clubId: string;
  name: string;
  isDemo: boolean;
  played: number;
  wins: number;
  draws: number;
  losses: number;
  goalsFor: number;
  goalsAgainst: number;
  points: number;
  provisional: number;
}

export function StandingsTable({ rows, division, promotion, relegation, myClubId }: { rows: StandingView[]; division: number; promotion: number; relegation: number; myClubId?: string | null }) {
  const n = rows.length;
  return (
    <div className="overflow-x-auto rounded-2xl border border-line">
      <table className="w-full min-w-[560px] text-sm">
        <caption className="sr-only">Division {division} 順位表</caption>
        <thead className="bg-bg-2 text-[11px] uppercase tracking-wider text-ink-3">
          <tr>
            <th className="px-3 py-2 text-left">#</th><th className="px-3 py-2 text-left">Club</th>
            <th className="px-2 py-2" title="Played">P</th><th className="px-2 py-2">W</th><th className="px-2 py-2">D</th><th className="px-2 py-2">L</th>
            <th className="px-2 py-2" title="Goals">GF:GA</th><th className="px-2 py-2" title="Goal difference">GD</th><th className="px-3 py-2">Pts</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((r, i) => {
            const promo = division > 1 && i < promotion && r.played > 0;
            const rel = division < 10 && n > promotion + relegation && i >= n - relegation;
            return (
              <tr key={r.clubId} className={cn("text-center", r.clubId === myClubId && "bg-crystal/8", promo && "shadow-[inset_3px_0_0_#3ddc97]", rel && "shadow-[inset_3px_0_0_#ff465f]")}>
                <td className="px-3 py-2.5 text-left font-display font-bold">{i + 1}</td>
                <td className="max-w-[220px] px-3 py-2.5 text-left">
                  <span className="truncate font-medium">{r.name}</span>
                  {r.isDemo && <Badge tone="warning" className="ml-2">DEMO</Badge>}
                  {r.provisional > 0 && <span className="ml-2 text-[10px] text-warning" title="ブラウザ報告の試合を含む暫定成績">暫定 {r.provisional}</span>}
                </td>
                <td>{r.played}</td><td>{r.wins}</td><td>{r.draws}</td><td>{r.losses}</td>
                <td className="tabular-nums">{r.goalsFor}:{r.goalsAgainst}</td>
                <td className="tabular-nums">{r.goalsFor - r.goalsAgainst > 0 ? "+" : ""}{r.goalsFor - r.goalsAgainst}</td>
                <td className="px-3 font-display text-base font-bold text-gold">{r.points}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <div className="flex flex-wrap gap-4 border-t border-line bg-bg-2/60 px-3 py-2 text-[11px] text-ink-3">
        <span className="flex items-center gap-1"><span className="h-3 w-1 bg-success" /> Promotion zone</span>
        <span className="flex items-center gap-1"><span className="h-3 w-1 bg-danger" /> Relegation zone</span>
        <span>暫定 = ブラウザ報告の試合数（ベータシーズンのみ集計）</span>
      </div>
    </div>
  );
}
