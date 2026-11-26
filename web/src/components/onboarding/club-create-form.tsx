"use client";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Notice, inputClass } from "@/components/ui/misc";
import { ClubEmblem } from "@/components/game/club-emblem";
import { LOGO_SHAPES, LOGO_SYMBOLS, type LogoShape, type LogoSymbol } from "@/lib/domain/emblem";
import { api, ClientApiError } from "@/lib/client-api";
import { cn } from "@/lib/utils";

const PALETTES: [string, string, string][] = [
  ["#101624", "#44d9ff", "#d5ac59"],
  ["#1b0f2e", "#9258ff", "#f3c65a"],
  ["#2a0d14", "#ff465f", "#f7f8fb"],
  ["#0d2a1e", "#3ddc97", "#d5ac59"],
  ["#f7f8fb", "#101624", "#44d9ff"],
  ["#2b2410", "#f3c65a", "#101624"],
];

export function ClubCreateForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [short, setShort] = useState("");
  const [shape, setShape] = useState<LogoShape>("shield");
  const [symbol, setSymbol] = useState<LogoSymbol>("crystal");
  const [colors, setColors] = useState<[string, string, string]>(PALETTES[0]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  return (
    <form method="post"
      className="grid gap-8 lg:grid-cols-[1fr_280px]"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setErr(null);
        try {
          await api("/api/club", { body: { name, shortName: short, logo: { shape, symbol }, colors } });
          router.push("/onboarding");
          router.refresh();
        } catch (e) {
          setErr(e instanceof ClientApiError ? (e.issues?.map((i) => i.message).join(" / ") || e.message) : "作成に失敗しました");
          setBusy(false);
        }
      }}
    >
      <div className="flex flex-col gap-6">
        <div className="grid gap-4 sm:grid-cols-[1fr_140px]">
          <Field label="Club name" htmlFor="name" hint="3〜24文字">
            <input id="name" value={name} onChange={(e) => setName(e.target.value)} required minLength={3} maxLength={24} className={inputClass} placeholder="Crystal Rovers" />
          </Field>
          <Field label="Short name" htmlFor="short" hint="英大文字・数字2〜4">
            <input id="short" value={short} onChange={(e) => setShort(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4))} required minLength={2} className={cn(inputClass, "font-display tracking-widest")} placeholder="CRV" />
          </Field>
        </div>
        <fieldset>
          <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-2">Emblem shape</legend>
          <div className="flex flex-wrap gap-2">
            {LOGO_SHAPES.map((s) => (
              <button type="button" key={s} aria-pressed={shape === s} onClick={() => setShape(s)} className={cn("rounded-xl border p-2 transition-colors", shape === s ? "border-gold bg-gold/10" : "border-line hover:border-crystal/50")}>
                <ClubEmblem shape={s} symbol={symbol} colors={colors} className="h-12 w-12" label={s} />
              </button>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-2">Symbol</legend>
          <div className="flex flex-wrap gap-2">
            {LOGO_SYMBOLS.map((s) => (
              <button type="button" key={s} aria-pressed={symbol === s} onClick={() => setSymbol(s)} className={cn("rounded-xl border p-2 transition-colors", symbol === s ? "border-gold bg-gold/10" : "border-line hover:border-crystal/50")}>
                <ClubEmblem shape={shape} symbol={s} colors={colors} className="h-10 w-10" label={s} />
              </button>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-2">Club colours</legend>
          <div className="flex flex-wrap gap-2">
            {PALETTES.map((p, i) => (
              <button type="button" key={i} aria-pressed={colors === p} aria-label={`配色 ${i + 1}`} onClick={() => setColors(p)} className={cn("flex overflow-hidden rounded-lg border-2", colors === p ? "border-gold" : "border-transparent")}>
                {p.map((c) => <span key={c} className="h-9 w-7" style={{ background: c }} />)}
              </button>
            ))}
          </div>
        </fieldset>
        {err && <Notice tone="danger">{err}</Notice>}
        <Button type="submit" variant="gold" size="lg" disabled={busy || name.trim().length < 3 || short.length < 2}>
          {busy ? "クラブを作成中…" : "クラブを作成して選手を受け取る"}
        </Button>
        <p className="text-xs text-ink-3">作成すると、運営が用意したクリスタリア族の対象選手からCommon選手8人（GK・DF・MF・FW 各2人）がランダムに配属されます。選手を自分で選んだり生成したりはできません。</p>
      </div>
      <div className="flex flex-col items-center gap-3 rounded-2xl border border-line bg-bg-2 p-6">
        <ClubEmblem shape={shape} symbol={symbol} colors={colors} className="h-40 w-40 drop-shadow-[0_0_30px_rgba(68,217,255,0.3)]" label="プレビュー" />
        <div className="text-center">
          <div className="font-display text-xl font-bold uppercase">{name || "Your club"}</div>
          <div className="font-display text-sm tracking-[0.3em] text-ink-2">{short || "ABC"}</div>
        </div>
      </div>
    </form>
  );
}
