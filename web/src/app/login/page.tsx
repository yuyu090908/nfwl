import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { LoginForm } from "@/components/auth/auth-forms";
import { AuthShell } from "@/components/auth/auth-shell";
import { getCurrentUser } from "@/lib/server/session";
import { enabledProviders } from "@/lib/server/oauth";

export const metadata: Metadata = { title: "ログイン" };

export default async function LoginPage() {
  if (await getCurrentUser()) redirect("/club");
  return (
    <AuthShell title="Welcome back" subtitle="クラブの指揮に戻りましょう。">
      <Suspense>
        <LoginForm providers={enabledProviders()} />
      </Suspense>
    </AuthShell>
  );
}
