import { expect, test } from "@playwright/test";

// Management pages render on a phone without horizontal scrolling.
for (const path of ["/", "/league", "/rankings", "/marketplace", "/predictions", "/help", "/login"]) {
  test(`mobile layout ${path}`, async ({ page }) => {
    await page.goto(path);
    await expect(page.locator("main")).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(1);
  });
}
