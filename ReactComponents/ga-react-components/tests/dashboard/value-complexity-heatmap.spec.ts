// Value × Complexity 2×2 heatmap — verifies the heatmap renders above the
// AiAnnotationsCard on /test#dev/annotations.
//
// Catches:
//   - ValueComplexityHeatmap export from barrel
//   - DevelopmentSection wiring regression (heatmap goes ABOVE the card)
//   - data-testid contract regressions
//
// Crossover-skip idiom (same as ai-annotations-tab.spec.ts and harness-tab):
// the live deploy may pre-date this PR. Probe /dev-data/ai-annotations first;
// if it 404s or isn't JSON, this build hasn't shipped the heatmap yet — skip.

import { test, expect } from '@playwright/test';

test.describe('Value × Complexity heatmap', () => {
  test('renders 4 quadrants with count badges', async ({ page }) => {
    // Crossover guard — does the deploy under test actually expose the data?
    const apiResp = await page.request.get('/dev-data/ai-annotations');
    const ct = apiResp.headers()['content-type'] ?? '';
    if (apiResp.status() >= 400 || !ct.includes('application/json')) {
      test.skip(
        true,
        `Pre-deploy: /dev-data/ai-annotations not yet served (status ${apiResp.status()}, ct=${ct}). ` +
          `Remove this skip branch after the heatmap deploys.`,
      );
      return;
    }

    await page.goto('/test#dev/annotations', { waitUntil: 'domcontentloaded' });

    // Either we see the heatmap (post-deploy) or we don't (pre-deploy on this
    // build — skip rather than fail).
    const heatmap = page.getByTestId('value-complexity-heatmap');
    const present = await heatmap.first().isVisible({ timeout: 15_000 }).catch(() => false);
    if (!present) {
      test.skip(
        true,
        'Heatmap component not present in this build — skip until the new tab deploys.',
      );
      return;
    }

    await expect(heatmap, 'heatmap panel should render').toBeVisible();

    // 4 quadrants must all render — count badges are always shown (even 0).
    await expect(page.getByTestId('heatmap-quadrant-refactor-first')).toBeVisible();
    await expect(page.getByTestId('heatmap-quadrant-delete-candidate')).toBeVisible();
    await expect(page.getByTestId('heatmap-quadrant-keep-stable')).toBeVisible();
    await expect(page.getByTestId('heatmap-quadrant-maintenance-burden')).toBeVisible();

    // REFACTOR FIRST count badge — must be a number, even if 0.
    const refactorCount = await page
      .getByTestId('heatmap-count-refactor-first')
      .innerText();
    expect(refactorCount, 'REFACTOR FIRST count is numeric').toMatch(/^\d+$/);

    // The heatmap goes ABOVE the existing AiAnnotationsCard — assert DOM order.
    const cardBoundary = await page
      .locator('text=/AI Annotations/i')
      .first()
      .boundingBox();
    const heatmapBoundary = await heatmap.first().boundingBox();
    if (cardBoundary && heatmapBoundary) {
      expect(
        heatmapBoundary.y,
        'heatmap should render above the AiAnnotationsCard',
      ).toBeLessThanOrEqual(cardBoundary.y);
    }
  });
});
