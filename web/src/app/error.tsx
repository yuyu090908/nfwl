"use client";
import { Button } from "@/components/ui/button";

export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="mx-auto flex min-h-[60svh] max-w-lg flex-col items-center justify-center gap-4 px-4 text-center" role="alert">
      <div className="font-display text-3xl font-bold">問題が発生しました</div>
      <p className="text-ink-2">一時的なエラーの可能性があります。もう一度お試しください。</p>
      <Button variant="gold" onClick={reset}>再読み込み</Button>
    </div>
  );
}
