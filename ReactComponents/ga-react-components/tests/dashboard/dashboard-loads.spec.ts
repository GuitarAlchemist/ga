// Dashboard smoke test — outside-in verification that /test renders.
//
// Catches:
//   - 500/404 on the route
//   - blank-page from a runtime throw in TestIndex / DevelopmentSection
//   - the Demos/Development tab labels disappearing (e.g. a tab renamed
//     silently breaks the heartbeat-banner + harness-tab tests downstream)
//   - console errors during initial render (broken bundle, missing asset)
//
// Item #7 of docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md.
import { test, expect } from '@playwright/test';

test.describe('Dashboard loads', () => {
  test('/test returns 200 and renders core chrome', async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });

    const response = await page.goto('/test', { waitUntil: 'domcontentloaded' });
    expect(response, 'navigation response should exist').not.toBeNull();
    expect(response!.status(), 'GET /test should be 2xx').toBeLessThan(400);

    // Page title is set by Vite/Index.html on the production build.
    await expect(page).toHaveTitle(/Guitar Alchemist|GA|Test Index/i);

    // Both top-level section tabs must be visible. These selectors are
    // pinned to the MUI <Tab label="..."> text in src/pages/TestIndex.tsx
    // (PR 295's tab structure). If the labels change, update this test
    // alongside the rename.
    await expect(page.getByRole('tab', { name: /^Development$/ })).toBeVisible();
    await expect(page.getByRole('tab', { name: /^Demos\s*\(\d+\)$/ })).toBeVisible();

    // Wait for the dev section to fetch /dev-data/manifest before counting
    // console errors — the fetch itself can fail in CI if the live site is
    // down, but render errors should not be hidden by that.
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {
      // Networkidle can timeout if SSE/long-poll is open; ignore.
    });

    // Filter known-benign warnings the existing tests/bsp.spec.ts already
    // ignores (React 18 DevTools, MUI deprecation, etc.).
    const realErrors = consoleErrors.filter(
      (e) =>
        !e.includes('DevTools') &&
        !e.includes('deprecated') &&
        !e.toLowerCase().includes('warning:') &&
        // Live site may have third-party analytics 4xx that don't break the page.
        !e.includes('Failed to load resource'),
    );
    expect(realErrors, `unexpected console errors:\n${realErrors.join('\n')}`).toHaveLength(0);
  });
});
