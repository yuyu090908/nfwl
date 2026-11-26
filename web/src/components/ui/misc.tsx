import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

export function Skeleton({ className, ...p }: ComponentProps<"div">) {
  return <div aria-hidden className={cn("skeleton rounded-lg", className)} {...p} />;
}

export function Progress({ value, max = 100, className, tone = "crystal", label }: { value: number; max?: number; className?: string; tone?: "crystal" | "gold"; label?: string }) {
  const pct = Math.max(0, Math.min(100, (value / Math.max(1, max)) * 100));
  return (
    <div role="progressbar" aria-valuemin={0} aria-valuemax={max} aria-valuenow={value} aria-label={label} className={cn("h-2 overflow-hidden rounded-full bg-bg-2", className)}>
      <div className={cn("h-full rounded-full transition-[width] duration-500", tone === "gold" ? "bg-gradient-to-r from-gold to-legendary" : "bg-gradient-to-r from-[#1f8fb0] to-crystal")} style={{ width: `${pct}%` }} />
    </div>
  );
}

export function StatTile({ label, value, hint, accent }: { label: string; value: ReactNode; hint?: ReactNode; accent?: boolean }) {
  return (
    <div className={cn("rounded-xl border border-line bg-bg-2/70 p-4", accent && "border-gold/40")}>
      <div className="text-[11px] font-semibold uppercase tracking-widest text-ink-3">{label}</div>
      <div className={cn("mt-1 font-display text-2xl font-bold", accent ? "text-gold" : "text-ink")}>{value}</div>
      {hint && <div className="mt-0.5 text-xs text-ink-2">{hint}</div>}
    </div>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-line bg-bg-2/40 px-6 py-12 text-center">
      <div className="font-display text-lg font-semibold">{title}</div>
      {children && <div className="max-w-md text-sm text-ink-2">{children}</div>}
      {action}
    </div>
  );
}

export function PageHeader({ eyebrow, title, children, actions }: { eyebrow?: string; title: string; children?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {eyebrow && <div className="mb-1 text-xs font-semibold uppercase tracking-[0.25em] text-crystal">{eyebrow}</div>}
        <h1 className="font-display text-3xl font-bold uppercase tracking-wide sm:text-4xl">{title}</h1>
        {children && <div className="mt-2 max-w-2xl text-sm text-ink-2">{children}</div>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

export function Notice({ tone = "info", title, children }: { tone?: "info" | "warning" | "danger"; title?: string; children: ReactNode }) {
  const c = tone === "warning" ? "border-warning/40 bg-warning/8 text-warning" : tone === "danger" ? "border-danger/40 bg-danger/8 text-danger" : "border-crystal/30 bg-crystal/6 text-crystal";
  return (
    <div role={tone === "danger" ? "alert" : "note"} className={cn("rounded-xl border px-4 py-3 text-sm", c)}>
      {title && <div className="mb-0.5 font-semibold">{title}</div>}
      <div className="text-ink-2">{children}</div>
    </div>
  );
}

export function Field({ label, error, hint, children, htmlFor }: { label: string; error?: string; hint?: string; children: ReactNode; htmlFor: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={htmlFor} className="text-xs font-semibold uppercase tracking-wider text-ink-2">{label}</label>
      {children}
      {error ? <p role="alert" className="text-xs text-danger">{error}</p> : hint ? <p className="text-xs text-ink-3">{hint}</p> : null}
    </div>
  );
}

export const inputClass =
  "h-11 w-full rounded-xl border border-line bg-bg-2 px-3 text-sm text-ink placeholder:text-ink-3 transition-colors focus:border-crystal/70 focus:outline-none aria-[invalid=true]:border-danger";
