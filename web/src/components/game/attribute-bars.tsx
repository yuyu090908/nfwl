import { ATTRIBUTE_KEYS, ATTRIBUTE_LABELS, type Attributes } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";

const tone = (v: number) => (v >= 85 ? "bg-legendary" : v >= 75 ? "bg-success" : v >= 65 ? "bg-crystal" : v >= 55 ? "bg-warning" : "bg-danger");

/** 40..99 attribute bars. `compare` shows a before/after delta (upgrade preview, position penalty). */
export function AttributeBars({ attrs, compare, cap }: { attrs: Attributes; compare?: Partial<Attributes>; cap?: number }) {
  return (
    <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
      {ATTRIBUTE_KEYS.map((k) => {
        const v = attrs[k];
        const c = compare?.[k];
        const d = c !== undefined ? c - v : 0;
        return (
          <div key={k} className="grid grid-cols-[110px_1fr_auto] items-center gap-2">
            <dt className="truncate text-xs text-ink-2">{ATTRIBUTE_LABELS[k]}</dt>
            <div className="relative h-1.5 overflow-hidden rounded-full bg-bg-2">
              <div className={cn("h-full rounded-full", tone(c ?? v))} style={{ width: `${(((c ?? v) - 40) / 59) * 100}%` }} />
              {cap && <div className="absolute top-0 h-full w-px bg-gold/70" style={{ left: `${((cap - 40) / 59) * 100}%` }} title={`上限 ${cap}`} />}
            </div>
            <dd className="w-14 text-right font-display text-sm font-semibold tabular-nums">
              {c ?? v}
              {d !== 0 && <span className={cn("ml-1 text-[11px]", d > 0 ? "text-success" : "text-danger")}>{d > 0 ? `+${d}` : d}</span>}
            </dd>
          </div>
        );
      })}
    </dl>
  );
}
