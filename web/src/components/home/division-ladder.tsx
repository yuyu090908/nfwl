const TIERS = [
  { id: 0, name: "Champions League", note: "Division 1 上位32クラブ（出場資格）" },
  { id: 1, name: "Division 1", note: "Elite · 900 RP〜" },
  { id: 2, name: "Division 2", note: "Master · 800 RP〜" },
  { id: 3, name: "Division 3", note: "Diamond · 700 RP〜" },
  { id: 4, name: "Division 4", note: "Platinum · 600 RP〜" },
  { id: 5, name: "Division 5", note: "Gold · 500 RP〜" },
  { id: 6, name: "Division 6", note: "Silver · 400 RP〜" },
  { id: 7, name: "Division 7", note: "Bronze · 300 RP〜" },
  { id: 8, name: "Division 8", note: "Rookie III · 200 RP〜" },
  { id: 9, name: "Division 9", note: "Rookie II · 100 RP〜" },
  { id: 10, name: "Division 10", note: "Rookie I · 新規クラブはここから" },
];

/** Division 10 -> 1 -> Champions League, drawn as a widening ladder (prompt 5.5). */
export function DivisionLadder({ current }: { current?: number }) {
  return (
    <ol className="mx-auto flex max-w-3xl flex-col items-center gap-1.5" aria-label="Division構造（上が最上位）">
      {TIERS.map((t, i) => {
        const cl = t.id === 0;
        const width = 46 + i * 5.4;
        return (
          <li key={t.id} style={{ width: `${width}%` }}
            className={[
              "relative flex min-w-[260px] max-w-full items-center justify-between gap-3 rounded-lg border px-4 py-2 text-left transition-colors",
              cl ? "border-gold/60 bg-gradient-to-r from-gold/25 via-legendary/15 to-gold/25 shadow-gold" : "border-line bg-card/70",
              current === t.id ? "ring-2 ring-crystal" : "",
            ].join(" ")}>
            <span className={`font-display text-sm font-bold uppercase tracking-wider ${cl ? "text-gold" : ""}`}>{t.name}</span>
            <span className="truncate text-[11px] text-ink-2">{t.note}</span>
            {current === t.id && <span className="absolute -right-2 -top-2 rounded bg-crystal px-1.5 text-[10px] font-bold text-bg">YOU</span>}
          </li>
        );
      })}
    </ol>
  );
}
