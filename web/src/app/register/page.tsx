import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { RegisterForm } from "@/components/auth/auth-forms";
import { AuthShell } from "@/components/auth/auth-shell";
import { getCurrentUser } from "@/lib/server/session";
import { enabledProviders } from "@/lib/server/oauth";

export const metadata: Metadata = { title: "新規登録" };

export default async function RegisterPage() {
  if (await getCurrentUser()) redirect("/onboarding");
  return (
    <AuthShell title="Create your account" subtitle="登録後、クラブ作成と初期選手の受け取りに進みます。">
      <RegisterForm providers={enabledProviders()} />
    </AuthShell>
  );
}
