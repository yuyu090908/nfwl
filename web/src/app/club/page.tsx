import type { Metadata } from "next";
import Link from "next/link";
import { requireClub, clubRecord, worldRank } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { getRoster } from "@/lib/server/club";
import { activeSeason } from "@/lib/server/league";
import { balanceOf } from "@/lib/server/ledger";
import { ClubEmblem } from "@/components/game/club-emblem";
import { PlayerCard } from "@/components/game/player-card";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress, StatTile } from "@/components/ui/misc";
import { buttonVariants } from "@/components/ui/button";
import type { LogoShape, LogoSymbol } from "@/lib/domain/emblem";
import type { Rarity } from "@/lib/domain/rules";
import { fmtDate, cn } from "@/lib/utils";

export const metadata: Metadata = { title: "マイクラブ" };

export default async function ClubPage() {
  const { user, club } = await requireClub("/club");
  const [record, rank, roster, season, points, recent, squad] = await Promise.all([
    clubRecord(club.id),
    worldRank(club.id),
    getRoster(club.id),
    activeSeason(),
    balanceOf(user.id, "NFWL_POINTS"),
    db.match.findMany({ where: { OR: [{ homeClubId: club.id }, { awayClubId: club.id }], status: { in: ["FINISHED", "REJECTED"] } }, orderBy: { finishedAt: "desc" }, take: 5, include: { homeClub: true, awayClub: true } }),
    db.squad.findFirst({ where: { clubId: club.id, isActive: true } }),
  ]);
  const logo = club.logo as { shape: LogoShape; symbol: LogoSymbol };
  const starters = (squad?.starters ?? []).map((id) => roster.find((p) => p.id === id)).filter(Boolean);
  const xpInLevel = club.clubXp % 200;

  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <section className="relative mb-8 overflow-hidden rounded-2xl border border-line bg-gradient-to-br from-card to-bg-2 p-6 sm:p-8">
        <div className="flex flex-col items-start gap-6 sm:flex-row sm:items-center">
          <ClubEmblem shape={logo.shape} symbol={logo.symbol} colors={club.colors as string[]} className="h-24 w-24 shrink-0 sm:h-28 sm:w-28" label={`${club.name} のエンブレム`} />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone="crystal">Division {club.divisionId} · {club.division.name}</Badge>
              <Badge tone="gold">World rank {rank ? `#${rank}` : "—"}</Badge>
              {season && <Badge>Season {season.number}</Badge>}
            </div>
            <h1 className="mt-2 truncate font-display text-3xl font-bold uppercase sm:text-4xl">{club.name}</h1>
            <div className="mt-1 font-display tracking-[0.3em] text-ink-2">{club.shortName}</div>
            <div className="mt-3 max-w-xs">
              <div className="mb-1 flex justify-between text-xs text-ink-2"><span>Club level {club.level}</span><span>{xpInLevel}/200 XP</span></div>
              <Progress value={xpInLevel} max={200} tone="gold" label="クラブ経験値" />
            </div>
          </div>
          <div className="flex w-full flex-col gap-2 sm:w-auto">
            <Link href="/play" className={buttonVariants({ variant: "gold" })}>Play match</Link>
            <Link href="/squad" className={buttonVariants({ variant: "outline" })}>Edit squad</Link>
          </div>
        </div>
      </section>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-8">
        <StatTile label="Wins" value={record.wins} />
        <StatTile label="Draws" value={record.draws} />
        <StatTile label="Losses" value={record.losses} />
        <StatTile label="Goals for" value={record.goalsFor} />
        <StatTile label="Goals against" value={record.goalsAgainst} />
        <StatTile label="Win rate" value={record.winRate === null ? "—" : `${record.winRate}%`} />
        <StatTile label="RP" value={club.rp} accent />
        <StatTile label="NFWL Points" value={points} hint="譲渡・換金不可" />
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-[1.4fr_1fr]">
        <Card>
          <CardHeader><CardTitle>Starting five</CardTitle><Link href="/squad" className="text-xs text-crystal hover:underline">編成</Link></CardHeader>
          <CardBody>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
              {starters.map((p) => p && (
                <PlayerCard key={p.id} size="sm" href={`/players/${p.id}`} p={{ id: p.id, name: p.name, position: p.position, rarity: p.rarity as Rarity, ovr: p.ovr, level: p.level, crystalType: p.crystalType, nft: !!p.nft }} />
              ))}
            </div>
          </CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Recent matches</CardTitle><Link href="/matches" className="text-xs text-crystal hover:underline">すべて</Link></CardHeader>
          <CardBody>
            {recent.length === 0 ? (
              <p className="py-6 text-center text-sm text-ink-2">まだ試合がありません。<Link href="/play" className="text-crystal hover:underline">最初の試合へ</Link></p>
            ) : (
              <ul className="divide-y divide-line">
                {recent.map((m) => {
                  const home = m.homeClubId === club.id;
                  const f = home ? m.homeScore : m.awayScore;
                  const a = home ? m.awayScore : m.homeScore;
                  const res = m.status === "REJECTED" ? "—" : f! > a! ? "W" : f === a ? "D" : "L";
                  const opp = home ? (m.awayClub?.name ?? (m.cpuOpponent as { name?: string } | null)?.name ?? "CPU") : m.homeClub?.name;
                  return (
                    <li key={m.id}>
                      <Link href={`/matches/${m.id}`} className="flex items-center gap-3 py-2.5 hover:text-crystal">
                        <span className={cn("flex h-7 w-7 items-center justify-center rounded font-display text-sm font-bold", res === "W" ? "bg-success/20 text-success" : res === "D" ? "bg-ink-3/20 text-ink-2" : res === "L" ? "bg-danger/20 text-danger" : "bg-card-2 text-ink-3")}>{res}</span>
                        <span className="min-w-0 flex-1 truncate text-sm">vs {opp}</span>
                        <span className="font-display font-semibold tabular-nums">{f ?? "-"} - {a ?? "-"}</span>
                        <span className="hidden text-xs text-ink-3 sm:inline">{fmtDate(m.finishedAt)}</span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            )}
          </CardBody>
        </Card>
      </div>

      <div className="mt-8">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-display text-lg font-semibold uppercase tracking-wider">Roster <span className="text-ink-3">({roster.length}/30)</span></h2>
          <Link href="/players" className="text-xs text-crystal hover:underline">選手一覧</Link>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-8">
          {roster.map((p) => (
            <PlayerCard key={p.id} size="sm" href={`/players/${p.id}`} p={{ id: p.id, name: p.name, position: p.position, rarity: p.rarity as Rarity, ovr: p.ovr, level: p.level, crystalType: p.crystalType, nft: !!p.nft }} />
          ))}
        </div>
        <p className="mt-4 text-xs text-ink-3">クラブ作成日 {fmtDate(club.createdAt, false)}</p>
      </div>
    </div>
  );
}
