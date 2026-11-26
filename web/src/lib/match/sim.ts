// Server-authoritative quick simulation for league fixtures between clubs (LEAGUE_SIM).
// Deterministic from (seed, lineups): anyone can re-run it and get the same result, which is what
// makes it usable for official standings, unlike a result reported by a browser.
// It is a statistical model (Poisson goals from squad strength), not the Unity physics match.

import { rng } from "@/lib/domain/rules";

export interface SimTeam {
  /** OVR per formation slot (GK, DF, DF, MF, FW). */
  ovr: number[];
}

export interface SimGoal {
  team: "home" | "away";
  scorerSlot: number;
  gameClock: number;
}

function poisson(lambda: number, u: () => number) {
  const L = Math.exp(-lambda);
  let k = 0;
  let p = 1;
  do {
    k++;
    p *= u();
  } while (p > L && k < 20);
  return k - 1;
}

export function simulateMatch(home: SimTeam, away: SimTeam, seed: number, halfSeconds = 600) {
  const r = rng(seed);
  const attack = (t: SimTeam) => (t.ovr[3] + t.ovr[4] * 1.4 + (t.ovr[1] + t.ovr[2]) * 0.3) / 3;
  const defence = (t: SimTeam) => (t.ovr[0] * 1.3 + t.ovr[1] + t.ovr[2] + t.ovr[3] * 0.5) / 3.8;
  // ~2.4 goals per team in a 20 minute 5-a-side match at equal strength; +-10 OVR shifts it by ~45%.
  const lam = (a: SimTeam, d: SimTeam, homeEdge: number) => Math.max(0.3, 2.4 * Math.exp((attack(a) - defence(d) + homeEdge) / 25));
  const hg = poisson(lam(home, away, 1.5), r.next);
  const ag = poisson(lam(away, home, 0), r.next);
  const full = halfSeconds * 2;
  const goals: SimGoal[] = [];
  const scorer = () => {
    const x = r.next();
    return x < 0.5 ? 4 : x < 0.8 ? 3 : x < 0.97 ? 1 + r.int(0, 2) : 0;
  };
  for (let i = 0; i < hg; i++) goals.push({ team: "home", scorerSlot: scorer(), gameClock: Math.round(r.next() * full) });
  for (let i = 0; i < ag; i++) goals.push({ team: "away", scorerSlot: scorer(), gameClock: Math.round(r.next() * full) });
  goals.sort((a, b) => a.gameClock - b.gameClock);
  return { homeScore: hg, awayScore: ag, goals };
}
