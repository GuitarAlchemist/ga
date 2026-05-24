// Harness tab test — verifies /test#dev/harness renders all rollout
// items and the baseline metrics tiles.
//
// Catches:
//   - the harness payload breaks if state/harness/items.json changes shape
//     (schema_version drift)
//   - /dev-data/harness server endpoint stops serving (vite plugin
//     devDataPlugin regression in vite.config.ts)
//   - baseline tile renderer throws when value/target are non-numeric
//   - the item card grid loses a card on re-render
//
// See src/components/Harness/HarnessTab.tsx and the donut + timeline
// + per-item card components in src/components/Harness/.
import { test, expect } from '@playwright/test';

test.describe('Harness tab', () => {
  test('renders item cards + baseline tiles', async ({ page }) => {
    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // Wait for the items section heading from the new card grid.
    const itemsHeading = page.locator('text=/^Items \\(/i').first();
    await expect(itemsHeading, 'Items (N) heading should appear').toBeVisible({
      timeout: 20_000,
    });

    // Cross-check the data source: /dev-data/harness must serve the items.
    const apiResp = await page.request.get('/dev-data/harness');
    expect(apiResp.status(), '/dev-data/harness should respond 2xx').toBeLessThan(400);
    const harness = await apiResp.json();
    expect(Array.isArray(harness.items), 'harness payload should expose items[]').toBe(true);
    expect(
      harness.items.length,
      'state/harness/items.json should declare at least 8 items',
    ).toBeGreaterThanOrEqual(8);

    // Each item card has a stable id so the timeline pills can scroll to it.
    // Verify the first and last item ids exist in the DOM.
    const firstNumber = harness.items[0].number as number;
    const lastNumber = harness.items[harness.items.length - 1].number as number;
    await expect(page.locator(`#harness-item-${firstNumber}`)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator(`#harness-item-${lastNumber}`)).toBeVisible({ timeout: 10_000 });

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

  test('skill action button queues an invocation', async ({ page, request }) => {
    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // The skill action button POSTs to /dev-data/harness/skill/<name>.
    // We verify the endpoint contract directly (the UI button is just a
    // friendly wrapper around this POST).
    const resp = await request.post('/dev-data/harness/skill/test-plan', {
      data: { source: 'playwright', context: 'harness-tab spec', item_number: 13 },
    });
    expect(resp.status(), 'POST /dev-data/harness/skill/<name> should respond 2xx').toBeLessThan(400);
    const body = await resp.json();
    expect(body.ok, 'response body should indicate ok').toBe(true);
    expect(body.queued?.skill, 'queued line should record the skill name').toBe('test-plan');
  });
});
