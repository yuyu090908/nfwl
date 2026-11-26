import type { Metadata } from "next";
import { requireClub } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { evaluateLineup, getRoster } from "@/lib/server/club";
import { getGameConfig } from "@/lib/server/game-config";
import { unityBuildInfo } from "@/lib/server/unity-build";
import { practiceHalfOptions } from "@/lib/domain/config";
import { PageHeader } from "@/components/ui/misc";
import { PlayClient } from "@/components/game/play-client";

export const metadata: Metadata = { title: "Play" };

export default async function PlayPage() {
  const { club } = await requireClub("/play");
  const [cfg, roster, squad, build] = await Promise.all([getGameConfig(), getRoster(club.id), db.squad.findFirst({ where: { clubId: club.id, isActive: true } }), unityBuildInfo()]);
  const lineup = evaluateLineup(roster, squad?.starters ?? [], cfg);
  // The simulator is a development tool: never available in a production build.
  const simulatorEnabled = process.env.NODE_ENV !== "production" && process.env.NFWL_ENABLE_BRIDGE_SIMULATOR === "1";
  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow={club.name} title="Play">
        ログイン → クラブ読み込み → スタメン5人の確認 → 試合モード選択 → Unity起動 → 試合終了後、サーバーが結果を検証して成績とXPを更新します。
      </PageHeader>
      <PlayClient build={build} halfOptions={practiceHalfOptions(cfg)} simulatorEnabled={simulatorEnabled} squadOk={lineup.ok} squadErrors={lineup.errors} />
    </div>
  );
}
