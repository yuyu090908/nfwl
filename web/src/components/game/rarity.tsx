import { RARITY_COLORS, type Rarity } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";

export const RARITY_STYLE: Record<Rarity, { frame: string; text: string; glow: string; bg: string }> = {
  Common: { frame: "from-[#e8edf5] via-[#8b95a7] to-[#e8edf5]", text: "text-common", glow: "shadow-[0_0_30px_-10px_rgba(201,209,222,0.6)]", bg: "from-[#2a3142] to-[#141a26]" },
  Rare: { frame: "from-[#9ff0ff] via-[#1d8cb0] to-[#9ff0ff]", text: "text-crystal", glow: "shadow-[0_0_34px_-8px_rgba(68,217,255,0.6)]", bg: "from-[#0f3346] to-[#0c1622]" },
  Epic: { frame: "from-[#d2b8ff] via-[#6431d6] to-[#d2b8ff]", text: "text-[#b796ff]", glow: "shadow-[0_0_38px_-8px_rgba(146,88,255,0.7)]", bg: "from-[#2b1752] to-[#120c22]" },
  Mythic: { frame: "from-[#ffb3be] via-[#c0152f] to-[#ffb3be]", text: "text-mythic", glow: "shadow-[0_0_40px_-8px_rgba(255,70,95,0.7)]", bg: "from-[#4a1220] to-[#1a0a10]" },
  Legendary: { frame: "from-[#fff1c7] via-[#c18f2c] to-[#fff1c7]", text: "text-legendary", glow: "shadow-[0_0_46px_-6px_rgba(243,198,90,0.8)]", bg: "from-[#4a3610] to-[#1a1307]" },
};

export function RarityBadge({ rarity, className }: { rarity: Rarity; className?: string }) {
  return (
    <span
      className={cn("inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest", RARITY_STYLE[rarity].text, className)}
      style={{ borderColor: `${RARITY_COLORS[rarity].hex}66`, background: `${RARITY_COLORS[rarity].hex}14` }}
    >
      <span aria-hidden className="h-1.5 w-1.5 rotate-45" style={{ background: RARITY_COLORS[rarity].hex }} />
      {rarity}
    </span>
  );
}
