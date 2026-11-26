// Integration tests against the real PostgreSQL database (DATABASE_URL from .env).
// They create their own users with unique emails and never touch other rows.
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { db } from "@/lib/server/db";
import { balanceOf, grant, LedgerError, spend, verifyLedger } from "@/lib/server/ledger";
import { createClubWithStarters, getRoster } from "@/lib/server/club";
import { createPracticeSession, ingestEvent } from "@/lib/server/match-session";
import { breakthrough } from "@/lib/server/upgrade";
import { parseBridgeMessage, type BridgeMessage } from "@/lib/bridge/protocol";
import { overall, type Position } from "@/lib/domain/rules";
import { attrsOf } from "@/lib/server/club";

process.env.NFWL_ALLOW_SHORT_MATCHES = "1";

const run = `it${Date.now().toString(36)}`;
let n = 0;
async function newUser() {
  return db.user.create({ data: { email: `${run}-${n++}@test.invalid`, displayName: "Integration" } });
}
async function newClubUser() {
  const u = await newUser();
  await createClubWithStarters(u.id, {
    name: `Test FC ${run}${n}`,
    shortName: `T${n}`.slice(0, 4).padEnd(3, "X"),
    logo: { shape: "shield", symbol: "star" },
    colors: ["#D5AC59", "#44D9FF", "#080B12"],
  });
  return u;
}

beforeAll(async () => {
  await db.$queryRaw`SELECT 1`;
});
afterEach(() => vi.useRealTimers());
afterAll(async () => {
  await db.$disconnect();
});

describe("ledger", () => {
  it("is idempotent, refuses overdraft and stays balanced", async () => {
    const u = await newUser();
    await db.$transaction((tx) => grant(tx, u.id, "NFWL_POINTS", 300, `${run}:g1`, "TEST"));
    await db.$transaction((tx) => grant(tx, u.id, "NFWL_POINTS", 300, `${run}:g1`, "TEST")); // replay
    expect(await balanceOf(u.id, "NFWL_POINTS")).toBe(300);

    await db.$transaction((tx) => spend(tx, u.id, "NFWL_POINTS", 200, `${run}:s1`, "TEST"));
    await expect(db.$transaction((tx) => spend(tx, u.id, "NFWL_POINTS", 200, `${run}:s2`, "TEST"))).rejects.toBeInstanceOf(LedgerError);
    expect(await balanceOf(u.id, "NFWL_POINTS")).toBe(100);

    // Concurrent double spend of the same balance: exactly one succeeds.
    const results = await Promise.allSettled([
      db.$transaction((tx) => spend(tx, u.id, "NFWL_POINTS", 80, `${run}:c1`, "TEST")),
      db.$transaction((tx) => spend(tx, u.id, "NFWL_POINTS", 80, `${run}:c2`, "TEST")),
    ]);
    expect(results.filter((r) => r.status === "fulfilled")).toHaveLength(1);
    expect(await balanceOf(u.id, "NFWL_POINTS")).toBe(20);

    const v = await verifyLedger();
    expect(v.mismatched).toEqual([]);
    expect(v.unbalancedTransactions).toEqual([]);
  });
});

describe("club creation", () => {
  it("gives 8 operator-pool Commons (OVR 55-60), a valid squad and 500 points, once", async () => {
    const u = await newClubUser();
    const club = await db.club.findUniqueOrThrow({ where: { ownerId: u.id } });
    expect(club.divisionId).toBe(10);
    const roster = await getRoster(club.id);
    expect(roster).toHaveLength(8);
    for (const p of roster) {
      expect(p.rarity).toBe("Common");
      const ovr = overall(attrsOf(p.attributes!), p.position as Position);
      expect(ovr).toBeGreaterThanOrEqual(55);
      expect(ovr).toBeLessThanOrEqual(60);
    }
    expect(new Set(roster.map((p) => p.position))).toEqual(new Set(["GK", "DF", "MF", "FW"]));
    const squad = await db.squad.findFirstOrThrow({ where: { clubId: club.id, isActive: true } });
    expect(squad.starters).toHaveLength(5);
    expect(await balanceOf(u.id, "NFWL_POINTS")).toBe(500);
    await expect(
      createClubWithStarters(u.id, { name: "Again", shortName: "AGN", logo: { shape: "shield", symbol: "star" }, colors: ["#000000", "#111111", "#222222"] }),
    ).rejects.toMatchObject({ status: 409 });
  });
});

/** Plays a practice match through ingestEvent with a fake wall clock. */
async function playMatch(userId: string, opts: { realSeconds: number; goals?: { team: "home" | "away"; slot: number | null; at: number }[] }) {
  vi.useFakeTimers({ toFake: ["Date"] });
  const t0 = Date.now();
  vi.setSystemTime(t0);
  const cfg = await createPracticeSession(userId, 10, "beginner");
  let seq = 0;
  const msg = (eventType: string, payload: unknown, id?: string): BridgeMessage => {
    const r = parseBridgeMessage({ schemaVersion: 1, sessionId: cfg.sessionId, messageId: id ?? `${run}m${seq + 1}x${Math.random().toString(36).slice(2, 8)}`, eventType, seq: ++seq, payload });
    if (!r.ok) throw new Error(r.error);
    return r.message;
  };
  const results: Awaited<ReturnType<typeof ingestEvent>>[] = [];
  results.push(await ingestEvent(userId, msg("GameReady", { unityVersion: "6000.6.9f1", buildVersion: "test", platform: "WebGL" })));
  const started = msg("MatchStarted", { half: 1, gameClock: 0 });
  results.push(await ingestEvent(userId, started));
  // Same messageId again (a Unity / network retry): accepted as a duplicate, nothing stored twice.
  results.push(await ingestEvent(userId, started));

  let home = 0, away = 0;
  for (const g of opts.goals ?? []) {
    vi.setSystemTime(t0 + g.at * 1000 * (opts.realSeconds / 20));
    if (g.team === "home") home++; else away++;
    results.push(await ingestEvent(userId, msg("GoalScored", { team: g.team, scorerSlot: g.slot, homeScore: home, awayScore: away, half: g.at < 10 ? 1 : 2, gameClock: g.at })));
  }
  vi.setSystemTime(t0 + (opts.realSeconds / 2) * 1000);
  results.push(await ingestEvent(userId, msg("HalfTime", { homeScore: home, awayScore: away, gameClock: 10 })));
  vi.setSystemTime(t0 + opts.realSeconds * 1000);
  const fin = await ingestEvent(userId, msg("MatchFinished", { homeScore: home, awayScore: away, gameClock: 20 }));
  return { cfg, results, fin };
}

describe("practice match session", () => {
  it("records a plausible match as CLIENT_REPORTED and applies XP once", async () => {
    const u = await newClubUser();
    const { cfg, results, fin } = await playMatch(u.id, { realSeconds: 21, goals: [{ team: "home", slot: 4, at: 5 }, { team: "away", slot: 2, at: 15 }] });
    expect(results[2]).toMatchObject({ duplicate: true });
    expect(fin.status).toBe("FINISHED");
    const r = fin.result as { verdict: string; xp: { amount: number }[] };
    expect(r.verdict).toBe("CLIENT_REPORTED");
    expect(r.xp).toHaveLength(5);

    // The lineup sent to Unity: 5 + 5 players, no secrets.
    expect(cfg.home).toHaveLength(5);
    expect(cfg.away).toHaveLength(5);
    expect(JSON.stringify(cfg)).not.toMatch(/token\b|password|privateKey|nfwl_session/i);

    const m = await db.match.findUniqueOrThrow({ where: { id: cfg.matchId } });
    expect(m).toMatchObject({ status: "FINISHED", verification: "CLIENT_REPORTED", homeScore: 1, awayScore: 1 });
    expect(m.appliedAt).not.toBeNull();
    expect(await db.matchEvent.count({ where: { matchId: m.id } })).toBe(6);
    const stats = await db.matchStatistic.findMany({ where: { matchId: m.id, side: "home" } });
    expect(stats.find((s) => s.slot === 4)?.goals).toBe(1);

    // After finishing, a late message is refused for good.
    await expect(
      ingestEvent(u.id, parseBridgeMessageOrThrow({ schemaVersion: 1, sessionId: cfg.sessionId, messageId: `${run}late01`, eventType: "GamePaused", seq: 99, payload: { reason: "x", gameClock: 20 } })),
    ).rejects.toMatchObject({ status: 409 });
  });

  it("rejects a match that finished faster than real time (TOO_FAST) and grants nothing", async () => {
    const u = await newClubUser();
    const before = await db.player.findMany({ where: { ownership: { some: { club: { ownerId: u.id } } } }, select: { id: true, xp: true } });
    const { cfg, fin } = await playMatch(u.id, { realSeconds: 5, goals: [{ team: "home", slot: 4, at: 3 }] });
    const r = fin.result as { verdict: string; anomalies: { code: string }[] };
    expect(r.verdict).toBe("REJECTED");
    expect(r.anomalies.map((a) => a.code)).toContain("TOO_FAST");
    const m = await db.match.findUniqueOrThrow({ where: { id: cfg.matchId } });
    expect(m.verification).toBe("REJECTED");
    expect(m.appliedAt).toBeNull();
    const after = await db.player.findMany({ where: { id: { in: before.map((p) => p.id) } }, select: { id: true, xp: true } });
    expect(after.map((p) => p.xp).sort()).toEqual(before.map((p) => p.xp).sort());
  });

  it("refuses another user's session", async () => {
    const a = await newClubUser();
    const b = await newClubUser();
    const cfg = await createPracticeSession(a.id, 10, "beginner");
    await expect(
      ingestEvent(b.id, parseBridgeMessageOrThrow({ schemaVersion: 1, sessionId: cfg.sessionId, messageId: `${run}other1`, eventType: "GameReady", seq: 1, payload: { unityVersion: "x", buildVersion: "x", platform: "x" } })),
    ).rejects.toMatchObject({ status: 404 });
  });
});

describe("level cap breakthrough", () => {
  it("spends points once per idempotency key and refuses below the required level", async () => {
    const u = await newClubUser();
    const club = await db.club.findUniqueOrThrow({ where: { ownerId: u.id } });
    const [p] = await getRoster(club.id);
    await expect(breakthrough(u.id, p.id, `${run}-low`)).rejects.toMatchObject({ code: "LEVEL_TOO_LOW" });
    await db.player.update({ where: { id: p.id }, data: { level: p.levelCap } });
    const first = await breakthrough(u.id, p.id, `${run}-k1`);
    const again = await breakthrough(u.id, p.id, `${run}-k1`);
    expect(first.replay).toBe(false);
    expect(again.replay).toBe(true);
    expect(await balanceOf(u.id, "NFWL_POINTS")).toBe(300);
    expect((await db.player.findUniqueOrThrow({ where: { id: p.id } })).levelCap).toBeGreaterThan(p.levelCap);
  });
});

function parseBridgeMessageOrThrow(raw: unknown) {
  const r = parseBridgeMessage(raw);
  if (!r.ok) throw new Error(r.error);
  return r.message;
}
