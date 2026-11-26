import "server-only";
import { Prisma } from "@/generated/prisma/client";
import type { Currency, LedgerAccountKind } from "@/generated/prisma/enums";
import { db, type Tx } from "./db";

// Double-entry ledger for Stage 1 NFWL Points and Free Pick score (Master Doc 8.6).
// - every transaction has an idempotency key (unique) -> retries never post twice
// - lines must sum to zero
// - accounts are row-locked (FOR UPDATE) and user balances may not go negative -> no double spend
// - the cached balance can always be recomputed from entries (verifyLedger)

const SYSTEM_IDS: Record<string, string> = {
  "SYSTEM_ISSUANCE:NFWL_POINTS": "00000000-0000-4000-8000-000000000001",
  "SYSTEM_SINK:NFWL_POINTS": "00000000-0000-4000-8000-000000000002",
  "SYSTEM_LOCKED:NFWL_POINTS": "00000000-0000-4000-8000-000000000003",
  "SYSTEM_ISSUANCE:PICK_SCORE": "00000000-0000-4000-8000-000000000011",
  "SYSTEM_SINK:PICK_SCORE": "00000000-0000-4000-8000-000000000012",
};

export class LedgerError extends Error {
  constructor(
    public code: "INSUFFICIENT_BALANCE" | "UNBALANCED" | "IDEMPOTENCY_MISMATCH",
    message: string,
  ) {
    super(message);
  }
}

export async function systemAccount(tx: Tx, kind: Exclude<LedgerAccountKind, "USER">, currency: Currency) {
  const id = SYSTEM_IDS[`${kind}:${currency}`];
  if (!id) throw new Error(`no system account ${kind}:${currency}`);
  return tx.ledgerAccount.upsert({ where: { id }, update: {}, create: { id, kind, currency } });
}

export async function userAccount(tx: Tx, userId: string, currency: Currency) {
  return tx.ledgerAccount.upsert({
    where: { kind_currency_userId: { kind: "USER", currency, userId } },
    update: {},
    create: { kind: "USER", currency, userId },
  });
}

export interface PostInput {
  idempotencyKey: string;
  currency: Currency;
  kind: string;
  memo?: string;
  lines: { accountId: string; amount: number }[];
}

/** Posts a balanced transaction inside the caller's DB transaction. Returns the existing one on a retry. */
export async function post(tx: Tx, input: PostInput) {
  const existing = await tx.tokenTransaction.findUnique({
    where: { idempotencyKey: input.idempotencyKey },
    include: { entries: true },
  });
  if (existing) {
    if (existing.kind !== input.kind || existing.currency !== input.currency)
      throw new LedgerError("IDEMPOTENCY_MISMATCH", "idempotency key reused for a different operation");
    return { tx: existing, replay: true };
  }
  const sum = input.lines.reduce((s, l) => s + l.amount, 0);
  if (sum !== 0 || input.lines.some((l) => !Number.isInteger(l.amount)))
    throw new LedgerError("UNBALANCED", "ledger lines must be integers summing to zero");

  const ids = [...new Set(input.lines.map((l) => l.accountId))].sort();
  // Lock in a stable order to avoid deadlocks.
  await tx.$queryRaw`SELECT id FROM "LedgerAccount" WHERE id IN (${Prisma.join(ids.map((i) => Prisma.sql`${i}::uuid`))}) ORDER BY id FOR UPDATE`;
  const accounts = await tx.ledgerAccount.findMany({ where: { id: { in: ids } } });
  const delta = new Map<string, number>();
  for (const l of input.lines) delta.set(l.accountId, (delta.get(l.accountId) ?? 0) + l.amount);
  for (const a of accounts) {
    const next = a.balance + (delta.get(a.id) ?? 0);
    if (a.kind === "USER" && next < 0) throw new LedgerError("INSUFFICIENT_BALANCE", "not enough balance");
  }
  const created = await tx.tokenTransaction.create({
    data: {
      idempotencyKey: input.idempotencyKey,
      currency: input.currency,
      kind: input.kind,
      memo: input.memo,
      status: "CONFIRMED",
      confirmedAt: new Date(),
      entries: { create: input.lines.map((l) => ({ accountId: l.accountId, amount: l.amount })) },
    },
    include: { entries: true },
  });
  for (const [id, d] of delta) await tx.ledgerAccount.update({ where: { id }, data: { balance: { increment: d } } });
  return { tx: created, replay: false };
}

/** Issue points from the system to a user (rewards, starter grant). */
export async function grant(tx: Tx, userId: string, currency: Currency, amount: number, idempotencyKey: string, kind: string, memo?: string) {
  const from = await systemAccount(tx, "SYSTEM_ISSUANCE", currency);
  const to = await userAccount(tx, userId, currency);
  return post(tx, {
    idempotencyKey,
    currency,
    kind,
    memo,
    lines: [
      { accountId: from.id, amount: -amount },
      { accountId: to.id, amount },
    ],
  });
}

/** Spend points into the sink (burn). Fails with INSUFFICIENT_BALANCE instead of going negative. */
export async function spend(tx: Tx, userId: string, currency: Currency, amount: number, idempotencyKey: string, kind: string, memo?: string) {
  const from = await userAccount(tx, userId, currency);
  const to = await systemAccount(tx, "SYSTEM_SINK", currency);
  return post(tx, {
    idempotencyKey,
    currency,
    kind,
    memo,
    lines: [
      { accountId: from.id, amount: -amount },
      { accountId: to.id, amount },
    ],
  });
}

export async function balanceOf(userId: string, currency: Currency) {
  const a = await db.ledgerAccount.findUnique({ where: { kind_currency_userId: { kind: "USER", currency, userId } } });
  return a?.balance ?? 0;
}

/** Recomputes every balance from its entries. Returns the accounts that disagree. */
export async function verifyLedger() {
  const rows = await db.$queryRaw<{ id: string; balance: number; computed: bigint | null }[]>`
    SELECT a.id, a.balance, COALESCE(SUM(e.amount), 0) AS computed
    FROM "LedgerAccount" a LEFT JOIN "TokenLedgerEntry" e ON e."accountId" = a.id
    GROUP BY a.id, a.balance`;
  const unbalanced = await db.$queryRaw<{ id: string }[]>`
    SELECT t.id FROM "TokenTransaction" t JOIN "TokenLedgerEntry" e ON e."transactionId" = t.id
    GROUP BY t.id HAVING SUM(e.amount) <> 0`;
  return {
    mismatched: rows.filter((r) => Number(r.computed ?? 0) !== r.balance),
    unbalancedTransactions: unbalanced,
  };
}
