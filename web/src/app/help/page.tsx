import type { Metadata } from "next";
import { PageHeader } from "@/components/ui/misc";

export const metadata: Metadata = { title: "ヘルプ" };

const SECTIONS: { id: string; q: string; a: React.ReactNode }[] = [
  { id: "start", q: "はじめ方", a: "アカウントを作成し、クラブ名とエンブレムを決めると、Common選手8人（GK・DF・MF・FW 各2人）がランダムに配属されます。編成（Squad）で5人を選び、Play から AI Match を始めます。" },
  { id: "controls", q: "操作方法（チュートリアル）", a: (
    <ul className="list-disc pl-5">
      <li>移動・ドリブル: W A S D / 矢印、ダッシュ: Shift</li>
      <li>ショートパス: J（長押しで強く）、スルーパス: Alt + J、ロングパス: L</li>
      <li>シュート: K（長押しでチャージ。強すぎると浮きます）</li>
      <li>守備: 相手ボールのとき近くで J でタックル、Q で選手切替</li>
      <li>カメラ: Z、キックオフに戻す: R、音: M、画質: F3</li>
    </ul>
  ) },
  { id: "devices", q: "対応端末", a: "試合（Unity WebGL）は、キーボードのあるPCの最新ブラウザ（Chrome / Edge / Firefox / Safari）で動きます。WebGL 2 に対応していない環境やスマートフォンでは起動できませんが、クラブ管理・編成・マーケット・予想は使えます。" },
  { id: "fair-play", q: "試合結果はどう記録されますか", a: "ブラウザで動く試合の結果は改ざんできるため、そのまま公式記録にはしません。サーバーが時間（試合時間より短い実時間で終わっていないか）、得点の順番と最終スコアの一致などを検証し、通過したものを「Client reported」として記録します。公式シーズンでは、サーバーで計算したリーグ戦（Server verified）だけが順位に反映されます。ベータシーズンでは AI Match も「暫定」として順位表に表示します。" },
  { id: "points", q: "NFWL Points と Free Pick", a: "NFWL Points はゲームプレイでのみ得られるゲーム内ポイントで、購入・譲渡・換金はできません。Free Pick（試合予想）は無料で、的中すると予想スコアが増えます。予想スコアにも金銭的価値はありません。トークンを賭ける予想は、法令・年齢・地域の確認が終わるまで提供しません。" },
  { id: "nft", q: "NFTについて", a: "選手はすべてNFWLが制作したオリジナルのクリスタリア族です。初期配布の選手はクラブ選手（非NFT）で、売買はできません。NFTのチェーンとスマートコントラクトが決まるまで、マーケットでの売買は無効です。大会の出場資格は成績で決まり、NFTの所有とは関係ありません。" },
  { id: "growth", q: "育成", a: "試合でXPを獲得します（必要XP = 100 + 25 ×（レベル − 1）、1試合最大400、AI Matchは半分）。レベルアップごとに成長ポイント+3。能力はレアリティごとの上限（Common 72 〜 Legendary 95）を超えられません。" },
];

export default function HelpPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Support" title="Help" />
      <div className="flex flex-col gap-3">
        {SECTIONS.map((s) => (
          <details key={s.id} id={s.id} className="group rounded-2xl border border-line bg-card/70 p-5 open:border-crystal/40" open={s.id === "controls"}>
            <summary className="cursor-pointer list-none font-display text-lg font-semibold">{s.q}</summary>
            <div className="mt-3 text-sm leading-relaxed text-ink-2">{s.a}</div>
          </details>
        ))}
      </div>
    </div>
  );
}
