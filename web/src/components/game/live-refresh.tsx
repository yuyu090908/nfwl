"use client";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

/** Subscribes to the standings SSE stream and refreshes the server-rendered table on change. */
export function LiveRefresh({ division }: { division: number }) {
  const router = useRouter();
  const [live, setLive] = useState(false);
  useEffect(() => {
    let first = true;
    const es = new EventSource(`/api/league/stream?division=${division}`);
    es.addEventListener("open", () => setLive(true));
    es.addEventListener("error", () => setLive(false));
    es.addEventListener("standings", () => {
      if (first) first = false;
      else router.refresh();
    });
    return () => es.close();
  }, [division, router]);
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-ink-2" role="status">
      <span className={`h-2 w-2 rounded-full ${live ? "animate-pulse bg-success" : "bg-ink-3"}`} />
      {live ? "Live" : "Reconnecting…"}
    </span>
  );
}
