"use client";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Minus, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Notice, Progress, StatTile } from "@/components/ui/misc";
import { Dialog } from "@/components/ui/dialog";
import { AttributeBars } from "@/components/game/attribute-bars";
import { ATTRIBUTE_KEYS, ATTRIBUTE_LABELS, overall, type AttributeKey, type Attributes, type Position } from "@/lib/domain/rules";
import { api, ClientApiError, newKey } from "@/lib/client-api";

export interface UpgradePlayer {
  id: string;
  name: string;
  position: Position;
  level: number;
  levelCap: number;
  xp: number;
  xpToNext: number;
  growthPoints: number;
  attrs: Attributes;
  cap: number;
  breakthrough: { from: number; to: number; cost: number; requiresLevel: number } | null;
}

export function UpgradePanel({ p, balance }: { p: UpgradePlayer; balance: number }) {
  const router = useRouter();
  const [alloc, setAlloc] = useState<Partial<Record<AttributeKey, number>>>({});
  const [msg, setMsg] = useState<{ tone: "info" | "danger"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(false);
  // One idempotency key per intended operation: a double click or a retry after a timeout reuses it.
  const [btKey, setBtKey] = useState(newKey);
  const used = Object.values(alloc).reduce((a, b) => a + (b ?? 0), 0);
  const after = useMemo(() => {
    const a = { ...p.attrs };
    for (const k of ATTRIBUTE_KEYS) a[k] += alloc[k] ?? 0;
    return a;
  }, [alloc, p.attrs]);
  const bt = p.breakthrough;
  const canBreak = !!bt && p.level >= bt.requiresLevel && balance >= bt.cost;

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatTile label="Current level" value={p.level} accent />
        <StatTile label="Level cap" value={p.levelCap} />
        <StatTile label="Next level" value={p.level >= p.levelCap ? "上限" : p.level + 1} />
        <StatTile label="Required XP" value={p.level >= p.levelCap ? "—" : `${p.xpToNext - p.xp}`} hint={`現在 ${p.xp} / ${p.xpToNext}`} />
      </div>
      <Progress value={p.xp} max={p.xpToNext} label="経験値" />

      <section className="rounded-2xl border border-line bg-card/70 p-5" aria-labelledby="bt-h">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="bt-h" className="font-display text-lg font-bold uppercase">Level cap breakthrough</h2>
          <Badge tone="warning" title="オフチェーンのNFWL Pointsを使うテストモードです">TEST MODE · off-chain</Badge>
        </div>
        {bt ? (
          <>
            <p className="mt-2 text-sm text-ink-2">
              レベル上限 {bt.from} → <b className="text-ink">{bt.to}</b>。必要: <b className="text-gold">{bt.cost} NFWL Points</b>（所持 {balance}）、Lv{bt.requiresLevel} 到達。
            </p>
            {p.level < bt.requiresLevel && <p className="mt-1 text-xs text-warning">あと {bt.requiresLevel - p.level} レベルで解放できます。</p>}
            {balance < bt.cost && <p className="mt-1 text-xs text-warning">NFWL Points が足りません。</p>}
            <Button className="mt-3" variant="gold" disabled={!canBreak || busy} onClick={() => setConfirm(true)}>上限を解放する</Button>
          </>
        ) : (
          <p className="mt-2 text-sm text-ink-2">最大レベルに達しています。</p>
        )}
      </section>

      <section className="rounded-2xl border border-line bg-card/70 p-5" aria-labelledby="gp-h">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 id="gp-h" className="font-display text-lg font-bold uppercase">Growth points</h2>
          <span className="text-sm text-ink-2">残り <b className="font-display text-lg text-gold">{p.growthPoints - used}</b> / {p.growthPoints}（レベルアップごとに +3）</span>
        </div>
        <div className="mt-4 grid gap-2 sm:grid-cols-2">
          {ATTRIBUTE_KEYS.map((k) => {
            const v = alloc[k] ?? 0;
            const atCap = p.attrs[k] + v >= p.cap;
            return (
              <div key={k} className="flex items-center justify-between gap-2 rounded-lg bg-bg-2/70 px-3 py-1.5">
                <span className="text-sm">{ATTRIBUTE_LABELS[k]} <span className="text-ink-3">{p.attrs[k]}</span>{v > 0 && <span className="text-success"> → {p.attrs[k] + v}</span>}</span>
                <span className="flex items-center gap-1">
                  <Button size="icon" variant="ghost" className="h-8 w-8" aria-label={`${ATTRIBUTE_LABELS[k]} を減らす`} disabled={v === 0} onClick={() => setAlloc({ ...alloc, [k]: v - 1 })}><Minus className="h-3 w-3" /></Button>
                  <span className="w-5 text-center font-display">{v}</span>
                  <Button size="icon" variant="ghost" className="h-8 w-8" aria-label={`${ATTRIBUTE_LABELS[k]} を増やす`} disabled={used >= p.growthPoints || atCap} onClick={() => setAlloc({ ...alloc, [k]: v + 1 })}><Plus className="h-3 w-3" /></Button>
                </span>
              </div>
            );
          })}
        </div>
        <div className="mt-5">
          <div className="mb-2 flex items-center justify-between">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-ink-2">Upgrade preview</h3>
            <span className="text-sm">OVR {overall(p.attrs, p.position)} → <b className="font-display text-gold">{overall(after, p.position)}</b></span>
          </div>
          <AttributeBars attrs={p.attrs} compare={after} cap={p.cap} />
        </div>
        <Button
          className="mt-4"
          variant="crystal"
          disabled={used === 0 || busy}
          onClick={async () => {
            setBusy(true);
            setMsg(null);
            try {
              await api("/api/upgrade/growth", { body: { playerId: p.id, alloc, idempotencyKey: newKey() } });
              setAlloc({});
              setMsg({ tone: "info", text: "能力値を更新しました。" });
              router.refresh();
            } catch (e) {
              setMsg({ tone: "danger", text: e instanceof ClientApiError ? e.message : "更新できませんでした" });
            } finally {
              setBusy(false);
            }
          }}
        >
          配分を確定
        </Button>
      </section>
      {msg && <Notice tone={msg.tone}>{msg.text}</Notice>}

      <Dialog open={confirm} onOpenChange={setConfirm} title="レベル上限を解放しますか？" description="テストモード：オフチェーンのNFWL Pointsを消費します。取り消しはできません。">
        {bt && (
          <div className="flex flex-col gap-4">
            <dl className="grid grid-cols-2 gap-2 text-sm">
              <dt className="text-ink-2">選手</dt><dd>{p.name}</dd>
              <dt className="text-ink-2">上限</dt><dd>{bt.from} → {bt.to}</dd>
              <dt className="text-ink-2">消費</dt><dd className="text-gold">{bt.cost} NFWL Points</dd>
              <dt className="text-ink-2">残高（後）</dt><dd>{balance - bt.cost}</dd>
            </dl>
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setConfirm(false)}>キャンセル</Button>
              <Button
                variant="gold"
                disabled={busy}
                onClick={async () => {
                  setBusy(true);
                  try {
                    await api("/api/upgrade/breakthrough", { body: { playerId: p.id, idempotencyKey: btKey } });
                    setMsg({ tone: "info", text: `レベル上限を ${bt.to} に解放しました。` });
                    setBtKey(newKey());
                    setConfirm(false);
                    router.refresh();
                  } catch (e) {
                    setMsg({ tone: "danger", text: e instanceof ClientApiError ? e.message : "解放できませんでした。残高は変わっていません。" });
                    setConfirm(false);
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                {busy ? "処理中…" : "解放する"}
              </Button>
            </div>
          </div>
        )}
      </Dialog>
    </div>
  );
}
