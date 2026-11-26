import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="mx-auto flex min-h-[60svh] max-w-lg flex-col items-center justify-center gap-4 px-4 text-center">
      <div className="font-display text-7xl font-bold text-gradient-gold">404</div>
      <p className="text-ink-2">ページが見つかりません。</p>
      <Link href="/" className={buttonVariants({ variant: "outline" })}>ホームへ</Link>
    </div>
  );
}
