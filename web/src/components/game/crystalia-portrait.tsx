import { CRYSTAL } from "./crystalia";

/**
 * Stylised Crystalia head-and-shoulders, drawn in SVG from the element colours
 * (pointed ears, glowing eyes, spiked hair, shoulder crystals as in yu's character sheet).
 * Placeholder until rendered art of each player exists; same input always gives the same face.
 */
export function CrystaliaPortrait({ crystal, seed = "x", className, kit = "#0f1420" }: { crystal: string; seed?: string; className?: string; kit?: string }) {
  const c = CRYSTAL[crystal] ?? CRYSTAL.ICE;
  let h = 0;
  for (const ch of seed) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  const spikes = 5 + (h % 3);
  const tilt = ((h >> 3) % 7) - 3;
  const id = `g${h.toString(36)}`;
  const hair = Array.from({ length: spikes }, (_, i) => {
    const x = 30 + (i * 40) / (spikes - 1);
    const len = 14 + ((h >> (i + 2)) % 9);
    return `M${x - 6} 40 L${x + tilt} ${40 - len} L${x + 6} 40 Z`;
  }).join(" ");
  return (
    <svg viewBox="0 0 100 100" className={className} role="img" aria-label={`${c.name} Crystalia player`}>
      <defs>
        <radialGradient id={`${id}-bg`} cx="50%" cy="35%" r="70%">
          <stop offset="0%" stopColor={c.crystal} stopOpacity="0.35" />
          <stop offset="100%" stopColor="#080b12" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${id}-cr`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#ffffff" stopOpacity="0.9" />
          <stop offset="100%" stopColor={c.crystal} />
        </linearGradient>
        <filter id={`${id}-glow`}><feGaussianBlur stdDeviation="1.2" /></filter>
      </defs>
      <rect width="100" height="100" fill={`url(#${id}-bg)`} />
      {/* shoulders + kit */}
      <path d="M14 100 C16 80 30 72 50 72 C70 72 84 80 86 100 Z" fill={kit} stroke={c.crystal} strokeOpacity="0.5" strokeWidth="0.8" />
      <path d="M44 72 L50 82 L56 72" fill="none" stroke={c.crystal} strokeWidth="1.2" />
      {/* shoulder crystals */}
      <path d="M18 84 L24 70 L28 86 Z M82 84 L76 70 L72 86 Z" fill={`url(#${id}-cr)`} />
      {/* neck */}
      <rect x="44" y="60" width="12" height="14" rx="4" fill={c.skin} />
      {/* ears */}
      <path d="M30 46 L16 32 L32 54 Z M70 46 L84 32 L68 54 Z" fill={c.skin} />
      {/* head */}
      <ellipse cx="50" cy="48" rx="20" ry="22" fill={c.skin} />
      {/* hair */}
      <path d={hair} fill={c.hair} />
      <path d="M30 44 C30 28 70 28 70 44 C64 36 36 36 30 44 Z" fill={c.hair} />
      {/* forehead crystal */}
      <path d="M50 33 L53 38 L50 43 L47 38 Z" fill={`url(#${id}-cr)`} />
      {/* eyes */}
      <g filter={`url(#${id}-glow)`}>
        <ellipse cx="42" cy="50" rx="3.4" ry="2.2" fill={c.eye} />
        <ellipse cx="58" cy="50" rx="3.4" ry="2.2" fill={c.eye} />
      </g>
      <ellipse cx="42" cy="50" rx="1.6" ry="1.4" fill="#fff" />
      <ellipse cx="58" cy="50" rx="1.6" ry="1.4" fill="#fff" />
      <path d="M45 61 Q50 63 55 61" stroke="#8a5a64" strokeWidth="1" fill="none" strokeLinecap="round" />
    </svg>
  );
}
