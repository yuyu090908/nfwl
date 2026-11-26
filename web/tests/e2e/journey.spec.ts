import { expect, test } from "@playwright/test";

// Acceptance path (prompt 20): register -> club -> 8 Commons -> 5 starters -> lineup to the game ->
// AI match to the end (bridge simulator stands in for the Unity build) -> result saved -> history ->
// data kept after logging in again.
test("new user plays a first practice match end to end", async ({ page }) => {
  const email = `e2e-${Date.now()}@test.invalid`;
  const password = "correct-horse-42";

  await page.goto("/register");
  await page.waitForLoadState("networkidle"); // hydrated: the form submits through fetch
  await page.fill("#displayName", "E2E Tester");
  await page.fill("#email", email);
  await page.fill("#password", password);
  await page.check('input[name="terms"]');
  await page.getByRole("button", { name: "アカウントを作成" }).click();
  await page.waitForURL(/\/onboarding/);

  await page.fill("#name", "E2E Rovers");
  await page.fill("#short", "E2E");
  await page.getByRole("button", { name: /クラブを作成して/ }).click();
  // Starters revealed: 8 player cards from the operator pool.
  await expect(page.locator('a[href^="/players/"]')).toHaveCount(8, { timeout: 20_000 });

  await page.goto("/squad");
  for (let i = 0; i < 5; i++) await expect(page.getByTestId(`slot-${i}`)).not.toContainText("空き");

  await page.goto("/play");
  await page.waitForLoadState("networkidle");
  await page.getByRole("button", { name: /20秒 \(test\)/ }).click();
  await page.getByTestId("use-simulator").check();
  const [cfgRes] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith("/api/match-sessions") && r.request().method() === "POST"),
    page.getByTestId("kickoff").click(),
  ]);
  const cfg = (await cfgRes.json()) as { home: unknown[]; away: unknown[] };
  expect(cfg.home).toHaveLength(5);
  expect(cfg.away).toHaveLength(5);
  expect(JSON.stringify(cfg)).not.toMatch(/password|privateKey|nfwl_session/i);

  const result = page.getByTestId("result");
  await expect(result).toBeVisible({ timeout: 60_000 });
  await expect(result).toContainText("記録済み");

  await result.getByRole("link", { name: "Match report" }).click();
  await expect(page).toHaveURL(/\/matches\/[0-9a-f-]+/);
  await expect(page.locator("body")).toContainText("E2E ROVERS", { ignoreCase: true });

  // Log out (clear cookie via the API) and log in again: the club and the match are still there.
  await page.request.post("/api/auth/logout", { headers: { "Content-Type": "application/json", Origin: "http://localhost:3100" }, data: {} });
  await page.goto("/login?next=/matches");
  await page.waitForLoadState("networkidle");
  await page.fill("#email", email);
  await page.fill("#password", password);
  await page.getByRole("button", { name: "ログイン", exact: true }).click();
  await page.waitForURL(/\/matches/);
  await expect(page.locator("main")).toContainText(/E2E/i);
});
