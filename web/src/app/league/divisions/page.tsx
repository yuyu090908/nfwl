import type { Metadata } from "next";
import Link from "next/link";
import { db } from "@/lib/server/db";
import { PageHeader } from "@/components/ui/misc";
import { DivisionLadder } from "@/components/home/division-ladder";
import { getCurrentUser } from "@/lib/server/session";

export const metadata: Metadata = { title: "Divisions" };

export default async function DivisionsPage() {
  const [divs, counts, user] = await Promise.all([
    db.division.findMany({ orderBy: { id: "asc" } }),
    db.club.groupBy({ by: ["divisionId"], where: { isDemo: false }, _count: true }),
    getCurrentUser(),
  ]);
  const my = user ? await db.club.findUnique({ where: { ownerId: user.id } }) : null;
  const count = (id: number) => counts.find((c) => c.divisionId === id)?._count ?? 0;
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="World League" title="Divisions">RPに応じて自動で昇格・降格します（Master Document 4章）。</PageHeader>
      <DivisionLadder current={my?.divisionId} />
      <div className="mt-10 grid gap-2 sm:grid-cols-2">
        {divs.map((d) => (
          <Link key={d.id} href={d.id === 0 ? "/league/champions" : `/league?division=${d.id}`} className="flex items-center justify-between rounded-xl border border-line bg-card/70 px-4 py-3 hover:border-crystal/40">
            <span><span className="font-display font-bold">{d.id === 0 ? "Division 0" : `Division ${d.id}`}</span> <span className="text-ink-2">· {d.name}</span></span>
            <span className="text-sm text-ink-2">{d.id === 0 ? "招待制" : d.minRp !== null ? `${d.minRp}${d.maxRp !== null ? `〜${d.maxRp}` : "+"} RP` : ""} · {count(d.id)} clubs</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
