import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Days left until a date (never negative). Kept out of components so renders stay pure. */
export function daysUntil(d: Date) {
  return Math.max(0, Math.ceil((d.getTime() - Date.now()) / 86400_000));
}

export function nowMs() {
  return Date.now();
}

export function fmtDate(d: Date | string | null | undefined, withTime = true) {
  if (!d) return "—";
  return new Intl.DateTimeFormat("ja-JP", {
    year: "numeric", month: "2-digit", day: "2-digit",
    ...(withTime ? { hour: "2-digit", minute: "2-digit" } : {}),
    timeZone: "Asia/Tokyo",
  }).format(new Date(d));
}
