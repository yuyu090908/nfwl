import type { Metadata } from "next";
import { requireUser } from "@/lib/server/queries";
import { db } from "@/lib/server/db";
import { PageHeader } from "@/components/ui/misc";
import { SettingsForm } from "@/components/club/settings-form";

export const metadata: Metadata = { title: "設定" };

export default async function SettingsPage() {
  const user = await requireUser("/settings");
  const u = await db.user.findUniqueOrThrow({ where: { id: user.id } });
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <PageHeader eyebrow="Account" title="Settings" />
      <SettingsForm displayName={u.displayName} settings={u.settings as object} />
    </div>
  );
}
