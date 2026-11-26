"use client";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Notice, inputClass } from "@/components/ui/misc";
import { api, ClientApiError } from "@/lib/client-api";
import { WalletButton } from "./wallet-button";

function safeNext(n: string | null, fallback: string) {
  return n && n.startsWith("/") && !n.startsWith("//") ? n : fallback;
}

export function LoginForm({ providers }: { providers: string[] }) {
  const router = useRouter();
  const params = useSearchParams();
  const [error, setError] = useState<string | null>(params.get("error") ? "ソーシャルログインに失敗しました。もう一度お試しください。" : null);
  const [busy, setBusy] = useState(false);
  return (
    <div className="flex flex-col gap-5">
      <form method="post"
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          setBusy(true);
          setError(null);
          try {
            const r = await api<{ next: string }>("/api/auth/login", { body: { email: f.get("email"), password: f.get("password") } });
            router.push(safeNext(params.get("next"), r.next));
            router.refresh();
          } catch (err) {
            setError(err instanceof ClientApiError ? err.message : "ログインに失敗しました");
          } finally {
            setBusy(false);
          }
        }}
      >
        <Field label="Email" htmlFor="email">
          <input id="email" name="email" type="email" autoComplete="email" required className={inputClass} />
        </Field>
        <Field label="Password" htmlFor="password">
          <input id="password" name="password" type="password" autoComplete="current-password" required className={inputClass} />
        </Field>
        {error && <Notice tone="danger">{error}</Notice>}
        <Button type="submit" variant="gold" disabled={busy}>{busy ? "ログイン中…" : "ログイン"}</Button>
      </form>
      <SocialButtons providers={providers} />
      <p className="text-center text-sm text-ink-2">
        アカウントがない方は <Link href="/register" className="text-crystal hover:underline">新規登録</Link>
      </p>
    </div>
  );
}

export function RegisterForm({ providers }: { providers: string[] }) {
  const router = useRouter();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  return (
    <div className="flex flex-col gap-5">
      <form method="post"
        noValidate
        className="flex flex-col gap-4"
        onSubmit={async (e) => {
          e.preventDefault();
          const f = new FormData(e.currentTarget);
          const by = f.get("birthYear") ? Number(f.get("birthYear")) : undefined;
          setBusy(true);
          setError(null);
          setErrors({});
          try {
            await api("/api/auth/register", {
              body: { email: f.get("email"), password: f.get("password"), displayName: f.get("displayName"), birthYear: by, acceptTerms: f.get("terms") === "on" },
            });
            router.push("/onboarding");
            router.refresh();
          } catch (err) {
            if (err instanceof ClientApiError && err.issues) {
              const m: Record<string, string> = {};
              for (const i of err.issues) m[String(i.path[0])] = i.path[0] === "acceptTerms" ? "利用規約への同意が必要です" : i.message;
              setErrors(m);
            }
            setError(err instanceof ClientApiError ? err.message : "登録に失敗しました");
          } finally {
            setBusy(false);
          }
        }}
      >
        <Field label="Manager name" htmlFor="displayName" error={errors.displayName} hint="2〜32文字。ランキングに表示されます">
          <input id="displayName" name="displayName" required minLength={2} maxLength={32} className={inputClass} aria-invalid={!!errors.displayName} />
        </Field>
        <Field label="Email" htmlFor="email" error={errors.email}>
          <input id="email" name="email" type="email" autoComplete="email" required className={inputClass} aria-invalid={!!errors.email} />
        </Field>
        <Field label="Password" htmlFor="password" error={errors.password} hint="10文字以上">
          <input id="password" name="password" type="password" autoComplete="new-password" required minLength={10} className={inputClass} aria-invalid={!!errors.password} />
        </Field>
        <Field label="Birth year（任意）" htmlFor="birthYear" hint="年齢制限のある機能の判定に使います">
          <input id="birthYear" name="birthYear" type="number" inputMode="numeric" min={1900} max={new Date().getFullYear()} className={inputClass} />
        </Field>
        <label className="flex items-start gap-2 text-sm text-ink-2">
          <input type="checkbox" name="terms" className="mt-1 h-4 w-4 accent-[#d5ac59]" />
          <span>利用規約とプライバシーポリシーに同意します{errors.acceptTerms && <span className="block text-xs text-danger">{errors.acceptTerms}</span>}</span>
        </label>
        {error && <Notice tone="danger">{error}</Notice>}
        <Button type="submit" variant="gold" disabled={busy}>{busy ? "登録中…" : "アカウントを作成"}</Button>
      </form>
      <SocialButtons providers={providers} />
      <p className="text-center text-sm text-ink-2">
        登録済みの方は <Link href="/login" className="text-crystal hover:underline">ログイン</Link>
      </p>
    </div>
  );
}

function SocialButtons({ providers }: { providers: string[] }) {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-3 text-xs text-ink-3"><span className="h-px flex-1 bg-line" />または<span className="h-px flex-1 bg-line" /></div>
      {(["google", "discord"] as const).map((p) =>
        providers.includes(p) ? (
          <a key={p} href={`/api/auth/oauth/${p}`} className="flex h-11 items-center justify-center rounded-xl border border-line bg-card text-sm font-medium hover:border-crystal/50">
            {p === "google" ? "Google で続ける" : "Discord で続ける"}
          </a>
        ) : (
          <button key={p} disabled title="サーバーにOAuthの設定がないため使えません" className="flex h-11 items-center justify-center rounded-xl border border-dashed border-line text-sm text-ink-3">
            {p === "google" ? "Google" : "Discord"}（未設定）
          </button>
        ),
      )}
      <WalletButton mode="login" />
    </div>
  );
}
