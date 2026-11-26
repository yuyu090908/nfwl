"use client";
import Link from "next/link";
import { Fragment, useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { AlertTriangle, Maximize, Pause, Play, RefreshCw, Volume2, VolumeX, Wifi, WifiOff } from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Notice, Progress } from "@/components/ui/misc";
import { api, ClientApiError } from "@/lib/client-api";
import { checkDevice, type DeviceCheck } from "@/lib/bridge/device";
import { WebBridge, type ConnectionState, type UnityLike } from "@/lib/bridge/web-bridge";
import { runSimulator } from "@/lib/bridge/simulator";
import type { BridgeMessage, UnityMatchConfig } from "@/lib/bridge/protocol";
import { cn } from "@/lib/utils";

interface UnityInstance extends UnityLike {
  SetFullscreen(v: 0 | 1): void;
  Quit(): Promise<void>;
}
declare global {
  interface Window {
    createUnityInstance?: (canvas: HTMLCanvasElement, config: Record<string, unknown>, onProgress: (p: number) => void) => Promise<UnityInstance>;
  }
}

export interface BuildInfo {
  baseUrl: string;
  name: string;
  ext: string; // "", ".br" or ".gz"
  available: boolean;
}

type Phase = "setup" | "creating" | "loading" | "playing" | "finished" | "error";

interface Result {
  verdict: "CLIENT_REPORTED" | "FLAGGED" | "REJECTED";
  anomalies: { code: string; detail: string }[];
  xp: { playerId: string; amount: number; levelTo: number }[];
  xpNote?: string | null;
}

const QUALITY = ["Low", "Medium", "High", "Ultra"] as const;

export function PlayClient({ build, halfOptions, simulatorEnabled, squadOk, squadErrors }: { build: BuildInfo; halfOptions: number[]; simulatorEnabled: boolean; squadOk: boolean; squadErrors: string[] }) {
  const device = useSyncExternalStore(noopSubscribe, clientDevice, serverDevice);
  const [force, setForce] = useState(false);
  const [phase, setPhase] = useState<Phase>("setup");
  const [half, setHalf] = useState(halfOptions.includes(300) ? 300 : halfOptions[0]);
  const [difficulty, setDifficulty] = useState<"beginner" | "standard" | "hard">("beginner");
  const [useSim, setUseSim] = useState(!build.available && simulatorEnabled);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [config, setConfig] = useState<UnityMatchConfig | null>(null);
  const [score, setScore] = useState({ home: 0, away: 0, clock: 0, half: 1 });
  const [log, setLog] = useState<string[]>([]);
  const [conn, setConn] = useState<{ state: ConnectionState; pending: number }>({ state: "online", pending: 0 });
  const [paused, setPaused] = useState(false);
  const [muted, setMuted] = useState(false);
  const [quality, setQuality] = useState(1);
  const [result, setResult] = useState<Result | null>(null);
  const [rejects, setRejects] = useState<string[]>([]);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const wrapRef = useRef<HTMLDivElement>(null);
  const bridgeRef = useRef<WebBridge | null>(null);
  const instRef = useRef<(UnityLike & { stop?: () => void; Quit?: () => Promise<void>; SetFullscreen?: (v: 0 | 1) => void }) | null>(null);


  const onMessage = useCallback((m: BridgeMessage) => {
    const p = m.payload as Record<string, number | string | null>;
    if ("homeScore" in p) setScore((s) => ({ ...s, home: Number(p.homeScore), away: Number(p.awayScore) }));
    if ("gameClock" in p) setScore((s) => ({ ...s, clock: Number(p.gameClock) }));
    if (m.eventType === "HalfTime") setScore((s) => ({ ...s, half: 2 }));
    if (m.eventType === "MatchStarted") setPhase("playing");
    if (m.eventType === "GamePaused") setPaused(true);
    if (m.eventType === "GameResumed") setPaused(false);
    if (m.eventType === "GameError") setError(`ゲーム内エラー: ${String(p.message)}`);
    if (m.eventType === "GoalScored") setLog((l) => [`${Math.floor(Number(p.gameClock) / 60)}' GOAL ${p.team === "home" ? "HOME" : "AWAY"} (${p.homeScore}-${p.awayScore})`, ...l].slice(0, 8));
  }, []);

  const teardown = useCallback(async () => {
    const inst = instRef.current;
    instRef.current = null;
    inst?.stop?.();
    try {
      await inst?.Quit?.();
    } catch {}
    bridgeRef.current?.dispose();
    bridgeRef.current = null;
  }, []);

  useEffect(() => () => void teardown(), [teardown]);

  // Pause when the tab is hidden; resume control stays with the player.
  useEffect(() => {
    const h = () => {
      if (document.hidden && phase === "playing") {
        bridgeRef.current?.command({ command: "pause" });
        setPaused(true);
      }
    };
    document.addEventListener("visibilitychange", h);
    return () => document.removeEventListener("visibilitychange", h);
  }, [phase]);

  // Warn before leaving during a match.
  useEffect(() => {
    if (phase !== "playing" && phase !== "loading") return;
    const h = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", h);
    return () => window.removeEventListener("beforeunload", h);
  }, [phase]);

  async function start() {
    setError(null);
    setResult(null);
    setRejects([]);
    setLog([]);
    setScore({ home: 0, away: 0, clock: 0, half: 1 });
    setPhase("creating");
    let cfg: UnityMatchConfig;
    try {
      cfg = await api<UnityMatchConfig>("/api/match-sessions", { body: { halfSeconds: half, difficulty } });
    } catch (e) {
      setError(e instanceof ClientApiError ? e.message : "試合を作成できませんでした");
      setPhase("error");
      return;
    }
    setConfig(cfg);
    const bridge = new WebBridge(cfg, {
      onMessage,
      onServerResult: (t, r) => {
        if (t === "MatchFinished") {
          setResult(r as Result);
          setPhase("finished");
        }
      },
      onConnection: (state, pending) => setConn({ state, pending }),
      onReject: (reason) => setRejects((x) => [reason, ...x].slice(0, 5)),
    });
    bridge.install();
    bridgeRef.current = bridge;
    setPhase("loading");
    setProgress(0);

    if (useSim) {
      setProgress(1);
      const sim = runSimulator(cfg, (json) => bridge.receive(json));
      instRef.current = sim;
      bridge.attachUnity(sim);
      return;
    }
    try {
      await loadScript(`${build.baseUrl}/${build.name}.loader.js`);
      if (!window.createUnityInstance || !canvasRef.current) throw new Error("Unity loader not found");
      const inst = await window.createUnityInstance(
        canvasRef.current,
        {
          dataUrl: `${build.baseUrl}/${build.name}.data${build.ext}`,
          frameworkUrl: `${build.baseUrl}/${build.name}.framework.js${build.ext}`,
          codeUrl: `${build.baseUrl}/${build.name}.wasm${build.ext}`,
          streamingAssetsUrl: "StreamingAssets",
          companyName: "NFWL",
          productName: "NFWL",
          productVersion: "0.7",
          matchWebGLToCanvasSize: true,
        },
        (p) => setProgress(p),
      );
      instRef.current = inst;
      bridge.attachUnity(inst);
      bridge.command({ command: "setQuality", level: quality as 0 | 1 | 2 | 3 });
    } catch (e) {
      setError(`ゲームを読み込めませんでした（${e instanceof Error ? e.message : String(e)}）。通信環境を確認して再試行してください。`);
      setPhase("error");
    }
  }

  async function abort() {
    if (config) await api(`/api/match-sessions/${config.sessionId}/abort`, { method: "POST" }).catch(() => {});
    await teardown();
    setPhase("setup");
  }

  const fullscreen = () => {
    const inst = instRef.current;
    if (inst?.SetFullscreen) inst.SetFullscreen(1);
    else wrapRef.current?.requestFullscreen?.().catch(() => {});
  };

  const blocked = device?.verdict === "unsupported" && !force;
  const mm = (s: number) => `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(Math.floor(s % 60)).padStart(2, "0")}`;

  return (
    <div className="flex flex-col gap-6">
      {device && device.verdict !== "ok" && phase === "setup" && (
        <Notice tone={device.verdict === "unsupported" ? "danger" : "warning"} title={device.verdict === "unsupported" ? "この端末ではゲームを起動できません" : "この端末では動作が重くなる可能性があります"}>
          <ul className="list-disc pl-5">{device.reasons.map((r) => <li key={r}>{r}</li>)}</ul>
          {device.verdict === "unsupported" && (
            <p className="mt-2">
              クラブ管理・編成・マーケットはこの端末でも使えます。試合はキーボードのあるPCのブラウザ（Chrome / Edge / Firefox / Safari の最新版）で起動してください。
              {device.webgl2 && <button className="ml-2 text-crystal underline" onClick={() => setForce(true)}>それでも起動する</button>}
            </p>
          )}
        </Notice>
      )}

      {phase === "setup" || phase === "error" ? (
        <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
          <div className="rounded-2xl border border-line bg-card/80 p-6">
            <h2 className="font-display text-xl font-bold uppercase">AI Match <span className="text-sm text-ink-2">Practice vs CPU</span></h2>
            <p className="mt-1 text-sm text-ink-2">あなたの保存済みスタメン5人（ポジション補正込み）がUnityに渡されます。相手チームはサーバーが生成します。</p>
            <div className="mt-5 grid gap-5 sm:grid-cols-2">
              <fieldset>
                <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-2">Difficulty</legend>
                <div className="flex flex-wrap gap-2">
                  {(["beginner", "standard", "hard"] as const).map((d) => (
                    <button key={d} type="button" aria-pressed={difficulty === d} onClick={() => setDifficulty(d)} className={cn("rounded-lg border px-3 py-2 text-sm capitalize", difficulty === d ? "border-gold bg-gold/10 text-gold" : "border-line text-ink-2")}>{d}</button>
                  ))}
                </div>
              </fieldset>
              <fieldset>
                <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-2">Match length</legend>
                <div className="flex flex-wrap gap-2">
                  {halfOptions.map((h) => (
                    <button key={h} type="button" aria-pressed={half === h} onClick={() => setHalf(h)} className={cn("rounded-lg border px-3 py-2 text-sm", half === h ? "border-gold bg-gold/10 text-gold" : "border-line text-ink-2")}>
                      {h < 60 ? `${h * 2}秒 (test)` : `${(h * 2) / 60}分`}
                    </button>
                  ))}
                </div>
              </fieldset>
            </div>
            {simulatorEnabled && (
              <label className="mt-5 flex items-start gap-2 rounded-lg border border-warning/40 bg-warning/5 p-3 text-sm">
                <input type="checkbox" checked={useSim} onChange={(e) => setUseSim(e.target.checked)} className="mt-0.5 h-4 w-4" data-testid="use-simulator" />
                <span><b className="text-warning">DEV SIMULATOR</b> を使う（開発環境のみ）。Unityビルドの代わりに同じ通信メッセージを送り、連携を確認します。実際の試合ではありません。</span>
              </label>
            )}
            {!build.available && !useSim && (
              <div className="mt-5"><Notice tone="warning" title="Unity WebGL ビルドが配置されていません">{`${build.baseUrl}/${build.name}.loader.js が見つかりません。Unity で「NFWL > Build WebGL」を実行し、出力を配置してください（README 参照）。`}</Notice></div>
            )}
            {!squadOk && <div className="mt-5"><Notice tone="danger" title="編成を確認してください">{squadErrors.join(" / ")} <Link href="/squad" className="ml-1 text-crystal underline">編成へ</Link></Notice></div>}
            {error && <div className="mt-5"><Notice tone="danger">{error}</Notice></div>}
            <div className="mt-6 flex flex-wrap gap-3">
              <Button variant="gold" size="lg" onClick={start} disabled={blocked || !squadOk || (!build.available && !useSim)} data-testid="kickoff">
                <Play className="h-5 w-5" /> {phase === "error" ? "再試行" : "Kick off"}
              </Button>
              <Link href="/squad" className={buttonVariants({ variant: "outline", size: "lg" })}>Squad</Link>
            </div>
          </div>
          <Controls />
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-card/80 px-4 py-2">
            <div className="flex items-center gap-4">
              <div className="font-display text-sm uppercase tracking-wider text-ink-2">{config?.homeShort ?? "HOME"}</div>
              <div className="font-display text-3xl font-bold tabular-nums" aria-live="polite" data-testid="scoreboard">{score.home} - {score.away}</div>
              <div className="font-display text-sm uppercase tracking-wider text-ink-2">{config?.awayShort ?? "CPU"}</div>
              <Badge>{score.half === 1 ? "1st" : "2nd"} · {mm(score.clock)}</Badge>
              {useSim && <Badge tone="warning">DEV SIMULATOR</Badge>}
            </div>
            <div className="flex items-center gap-1.5">
              <span className={cn("flex items-center gap-1 text-xs", conn.state === "online" ? "text-success" : conn.state === "retrying" ? "text-warning" : "text-danger")} role="status">
                {conn.state === "online" ? <Wifi className="h-4 w-4" /> : <WifiOff className="h-4 w-4" />}
                {conn.state === "online" ? "Connected" : conn.state === "retrying" ? `再送中 (${conn.pending})` : `オフライン (${conn.pending}件 保留)`}
              </span>
              <Button size="icon" variant="ghost" aria-label={paused ? "再開" : "一時停止"} disabled={phase !== "playing"} onClick={() => { bridgeRef.current?.command({ command: paused ? "resume" : "pause" }); setPaused(!paused); }}>
                {paused ? <Play className="h-4 w-4" /> : <Pause className="h-4 w-4" />}
              </Button>
              <Button size="icon" variant="ghost" aria-label={muted ? "音を出す" : "ミュート"} onClick={() => { bridgeRef.current?.command({ command: "setMuted", muted: !muted }); setMuted(!muted); }}>
                {muted ? <VolumeX className="h-4 w-4" /> : <Volume2 className="h-4 w-4" />}
              </Button>
              <label className="sr-only" htmlFor="quality">画質</label>
              <select id="quality" value={quality} onChange={(e) => { const q = Number(e.target.value); setQuality(q); bridgeRef.current?.command({ command: "setQuality", level: q as 0 | 1 | 2 | 3 }); }} className="h-9 rounded-lg border border-line bg-bg-2 px-2 text-xs">
                {QUALITY.map((q, i) => <option key={q} value={i}>{q}</option>)}
              </select>
              <Button size="icon" variant="ghost" aria-label="フルスクリーン" onClick={fullscreen}><Maximize className="h-4 w-4" /></Button>
            </div>
          </div>
          <div ref={wrapRef} className="relative aspect-video w-full overflow-hidden rounded-2xl border border-line bg-black">
            <canvas ref={canvasRef} id="unity-canvas" tabIndex={-1} className={cn("h-full w-full", useSim && "hidden")} aria-label="NFWL 試合画面" />
            {useSim && (
              <div className="pitch-grass flex h-full flex-col items-center justify-center gap-2 text-center">
                <Badge tone="warning">DEV SIMULATOR — Unity ビルドなし</Badge>
                <div className="font-display text-6xl font-bold">{score.home} - {score.away}</div>
                <div className="text-sm text-ink-2">{mm(score.clock)} / {mm((config?.halfLengthSeconds ?? 0) * 2)}</div>
              </div>
            )}
            {phase === "loading" && !useSim && (
              <div className="absolute inset-0 flex flex-col items-center justify-center gap-4 bg-bg/90">
                <div className="font-display text-lg uppercase tracking-widest">Loading stadium…</div>
                <Progress value={Math.round(progress * 100)} className="w-64" label="読み込み" />
                <div className="text-xs text-ink-2">{Math.round(progress * 100)}%</div>
              </div>
            )}
            {paused && phase === "playing" && (
              <div className="absolute inset-0 flex items-center justify-center bg-bg/60">
                <Button variant="gold" size="lg" onClick={() => { bridgeRef.current?.command({ command: "resume" }); setPaused(false); }}><Play className="h-5 w-5" /> Resume</Button>
              </div>
            )}
          </div>
          {rejects.length > 0 && <Notice tone="warning" title="サーバーが受け付けなかったメッセージ">{rejects.join(" / ")}</Notice>}
          {log.length > 0 && <ul className="text-xs text-ink-2">{log.map((l, i) => <li key={i}>{l}</li>)}</ul>}
          {phase === "finished" && result && (
            <div className="rounded-2xl border border-gold/40 bg-card p-6 shadow-gold" data-testid="result">
              <div className="flex flex-wrap items-center gap-3">
                <h2 className="font-display text-2xl font-bold uppercase">Full time {score.home} - {score.away}</h2>
                <Badge tone={result.verdict === "CLIENT_REPORTED" ? "success" : result.verdict === "FLAGGED" ? "warning" : "danger"}>
                  {result.verdict === "CLIENT_REPORTED" ? "記録済み（クライアント報告・検証通過）" : result.verdict === "FLAGGED" ? "要確認として記録" : "結果を受理できませんでした"}
                </Badge>
              </div>
              {result.anomalies.length > 0 && <ul className="mt-2 text-xs text-warning">{result.anomalies.map((a) => <li key={a.code}><AlertTriangle className="mr-1 inline h-3 w-3" />{a.code}: {a.detail}</li>)}</ul>}
              {result.xp.length > 0 && <p className="mt-2 text-sm text-ink-2">獲得XP: {result.xp.map((x) => `+${x.amount}`).join(" / ")}</p>}
              {result.xpNote && <p className="mt-2 text-sm text-warning">{result.xpNote}</p>}
              <div className="mt-4 flex flex-wrap gap-2">
                <Link href={`/matches/${config?.matchId}`} className={buttonVariants({ variant: "gold" })}>Match report</Link>
                <Button onClick={async () => { await teardown(); setPhase("setup"); }}><RefreshCw className="h-4 w-4" /> もう一度</Button>
              </div>
            </div>
          )}
          {phase !== "finished" && (
            <div className="flex justify-end">
              <Button variant="ghost" size="sm" onClick={abort}>試合を中止（結果は記録されません）</Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

let deviceCache: DeviceCheck | null = null;
const noopSubscribe = () => () => {};
const clientDevice = () => (deviceCache ??= checkDevice());
const serverDevice = () => null;

function loadScript(src: string) {
  return new Promise<void>((resolve, reject) => {
    if (document.querySelector(`script[data-src="${src}"]`)) return resolve();
    const s = document.createElement("script");
    s.src = src;
    s.async = true;
    s.dataset.src = src;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error(`${src} を読み込めません`));
    document.body.appendChild(s);
  });
}

function Controls() {
  const rows = [
    ["移動・ドリブル", "W A S D / 矢印"], ["ダッシュ", "Shift"], ["ショートパス", "J（長押しで強く）"], ["スルーパス", "Alt + J"],
    ["ロングパス", "L"], ["シュート", "K（長押しでチャージ）"], ["タックル", "相手の近くで J"], ["選手切替", "Q"],
    ["カメラ", "Z"], ["キックオフに戻す", "R"], ["音", "M"], ["画質", "F3"],
  ];
  return (
    <aside className="rounded-2xl border border-line bg-card/60 p-5" aria-labelledby="controls-h">
      <h2 id="controls-h" className="mb-3 font-display text-sm font-semibold uppercase tracking-wider text-ink-2">Keyboard controls</h2>
      <dl className="grid grid-cols-[1fr_auto] gap-x-3 gap-y-1.5 text-sm">
        {rows.map(([k, v]) => (<Fragment key={k}><dt className="text-ink-2">{k}</dt><dd className="text-right font-mono text-xs">{v}</dd></Fragment>))}
      </dl>
      <p className="mt-3 text-xs text-ink-3">Unity v0.7 の操作です（unity/README.md）。</p>
    </aside>
  );
}
