import type { Metadata } from "next";
import { requireClub } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { balanceOf } from "@/lib/server/ledger";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState, PageHeader, Progress, StatTile } from "@/components/ui/misc";
import { Badge } from "@/components/ui/badge";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "報酬" };

// Loyalty tiers from activity only (matches played, seasons, achievements). Never from points held or staked.
const TIERS = [
  { name: "Rookie", min: 0 },
  { name: "Regular", min: 10 },
  { name: "Veteran", min: 40 },
  { name: "Captain", min: 100 },
  { name: "Legend", min: 250 },
];

export default async function RewardsPage() {
  const { user, club } = await requireClub("/rewards");
  const [rewards, achievements, mine, points, played] = await Promise.all([
    db.rewardTransaction.findMany({ where: { clubId: club.id }, orderBy: { createdAt: "desc" }, take: 30 }),
    db.achievement.findMany({ orderBy: { id: "asc" } }),
    db.userAchievement.findMany({ where: { userId: user.id } }),
    balanceOf(user.id, "NFWL_POINTS"),
    db.match.count({ where: { homeClubId: club.id, status: "FINISHED", verification: { in: ["CLIENT_REPORTED", "SERVER_SIMULATED"] } } }),
  ]);
  const loyalty = played + mine.length * 5;
  const tierIdx = TIERS.findLastIndex((t) => loyalty >= t.min);
  const next = TIERS[tierIdx + 1];
  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow={club.name} title="Rewards">XP、クラブ経験値、実績バッジ。トークン報酬は経済設計と法令確認の後に追加します。</PageHeader>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatTile label="Club level" value={club.level} accent />
        <StatTile label="Club XP" value={club.clubXp} />
        <StatTile label="NFWL Points" value={points} hint="ゲーム内専用" />
        <StatTile label="Badges" value={`${mine.length}/${achievements.length}`} />
      </div>
      <Card className="mt-6">
        <CardHeader><CardTitle>Loyalty</CardTitle><Badge tone="gold">{TIERS[tierIdx].name}</Badge></CardHeader>
        <CardBody>
          <Progress value={loyalty - TIERS[tierIdx].min} max={next ? next.min - TIERS[tierIdx].min : 1} tone="gold" label="ロイヤルティ" />
          <p className="mt-2 text-xs text-ink-2">試合参加・実績・シーズン成績で上がります（保有ポイントや予想の量では上がりません）。{next ? `次の段階「${next.name}」まで ${next.min - loyalty}` : "最高段階です"}</p>
        </CardBody>
      </Card>
      <h2 className="mb-3 mt-8 font-display text-lg font-semibold uppercase tracking-wider">Achievement badges</h2>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {achievements.map((a) => {
          const got = mine.find((m) => m.achievementId === a.id);
          return (
            <div key={a.id} className={`rounded-xl border p-4 text-center ${got ? "border-gold/50 bg-gold/8" : "border-line opacity-50"}`}>
              <div className="font-display font-bold">{a.name}</div>
              <div className="mt-1 text-[11px] text-ink-2">{a.description}</div>
              {got && <div className="mt-1 text-[10px] text-gold">{fmtDate(got.earnedAt, false)}</div>}
            </div>
          );
        })}
      </div>
      <h2 className="mb-3 mt-8 font-display text-lg font-semibold uppercase tracking-wider">History</h2>
      {rewards.length === 0 ? <EmptyState title="まだ報酬はありません" /> : (
        <ul className="divide-y divide-line rounded-2xl border border-line">
          {rewards.map((r) => (
            <li key={r.id} className="flex items-center justify-between px-4 py-2.5 text-sm">
              <span><Badge>{r.kind}</Badge> <span className="ml-2 text-ink-2">{r.reason}</span></span>
              <span className="flex items-center gap-3"><span className="font-display text-success">+{r.amount}</span><span className="text-xs text-ink-3">{fmtDate(r.createdAt)}</span></span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
