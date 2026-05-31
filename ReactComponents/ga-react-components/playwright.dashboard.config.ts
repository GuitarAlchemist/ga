// Playwright config for the dashboard + chatbot showcase E2E job.
//
// Harness item #7 — `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md`.
//
// This config is **separate** from `playwright.config.ts` (which runs the
// existing component-test suite against a local `npm run dev`). The dashboard
// suite is designed to run against a live URL (default:
// https://demos.guitaralchemist.com) so it tests what users actually see and
// does NOT need a webServer in CI. Override via PLAYWRIGHT_BASE_URL.
import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'https://demos.guitaralchemist.com';

export default defineConfig({
  testDir: './tests/dashboard',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI
    ? [
        ['list'],
        ['json', { outputFile: 'test-results/dashboard-results.json' }],
        ['html', { outputFolder: 'playwright-report-dashboard', open: 'never' }],
      ]
    : 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    // The live URL serves the dashboard at /test. Some tests navigate to
    // /test/manifest or /chatbot/ which are absolute paths off baseURL.
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // No webServer — tests target a live URL by design (zero CI build burden).
  // To run locally against a local preview, set PLAYWRIGHT_BASE_URL=http://localhost:5176
  // and run `npm run dev` in another terminal.
});
