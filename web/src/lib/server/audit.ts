import "server-only";
import type { Prisma } from "@/generated/prisma/client";
import { db, type Tx } from "./db";

export async function audit(
  action: string,
  opts: { userId?: string | null; target?: string; severity?: "info" | "warn" | "alert"; data?: Prisma.InputJsonValue; ip?: string | null },
  tx?: Tx,
) {
  const c = tx ?? db;
  await c.auditLog.create({
    data: {
      action,
      userId: opts.userId ?? null,
      target: opts.target,
      severity: opts.severity ?? "info",
      data: opts.data ?? {},
      ip: opts.ip ?? null,
    },
  });
}
