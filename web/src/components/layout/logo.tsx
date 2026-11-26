import Link from "next/link";

export function Logo() {
  return (
    <Link href="/" className="flex items-center gap-2" aria-label="NFWL ホーム">
      <svg viewBox="0 0 40 40" className="h-8 w-8" aria-hidden>
        <defs>
          <linearGradient id="lg" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor="#7ae6ff" />
            <stop offset="1" stopColor="#9258ff" />
          </linearGradient>
        </defs>
        <path d="M20 2 L35 11 V29 L20 38 L5 29 V11 Z" fill="none" stroke="url(#lg)" strokeWidth="2.5" />
        <path d="M20 9 L27 19 L20 31 L13 19 Z" fill="url(#lg)" />
        <path d="M13 19 H27" stroke="#080b12" strokeWidth="1.2" />
      </svg>
      <span className="font-display text-lg font-bold tracking-[0.2em]">
        NF<span className="text-gold">WL</span>
      </span>
    </Link>
  );
}
