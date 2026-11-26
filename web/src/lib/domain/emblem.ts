// Club emblem built from presets (Full Spec 5.1: no image upload in the first release).
export const LOGO_SHAPES = ["shield", "circle", "hexagon", "diamond", "crest"] as const;
export const LOGO_SYMBOLS = ["crystal", "star", "bolt", "crown", "wing", "flame", "orbit", "ball"] as const;
export type LogoShape = (typeof LOGO_SHAPES)[number];
export type LogoSymbol = (typeof LOGO_SYMBOLS)[number];

export const SHAPE_PATHS: Record<LogoShape, string> = {
  shield: "M50 4 L92 18 V52 C92 76 72 92 50 98 C28 92 8 76 8 52 V18 Z",
  circle: "M50 4 A46 46 0 1 1 49.9 4 Z",
  hexagon: "M50 3 L93 27 V73 L50 97 L7 73 V27 Z",
  diamond: "M50 2 L96 50 L50 98 L4 50 Z",
  crest: "M14 6 H86 V48 C86 74 68 90 50 98 C32 90 14 74 14 48 Z",
};

export const SYMBOL_PATHS: Record<LogoSymbol, string> = {
  crystal: "M50 22 L64 42 L50 80 L36 42 Z M36 42 H64",
  star: "M50 22 L57 42 L78 42 L61 54 L68 75 L50 62 L32 75 L39 54 L22 42 L43 42 Z",
  bolt: "M56 18 L34 54 H50 L44 82 L68 44 H52 Z",
  crown: "M26 66 L30 36 L42 50 L50 30 L58 50 L70 36 L74 66 Z",
  wing: "M24 64 C40 30 62 26 78 28 C66 36 62 44 60 52 C54 50 46 52 40 58 C36 60 30 62 24 64 Z",
  flame: "M50 20 C62 38 70 48 66 62 C63 74 56 80 50 80 C44 80 37 74 34 62 C32 52 40 46 44 36 C46 48 52 50 54 46 C56 38 52 30 50 20 Z",
  orbit: "M50 30 A20 20 0 1 1 49.9 30 Z M20 58 C30 40 70 36 82 46",
  ball: "M50 24 A26 26 0 1 1 49.9 24 Z M50 36 L60 44 L56 56 H44 L40 44 Z",
};
