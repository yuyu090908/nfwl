import { Badge } from "@/components/ui/badge";

const MAP: Record<string, { tone: "success" | "warning" | "danger" | "crystal" | "neutral"; label: string; title: string }> = {
  SERVER_SIMULATED: { tone: "success", label: "Server verified", title: "サーバーで試合を計算した公式結果です" },
  CLIENT_REPORTED: { tone: "crystal", label: "Client reported", title: "ブラウザから報告され、サーバーの整合性チェックを通過した結果です（公式シーズンには反映されません）" },
  FLAGGED: { tone: "warning", label: "Flagged", title: "整合性チェックで注意点があり、成績には反映していません" },
  REJECTED: { tone: "danger", label: "Rejected", title: "不可能な結果と判定され、記録していません" },
  PENDING: { tone: "neutral", label: "Pending", title: "試合中または未確定" },
};

export function VerificationBadge({ v }: { v: string }) {
  const m = MAP[v] ?? MAP.PENDING;
  return <Badge tone={m.tone} title={m.title}>{m.label}</Badge>;
}
