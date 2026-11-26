import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export function Card({ className, ...p }: ComponentProps<"div">) {
  return <div className={cn("rounded-[var(--radius-card)] border border-line bg-card/80 backdrop-blur-sm", className)} {...p} />;
}
export function CardHeader({ className, ...p }: ComponentProps<"div">) {
  return <div className={cn("flex items-start justify-between gap-3 p-5 pb-3", className)} {...p} />;
}
export function CardTitle({ className, ...p }: ComponentProps<"h2">) {
  return <h2 className={cn("font-display text-base font-semibold uppercase tracking-wider text-ink", className)} {...p} />;
}
export function CardBody({ className, ...p }: ComponentProps<"div">) {
  return <div className={cn("p-5 pt-2", className)} {...p} />;
}
