"use client";

export interface DeviceCheck {
  webgl2: boolean;
  mobile: boolean;
  touchOnly: boolean;
  memoryGb: number | null;
  cores: number | null;
  verdict: "ok" | "low" | "unsupported";
  reasons: string[];
}

/**
 * Decides whether this browser should run the Unity WebGL match (prompt 2.3 / 17).
 * - no WebGL 2 -> unsupported
 * - phone / touch-only device -> unsupported (the match needs a keyboard; club, squad and market still work)
 * - < 4 GB memory or < 4 cores -> low (allowed, with a warning and Low graphics)
 */
export function checkDevice(): DeviceCheck {
  const reasons: string[] = [];
  let webgl2 = false;
  try {
    const c = document.createElement("canvas");
    webgl2 = !!c.getContext("webgl2");
  } catch {}
  const ua = navigator.userAgent;
  const mobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua));
  const touchOnly = matchMedia("(pointer: coarse)").matches && !matchMedia("(any-pointer: fine)").matches;
  const memoryGb = (navigator as unknown as { deviceMemory?: number }).deviceMemory ?? null;
  const cores = navigator.hardwareConcurrency ?? null;
  if (!webgl2) reasons.push("このブラウザは WebGL 2 に対応していません");
  if (mobile || touchOnly) reasons.push("スマートフォン・タブレットでは試合を起動できません（キーボード操作が必要です）");
  if (memoryGb !== null && memoryGb < 4) reasons.push(`端末メモリが少なめです（${memoryGb}GB）`);
  if (cores !== null && cores < 4) reasons.push(`CPUコア数が少なめです（${cores}）`);
  const verdict = !webgl2 || mobile || touchOnly ? "unsupported" : reasons.length ? "low" : "ok";
  return { webgl2, mobile, touchOnly, memoryGb, cores, verdict, reasons };
}
