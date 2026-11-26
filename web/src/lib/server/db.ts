import "server-only";
import { PrismaPg } from "@prisma/adapter-pg";
import { PrismaClient } from "@/generated/prisma/client";

const g = globalThis as unknown as { __nfwlPrisma?: PrismaClient };

function create() {
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error("DATABASE_URL is not set");
  return new PrismaClient({ adapter: new PrismaPg({ connectionString: url }) });
}

export const db: PrismaClient = g.__nfwlPrisma ?? (g.__nfwlPrisma = create());
export type Tx = Parameters<Parameters<PrismaClient["$transaction"]>[0]>[0];
