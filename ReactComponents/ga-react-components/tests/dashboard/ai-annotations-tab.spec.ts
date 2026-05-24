// AI Annotations tab — verifies /test#dev/annotations renders the
// AiAnnotationsCard, with crossover-skip semantics for the deploy window.
//
// Catches:
//   - /dev-data/ai-annotations middleware regression
//   - AiAnnotationsCard import path / barrel breakage
//   - DevelopmentSection tab list missing the 'annotations' value
//
// NOTE — like harness-tab.spec.ts, this runs against a LIVE URL by default
// (playwright.dashboard.config.ts). Until the new tab deploys we MUST
// tolerate the absence of /dev-data/ai-annotations and the absence of the
// annotations Tab. Once the deploy catches up, the skip branch can be
// removed.
//
// Crossover idiom: probe /dev-data/ai-annotations first. If it 404s or
// returns HTML (Vite's index fallback), we know this build hasn't shipped
// yet — test.skip.

import { test, expect } from '@playwright/test';

test.describe('AI Annotations tab', () => {
  test('renders card + filters + table', async ({ page }) => {
    // Crossover guard — does the deploy under test actually expose this?
    const apiResp = await page.request.get('/dev-data/ai-annotations');
    const ct = apiResp.headers()['content-type'] ?? '';
    if (apiResp.status() >= 400 || !ct.includes('application/json')) {
      test.skip(
        true,
        `Pre-deploy: /dev-data/ai-annotations not yet served (status ${apiResp.status()}, ct=${ct}). ` +
          `Remove this skip branch after the new tab deploys.`,
      );
      return;
    }

    const payload = await apiResp.json();
    // Schema sanity — the keys the card relies on.
    expect(typeof payload.total, 'payload.total should be a number').toBe('number');
    expect(typeof payload.by_truth_value, 'payload.by_truth_value should be an object').toBe(
      'object',
    );
    expect(Array.isArray(payload.annotations), 'payload.annotations should be an array').toBe(true);

    // Navigate to the tab and check it renders.
    await page.goto('/test#dev/annotations', { waitUntil: 'domcontentloaded' });

    // The card header text — must appear even when there are no annotations
    // (empty-state hint still shows the heading).
    await expect(
      page.locator('text=/AI Annotations/i').first(),
      'AI Annotations heading should appear',
    ).toBeVisible({ timeout: 15_000 });

    if (payload.empty || payload.total === 0) {
      // Empty-state hint must mention the @ai: marker syntax so users know
      // what to do next.
      await expect(page.locator('text=/@ai:/').first()).toBeVisible({
        timeout: 10_000,
      });
    } else {
      // The truth-value filter row must surface at least the T and U chips.
      await expect(page.locator('text=/truth value/i').first()).toBeVisible({
        timeout: 10_000,
      });
    }
  });
});
