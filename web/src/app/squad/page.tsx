import type { Metadata } from "next";
import { requireClub } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { getRoster } from "@/lib/server/club";
import { getGameConfig } from "@/lib/server/game-config";
import { PageHeader } from "@/components/ui/misc";
import { SquadEditor, type EditorPlayer } from "@/components/club/squad-editor";
import type { Position, Rarity } from "@/lib/domain/rules";

export const metadata: Metadata = { title: "チーム編成" };

export default async function SquadPage() {
  const { club } = await requireClub("/squad");
  const [roster, squad, cfg] = await Promise.all([getRoster(club.id), db.squad.findFirst({ where: { clubId: club.id, isActive: true } }), getGameConfig()]);
  const players: EditorPlayer[] = roster.map((p) => ({
    id: p.id, name: p.name, position: p.position as Position, subPositions: p.subPositions as Position[], rarity: p.rarity as Rarity,
    crystalType: p.crystalType, level: p.level, attrs: p.attrs, ovr: p.ovr, locked: p.locked,
  }));
  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow={club.name} title="Squad">
        2-1-1（GK 1・DF 2・MF 1・FW 1）。ポジション適性が低い選手は能力が下がり、その値のまま試合に出場します。
      </PageHeader>
      <SquadEditor roster={players} initial={squad?.starters ?? []} penalties={cfg.penalties} />
    </div>
  );
}
