import "server-only";
import { DEFAULT_CONFIG, deepMerge, type GameConfig } from "@/lib/domain/config";
import { db } from "./db";

let cached: { at: number; value: GameConfig } | null = null;

/** Defaults deep-merged with the "game" GameConfig row. Cached for 30 s per server instance. */
export async function getGameConfig(): Promise<GameConfig> {
  if (cached && Date.now() - cached.at < 30_000) return cached.value;
  const row = await db.gameConfig.findUnique({ where: { key: "game" } });
  const value = deepMerge(DEFAULT_CONFIG, row?.value ?? {});
  cached = { at: Date.now(), value };
  return value;
}

export function clearGameConfigCache() {
  cached = null;
}
