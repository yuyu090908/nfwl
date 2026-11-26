"use client";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, ClientApiError } from "@/lib/client-api";
import { cn } from "@/lib/utils";

export function PickButtons({ matchId, current, disabled, labels }: { matchId: string; current: string | null; disabled: boolean; labels: [string, string] }) {
  const router = useRouter();
  const [pick, setPick] = useState(current);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const opts = [["HOME", labels[0]], ["DRAW", "Draw"], ["AWAY", labels[1]]] as const;
  return (
    <div>
      <div className="grid grid-cols-3 gap-2" role="group" aria-label="予想">
        {opts.map(([k, l]) => (
          <button key={k} type="button" disabled={disabled || busy} aria-pressed={pick === k}
            onClick={async () => {
              setBusy(true);
              setErr(null);
              try {
                await api("/api/predictions", { body: { matchId, pick: k } });
                setPick(k);
                router.refresh();
              } catch (e) {
                setErr(e instanceof ClientApiError ? e.message : "送信できませんでした");
              } finally {
                setBusy(false);
              }
            }}
            className={cn("truncate rounded-lg border px-2 py-2 text-xs font-semibold transition-colors disabled:opacity-50", pick === k ? "border-gold bg-gold/15 text-gold" : "border-line hover:border-crystal/50")}>
            {l}
          </button>
        ))}
      </div>
      {err && <p role="alert" className="mt-1 text-xs text-danger">{err}</p>}
    </div>
  );
}
