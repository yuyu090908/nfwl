"use client";
import Link from "next/link";
import { motion, useReducedMotion } from "motion/react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { StadiumScene } from "./stadium-scene";

export function Hero({ signedIn, hasClub }: { signedIn: boolean; hasClub: boolean }) {
  const reduce = useReducedMotion();
  const fade = (d: number) => (reduce ? {} : { initial: { opacity: 0, y: 18 }, animate: { opacity: 1, y: 0 }, transition: { duration: 0.7, delay: d, ease: [0.2, 0.8, 0.2, 1] as const } });
  return (
    <section className="relative isolate overflow-hidden border-b border-line/60">
      <StadiumScene />
      <div className="relative mx-auto flex min-h-[78svh] max-w-7xl flex-col items-center justify-center px-4 py-24 text-center sm:px-6">
        <motion.div {...fade(0)} className="mb-5 inline-flex items-center gap-2 rounded-full border border-crystal/30 bg-bg/60 px-4 py-1.5 text-[11px] font-semibold uppercase tracking-[0.3em] text-crystal backdrop-blur">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-crystal" /> 5v5 Crystalia Football
        </motion.div>
        <motion.h1 {...fade(0.1)} className="font-display text-[clamp(2.6rem,9vw,6.5rem)] font-bold uppercase leading-[0.92] tracking-tight">
          <span className="block">Build your club.</span>
          <span className="block text-gradient-gold">Rule the world.</span>
        </motion.h1>
        <motion.p {...fade(0.25)} className="mt-6 max-w-xl text-base text-ink-2 sm:text-lg">
          Create your team, develop your players, and rise through the global football leagues.
        </motion.p>
        <motion.div {...fade(0.4)} className="mt-10 flex w-full flex-col items-center justify-center gap-3 sm:w-auto sm:flex-row">
          <Link href={signedIn ? (hasClub ? "/play" : "/onboarding") : "/register"} className={cn(buttonVariants({ variant: "gold", size: "lg" }), "w-full sm:w-auto")}>
            Play now
          </Link>
          <Link href={signedIn ? (hasClub ? "/club" : "/onboarding") : "/register"} className={cn(buttonVariants({ variant: "outline", size: "lg" }), "w-full border-crystal/40 sm:w-auto")}>
            {hasClub ? "My club" : "Create your club"}
          </Link>
        </motion.div>
      </div>
    </section>
  );
}
