import "server-only";
import { redirect } from "next/navigation";
import { db } from "./db";
import { getCurrentUser } from "./session";
import { activeSeason, squadStrength } from "./league";
import { getGameConfig } from "./game-config";
import { attrsOf, getRoster, playerInclude } from "./club";
import { overall, sortStandings, type Position } from "@/lib/domain/rules";
import { balanceOf } from "./ledger";

export async function requireUser(next = "/") {
  const u = await getCurrentUser();
  if (!u) redirect(`/login?next=${encodeURIComponent(next)}`);
  return u;
}

export async function requireClub(next = "/club") {
  const u = await requireUser(next);
  const club = await db.club.findUnique({ where: { ownerId: u.id }, include: { division: true } });
  if (!club) redirect("/onboarding");
  return { user: u, club };
}

export async function clubRecord(clubId: string) {
  const ms = await db.match.findMany({
    where: { status: "FINISHED", verification: { in: ["CLIENT_REPORTED", "SERVER_SIMULATED"] }, OR: [{ homeClubId: clubId }, { awayClubId: clubId }] },
    select: { homeClubId: true, homeScore: true, awayScore: true },
  });
  let w = 0, d = 0, l = 0, gf = 0, ga = 0;
  for (const m of ms) {
    const home = m.homeClubId === clubId;
    const f = (home ? m.homeScore : m.awayScore) ?? 0;
    const a = (home ? m.awayScore : m.homeScore) ?? 0;
    gf += f; ga += a;
    if (f > a) w++; else if (f === a) d++; else l++;
  }
  const played = w + d + l;
  return { played, wins: w, draws: d, losses: l, goalsFor: gf, goalsAgainst: ga, winRate: played ? Math.round((w / played) * 100) : null };
}

/** World rank = position by RP, then MMR, among non-demo clubs. */
export async function worldRank(clubId: string) {
  const c = await db.club.findUnique({ where: { id: clubId } });
  if (!c || c.isDemo) return null;
  const ahead = await db.club.count({ where: { isDemo: false, OR: [{ rp: { gt: c.rp } }, { rp: c.rp, mmr: { gt: c.mmr } }, { rp: c.rp, mmr: c.mmr, createdAt: { lt: c.createdAt } }] } });
  return ahead + 1;
}

export async function divisionTable(divisionId: number, includeDemo = false) {
  const season = await activeSeason();
  const cfg = await getGameConfig();
  if (!season) return { season: null, rows: [] as never[], cfg };
  const rows = await db.leagueStanding.findMany({
    where: { seasonId: season.id, divisionId, club: includeDemo ? {} : { isDemo: false } },
    include: { club: { select: { id: true, name: true, shortName: true, logo: true, colors: true, isDemo: true } } },
  });
  return { season, rows: sortStandings(rows, cfg.league.tieBreak), cfg };
}

export async function homeOverview() {
  const season = await activeSeason();
  const [clubs, matches, top] = await Promise.all([
    db.club.count({ where: { isDemo: false } }),
    db.match.count({ where: { status: "FINISHED", seasonId: season?.id ?? -1, homeClub: { isDemo: false } } }),
    db.club.findMany({ where: { isDemo: false }, orderBy: [{ rp: "desc" }, { mmr: "desc" }], take: 5, include: { division: true } }),
  ]);
  const div1 = await db.club.count({ where: { isDemo: false, divisionId: 1 } });
  return { season, clubs, matches, top, div1 };
}

/** Highest-OVR players of real clubs. Empty when there are none (the page then shows labelled samples). */
export async function featuredPlayers(limit = 4) {
  const owned = await db.clubPlayer.findMany({
    where: { active: true, club: { isDemo: false } },
    include: { club: true, player: { include: playerInclude } },
    take: 200,
    orderBy: { joinedAt: "desc" },
  });
  return owned
    .map((o) => ({ ...o.player, club: o.club.name, ovr: overall(attrsOf(o.player.attributes!), o.player.position as Position) }))
    .sort((a, b) => b.ovr - a.ovr || b.level - a.level)
    .slice(0, limit);
}

export async function myOverview(userId: string) {
  const club = await db.club.findUnique({ where: { ownerId: userId }, include: { division: true } });
  if (!club) return null;
  const [record, rank, roster, points, pick, squad] = await Promise.all([
    clubRecord(club.id),
    worldRank(club.id),
    getRoster(club.id),
    balanceOf(userId, "NFWL_POINTS"),
    balanceOf(userId, "PICK_SCORE"),
    db.squad.findFirst({ where: { clubId: club.id, isActive: true } }),
  ]);
  return { club, record, rank, roster, points, pick, squad };
}

export async function upcomingFixtures(limit = 30) {
  return db.match.findMany({
    where: { mode: "LEAGUE_SIM", status: "SCHEDULED", kickoffAt: { gt: new Date() } },
    include: { homeClub: true, awayClub: true },
    orderBy: { kickoffAt: "asc" },
    take: limit,
  });
}

export { squadStrength };
