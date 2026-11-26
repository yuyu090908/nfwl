import { defineConfig, devices } from "@playwright/test";

// E2E against a dev server with the bridge simulator (no Unity build needed) and 10 s test halves.
export default defineConfig({
  testDir: "tests/e2e",
  timeout: 120_000,
  fullyParallel: false,
  workers: 1,
  use: { baseURL: "http://localhost:3100", trace: "off", locale: "ja-JP" },
  webServer: {
    command: "npx next dev -p 3100",
    url: "http://localhost:3100/api/health",
    reuseExistingServer: true,
    timeout: 120_000,
    env: { NFWL_ENABLE_BRIDGE_SIMULATOR: "1", NFWL_ALLOW_SHORT_MATCHES: "1" },
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] }, testIgnore: /mobile/ },
    { name: "mobile", use: { ...devices["Pixel 7"] }, testMatch: /mobile/ },
  ],
});
