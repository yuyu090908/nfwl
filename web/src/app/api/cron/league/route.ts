import { NextResponse, type NextRequest } from "next/server";
import { timingSafeEqual } from "node:crypto";
import { resolveDueFixtures, scheduleFixtures } from "@/lib/server/league";

/** Called by a scheduler (e.g. Vercel Cron) with "Authorization: Bearer $CRON_SECRET". */
async function handle(req: NextRequest) {
  const secret = process.env.CRON_SECRET;
  const got = req.headers.get("authorization")?.replace(/^Bearer /, "") ?? "";
  if (!secret || got.length !== secret.length || !timingSafeEqual(Buffer.from(got), Buffer.from(secret)))
    return NextResponse.json({ error: "UNAUTHORIZED" }, { status: 401 });
  const resolved = await resolveDueFixtures();
  const scheduled = await scheduleFixtures(Number(req.nextUrl.searchParams.get("hoursAhead") ?? 2));
  return NextResponse.json({ ...resolved, ...scheduled });
}
export const GET = handle;
export const POST = handle;
