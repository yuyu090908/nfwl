import type { Metadata } from "next";
import Link from "next/link";
import { Check } from "lucide-react";
import { requireUser } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { getRoster } from "@/lib/server/club";
import { ClubCreateForm } from "@/components/onboarding/club-create-form";
import { PlayerCard } from "@/components/game/player-card";
import { PageHeader } from "@/components/ui/misc";
import { buttonVariants } from "@/components/ui/button";
import { Reveal } from "@/components/home/reveal";
import type { Rarity } from "@/lib/domain/rules";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "クラブ作成" };

export default async function OnboardingPage() {
  const user = await requireUser("/onboarding");
  const club = await db.club.findUnique({ where: { ownerId: user.id } });

  if (!club) {
    return (
      <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
        <PageHeader eyebrow="Step 2–3 / 7" title="Create your club">クラブ名とエンブレムを決めましょう。</PageHeader>
        <ClubCreateForm />
      </div>
    );
  }

  const roster = await getRoster(club.id);
  const played = await db.match.count({ where: { homeClubId: club.id, status: "FINISHED" } });
  const steps = [
    { label: "Account creation", done: true },
    { label: "Club name", done: true },
    { label: "Club logo", done: true },
    { label: "Starter player allocation", done: roster.length > 0 },
    { label: "Team formation", done: club.onboardingStep >= 3, href: "/squad" },
    { label: "Tutorial", done: club.onboardingStep >= 3, href: "/help#controls" },
    { label: "First match", done: played > 0, href: "/play" },
  ];
  const next = steps.find((s) => !s.done);

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Welcome, manager" title={`${club.name} is ready`}>
        運営の選手プールからCommon選手 {roster.length} 人がランダムに配属されました。これらはクラブ選手（非NFT）で、売買はできませんが育成・出場・記録はNFT選手と同じです。
      </PageHeader>
      <ol className="mb-10 grid gap-2 sm:grid-cols-7" aria-label="初回登録の進行状況">
        {steps.map((s, i) => (
          <li key={s.label} className={cn("flex items-center gap-2 rounded-lg border px-3 py-2 text-xs", s.done ? "border-success/40 text-success" : s === next ? "border-gold text-gold" : "border-line text-ink-3")}>
            {s.done ? <Check className="h-4 w-4 shrink-0" /> : <span className="font-display">{i + 1}</span>}
            <span className="leading-tight">{s.label}</span>
          </li>
        ))}
      </ol>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        {roster.map((p, i) => (
          <Reveal key={p.id} delay={i * 0.08}>
            <PlayerCard p={{ id: p.id, name: p.name, position: p.position, rarity: p.rarity as Rarity, ovr: p.ovr, level: p.level, crystalType: p.crystalType }} href={`/players/${p.id}`} />
          </Reveal>
        ))}
      </div>
      {next?.href && (
        <div className="mt-10 flex flex-wrap justify-center gap-3">
          <Link href={next.href} className={cn(buttonVariants({ variant: "gold", size: "lg" }))}>
            Next: {next.label}
          </Link>
          <Link href="/club" className={buttonVariants({ variant: "ghost", size: "lg" })}>あとで</Link>
        </div>
      )}
    </div>
  );
}
