"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Notice, inputClass } from "@/components/ui/misc";
import { api, ClientApiError } from "@/lib/client-api";

export function SettingsForm({ displayName, settings }: { displayName: string; settings: { graphics?: string; muted?: boolean; reducedMotion?: boolean; showWalletPublicly?: boolean } }) {
  const [msg, setMsg] = useState<{ tone: "info" | "danger"; text: string } | null>(null);
  return (
    <form method="post"
      className="flex max-w-lg flex-col gap-5"
      onSubmit={async (e) => {
        e.preventDefault();
        const f = new FormData(e.currentTarget);
        try {
          await api("/api/settings", {
            method: "PUT",
            body: {
              displayName: f.get("displayName"),
              settings: { graphics: f.get("graphics"), muted: f.get("muted") === "on", reducedMotion: f.get("reducedMotion") === "on", showWalletPublicly: f.get("showWalletPublicly") === "on" },
            },
          });
          setMsg({ tone: "info", text: "保存しました。" });
        } catch (err) {
          setMsg({ tone: "danger", text: err instanceof ClientApiError ? err.message : "保存できませんでした" });
        }
      }}
    >
      <Field label="Manager name" htmlFor="displayName"><input id="displayName" name="displayName" defaultValue={displayName} minLength={2} maxLength={32} className={inputClass} /></Field>
      <Field label="Default graphics" htmlFor="graphics">
        <select id="graphics" name="graphics" defaultValue={settings.graphics ?? "auto"} className={inputClass}>
          {["auto", "low", "medium", "high", "ultra"].map((g) => <option key={g} value={g}>{g}</option>)}
        </select>
      </Field>
      {[["muted", "ゲーム開始時に音をミュート", settings.muted], ["reducedMotion", "アニメーションを減らす", settings.reducedMotion], ["showWalletPublicly", "ウォレットアドレスを公開プロフィールに表示", settings.showWalletPublicly]].map(([k, l, v]) => (
        <label key={String(k)} className="flex items-center gap-2 text-sm"><input type="checkbox" name={String(k)} defaultChecked={!!v} className="h-4 w-4 accent-[#d5ac59]" />{String(l)}</label>
      ))}
      {msg && <Notice tone={msg.tone}>{msg.text}</Notice>}
      <Button type="submit" variant="gold" className="self-start">保存</Button>
    </form>
  );
}
