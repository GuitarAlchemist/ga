// Harness tab test — verifies /test#dev/harness renders all 8 rollout
// items and the baseline metrics tiles.
//
// Catches:
//   - the harness table breaks if state/harness/items.json changes shape
//     (schema_version drift)
//   - /dev-data/harness server endpoint stops serving (vite plugin
//     devDataPlugin regression in vite.config.ts)
//   - baseline tile renderer throws when value/target are non-numeric
//
// See src/pages/DevelopmentSection.tsx (HarnessSection ~line 1080).
import { test, expect } from '@playwright/test';

test.describe('Harness tab', () => {
  test('renders 8 rollout items + baseline tiles', async ({ page }) => {
    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // The Harness sub-tab loads /dev-data/harness; wait for the items table
    // to render. Each row's first <td> is the item number (1..8).
    // We use the text "Prioritized rollout" as the section heading anchor
    // — see DevelopmentSection.tsx ~line 1109.
    const rolloutHeading = page.locator('text=/Prioritized rollout/i').first();
    await expect(rolloutHeading, 'Prioritized rollout heading should appear').toBeVisible({
      timeout: 20_000,
    });

    // Cross-check the data source: /dev-data/harness must serve the items.
    const apiResp = await page.request.get('/dev-data/harness');
    expect(apiResp.status(), '/dev-data/harness should respond 2xx').toBeLessThan(400);
    const harness = await apiResp.json();
    expect(Array.isArray(harness.items), 'harness payload should expose items[]').toBe(true);
    expect(
      harness.items.length,
      'state/harness/items.json should declare 8 prioritized items',
    ).toBe(8);

    // Each item title should be visible in the rollout table. We sample
    // the first item (most likely to render fastest) and the last item
    // (verifies the table didn't truncate).
    const firstTitle = harness.items[0].title as string;
    const lastTitle = harness.items[harness.items.length - 1].title as string;
    // Titles can be long; use a substring match to keep the selector flexible.
    const firstFragment = firstTitle.slice(0, Math.min(20, firstTitle.length));
    const lastFragment = lastTitle.slice(0, Math.min(20, lastTitle.length));
    await expect(page.getByText(firstFragment, { exact: false }).first()).toBeVisible({
      timeout: 10_000,
    });
    await expect(page.getByText(lastFragment, { exact: false }).first()).toBeVisible({
      timeout: 10_000,
    });

    // Baseline tiles section ("Baseline metrics") should be visible.
    await expect(
      page.locator('text=/Baseline metrics/i').first(),
      'Baseline metrics heading should appear',
    ).toBeVisible({ timeout: 10_000 });
    // And the payload must declare at least one baseline.
    expect(
      Object.keys(harness.baselines ?? {}).length,
      'harness payload should expose at least one baseline metric',
    ).toBeGreaterThan(0);
  });
});
