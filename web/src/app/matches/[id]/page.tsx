import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { db } from "@/lib/server/db";
import { getCurrentUser } from "@/lib/server/session";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Notice } from "@/components/ui/misc";
import { VerificationBadge } from "@/components/game/verification-badge";
import type { UnityMatchConfig } from "@/lib/bridge/protocol";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "試合詳細" };

export default async function MatchDetail({ params }: PageProps<"/matches/[id]">) {
  const { id } = await params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) notFound();
  const m = await db.match.findUnique({
    where: { id },
    include: { homeClub: true, awayClub: true, events: { orderBy: [{ seq: "asc" }, { receivedAt: "asc" }] }, statistics: { orderBy: [{ side: "desc" }, { slot: "asc" }] }, session: true, season: true },
  });
  if (!m) notFound();
  const user = await getCurrentUser();
  const isOwner = !!user && (m.homeClub?.ownerId === user.id || m.awayClub?.ownerId === user.id);
  const cfg = m.session?.clientConfig as unknown as UnityMatchConfig | undefined;
  const names = (side: "home" | "away", slot: number | null) => (slot === null ? "Own goal / unknown" : cfg?.[side]?.[slot]?.name ?? `#${slot + 1}`);
  const homeName = m.homeClub?.name ?? "—";
  const awayName = m.awayClub?.name ?? (m.cpuOpponent as { name?: string } | null)?.name ?? "CPU";
  const goals = m.events.filter((e) => e.eventType === "GoalScored");
  const anomalies = (m.anomalies as { code: string; detail?: string }[]) ?? [];

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <div className="rounded-2xl border border-line bg-gradient-to-br from-card to-bg-2 p-6 text-center">
        <div className="flex flex-wrap items-center justify-center gap-2">
          <Badge>{m.mode === "PRACTICE_CPU" ? "AI Match" : m.mode === "LEAGUE_SIM" ? "League" : "Friendly"}</Badge>
          <VerificationBadge v={m.verification} />
          {m.season && <Badge>Season {m.season.number}{m.season.official ? "" : " (beta)"}</Badge>}
        </div>
        <div className="mt-4 grid grid-cols-[1fr_auto_1fr] items-center gap-4">
          <div className="truncate text-right font-display text-lg font-bold uppercase sm:text-2xl">{homeName}</div>
          <div className="font-display text-4xl font-bold tabular-nums sm:text-6xl">{m.homeScore ?? "-"} : {m.awayScore ?? "-"}</div>
          <div className="truncate text-left font-display text-lg font-bold uppercase sm:text-2xl">{awayName}</div>
        </div>
        <div className="mt-2 text-xs text-ink-2">{fmtDate(m.finishedAt ?? m.kickoffAt)} · {m.status}</div>
      </div>

      {anomalies.length > 0 && (
        <div className="mt-6">
          <Notice tone={m.verification === "REJECTED" ? "danger" : "warning"} title="サーバー検証の結果">
            <ul className="list-disc pl-5">{anomalies.map((a, i) => <li key={i}>{a.code}{a.detail ? `: ${a.detail}` : ""}</li>)}</ul>
          </Notice>
        </div>
      )}

      <div className="mt-6 grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Goals</CardTitle></CardHeader>
          <CardBody>
            {goals.length === 0 ? <p className="text-sm text-ink-2">得点はありません。</p> : (
              <ol className="flex flex-col gap-1.5 text-sm">
                {goals.map((g) => {
                  const p = g.payload as { team: "home" | "away"; scorerSlot: number | null; gameClock: number };
                  return (
                    <li key={g.id} className="flex items-center gap-3">
                      <span className="w-10 font-display text-ink-2">{Math.floor(p.gameClock / 60)}&apos;</span>
                      <span className={p.team === "home" ? "text-crystal" : "text-mythic"}>{p.team === "home" ? homeName : awayName}</span>
                      <span className="truncate text-ink-2">{names(p.team, p.scorerSlot)}</span>
                    </li>
                  );
                })}
              </ol>
            )}
          </CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Player statistics</CardTitle></CardHeader>
          <CardBody>
            {m.statistics.length === 0 ? <p className="text-sm text-ink-2">統計はありません。</p> : (
              <table className="w-full text-sm">
                <thead className="text-left text-[11px] uppercase text-ink-3"><tr><th className="py-1">Player</th><th>G</th><th>Min</th><th>XP</th></tr></thead>
                <tbody>
                  {m.statistics.filter((s) => s.side === "home").map((s) => (
                    <tr key={s.id} className="border-t border-line">
                      <td className="max-w-[160px] truncate py-1.5">{s.playerId ? <Link href={`/players/${s.playerId}`} className="hover:text-crystal">{names("home", s.slot)}</Link> : names("home", s.slot)}</td>
                      <td>{s.goals}</td><td>{s.minutes}</td><td className="text-success">{s.xpAwarded ? `+${s.xpAwarded}` : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardBody>
        </Card>
      </div>

      {isOwner && m.events.length > 0 && (
        <details className="mt-6 rounded-2xl border border-line bg-card/60 p-4">
          <summary className="cursor-pointer text-sm font-semibold">Event log（Unity → Web bridge, {m.events.length} 件）</summary>
          <ol className="mt-3 max-h-80 overflow-auto font-mono text-xs text-ink-2">
            {m.events.map((e) => <li key={e.id}>#{e.seq} {e.eventType} {e.gameClock !== null ? `t=${e.gameClock}` : ""} · {fmtDate(e.receivedAt)}</li>)}
          </ol>
        </details>
      )}
    </div>
  );
}
