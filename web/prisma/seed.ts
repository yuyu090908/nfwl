// Reference data every environment needs (divisions, formation, skills, achievements, starter pool,
// the current season). Safe to run repeatedly. Contains no demo clubs or fake numbers;
// demo content is in seed-demo.ts and is flagged isDemo.
import "dotenv/config";
import { PrismaPg } from "@prisma/adapter-pg";
import { PrismaClient } from "../src/generated/prisma/client";

const db = new PrismaClient({ adapter: new PrismaPg({ connectionString: process.env.DATABASE_URL! }) });

const DIVISIONS = [
  [0, "Champions League", null, null],
  [1, "Elite", 900, null],
  [2, "Master", 800, 899],
  [3, "Diamond", 700, 799],
  [4, "Platinum", 600, 699],
  [5, "Gold", 500, 599],
  [6, "Silver", 400, 499],
  [7, "Bronze", 300, 399],
  [8, "Rookie III", 200, 299],
  [9, "Rookie II", 100, 199],
  [10, "Rookie I", 0, 99],
] as const;

// Formation 2-1-1 in Unity slot order (MatchManager.SpawnTeams). x/y are 0..1 on a pitch drawn
// with the own goal at the bottom, for the web preview only.
const FORMATION_211 = [
  { role: "GK", x: 0.5, y: 0.92 },
  { role: "DF", x: 0.28, y: 0.68 },
  { role: "DF", x: 0.72, y: 0.68 },
  { role: "MF", x: 0.5, y: 0.45 },
  { role: "FW", x: 0.5, y: 0.2 },
];

// unitySkillId = SkillId in Core/PlayerData.cs. null = not in the Unity build yet (shown, not applied in matches).
const SKILLS = [
  { id: "precision-passer", name: "Precision Passer", description: "Pass error x0.8", minRarity: "Epic", positions: ["MF", "DF"], unitySkillId: "PinpointPass", effect: { passError: 0.8 } },
  { id: "cool-finisher", name: "Cool Finisher", description: "Shot error x0.8, also under pressure", minRarity: "Epic", positions: ["FW", "MF"], unitySkillId: "CoolFinisher", effect: { shotError: 0.8 } },
  { id: "goalkeeper-reflex", name: "Goalkeeper Reflex", description: "Keeper reaction time -0.03 s", minRarity: "Epic", positions: ["GK"], unitySkillId: "CatReflexes", effect: { reactionBonusSeconds: 0.03 } },
  { id: "defensive-wall", name: "Defensive Wall", description: "Standing tackle success x1.1", minRarity: "Epic", positions: ["DF"], unitySkillId: "IronWall", effect: { tackleFactor: 1.1 } },
  { id: "weak-foot-master", name: "Weak Foot Master", description: "Weak foot penalty halved", minRarity: "Epic", positions: ["FW", "MF", "DF"], unitySkillId: "WeakFootMaster", effect: { weakFootPenalty: 0.5 } },
  { id: "power-shooter", name: "Power Shooter", description: "Shot speed +5% (planned, not in the Unity build yet)", minRarity: "Epic", positions: ["FW"], unitySkillId: null, effect: { shotSpeed: 1.05 } },
  { id: "speed-dribbler", name: "Speed Dribbler", description: "Less slowdown while dribbling (planned)", minRarity: "Epic", positions: ["FW", "MF"], unitySkillId: null, effect: { dribbleSpeed: 1.04 } },
  { id: "first-touch-master", name: "First Touch Master", description: "Trap error -20% (planned)", minRarity: "Epic", positions: ["FW", "MF", "DF"], unitySkillId: null, effect: { trapError: 0.8 } },
  { id: "aerial-specialist", name: "Aerial Specialist", description: "Header accuracy +5% (planned)", minRarity: "Epic", positions: ["FW", "DF"], unitySkillId: null, effect: { headerAccuracy: 1.05 } },
] as const;

const ACHIEVEMENTS = [
  { id: "first-club", name: "Founder", description: "Created a club", category: "club", icon: "shield" },
  { id: "first-match", name: "Kick-off", description: "Finished a first match", category: "match", icon: "play" },
  { id: "first-win", name: "First Victory", description: "Won a match", category: "match", icon: "trophy" },
  { id: "hat-trick", name: "Hat-trick", description: "One player scored 3 goals in a match", category: "match", icon: "flame" },
  { id: "first-pick", name: "Analyst", description: "Made a first Free Pick", category: "predictions", icon: "target" },
  { id: "level-10", name: "Rising Star", description: "A player reached level 10", category: "growth", icon: "star" },
];

const CRYSTALS = ["ICE", "SAPPHIRE", "AMETHYST", "EMERALD", "GOLD", "VIOLET"] as const;
// Original Crystalia names (operator-made pool for random starter allocation).
const STARTER_POOL: Record<string, string[]> = {
  GK: ["Vael Orrin", "Kesh Lumora", "Tarn Quillis", "Ivo Saphren", "Ryn Calder", "Oszar Venn"],
  DF: ["Brakka Sol", "Ilen Morrow", "Thane Crysto", "Dova Ketrel", "Zarek Hollis", "Mirel Faunt", "Corvin Ashe", "Pell Varo"],
  MF: ["Lio Seravin", "Neris Aldo", "Kaia Vespre", "Eron Tilt", "Sava Imrel", "Juno Pryce", "Ardel Kim", "Tesh Orlan"],
  FW: ["Zephyr Kael", "Ryo Astren", "Nova Leth", "Kade Solari", "Iska Vorn", "Mace Elric", "Taro Lumen", "Vex Arden"],
};

async function main() {
  for (const [id, name, minRp, maxRp] of DIVISIONS)
    await db.division.upsert({ where: { id }, update: { name, minRp, maxRp }, create: { id, name, minRp, maxRp } });

  await db.formation.upsert({
    where: { id: "2-1-1" },
    update: { slots: FORMATION_211 },
    create: { id: "2-1-1", name: "2-1-1", slots: FORMATION_211 },
  });

  for (const s of SKILLS) {
    const data = { ...s, positions: [...s.positions], minRarity: s.minRarity as "Epic" };
    await db.skill.upsert({ where: { id: s.id }, update: data, create: data });
  }
  for (const a of ACHIEVEMENTS) await db.achievement.upsert({ where: { id: a.id }, update: a, create: a });

  let n = 0;
  for (const [position, names] of Object.entries(STARTER_POOL)) {
    for (const name of names) {
      const code = `starter-${position.toLowerCase()}-${String(++n).padStart(2, "0")}`;
      const data = {
        code,
        name,
        position: position as "GK",
        crystalType: CRYSTALS[n % CRYSTALS.length],
        rarity: "Common" as const,
        baseLevel: 57,
        pool: "starter-common",
      };
      await db.playerTemplate.upsert({ where: { code }, update: data, create: data });
    }
  }

  const active = await db.season.findFirst({ where: { status: "ACTIVE" } });
  if (!active) {
    const startsAt = new Date();
    startsAt.setUTCHours(0, 0, 0, 0);
    const last = await db.season.findFirst({ orderBy: { number: "desc" } });
    await db.season.create({
      data: {
        number: (last?.number ?? 0) + 1,
        startsAt,
        endsAt: new Date(startsAt.getTime() + 30 * 86400_000),
        status: "ACTIVE",
        // Beta season: browser-reported results count as provisional. Flip to true for an official season.
        official: false,
      },
    });
  }
  console.log("seed ok");
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(() => db.$disconnect());
