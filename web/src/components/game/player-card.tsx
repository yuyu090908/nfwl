import Link from "next/link";
import type { Rarity } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";
import { CrystaliaPortrait } from "./crystalia-portrait";
import { RARITY_STYLE, RarityBadge } from "./rarity";

export interface PlayerCardData {
  id: string;
  name: string;
  position: string;
  rarity: Rarity;
  ovr: number;
  level: number;
  crystalType: string;
  skills?: string[];
  club?: string | null;
  nft?: boolean;
}

/** Rarity-framed player card (Silver / Blue / Purple / Red / Gold). */
export function PlayerCard({ p, href, size = "md", className, footer }: { p: PlayerCardData; href?: string; size?: "sm" | "md"; className?: string; footer?: React.ReactNode }) {
  const s = RARITY_STYLE[p.rarity];
  const body = (
    <div className={cn("group relative rounded-2xl bg-gradient-to-br p-[1.5px] transition-transform duration-300 hover:-translate-y-1", s.frame, s.glow, className)}>
      <div className={cn("relative overflow-hidden rounded-[15px] bg-gradient-to-b", s.bg)}>
        <div className="flex items-start justify-between p-3 pb-0">
          <div>
            <div className={cn("font-display font-bold leading-none", s.text, size === "sm" ? "text-2xl" : "text-3xl")}>{p.ovr}</div>
            <div className="mt-0.5 text-[11px] font-bold tracking-widest text-ink-2">{p.position}</div>
          </div>
          <div className="flex flex-col items-end gap-1">
            <RarityBadge rarity={p.rarity} />
            {p.nft ? <span className="text-[10px] font-semibold text-gold">NFT</span> : <span className="text-[10px] text-ink-3">CLUB</span>}
          </div>
        </div>
        <CrystaliaPortrait crystal={p.crystalType} seed={p.id} className={cn("mx-auto -mt-2 block", size === "sm" ? "h-24 w-24" : "h-32 w-32")} />
        <div className="border-t border-white/10 bg-black/25 px-3 py-2">
          <div className="truncate font-display text-sm font-semibold uppercase tracking-wide" title={p.name}>{p.name}</div>
          <div className="flex items-center justify-between text-[11px] text-ink-2">
            <span>Lv {p.level}</span>
            {p.club !== undefined && <span className="truncate pl-2">{p.club ?? "—"}</span>}
          </div>
          {p.skills && p.skills.length > 0 && (
            <div className="mt-1 flex flex-wrap gap-1">
              {p.skills.slice(0, 2).map((sk) => (
                <span key={sk} className="rounded bg-white/8 px-1.5 py-0.5 text-[10px] text-ink-2">{sk}</span>
              ))}
            </div>
          )}
          {footer}
        </div>
      </div>
    </div>
  );
  return href ? (
    <Link href={href} className="block rounded-2xl" aria-label={`${p.name} の詳細`}>
      {body}
    </Link>
  ) : (
    body
  );
}
