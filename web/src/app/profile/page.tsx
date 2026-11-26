import type { Metadata } from "next";
import Link from "next/link";
import { requireUser, myOverview } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { PageHeader, StatTile } from "@/components/ui/misc";
import { Badge } from "@/components/ui/badge";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "プロフィール" };

export default async function ProfilePage() {
  const user = await requireUser("/profile");
  const [ov, accounts, badges, u] = await Promise.all([
    myOverview(user.id),
    db.account.findMany({ where: { userId: user.id }, select: { provider: true, createdAt: true } }),
    db.userAchievement.findMany({ where: { userId: user.id }, include: { achievement: true } }),
    db.user.findUniqueOrThrow({ where: { id: user.id } }),
  ]);
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Manager" title={user.displayName}>登録日 {fmtDate(u.createdAt, false)}</PageHeader>
      {ov ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatTile label="Club" value={<Link href="/club" className="hover:text-crystal">{ov.club.shortName}</Link>} hint={ov.club.name} accent />
          <StatTile label="Division" value={ov.club.divisionId} />
          <StatTile label="World rank" value={ov.rank ? `#${ov.rank}` : "—"} />
          <StatTile label="Matches" value={ov.record.played} />
        </div>
      ) : (
        <Link href="/onboarding" className="text-crystal hover:underline">クラブを作成する</Link>
      )}
      <h2 className="mb-2 mt-8 font-display text-sm font-semibold uppercase tracking-wider text-ink-2">Sign-in methods</h2>
      <div className="flex flex-wrap gap-2">{accounts.map((a, i) => <Badge key={i} tone="crystal">{a.provider}</Badge>)}</div>
      <h2 className="mb-2 mt-8 font-display text-sm font-semibold uppercase tracking-wider text-ink-2">Badges</h2>
      <div className="flex flex-wrap gap-2">{badges.length === 0 ? <span className="text-sm text-ink-2">まだありません</span> : badges.map((b) => <Badge key={b.achievementId} tone="gold">{b.achievement.name}</Badge>)}</div>
    </div>
  );
}
