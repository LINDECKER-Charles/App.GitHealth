import { defineConfig } from "@playwright/test";

export default defineConfig({
  expect: {
    timeout: 10_000,
  },
  forbidOnly: Boolean(process.env["CI"]),
  fullyParallel: false,
  outputDir: "test-results",
  reporter: [
    ["line"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
  ],
  retries: process.env["CI"] ? 1 : 0,
  testDir: "./specs",
  timeout: 120_000,
  use: {
    browserName: "chromium",
    headless: true,
    trace: "retain-on-failure",
    viewport: { height: 900, width: 1440 },
  },
  workers: 1,
});
