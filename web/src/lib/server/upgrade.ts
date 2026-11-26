import "server-only";
import { Prisma } from "@/generated/prisma/client";
import { ATTRIBUTE_KEYS, ATTR_MAX, type AttributeKey, type Rarity } from "@/lib/domain/rules";
import { ApiError } from "./api";
import { audit } from "./audit";
import { attrsOf } from "./club";
import { db } from "./db";
import { getGameConfig } from "./game-config";
import { spend } from "./ledger";

async function ownedPlayer(userId: string, playerId: string) {
  const club = await db.club.findUnique({ where: { ownerId: userId } });
  if (!club) throw new ApiError(409, "NO_CLUB");
  const own = await db.clubPlayer.findFirst({ where: { clubId: club.id, playerId, active: true } });
  if (!own) throw new ApiError(403, "NOT_OWNER");
  if (own.lockedUntil && own.lockedUntil > new Date()) throw new ApiError(409, "PLAYER_LOCKED");
  return club;
}

/** Next breakthrough for a player, or null at the top. Cost is in NFWL Points (Stage 1, off-chain). */
export function nextBreakthrough(levelCap: number, cfg: Awaited<ReturnType<typeof getGameConfig>>) {
  const i = cfg.levelCaps.steps.indexOf(levelCap);
  if (i < 0 || i >= cfg.levelCaps.steps.length - 1) return null;
  return { from: levelCap, to: cfg.levelCaps.steps[i + 1], cost: cfg.levelCaps.cost[i], requiresLevel: levelCap };
}

/**
 * Raises the level cap. The point spend, the cap change and the upgrade record are one DB transaction,
 * keyed by the client's idempotency key: a retry returns the first result, a failure leaves nothing behind.
 * Off-chain test mode only. An on-chain version must wait for the transaction to confirm before applying.
 */
export async function breakthrough(userId: string, playerId: string, idempotencyKey: string) {
  const cfg = await getGameConfig();
  await ownedPlayer(userId, playerId);
  const key = `upgrade:${userId}:${idempotencyKey}`;
  return db.$transaction(async (tx) => {
    const prev = await tx.playerUpgrade.findUnique({ where: { idempotencyKey: key } });
    if (prev) {
      if (prev.playerId !== playerId) throw new ApiError(409, "IDEMPOTENCY_MISMATCH");
      return { replay: true, upgrade: prev };
    }
    await tx.$queryRaw`SELECT id FROM "Player" WHERE id = ${playerId}::uuid FOR UPDATE`;
    const p = await tx.player.findUniqueOrThrow({ where: { id: playerId } });
    const nb = nextBreakthrough(p.levelCap, cfg);
    if (!nb) throw new ApiError(409, "MAX_CAP");
    if (p.level < nb.requiresLevel) throw new ApiError(409, "LEVEL_TOO_LOW", `Lv${nb.requiresLevel}に到達すると解放できます`);
    const { tx: tokenTx } = await spend(tx, userId, "NFWL_POINTS", nb.cost, key, "LEVEL_CAP_BREAKTHROUGH", `player ${playerId} cap ${nb.from}->${nb.to}`);
    await tx.player.update({ where: { id: playerId }, data: { levelCap: nb.to } });
    const upgrade = await tx.playerUpgrade.create({
      data: {
        playerId,
        kind: "LEVEL_CAP",
        idempotencyKey: key,
        before: { levelCap: nb.from },
        after: { levelCap: nb.to },
        tokenTxId: tokenTx.id,
        status: "CONFIRMED",
        testMode: true,
      },
    });
    await audit("upgrade.breakthrough", { userId, target: playerId, data: { from: nb.from, to: nb.to, cost: nb.cost } }, tx);
    return { replay: false, upgrade };
  });
}

/** Spends growth points (+1 attribute per point, up to the rarity cap). Free, but still idempotent and audited. */
export async function allocateGrowth(userId: string, playerId: string, alloc: Partial<Record<AttributeKey, number>>, idempotencyKey: string) {
  const cfg = await getGameConfig();
  await ownedPlayer(userId, playerId);
  const key = `growth:${userId}:${idempotencyKey}`;
  const total = Object.values(alloc).reduce((a, b) => a + (b ?? 0), 0);
  if (total <= 0) throw new ApiError(400, "EMPTY");
  return db.$transaction(async (tx) => {
    const prev = await tx.playerUpgrade.findUnique({ where: { idempotencyKey: key } });
    if (prev) return { replay: true, upgrade: prev };
    await tx.$queryRaw`SELECT id FROM "Player" WHERE id = ${playerId}::uuid FOR UPDATE`;
    const p = await tx.player.findUniqueOrThrow({ where: { id: playerId }, include: { attributes: true } });
    if (p.growthPoints < total) throw new ApiError(409, "NOT_ENOUGH_GROWTH_POINTS");
    const cap = Math.min(ATTR_MAX, cfg.rarity[p.rarity as Rarity].attributeCap);
    const before = attrsOf(p.attributes!);
    const after = { ...before };
    for (const k of ATTRIBUTE_KEYS) {
      const add = alloc[k] ?? 0;
      if (add < 0 || !Number.isInteger(add)) throw new ApiError(400, "BAD_ALLOCATION");
      after[k] = before[k] + add;
      if (after[k] > cap) throw new ApiError(409, "OVER_CAP", `${k} は上限 ${cap} を超えられません`);
    }
    await tx.playerAttributes.update({ where: { playerId }, data: after });
    await tx.player.update({ where: { id: playerId }, data: { growthPoints: { decrement: total } } });
    const upgrade = await tx.playerUpgrade.create({
      data: {
        playerId,
        kind: "GROWTH",
        idempotencyKey: key,
        before: before as unknown as Prisma.InputJsonValue,
        after: after as unknown as Prisma.InputJsonValue,
        status: "CONFIRMED",
        testMode: false,
      },
    });
    await audit("upgrade.growth", { userId, target: playerId, data: { alloc } }, tx);
    return { replay: false, upgrade };
  });
}
