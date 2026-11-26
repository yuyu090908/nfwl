import { SHAPE_PATHS, SYMBOL_PATHS, type LogoShape, type LogoSymbol } from "@/lib/domain/emblem";

export function ClubEmblem({ shape, symbol, colors, className, label }: { shape: LogoShape; symbol: LogoSymbol; colors: string[]; className?: string; label?: string }) {
  const [main, sub, accent] = [colors[0] ?? "#171e2d", colors[1] ?? "#44d9ff", colors[2] ?? "#d5ac59"];
  return (
    <svg viewBox="0 0 100 100" className={className} role="img" aria-label={label ?? "Club emblem"}>
      <path d={SHAPE_PATHS[shape] ?? SHAPE_PATHS.shield} fill={main} stroke={accent} strokeWidth="4" />
      <path d={SHAPE_PATHS[shape] ?? SHAPE_PATHS.shield} fill="none" stroke={sub} strokeOpacity="0.5" strokeWidth="1.5" transform="translate(50 50) scale(0.84) translate(-50 -50)" />
      <path d={SYMBOL_PATHS[symbol] ?? SYMBOL_PATHS.crystal} fill={sub} stroke={accent} strokeWidth="1.5" strokeLinejoin="round" />
    </svg>
  );
}
