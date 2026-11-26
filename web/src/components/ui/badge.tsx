import { cva, type VariantProps } from "class-variance-authority";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

const badge = cva("inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider", {
  variants: {
    tone: {
      neutral: "bg-card-2 text-ink-2 border border-line",
      crystal: "bg-crystal/12 text-crystal border border-crystal/30",
      gold: "bg-gold/12 text-gold border border-gold/35",
      success: "bg-success/12 text-success border border-success/30",
      warning: "bg-warning/12 text-warning border border-warning/35",
      danger: "bg-danger/12 text-danger border border-danger/35",
      epic: "bg-epic/15 text-[#b796ff] border border-epic/40",
    },
  },
  defaultVariants: { tone: "neutral" },
});

export function Badge({ className, tone, ...p }: ComponentProps<"span"> & VariantProps<typeof badge>) {
  return <span className={cn(badge({ tone }), className)} {...p} />;
}

/** Marks anything that is sample content so it is never mistaken for live data (prompt 21). */
export function DemoBadge({ label = "DEMO", className }: { label?: string; className?: string }) {
  return (
    <Badge tone="warning" className={className} title="デモ表示です。実際のデータではありません。">
      {label}
    </Badge>
  );
}
