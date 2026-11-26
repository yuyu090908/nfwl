import "server-only";
import { NextResponse, type NextRequest } from "next/server";
import { z } from "zod";
import { getCurrentUser, type CurrentUser } from "./session";
import { rateLimit } from "./rate-limit";
import { LedgerError } from "./ledger";

export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message?: string,
  ) {
    super(message ?? code);
  }
}

export function clientIp(req: NextRequest): string {
  return req.headers.get("x-forwarded-for")?.split(",")[0]?.trim() || req.headers.get("x-real-ip") || "local";
}

/**
 * CSRF defence for cookie-authenticated JSON endpoints:
 * state-changing requests must come from our own origin (Origin, or Sec-Fetch-Site when Origin is absent)
 * and must be JSON (a cross-site <form> cannot send application/json without a CORS preflight).
 */
export function checkSameOrigin(req: NextRequest) {
  if (req.method === "GET" || req.method === "HEAD") return;
  const origin = req.headers.get("origin");
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host");
  if (origin) {
    let ok = false;
    try {
      ok = new URL(origin).host === host;
    } catch {}
    const allowed = (process.env.APP_ORIGIN ?? "").split(",").filter(Boolean);
    if (!ok && !allowed.includes(origin)) throw new ApiError(403, "BAD_ORIGIN");
  } else {
    const site = req.headers.get("sec-fetch-site");
    if (site && site !== "same-origin" && site !== "none") throw new ApiError(403, "BAD_ORIGIN");
  }
  const ct = req.headers.get("content-type") ?? "";
  if (req.headers.get("content-length") !== "0" && !ct.includes("application/json")) throw new ApiError(415, "JSON_REQUIRED");
}

type Ctx<P> = { req: NextRequest; user: CurrentUser; params: P; ip: string };
type PublicCtx<P> = Omit<Ctx<P>, "user"> & { user: CurrentUser | null };

interface Opts<B extends z.ZodType | undefined> {
  auth?: boolean;
  body?: B;
  /** requests per minute per user (or IP) for this route */
  limit?: number;
  name: string;
}

export function route<P = Record<string, string>, B extends z.ZodType | undefined = undefined>(
  opts: Opts<B> & { auth?: true },
  fn: (ctx: Ctx<P>, body: B extends z.ZodType ? z.infer<B> : undefined) => Promise<unknown>,
): (req: NextRequest, ctx: { params: Promise<P> }) => Promise<NextResponse>;
export function route<P = Record<string, string>, B extends z.ZodType | undefined = undefined>(
  opts: Opts<B> & { auth: false },
  fn: (ctx: PublicCtx<P>, body: B extends z.ZodType ? z.infer<B> : undefined) => Promise<unknown>,
): (req: NextRequest, ctx: { params: Promise<P> }) => Promise<NextResponse>;
export function route<P, B extends z.ZodType | undefined>(
  opts: Opts<B>,
  fn: (ctx: never, body: never) => Promise<unknown>,
) {
  return async (req: NextRequest, rc: { params: Promise<P> }) => {
    try {
      checkSameOrigin(req);
      const ip = clientIp(req);
      const user = await getCurrentUser();
      if (opts.auth !== false && !user) throw new ApiError(401, "UNAUTHENTICATED");
      const rl = rateLimit(`${opts.name}:${user?.id ?? ip}`, opts.limit ?? 60, 60_000);
      if (!rl.ok) {
        return NextResponse.json({ error: "RATE_LIMITED" }, { status: 429, headers: { "Retry-After": String(rl.retryAfter) } });
      }
      let body: unknown = undefined;
      if (opts.body) {
        let raw: unknown;
        try {
          raw = await req.json();
        } catch {
          throw new ApiError(400, "INVALID_JSON");
        }
        const parsed = opts.body.safeParse(raw);
        if (!parsed.success) {
          return NextResponse.json({ error: "VALIDATION", issues: parsed.error.issues.slice(0, 10) }, { status: 400 });
        }
        body = parsed.data;
      }
      const params = (await rc.params) as P;
      const out = await (fn as (c: unknown, b: unknown) => Promise<unknown>)({ req, user, params, ip }, body);
      if (out instanceof NextResponse) return out;
      return NextResponse.json(out ?? { ok: true }, { headers: { "Cache-Control": "no-store" } });
    } catch (e) {
      if (e instanceof ApiError) return NextResponse.json({ error: e.code, message: e.message }, { status: e.status });
      if (e instanceof LedgerError) return NextResponse.json({ error: e.code, message: e.message }, { status: 409 });
      console.error(`[api:${opts.name}]`, e);
      return NextResponse.json({ error: "INTERNAL" }, { status: 500 });
    }
  };
}
