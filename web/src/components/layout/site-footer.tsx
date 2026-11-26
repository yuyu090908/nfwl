import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="mt-20 border-t border-line/70 pb-24 lg:pb-0">
      <div className="mx-auto grid max-w-7xl gap-6 px-4 py-10 text-sm text-ink-3 sm:grid-cols-3 sm:px-6">
        <div>
          <div className="font-display font-bold tracking-[0.2em] text-ink">NFT FOOTBALL WORLD LEAGUE</div>
          <p className="mt-2 text-xs">BUILD YOUR CLUB. RULE THE WORLD.</p>
        </div>
        <div className="flex flex-col gap-1">
          <Link href="/help" className="hover:text-ink">ヘルプ・よくある質問</Link>
          <Link href="/help#points" className="hover:text-ink">NFWL Points と Free Pick について</Link>
          <Link href="/help#fair-play" className="hover:text-ink">フェアプレーと試合結果の検証</Link>
        </div>
        <p className="text-xs leading-relaxed">
          NFWL Points と予想スコアはゲーム内専用です。購入・譲渡・換金はできません。
          NFT の売買とトークンを使う予想は、法令確認と使用チェーンの確定まで提供していません。
        </p>
      </div>
    </footer>
  );
}
