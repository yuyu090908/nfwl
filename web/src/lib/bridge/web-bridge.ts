"use client";
// Browser side of the Unity <-> Web bridge.
// Unity (WebBridge.cs + NFWLBridge.jslib) calls window.NFWLBridge.receive(json) for every event and
// window.NFWLBridge.getConfigJson() once at start-up. This class validates each message, drops
// duplicates, and relays them in order to the server with retries. The page never gives Unity a
// cookie, token or wallet key: the session cookie stays HttpOnly in the browser.

import { parseBridgeMessage, type BridgeMessage, type UnityMatchConfig, type WebCommand } from "./protocol";

export type ConnectionState = "online" | "retrying" | "offline";

export interface BridgeCallbacks {
  onMessage(m: BridgeMessage): void;
  onServerResult(eventType: string, result: unknown): void;
  onConnection(state: ConnectionState, pending: number): void;
  onReject(reason: string): void;
}

export interface UnityLike {
  SendMessage(objectName: string, method: string, value?: string | number): void;
}

declare global {
  interface Window {
    NFWLBridge?: { getConfigJson(): string; getTeamsJson(): string; receive(json: string): void };
  }
}

export class WebBridge {
  private seen = new Set<string>();
  private queue: BridgeMessage[] = [];
  private sending = false;
  private unity: UnityLike | null = null;
  private closed = false;
  private storageKey: string;

  constructor(
    private config: UnityMatchConfig,
    private cb: BridgeCallbacks,
    private endpoint = `/api/match-sessions/${config.sessionId}/events`,
  ) {
    this.storageKey = `nfwl:outbox:${config.sessionId}`;
    // Recover messages that were not delivered before a reload.
    try {
      const saved = JSON.parse(sessionStorage.getItem(this.storageKey) ?? "[]") as BridgeMessage[];
      for (const m of saved) this.enqueue(m);
    } catch {}
  }

  install() {
    window.NFWLBridge = {
      // Everything except the line-ups (read by WebBridge.cs with JsonUtility).
      getConfigJson: () => JSON.stringify({ ...this.config, home: undefined, away: undefined }),
      // Line-ups in the shape Unity's JsonPlayerSource reads: {"home":[...5],"away":[...5]}.
      getTeamsJson: () => JSON.stringify({ home: this.config.home, away: this.config.away }),
      receive: (json: string) => this.receive(json),
    };
  }

  attachUnity(u: UnityLike) {
    this.unity = u;
  }

  dispose() {
    this.closed = true;
    if (window.NFWLBridge) delete window.NFWLBridge;
  }

  command(c: WebCommand) {
    try {
      this.unity?.SendMessage("NFWLWebBridge", "OnWebCommand", JSON.stringify(c));
    } catch {
      // Unity not ready yet or already quit: commands are best effort.
    }
  }

  /** Entry point for Unity (and the dev simulator). */
  receive(json: string) {
    let raw: unknown;
    try {
      raw = JSON.parse(json);
    } catch {
      return this.cb.onReject("invalid JSON from game");
    }
    const parsed = parseBridgeMessage(raw);
    if (!parsed.ok) return this.cb.onReject(parsed.error);
    const m = parsed.message;
    if (m.sessionId !== this.config.sessionId) return this.cb.onReject("message for another session");
    this.enqueue(m);
  }

  private enqueue(m: BridgeMessage) {
    if (this.seen.has(m.messageId)) return; // duplicate from Unity: ignore
    this.seen.add(m.messageId);
    this.cb.onMessage(m);
    this.queue.push(m);
    this.persist();
    void this.flush();
  }

  private persist() {
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify(this.queue));
    } catch {}
  }

  get pending() {
    return this.queue.length;
  }

  /** Sends queued messages one at a time, in order. Retries network / 5xx errors with backoff. */
  async flush() {
    if (this.sending || this.closed) return;
    this.sending = true;
    let attempt = 0;
    try {
      while (this.queue.length && !this.closed) {
        const m = this.queue[0];
        let res: Response | null = null;
        try {
          res = await fetch(this.endpoint, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "same-origin",
            body: JSON.stringify(m),
            keepalive: m.eventType === "MatchFinished",
          });
        } catch {
          res = null;
        }
        if (res && res.ok) {
          attempt = 0;
          const data = (await res.json().catch(() => ({}))) as { result?: unknown };
          this.queue.shift();
          this.persist();
          this.cb.onConnection("online", this.queue.length);
          if (data.result) this.cb.onServerResult(m.eventType, data.result);
          continue;
        }
        if (res && res.status >= 400 && res.status < 500 && res.status !== 429) {
          // The server refused this message for good (closed session, bad order...). Drop it and tell the page.
          const err = (await res.json().catch(() => ({}))) as { error?: string; message?: string };
          this.queue.shift();
          this.persist();
          this.cb.onReject(`${m.eventType}: ${err.message ?? err.error ?? res.status}`);
          continue;
        }
        attempt++;
        this.cb.onConnection(attempt > 4 ? "offline" : "retrying", this.queue.length);
        await new Promise((r) => setTimeout(r, Math.min(15_000, 500 * 2 ** attempt)));
      }
    } finally {
      this.sending = false;
    }
  }
}

/** Unique message id (Unity uses the same alphabet). */
export function messageId() {
  return crypto.randomUUID().replace(/-/g, "");
}
