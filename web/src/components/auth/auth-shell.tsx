import type { ReactNode } from "react";

export function AuthShell({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <div className="mx-auto grid min-h-[80svh] max-w-5xl items-center gap-10 px-4 py-12 sm:px-6 lg:grid-cols-2">
      <div className="hidden lg:block">
        <div className="text-xs font-semibold uppercase tracking-[0.3em] text-crystal">NFWL</div>
        <h1 className="mt-2 font-display text-5xl font-bold uppercase leading-none">
          Build your club.<br /><span className="text-gradient-gold">Rule the world.</span>
        </h1>
        <ol className="mt-8 space-y-3 text-sm text-ink-2">
          {["アカウント作成", "クラブ名・エンブレム", "初期Common選手8人の受け取り", "チーム編成（2-1-1）", "チュートリアル", "最初の試合"].map((s, i) => (
            <li key={s} className="flex items-center gap-3"><span className="flex h-6 w-6 items-center justify-center rounded-full border border-line font-display text-xs">{i + 1}</span>{s}</li>
          ))}
        </ol>
      </div>
      <div className="rounded-2xl border border-line bg-card/80 p-6 shadow-glow sm:p-8">
        <h2 className="font-display text-2xl font-bold uppercase">{title}</h2>
        <p className="mb-6 mt-1 text-sm text-ink-2">{subtitle}</p>
        {children}
      </div>
    </div>
  );
}
