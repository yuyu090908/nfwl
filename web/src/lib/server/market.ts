import "server-only";
import { Prisma } from "@/generated/prisma/client";
import { db } from "./db";
import { attrsOf } from "./club";
import { overall, type Position, type Rarity } from "@/lib/domain/rules";

export interface MarketFilter {
  rarity?: string;
  position?: string;
  crystal?: string;
  skill?: string;
  minOvr?: number;
  maxOvr?: number;
  minLevel?: number;
  maxPrice?: number;
  sort?: string;
  type?: "FIXED_PRICE" | "AUCTION";
}

/** Listings with player data. OVR is computed (not stored), so OVR filters/sorts run after the query. */
export async function searchListings(f: MarketFilter) {
  const where: Prisma.MarketplaceListingWhereInput = {
    status: { in: ["ACTIVE", "SOLD"] },
    type: f.type,
    player: {
      rarity: f.rarity ? (f.rarity as Rarity) : undefined,
      position: f.position ? (f.position as Position) : undefined,
      crystalType: f.crystal ? (f.crystal as "ICE") : undefined,
      level: f.minLevel ? { gte: f.minLevel } : undefined,
      skills: f.skill ? { some: { skillId: f.skill } } : undefined,
    },
    price: f.maxPrice ? { lte: f.maxPrice } : undefined,
  };
  const orderBy: Prisma.MarketplaceListingOrderByWithRelationInput =
    f.sort === "price_asc" ? { price: "asc" } : f.sort === "price_desc" ? { price: "desc" } : f.sort === "sold" ? { soldAt: { sort: "desc", nulls: "last" } } : { createdAt: "desc" };
  const rows = await db.marketplaceListing.findMany({
    where,
    orderBy,
    take: 120,
    include: { seller: { select: { displayName: true } }, player: { include: { attributes: true, skills: { include: { skill: true } }, nft: true } }, bids: { orderBy: { amount: "desc" }, take: 1 } },
  });
  let list = rows.map((r) => ({ ...r, ovr: overall(attrsOf(r.player.attributes!), r.player.position as Position) }));
  if (f.minOvr) list = list.filter((r) => r.ovr >= f.minOvr!);
  if (f.maxOvr) list = list.filter((r) => r.ovr <= f.maxOvr!);
  if (f.sort === "ovr") list.sort((a, b) => b.ovr - a.ovr);
  return list;
}
