"use client";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { AlertTriangle, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/misc";
import { Dialog } from "@/components/ui/dialog";
import { AttributeBars } from "@/components/game/attribute-bars";
import { CrystaliaPortrait } from "@/components/game/crystalia-portrait";
import { RarityBadge } from "@/components/game/rarity";
import { applyPositionPenalty, overall, type Attributes, type PenaltyRules, type Position, type Rarity } from "@/lib/domain/rules";
import { api, ClientApiError } from "@/lib/client-api";
import { cn } from "@/lib/utils";

export interface EditorPlayer {
  id: string;
  name: string;
  position: Position;
  subPositions: Position[];
  rarity: Rarity;
  crystalType: string;
  level: number;
  attrs: Attributes;
  ovr: number;
  locked: boolean;
}

const ROLES: Position[] = ["GK", "DF", "DF", "MF", "FW"];
// Top-down pitch, own goal at the bottom (same slot order as Unity's 2-1-1).
const SPOTS = [
  { x: 50, y: 88 },
  { x: 26, y: 66 },
  { x: 74, y: 66 },
  { x: 50, y: 44 },
  { x: 50, y: 20 },
];

/**
 * Lineup editor. Three ways to move players so it works everywhere:
 *  - drag a card onto a slot (mouse / pen, HTML5 drag and drop)
 *  - tap a bench card, then tap a slot (touch)
 *  - keyboard: Tab to a card, Enter to pick it, Tab to a slot, Enter to place it
 * Off-position players show the ability penalty before saving (Full Spec 6.4).
 */
export function SquadEditor({ roster, initial, penalties }: { roster: EditorPlayer[]; initial: string[]; penalties: PenaltyRules }) {
  const router = useRouter();
  const [starters, setStarters] = useState<(string | null)[]>(() => [0, 1, 2, 3, 4].map((i) => initial[i] ?? null));
  const [picked, setPicked] = useState<string | null>(null);
  const [detail, setDetail] = useState<{ p: EditorPlayer; slot: number | null } | null>(null);
  const [state, setState] = useState<{ tone: "info" | "danger"; text: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const byId = useMemo(() => new Map(roster.map((p) => [p.id, p])), [roster]);
  const dirty = starters.some((s, i) => s !== (initial[i] ?? null));

  const slotInfo = starters.map((id, i) => {
    const p = id ? byId.get(id) : undefined;
    if (!p) return null;
    const pen = applyPositionPenalty(p.attrs, p.position, p.subPositions, ROLES[i], penalties);
    return { p, ...pen, ovrInSlot: overall(pen.attributes, ROLES[i]) };
  });
  const teamOvr = slotInfo.every(Boolean) ? Math.round(slotInfo.reduce((a, s) => a + s!.ovrInSlot, 0) / 5) : null;
  const hasGk = slotInfo.some((s) => s?.p.position === "GK");

  function place(playerId: string, slot: number) {
    const p = byId.get(playerId);
    if (!p || p.locked) return;
    setStarters((cur) => {
      const next = [...cur];
      const from = next.indexOf(playerId);
      if (from >= 0) next[from] = next[slot]; // swap when moving between slots
      next[slot] = playerId;
      return next;
    });
    setPicked(null);
    setState(null);
  }

  async function save() {
    if (starters.some((s) => !s)) return setState({ tone: "danger", text: "5つの枠すべてに選手を配置してください。" });
    setSaving(true);
    try {
      await api("/api/squad", { method: "PUT", body: { starters, bench: roster.filter((p) => !starters.includes(p.id)).slice(0, 3).map((p) => p.id) } });
      setState({ tone: "info", text: "編成を保存しました。次の試合からこの5人で出場します。" });
      router.refresh();
    } catch (e) {
      setState({ tone: "danger", text: e instanceof ClientApiError ? e.message : "保存に失敗しました" });
    } finally {
      setSaving(false);
    }
  }

  const bench = roster.filter((p) => !starters.includes(p.id));

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
      <div>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div className="text-sm text-ink-2">
            Formation <span className="font-display font-bold text-ink">2-1-1</span> · Team OVR{" "}
            <span className="font-display text-lg font-bold text-gold">{teamOvr ?? "—"}</span>
          </div>
          <Button variant="gold" size="sm" onClick={save} disabled={saving || !dirty}>
            <Save className="h-4 w-4" /> {saving ? "保存中…" : dirty ? "編成を保存" : "保存済み"}
          </Button>
        </div>
        <div className="pitch-grass relative mx-auto aspect-[3/4] w-full max-w-[520px] overflow-hidden rounded-2xl border border-white/10 shadow-[inset_0_0_80px_rgba(0,0,0,0.5)]" aria-label="ピッチ配置図（下が自陣ゴール）">
          <div className="absolute inset-[4%] rounded border-2 border-white/25" />
          <div className="absolute inset-x-[4%] top-1/2 h-0.5 bg-white/25" />
          <div className="absolute left-1/2 top-1/2 h-[22%] w-[29%] -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white/25" />
          <div className="absolute bottom-[4%] left-1/2 h-[14%] w-[44%] -translate-x-1/2 rounded-t-full border-2 border-b-0 border-white/25" />
          <div className="absolute left-1/2 top-[4%] h-[14%] w-[44%] -translate-x-1/2 rounded-b-full border-2 border-t-0 border-white/25" />
          {SPOTS.map((s, i) => {
            const info = slotInfo[i];
            const warn = info && info.fit !== "main";
            return (
              <div key={i} className="absolute -translate-x-1/2 -translate-y-1/2" style={{ left: `${s.x}%`, top: `${s.y}%` }}>
                <button
                  type="button"
                  data-testid={`slot-${i}`}
                  aria-label={`${ROLES[i]} 枠${info ? `: ${info.p.name}` : "（空き）"}${picked ? "。ここに配置" : ""}`}
                  onDragOver={(e) => e.preventDefault()}
                  onDrop={(e) => {
                    e.preventDefault();
                    const id = e.dataTransfer.getData("text/player");
                    if (id) place(id, i);
                  }}
                  onClick={() => (picked ? place(picked, i) : info && setDetail({ p: info.p, slot: i }))}
                  draggable={!!info}
                  onDragStart={(e) => info && e.dataTransfer.setData("text/player", info.p.id)}
                  className={cn(
                    "flex w-[88px] flex-col items-center rounded-xl border bg-bg/75 p-1.5 text-center backdrop-blur transition-all sm:w-[104px]",
                    picked ? "border-gold animate-pulse" : warn ? "border-warning/70" : "border-white/20 hover:border-crystal",
                  )}
                >
                  <span className="text-[10px] font-bold tracking-widest text-crystal">{ROLES[i]}</span>
                  {info ? (
                    <>
                      <CrystaliaPortrait crystal={info.p.crystalType} seed={info.p.id} className="h-10 w-10" />
                      <span className="w-full truncate text-[11px] font-semibold">{info.p.name}</span>
                      <span className={cn("font-display text-sm font-bold", warn ? "text-warning" : "text-ink")}>
                        {info.ovrInSlot}
                        {info.ovrInSlot !== info.p.ovr && <span className="ml-1 text-[10px] text-danger">({info.ovrInSlot - info.p.ovr})</span>}
                      </span>
                      {warn && (
                        <span className="flex items-center gap-0.5 text-[9px] text-warning">
                          <AlertTriangle className="h-3 w-3" /> {info.fit === "sub" ? "サブ適性" : info.fit === "fpInGoal" ? "FPがGK" : "適性外"}
                        </span>
                      )}
                    </>
                  ) : (
                    <span className="py-4 text-[11px] text-ink-3">空き</span>
                  )}
                </button>
              </div>
            );
          })}
        </div>
        {!hasGk && <div className="mt-3"><Notice tone="warning">GKの選手が入っていません。GKを最低1人入れてください。</Notice></div>}
        {state && <div className="mt-3"><Notice tone={state.tone}>{state.text}</Notice></div>}
      </div>

      <div>
        <h2 className="mb-2 font-display text-sm font-semibold uppercase tracking-wider text-ink-2">
          Bench / Reserves <span className="text-ink-3">({bench.length})</span>
        </h2>
        <p className="mb-3 text-xs text-ink-3">カードをピッチへドラッグ、またはタップ（Enter）で選んでから枠をタップ（Enter）。</p>
        <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-2">
          {bench.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                data-testid={`bench-${p.id}`}
                draggable={!p.locked}
                disabled={p.locked}
                onDragStart={(e) => e.dataTransfer.setData("text/player", p.id)}
                onClick={() => setPicked(picked === p.id ? null : p.id)}
                aria-pressed={picked === p.id}
                className={cn(
                  "flex w-full items-center gap-2 rounded-xl border bg-card p-2 text-left transition-colors",
                  picked === p.id ? "border-gold bg-gold/10" : "border-line hover:border-crystal/50",
                  p.locked && "opacity-50",
                )}
              >
                <CrystaliaPortrait crystal={p.crystalType} seed={p.id} className="h-10 w-10 shrink-0" />
                <span className="min-w-0">
                  <span className="block truncate text-xs font-semibold">{p.name}</span>
                  <span className="text-[11px] text-ink-2">{p.position} · OVR <b className="text-ink">{p.ovr}</b></span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </div>

      <Dialog open={!!detail} onOpenChange={(o) => !o && setDetail(null)} title={detail?.p.name ?? ""} description={detail ? `${detail.p.position}（サブ: ${detail.p.subPositions.join(", ") || "なし"}） · Lv ${detail.p.level}` : undefined}>
        {detail && (() => {
          const role = detail.slot !== null ? ROLES[detail.slot] : detail.p.position;
          const pen = applyPositionPenalty(detail.p.attrs, detail.p.position, detail.p.subPositions, role, penalties);
          return (
            <div className="flex flex-col gap-4">
              <div className="flex items-center gap-3">
                <CrystaliaPortrait crystal={detail.p.crystalType} seed={detail.p.id} className="h-16 w-16" />
                <div>
                  <RarityBadge rarity={detail.p.rarity} />
                  <div className="mt-1 text-sm">この枠（{role}）での OVR <b className="font-display text-lg text-gold">{overall(pen.attributes, role)}</b></div>
                </div>
              </div>
              {pen.fit !== "main" && (
                <Notice tone="warning">
                  {pen.fit === "fpInGoal"
                    ? `フィールドプレーヤーをGKに置くと Goalkeeping は ${penalties.fpGoalkeeping} として扱われます（試合エンジンの下限は40）。`
                    : `${pen.fit === "sub" ? "サブポジション" : "適性外ポジション"}のため関連能力が −${pen.fit === "sub" ? penalties.sub : penalties.off} されます。`}
                </Notice>
              )}
              <AttributeBars attrs={detail.p.attrs} compare={pen.attributes} />
              <a href={`/players/${detail.p.id}`} className="text-sm text-crystal hover:underline">選手詳細ページへ</a>
            </div>
          );
        })()}
      </Dialog>
    </div>
  );
}
