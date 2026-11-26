import type { Metadata } from "next";
import Link from "next/link";
import { db } from "@/lib/server/db";
import { getCurrentUser } from "@/lib/server/session";
import { getGameConfig } from "@/lib/server/game-config";
import { balanceOf } from "@/lib/server/ledger";
import { squadStrength } from "@/lib/server/league";
import { EmptyState, Notice, PageHeader, StatTile } from "@/components/ui/misc";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { PickButtons } from "@/components/game/pick-buttons";
import { estimateOutcome } from "@/lib/domain/rules";
import { fmtDate, nowMs } from "@/lib/utils";

export const metadata: Metadata = { title: "Free Pick" };

async function form(clubId: string) {
  const ms = await db.match.findMany({
    where: { status: "FINISHED", verification: "SERVER_SIMULATED", OR: [{ homeClubId: clubId }, { awayClubId: clubId }] },
    orderBy: { finishedAt: "desc" },
    take: 5,
  });
  return ms.map((m) => {
    const f = m.homeClubId === clubId ? m.homeScore! : m.awayScore!;
    const a = m.homeClubId === clubId ? m.awayScore! : m.homeScore!;
    return f > a ? "W" : f === a ? "D" : "L";
  });
}

export default async function PredictionsPage() {
  const user = await getCurrentUser();
  const cfg = await getGameConfig();
  const fixtures = await db.match.findMany({
    where: { mode: "LEAGUE_SIM", OR: [{ status: "SCHEDULED" }, { status: "FINISHED", finishedAt: { gte: new Date(nowMs() - 3 * 86400_000) } }] },
    include: { homeClub: true, awayClub: true, predictions: user ? { where: { userId: user.id } } : false },
    orderBy: { kickoffAt: "asc" },
    take: 40,
  });
  const score = user ? await balanceOf(user.id, "PICK_SCORE") : 0;
  const mine = user ? await db.predictionEntry.groupBy({ by: ["status"], where: { userId: user.id }, _count: true }) : [];
  const won = mine.find((m) => m.status === "WON")?._count ?? 0;
  const settled = won + (mine.find((m) => m.status === "LOST")?._count ?? 0);

  const cards = await Promise.all(
    fixtures.map(async (m) => {
      const [hs, as, hf, af] = await Promise.all([squadStrength(db, m.homeClubId!), squadStrength(db, m.awayClubId!), form(m.homeClubId!), form(m.awayClubId!)]);
      const avg = (x: number[] | null) => (x ? x.reduce((a, b) => a + b, 0) / x.length : null);
      const est = cfg.predictions.showEstimate && hs && as ? estimateOutcome(avg(hs)!, avg(as)!) : null;
      const closes = (m.kickoffAt?.getTime() ?? 0) - cfg.predictions.closeMinutesBeforeKickoff * 60_000;
      return { m, est, hf, af, open: m.status === "SCHEDULED" && nowMs() < closes, pick: (m.predictions as { pick: string; status: string }[] | undefined)?.[0] };
    }),
  );

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Free Pick" title="Match predictions">
        リーグ戦（サーバーが計算する公式試合）の結果を無料で予想します。的中で予想スコアが増え、称号・バッジの対象になります。
      </PageHeader>
      <div className="mb-6">
        <Notice title="ポイントについて">
          予想スコアはゲーム内専用で、現金・NFT・暗号資産に交換できず、譲渡もできません。トークンを賭ける予想（ステーキング）は、法令・年齢・地域の確認が終わるまで提供しません。
        </Notice>
      </div>
      {user && (
        <div className="mb-8 grid grid-cols-3 gap-3">
          <StatTile label="Pick score" value={score} accent />
          <StatTile label="Correct" value={won} />
          <StatTile label="Accuracy" value={settled ? `${Math.round((won / settled) * 100)}%` : "—"} />
        </div>
      )}
      {cards.length === 0 ? (
        <EmptyState title="予想できる試合はまだありません">リーグ戦の日程が組まれると、ここに対戦カードが表示されます。</EmptyState>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {cards.map(({ m, est, hf, af, open, pick }) => (
            <Card key={m.id} className="p-5">
              <div className="flex items-center justify-between text-xs text-ink-2">
                <span>Division {m.homeClub?.divisionId} · {fmtDate(m.kickoffAt)}</span>
                {m.status === "FINISHED" ? <Badge tone="success">結果確定 {m.homeScore}-{m.awayScore}</Badge> : open ? <Badge tone="crystal">受付中</Badge> : <Badge>受付終了</Badge>}
              </div>
              <div className="my-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3">
                <div className="min-w-0"><div className="truncate font-display font-bold uppercase">{m.homeClub?.name}</div><div className="text-[11px] text-ink-3">直近 {hf.join(" ") || "—"}</div></div>
                <span className="text-ink-3">vs</span>
                <div className="min-w-0 text-right"><div className="truncate font-display font-bold uppercase">{m.awayClub?.name}</div><div className="text-[11px] text-ink-3">直近 {af.join(" ") || "—"}</div></div>
              </div>
              {est && (
                <div className="mb-3">
                  <div className="flex h-2 overflow-hidden rounded-full" aria-hidden>
                    <span className="bg-crystal" style={{ width: `${est.home}%` }} /><span className="bg-ink-3" style={{ width: `${est.draw}%` }} /><span className="bg-mythic" style={{ width: `${est.away}%` }} />
                  </div>
                  <p className="mt-1 text-[11px] text-ink-3">AI推定（スタメンOVRからの参考値）: {est.home}% / 引分 {est.draw}% / {est.away}%</p>
                </div>
              )}
              {user ? (
                <PickButtons matchId={m.id} current={pick?.pick ?? null} disabled={!open} labels={[m.homeClub?.shortName ?? "HOME", m.awayClub?.shortName ?? "AWAY"]} />
              ) : (
                <Link href="/login?next=/predictions" className="text-sm text-crystal hover:underline">ログインして予想する</Link>
              )}
              {pick && pick.status !== "OPEN" && <p className="mt-2 text-xs">{pick.status === "WON" ? <span className="text-success">的中 +{cfg.predictions.scoreCorrect}</span> : pick.status === "LOST" ? <span className="text-ink-2">不的中</span> : "無効試合"}</p>}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
