import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { db } from "@/lib/server/db";
import { attrsOf, playerInclude } from "@/lib/server/club";
import { getCurrentUser } from "@/lib/server/session";
import { getGameConfig } from "@/lib/server/game-config";
import { AttributeBars } from "@/components/game/attribute-bars";
import { PlayerCard } from "@/components/game/player-card";
import { CrystaliaPortrait } from "@/components/game/crystalia-portrait";
import { RarityBadge } from "@/components/game/rarity";
import { CRYSTAL } from "@/components/game/crystalia";
import { Card, CardBody, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress, StatTile } from "@/components/ui/misc";
import { buttonVariants } from "@/components/ui/button";
import { overall, xpToNext, unitySkillSlots, type Position, type Rarity } from "@/lib/domain/rules";
import { fmtDate } from "@/lib/utils";

export const metadata: Metadata = { title: "選手詳細" };

const UUID = /^[0-9a-f-]{36}$/i;

export default async function PlayerDetail({ params }: PageProps<"/players/[id]">) {
  const { id } = await params;
  if (!UUID.test(id)) notFound();
  const p = await db.player.findUnique({
    where: { id },
    include: {
      ...playerInclude,
      ownership: { where: { active: true }, include: { club: true } },
      upgrades: { orderBy: { createdAt: "desc" }, take: 10 },
      experience: { orderBy: { createdAt: "desc" }, take: 10 },
    },
  });
  if (!p || !p.attributes) notFound();
  const [user, cfg, stats] = await Promise.all([
    getCurrentUser(),
    getGameConfig(),
    db.matchStatistic.aggregate({ where: { playerId: p.id, match: { status: "FINISHED" } }, _count: true, _sum: { goals: true, assists: true, minutes: true, saves: true } }),
  ]);
  const a = attrsOf(p.attributes);
  const ovr = overall(a, p.position as Position);
  const own = p.ownership[0];
  const isOwner = !!user && own?.club.ownerId === user.id;
  const rarity = p.rarity as Rarity;
  const crystal = CRYSTAL[p.crystalType];

  return (
    <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6">
      <div className="grid gap-8 lg:grid-cols-[340px_1fr]">
        <div className="flex flex-col gap-4">
          {/* 2D preview now; the box is sized for a future Crystalia 3D model viewer. */}
          <div className="group relative aspect-square overflow-hidden rounded-2xl border border-line bg-[radial-gradient(circle_at_50%_30%,#1d2638,#080b12)] [perspective:900px]" aria-label="選手プレビュー">
            <div className="absolute inset-0 transition-transform duration-700 [transform-style:preserve-3d] group-hover:[transform:rotateY(14deg)_rotateX(4deg)]">
              <CrystaliaPortrait crystal={p.crystalType} seed={p.id} className="h-full w-full" />
            </div>
            <span className="absolute bottom-2 right-2 rounded bg-bg/70 px-2 py-0.5 text-[10px] text-ink-3">2D preview · 3D model 対応予定</span>
          </div>
          <PlayerCard p={{ id: p.id, name: p.name, position: p.position, rarity, ovr, level: p.level, crystalType: p.crystalType, skills: p.skills.map((s) => s.skill.name), club: own?.club.name ?? null, nft: !!p.nft }} />
        </div>

        <div className="flex min-w-0 flex-col gap-6">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <RarityBadge rarity={rarity} />
              <Badge tone="crystal">{p.position}</Badge>
              {p.subPositions.length > 0 && <Badge>Sub: {p.subPositions.join(", ")}</Badge>}
              <Badge>{crystal?.name ?? p.crystalType} crystal</Badge>
              {p.nft ? <Badge tone="gold">NFT</Badge> : <Badge>Club player（非NFT）</Badge>}
            </div>
            <h1 className="mt-3 font-display text-4xl font-bold uppercase">{p.name}</h1>
            <div className="mt-1 text-sm text-ink-2">{own ? <>所属 <Link className="text-crystal hover:underline" href={isOwner ? "/club" : "#"}>{own.club.name}</Link></> : "所属なし"} · {p.preferredFoot === "Left" ? "左利き" : "右利き"} · 逆足 {p.weakFoot}/5</div>
          </div>

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatTile label="OVR" value={ovr} accent />
            <StatTile label="Level" value={`${p.level} / ${p.levelCap}`} />
            <StatTile label="Growth pts" value={p.growthPoints} />
            <StatTile label="Skill slots" value={`${p.skills.length} / ${unitySkillSlots(rarity)}`} hint="試合エンジン準拠" />
          </div>
          <div>
            <div className="mb-1 flex justify-between text-xs text-ink-2"><span>Experience</span><span>{p.xp} / {xpToNext(p.level)} XP</span></div>
            <Progress value={p.xp} max={xpToNext(p.level)} label="経験値" />
          </div>

          <Card>
            <CardHeader><CardTitle>Attributes</CardTitle><span className="text-xs text-ink-3">上限 {cfg.rarity[rarity].attributeCap}（{rarity}）</span></CardHeader>
            <CardBody><AttributeBars attrs={a} cap={cfg.rarity[rarity].attributeCap} /></CardBody>
          </Card>

          <div className="grid gap-6 md:grid-cols-2">
            <Card>
              <CardHeader><CardTitle>Special skills</CardTitle></CardHeader>
              <CardBody>
                {p.skills.length === 0 ? <p className="text-sm text-ink-2">スキルはまだありません。Epic以上でスキル枠が開きます。</p> : (
                  <ul className="flex flex-col gap-2">{p.skills.map((s) => (
                    <li key={s.skillId} className="rounded-lg border border-line p-3">
                      <div className="font-semibold">{s.skill.name}</div>
                      <div className="text-xs text-ink-2">{s.skill.description}</div>
                      {!s.skill.unitySkillId && <div className="mt-1 text-[11px] text-warning">現在の試合エンジンでは未適用</div>}
                    </li>))}
                  </ul>
                )}
              </CardBody>
            </Card>
            <Card>
              <CardHeader><CardTitle>Match statistics</CardTitle></CardHeader>
              <CardBody className="grid grid-cols-2 gap-3">
                <StatTile label="Apps" value={stats._count} />
                <StatTile label="Goals" value={stats._sum.goals ?? 0} />
                <StatTile label="Minutes" value={stats._sum.minutes ?? 0} />
                <StatTile label="Saves" value={stats._sum.saves ?? 0} hint="GK統計は今後" />
              </CardBody>
            </Card>
            <Card>
              <CardHeader><CardTitle>Upgrade history</CardTitle>{isOwner && <Link href={`/upgrade?player=${p.id}`} className="text-xs text-crystal hover:underline">育成する</Link>}</CardHeader>
              <CardBody>
                {p.upgrades.length + p.experience.length === 0 ? <p className="text-sm text-ink-2">まだ履歴はありません。</p> : (
                  <ul className="flex flex-col gap-1.5 text-sm">
                    {p.upgrades.map((u) => <li key={u.id} className="flex justify-between gap-2"><span>{u.kind === "LEVEL_CAP" ? "レベル上限解放" : "成長ポイント配分"}{u.testMode && <Badge tone="warning" className="ml-2">TEST</Badge>}</span><span className="text-xs text-ink-3">{fmtDate(u.createdAt)}</span></li>)}
                    {p.experience.map((x) => <li key={x.id} className="flex justify-between gap-2"><span>+{x.amount} XP（{x.source}）{x.levelTo > x.levelFrom && ` Lv${x.levelFrom}→${x.levelTo}`}</span><span className="text-xs text-ink-3">{fmtDate(x.createdAt)}</span></li>)}
                  </ul>
                )}
              </CardBody>
            </Card>
            <Card>
              <CardHeader><CardTitle>Ownership</CardTitle></CardHeader>
              <CardBody>
                <dl className="grid grid-cols-[110px_1fr] gap-y-1.5 text-sm">
                  <dt className="text-ink-3">Player ID</dt><dd className="truncate font-mono text-xs">{p.id}</dd>
                  <dt className="text-ink-3">Origin</dt><dd>{p.origin}</dd>
                  <dt className="text-ink-3">Club</dt><dd>{own?.club.name ?? "—"}{own && <span className="text-ink-3">（{fmtDate(own.joinedAt, false)}〜）</span>}</dd>
                  <dt className="text-ink-3">NFT</dt>
                  <dd>{p.nft ? <>Chain {p.nft.chainId} · Token {p.nft.tokenId}<br /><span className="font-mono text-xs">{p.nft.contract}</span></> : "未発行（ゲーム内選手）"}</dd>
                </dl>
              </CardBody>
            </Card>
          </div>
          {isOwner && (
            <div className="flex flex-wrap gap-2">
              <Link href={`/upgrade?player=${p.id}`} className={buttonVariants({ variant: "gold" })}>Upgrade</Link>
              <Link href="/squad" className={buttonVariants({ variant: "outline" })}>Squad</Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
