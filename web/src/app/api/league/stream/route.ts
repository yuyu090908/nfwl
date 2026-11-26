import type { NextRequest } from "next/server";
import { db } from "@/lib/server/db";
import { activeSeason } from "@/lib/server/league";

export const dynamic = "force-dynamic";

/**
 * Server-Sent Events: pushes the standings of one division whenever they change (checked every 5 s).
 * SSE works on Vercel functions and through proxies without a separate WebSocket server.
 * A function has a max duration, so the browser's EventSource reconnects on its own.
 */
export async function GET(req: NextRequest) {
  const division = Number(req.nextUrl.searchParams.get("division") ?? 10);
  const season = await activeSeason();
  const enc = new TextEncoder();
  let last = "";
  let timer: ReturnType<typeof setInterval> | undefined;
  const stream = new ReadableStream({
    async start(controller) {
      const push = async () => {
        if (!season) return;
        const latest = await db.leagueStanding.aggregate({ where: { seasonId: season.id, divisionId: division }, _max: { updatedAt: true }, _count: true });
        const stamp = `${latest._max.updatedAt?.toISOString()}:${latest._count}`;
        if (stamp !== last) {
          last = stamp;
          controller.enqueue(enc.encode(`event: standings\ndata: ${JSON.stringify({ division, stamp })}\n\n`));
        } else controller.enqueue(enc.encode(`: ping\n\n`));
      };
      await push();
      timer = setInterval(() => push().catch(() => {}), 5000);
      req.signal.addEventListener("abort", () => {
        clearInterval(timer);
        try { controller.close(); } catch {}
      });
    },
    cancel() {
      clearInterval(timer);
    },
  });
  return new Response(stream, {
    headers: { "Content-Type": "text/event-stream", "Cache-Control": "no-cache, no-transform", Connection: "keep-alive" },
  });
}
