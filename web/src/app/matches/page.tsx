import type { Metadata } from "next";
import Link from "next/link";
import { requireClub } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { EmptyState, PageHeader } from "@/components/ui/misc";
import { VerificationBadge } from "@/components/game/verification-badge";
import { Badge } from "@/components/ui/badge";
import { fmtDate, cn } from "@/lib/utils";

export const metadata: Metadata = { title: "試合履歴" };

const MODE: Record<string, string> = { PRACTICE_CPU: "AI Match", LEAGUE_SIM: "League", FRIENDLY: "Friendly" };

export default async function MatchesPage() {
  const { club } = await requireClub("/matches");
  const matches = await db.match.findMany({
    where: { OR: [{ homeClubId: club.id }, { awayClubId: club.id }], status: { in: ["FINISHED", "REJECTED", "VOID", "SCHEDULED"] } },
    include: { homeClub: true, awayClub: true },
    orderBy: [{ finishedAt: { sort: "desc", nulls: "first" } }, { createdAt: "desc" }],
    take: 100,
  });
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow={club.name} title="Matches">AI Match はブラウザ（Unity）から報告された結果を、サーバーが検証して記録しています。</PageHeader>
      {matches.length === 0 ? (
        <EmptyState title="まだ試合がありません" action={<Link href="/play" className="text-crystal hover:underline">最初の試合へ</Link>} />
      ) : (
        <div className="overflow-hidden rounded-2xl border border-line">
          <table className="w-full text-sm">
            <caption className="sr-only">試合履歴</caption>
            <thead className="bg-bg-2 text-left text-[11px] uppercase tracking-wider text-ink-3">
              <tr><th className="px-3 py-2">Date</th><th className="px-3 py-2">Match</th><th className="px-3 py-2 text-center">Score</th><th className="hidden px-3 py-2 sm:table-cell">Mode</th><th className="hidden px-3 py-2 md:table-cell">Result</th></tr>
            </thead>
            <tbody className="divide-y divide-line">
              {matches.map((m) => {
                const opp = m.homeClubId === club.id ? m.awayClub?.name ?? (m.cpuOpponent as { name?: string } | null)?.name ?? "CPU" : m.homeClub?.name ?? "—";
                return (
                  <tr key={m.id} className="hover:bg-card/60">
                    <td className="whitespace-nowrap px-3 py-2.5 text-xs text-ink-2">{fmtDate(m.finishedAt ?? m.kickoffAt ?? m.createdAt)}</td>
                    <td className="max-w-[200px] truncate px-3 py-2.5"><Link href={`/matches/${m.id}`} className="hover:text-crystal">vs {opp}</Link></td>
                    <td className={cn("px-3 py-2.5 text-center font-display font-bold tabular-nums")}>{m.homeScore ?? "-"} - {m.awayScore ?? "-"}</td>
                    <td className="hidden px-3 py-2.5 sm:table-cell"><Badge>{MODE[m.mode]}</Badge></td>
                    <td className="hidden px-3 py-2.5 md:table-cell">{m.status === "VOID" ? <Badge>Void</Badge> : m.status === "SCHEDULED" ? <Badge>Scheduled</Badge> : <VerificationBadge v={m.verification} />}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
