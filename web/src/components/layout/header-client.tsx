"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { DropdownMenu, Dialog } from "radix-ui";
import { Home, Menu, Play, Shield, Store, Trophy, User, X } from "lucide-react";
import { MOBILE_TABS, MORE_NAV, PRIMARY_NAV } from "./nav-items";
import { cn } from "@/lib/utils";
import { api } from "@/lib/client-api";

const isActive = (path: string, href: string) => (href === "/" ? path === "/" : path === href || path.startsWith(href + "/"));

export function NavLinks() {
  const path = usePathname();
  return (
    <nav aria-label="メイン" className="ml-6 hidden items-center gap-1 lg:flex">
      {PRIMARY_NAV.map((n) => (
        <Link
          key={n.href}
          href={n.href}
          aria-current={isActive(path, n.href) ? "page" : undefined}
          className={cn(
            "relative rounded-lg px-3 py-2 font-display text-sm font-semibold uppercase tracking-wider text-ink-2 transition-colors hover:text-ink",
            isActive(path, n.href) && "text-ink after:absolute after:inset-x-3 after:-bottom-[13px] after:h-0.5 after:bg-gold",
          )}
        >
          {n.label}
        </Link>
      ))}
    </nav>
  );
}

export function UserMenu({ name, hasClub }: { name: string; hasClub: boolean }) {
  const router = useRouter();
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger className="flex h-10 items-center gap-2 rounded-xl border border-line bg-card px-3 text-sm hover:border-crystal/50" aria-label="アカウントメニュー">
        <User className="h-4 w-4 text-crystal" />
        <span className="hidden max-w-[120px] truncate sm:inline">{name}</span>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={8} className="z-50 min-w-48 rounded-xl border border-line bg-card p-1 shadow-glow">
          {!hasClub && (
            <DropdownMenu.Item asChild>
              <Link href="/onboarding" className="block rounded-lg px-3 py-2 text-sm text-gold outline-none data-[highlighted]:bg-card-2">クラブを作成</Link>
            </DropdownMenu.Item>
          )}
          {[["/profile", "プロフィール"], ["/wallet", "ウォレット"], ["/rewards", "報酬"], ["/settings", "設定"]].map(([h, l]) => (
            <DropdownMenu.Item key={h} asChild>
              <Link href={h} className="block rounded-lg px-3 py-2 text-sm outline-none data-[highlighted]:bg-card-2">{l}</Link>
            </DropdownMenu.Item>
          ))}
          <DropdownMenu.Separator className="my-1 h-px bg-line" />
          <DropdownMenu.Item
            onSelect={async () => {
              await api("/api/auth/logout", { method: "POST" }).catch(() => {});
              router.push("/");
              router.refresh();
            }}
            className="cursor-pointer rounded-lg px-3 py-2 text-sm text-danger outline-none data-[highlighted]:bg-card-2"
          >
            ログアウト
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}

export function MobileMenu({ signedIn }: { signedIn: boolean }) {
  const [open, setOpen] = useState(false);
  const path = usePathname();
  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <Dialog.Trigger className="flex h-10 w-10 items-center justify-center rounded-xl border border-line bg-card lg:hidden" aria-label="メニューを開く">
        <Menu className="h-5 w-5" />
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/70" />
        <Dialog.Content className="fixed inset-y-0 right-0 z-50 flex w-[min(320px,88vw)] flex-col overflow-y-auto border-l border-line bg-bg-2 p-5">
          <div className="mb-4 flex items-center justify-between">
            <Dialog.Title className="font-display text-lg font-bold tracking-widest">MENU</Dialog.Title>
            <Dialog.Description className="sr-only">サイト内のページ一覧</Dialog.Description>
            <Dialog.Close aria-label="閉じる" className="rounded-lg p-2 hover:bg-card"><X className="h-5 w-5" /></Dialog.Close>
          </div>
          <nav aria-label="モバイルメニュー" className="flex flex-col gap-1">
            {[...PRIMARY_NAV, ...MORE_NAV].map((n) => (
              <Link key={n.href} href={n.href} aria-current={isActive(path, n.href) ? "page" : undefined} onClick={() => setOpen(false)}
                className={cn("rounded-lg px-3 py-2.5 text-sm hover:bg-card", isActive(path, n.href) && "bg-card text-gold")}>
                {n.label}
              </Link>
            ))}
            {!signedIn && (
              <Link href="/register" className="mt-3 rounded-lg bg-gold px-3 py-2.5 text-center font-display font-semibold uppercase text-bg">Create club</Link>
            )}
          </nav>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

const ICONS = { home: Home, shield: Shield, play: Play, trophy: Trophy, store: Store };

export function MobileTabBar() {
  const path = usePathname();
  if (path.startsWith("/play")) return null;
  return (
    <nav aria-label="タブ" className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-bg/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl lg:hidden">
      <ul className="mx-auto grid max-w-md grid-cols-5">
        {MOBILE_TABS.map((t) => {
          const Icon = ICONS[t.icon];
          const active = isActive(path, t.href);
          return (
            <li key={t.href}>
              <Link href={t.href} aria-current={active ? "page" : undefined}
                className={cn("flex flex-col items-center gap-0.5 py-2 text-[10px] font-semibold uppercase tracking-wider", active ? "text-gold" : "text-ink-3")}>
                <Icon className={cn("h-5 w-5", t.icon === "play" && "text-crystal")} />
                {t.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
