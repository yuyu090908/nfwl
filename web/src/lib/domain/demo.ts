// Sample content shown ONLY when there is no real data yet, always with a DEMO badge (prompt 21).
import type { PlayerCardData } from "@/components/game/player-card";

export const DEMO_FEATURED: (PlayerCardData & { skills: string[] })[] = [
  { id: "demo-1", name: "Zephyr Kael", position: "FW", rarity: "Mythic", ovr: 86, level: 42, crystalType: "VIOLET", skills: ["Cool Finisher", "Weak Foot Master"], club: "Sample Club", nft: true },
  { id: "demo-2", name: "Lio Seravin", position: "MF", rarity: "Epic", ovr: 81, level: 29, crystalType: "GOLD", skills: ["Precision Passer"], club: "Sample Club", nft: true },
  { id: "demo-3", name: "Brakka Sol", position: "DF", rarity: "Rare", ovr: 76, level: 14, crystalType: "SAPPHIRE", skills: [], club: "Sample Club", nft: false },
  { id: "demo-4", name: "Vael Orrin", position: "GK", rarity: "Legendary", ovr: 91, level: 60, crystalType: "AMETHYST", skills: ["Goalkeeper Reflex", "Defensive Wall"], club: "Sample Club", nft: true },
];
