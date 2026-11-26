"use client";

export class ClientApiError extends Error {
  constructor(public status: number, public code: string, message: string, public issues?: { path: (string | number)[]; message: string }[]) {
    super(message);
  }
}

const MESSAGES: Record<string, string> = {
  RATE_LIMITED: "操作が多すぎます。少し待ってから再度お試しください。",
  UNAUTHENTICATED: "ログインが必要です。",
  INTERNAL: "サーバーでエラーが発生しました。時間をおいて再度お試しください。",
  VALIDATION: "入力内容を確認してください。",
};

/** JSON fetch with same-origin cookies. Errors become ClientApiError with a Japanese message. */
export async function api<T = unknown>(path: string, init: { method?: string; body?: unknown } = {}): Promise<T> {
  let res: Response;
  try {
    res = await fetch(path, {
      method: init.method ?? (init.body ? "POST" : "GET"),
      headers: init.body !== undefined ? { "Content-Type": "application/json" } : undefined,
      body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
      credentials: "same-origin",
    });
  } catch {
    throw new ClientApiError(0, "NETWORK", "ネットワークに接続できません。接続を確認してください。");
  }
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const code = data.error ?? "ERROR";
    const msg = data.message && data.message !== code ? data.message : MESSAGES[code] ?? `エラー: ${code}`;
    throw new ClientApiError(res.status, code, msg, data.issues);
  }
  return data as T;
}

export function newKey() {
  return crypto.randomUUID().replace(/-/g, "");
}
