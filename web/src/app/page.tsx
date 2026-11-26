import Link from "next/link";
import { Activity, ArrowUpRight, Coins, Crown, Gamepad2, Layers, Target, TrendingUp } from "lucide-react";
import { Hero } from "@/components/home/hero";
import { Reveal } from "@/components/home/reveal";
import { DivisionLadder } from "@/components/home/division-ladder";
import { PlayerCard } from "@/components/game/player-card";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { DemoBadge, Badge } from "@/components/ui/badge";
import { EmptyState, StatTile } from "@/components/ui/misc";
import { buttonVariants } from "@/components/ui/button";
import { getCurrentUser } from "@/lib/server/session";
import { featuredPlayers, homeOverview } from "@/lib/server/queries";
import { DEMO_FEATURED } from "@/lib/domain/demo";
import type { Rarity } from "@/lib/domain/rules";
import { daysUntil } from "@/lib/utils";

const FEATURES = [
  { icon: Gamepad2, title: "Real Football Gameplay", text: "Unity製の5対5リアルタイムサッカー。慣性のある動き、パス・シュートの強弱、GKのセーブ。" },
  { icon: Layers, title: "NFT Player Collection", text: "すべてNFWLが制作したオリジナルのクリスタリア族。5つのレアリティと固有のカード。" },
  { icon: Crown, title: "Club Management", text: "エンブレム、クラブカラー、2-1-1の編成。ポジション適性で能力が変わります。" },
  { icon: Activity, title: "World League", text: "Division 10から1、そしてChampions Leagueへ。30日ごとのシーズン。" },
  { icon: TrendingUp, title: "Player Evolution", text: "試合でXPを獲得してレベルアップ。成長ポイントで能力を伸ばし、上限を解放。" },
  { icon: Coins, title: "Marketplace", text: "NFT選手の売買とオークション。本番売買はチェーン確定まで準備中です。" },
  { icon: Target, title: "Match Prediction", text: "Free Pick：無料の試合予想。的中で称号とバッジ。換金性はありません。" },
];

export default async function HomePage() {
  const user = await getCurrentUser();
  const [ov, featured] = await Promise.all([homeOverview(), featuredPlayers(4)]);
  const s = ov.season;
  const remainingDays = s ? daysUntil(s.endsAt) : null;
  const useDemo = featured.length === 0;

  return (
    <>
      <Hero signedIn={!!user} hasClub={!!user?.clubId} />

      <section aria-labelledby="live" className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
        <Reveal>
          <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
            <div>
              <div className="text-xs font-semibold uppercase tracking-[0.3em] text-crystal">Live</div>
              <h2 id="live" className="font-display text-3xl font-bold uppercase">World League</h2>
            </div>
            {s && <Badge tone={s.official ? "success" : "warning"}>{s.official ? "Official season" : "Beta season（暫定集計）"}</Badge>}
          </div>
        </Reveal>
        {!s ? (
          <EmptyState title="シーズン開始前です">開始すると、ここに現在のシーズン情報が表示されます。</EmptyState>
        ) : (
          <div className="grid gap-4 lg:grid-cols-[1fr_1.2fr]">
            <div className="grid grid-cols-2 gap-3">
              <StatTile label="Season" value={`#${s.number}`} accent />
              <StatTile label="Remaining" value={`${remainingDays} days`} />
              <StatTile label="Total clubs" value={ov.clubs > 0 ? ov.clubs.toLocaleString() : "—"} hint={ov.clubs === 0 ? "まだ登録クラブはありません" : undefined} />
              <StatTile label="Matches played" value={ov.matches > 0 ? ov.matches.toLocaleString() : "—"} hint={ov.matches === 0 ? "今シーズンの試合はまだありません" : undefined} />
            </div>
            <Card>
              <CardHeader>
                <CardTitle>World ranking</CardTitle>
                <Link href="/rankings" className="text-xs text-crystal hover:underline">すべて見る</Link>
              </CardHeader>
              <CardBody>
                {ov.top.length === 0 ? (
                  <p className="py-6 text-center text-sm text-ink-2">ランキングはクラブの試合結果が入ると表示されます。</p>
                ) : (
                  <ol className="divide-y divide-line">
                    {ov.top.map((c, i) => (
                      <li key={c.id} className="flex items-center gap-3 py-2.5">
                        <span className="w-6 font-display text-lg font-bold text-gold">{i + 1}</span>
                        <span className="min-w-0 flex-1 truncate font-medium">{c.name}</span>
                        <span className="text-xs text-ink-2">Div {c.divisionId}</span>
                        <span className="w-16 text-right font-display font-semibold">{c.rp} RP</span>
                      </li>
                    ))}
                  </ol>
                )}
                <p className="mt-3 text-xs text-ink-3">
                  Champions League 出場候補（Division 1 上位32）: {ov.div1 > 0 ? `${Math.min(32, ov.div1)} クラブ` : "まだ Division 1 のクラブはありません"}
                </p>
              </CardBody>
            </Card>
          </div>
        )}
      </section>

      <section aria-labelledby="featured" className="border-y border-line/60 bg-bg-2/40">
        <div className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
          <Reveal>
            <div className="mb-8 flex flex-wrap items-end justify-between gap-3">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.3em] text-crystal">Crystalia</div>
                <h2 id="featured" className="font-display text-3xl font-bold uppercase">Featured players</h2>
              </div>
              {useDemo && (
                <div className="flex items-center gap-2 text-xs text-ink-2">
                  <DemoBadge /> 実在の選手データがまだないため、見本を表示しています
                </div>
              )}
            </div>
          </Reveal>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {(useDemo ? DEMO_FEATURED : featured.map((p) => ({ id: p.id, name: p.name, position: p.position, rarity: p.rarity as Rarity, ovr: p.ovr, level: p.level, crystalType: p.crystalType, skills: p.skills.map((s) => s.skill.name), club: p.club, nft: !!p.nft }))).map((p, i) => (
              <Reveal key={p.id} delay={i * 0.06}>
                <PlayerCard p={p} href={useDemo ? undefined : `/players/${p.id}`} footer={useDemo ? <DemoBadge className="mt-1" /> : undefined} />
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      <section aria-labelledby="features" className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
        <Reveal>
          <h2 id="features" className="mb-8 font-display text-3xl font-bold uppercase">Game features</h2>
        </Reveal>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map((f, i) => (
            <Reveal key={f.title} delay={i * 0.04}>
              <Card className="group h-full p-5 transition-colors hover:border-crystal/40">
                <f.icon className="h-7 w-7 text-crystal transition-transform group-hover:scale-110" aria-hidden />
                <h3 className="mt-4 font-display text-lg font-semibold uppercase">{f.title}</h3>
                <p className="mt-2 text-sm text-ink-2">{f.text}</p>
              </Card>
            </Reveal>
          ))}
          <Reveal delay={0.3}>
            <Link href={user ? "/play" : "/register"} className="group flex h-full flex-col justify-between rounded-[var(--radius-card)] border border-gold/40 bg-gradient-to-br from-gold/15 to-transparent p-5 shadow-gold">
              <span className="font-display text-lg font-bold uppercase text-gold">Start now</span>
              <span className="flex items-center gap-1 text-sm text-ink-2 group-hover:text-ink">クラブを作って最初の試合へ <ArrowUpRight className="h-4 w-4" /></span>
            </Link>
          </Reveal>
        </div>
      </section>

      <section aria-labelledby="competition" className="mx-auto max-w-7xl px-4 pb-8 sm:px-6">
        <Reveal>
          <div className="mb-8 text-center">
            <div className="text-xs font-semibold uppercase tracking-[0.3em] text-crystal">Global competition</div>
            <h2 id="competition" className="font-display text-3xl font-bold uppercase">From Division 10 to the top</h2>
            <p className="mx-auto mt-2 max-w-xl text-sm text-ink-2">RPで自動昇格・降格。Division 1 の上位32クラブが Champions League（Division 0）への出場資格を得ます。出場資格はNFTの所有とは関係ありません。</p>
          </div>
        </Reveal>
        <DivisionLadder />
        <div className="mt-10 text-center">
          <Link href="/league" className={buttonVariants({ variant: "outline" })}>World League を見る</Link>
        </div>
      </section>
    </>
  );
}
