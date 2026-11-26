import Link from "next/link";
import { getCurrentUser } from "@/lib/server/session";
import { Logo } from "./logo";
import { NavLinks, MobileMenu, UserMenu } from "./header-client";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export async function SiteHeader() {
  const user = await getCurrentUser();
  return (
    <header className="sticky top-0 z-40 border-b border-line/70 bg-bg/80 backdrop-blur-xl">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-card focus:px-3 focus:py-2">
        本文へスキップ
      </a>
      <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6">
        <Logo />
        <NavLinks />
        <div className="ml-auto flex items-center gap-2">
          {user ? (
            <>
              <Link href="/play" className={cn(buttonVariants({ variant: "gold", size: "sm" }), "hidden sm:inline-flex")}>Play now</Link>
              <UserMenu name={user.displayName} hasClub={!!user.clubId} />
            </>
          ) : (
            <>
              <Link href="/login" className={buttonVariants({ variant: "ghost", size: "sm" })}>ログイン</Link>
              <Link href="/register" className={cn(buttonVariants({ variant: "gold", size: "sm" }), "hidden sm:inline-flex")}>Create club</Link>
            </>
          )}
          <MobileMenu signedIn={!!user} />
        </div>
      </div>
    </header>
  );
}
