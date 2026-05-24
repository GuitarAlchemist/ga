// Harness tab test — verifies /test#dev/harness renders all rollout
// items and the baseline metrics tiles.
//
// Catches:
//   - the harness payload breaks if state/harness/items.json changes shape
//     (schema_version drift)
//   - /dev-data/harness server endpoint stops serving (vite plugin
//     devDataPlugin regression in vite.config.ts)
//   - baseline tile renderer throws when value/target are non-numeric
//   - the item card grid loses a card on re-render (new layout) OR the
//     rollout table loses a row (old layout) — supports both during the
//     deploy crossover window
//
// NOTE — runs against a LIVE URL (see playwright.dashboard.config.ts).
// Until the new layout is deployed, this test must accept the old layout
// (Prioritized rollout table) as well as the new (card grid + donut +
// timeline). After the crossover, the old branches can be deleted.
//
// See src/components/Harness/HarnessTab.tsx and the donut + timeline
// + per-item card components in src/components/Harness/.
import { test, expect } from '@playwright/test';

test.describe('Harness tab', () => {
  test('renders items + baseline tiles', async ({ page }) => {
    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // Cross-check the data source: /dev-data/harness must serve the items.
    const apiResp = await page.request.get('/dev-data/harness');
    expect(apiResp.status(), '/dev-data/harness should respond 2xx').toBeLessThan(400);
    const harness = await apiResp.json();
    expect(Array.isArray(harness.items), 'harness payload should expose items[]').toBe(true);
    expect(
      harness.items.length,
      'state/harness/items.json should declare at least 8 items',
    ).toBeGreaterThanOrEqual(8);

    // Wait for either the new card-grid layout OR the old rollout table.
    // The new design ships with HarnessTab.tsx and renders "Items (N)";
    // the old design renders "Prioritized rollout" in a <Paper> heading.
    const newHeading = page.locator('text=Items (').first();
    const oldHeading = page.locator('text=/Prioritized rollout/i').first();
    await expect.poll(async () => {
      const n = await newHeading.isVisible().catch(() => false);
      const o = await oldHeading.isVisible().catch(() => false);
      return n || o;
    }, {
      timeout: 20_000,
      message: 'Either Items (N) or Prioritized rollout heading should appear',
    }).toBe(true);

    // Each item title should appear in the rendered DOM (works for both
    // table rows and card layouts).
    const firstTitle = harness.items[0].title as string;
    const lastTitle = harness.items[harness.items.length - 1].title as string;
    const firstFragment = firstTitle.slice(0, Math.min(20, firstTitle.length));
    const lastFragment = lastTitle.slice(0, Math.min(20, lastTitle.length));
    await expect(page.getByText(firstFragment, { exact: false }).first()).toBeVisible({
      timeout: 10_000,
    });
    await expect(page.getByText(lastFragment, { exact: false }).first()).toBeVisible({
      timeout: 10_000,
    });

    // Baseline tiles section ("Baseline metrics") should be visible (kept
    // in both old and new layouts).
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

  test('skill action button queues an invocation', async ({ request }) => {
    // The skill action endpoint is local-only by design (gateLocal in
    // vite.config.ts), so this test only runs when targeting a local Vite.
    // Against the live URL it would correctly return 403 / 404. Skip it
    // unless the test runner is pointed at a local origin.
    const baseURL = (test.info().project.use.baseURL as string | undefined) ?? '';
    const isLocal = /localhost|127\.0\.0\.1/.test(baseURL);
    test.skip(!isLocal, 'skill POST endpoint is local-only; skipping on live deploy');

    const resp = await request.post('/dev-data/harness/skill/test-plan', {
      data: { source: 'playwright', context: 'harness-tab spec', item_number: 13 },
    });
    expect(resp.status(), 'POST /dev-data/harness/skill/<name> should respond 2xx').toBeLessThan(400);
    const body = await resp.json();
    expect(body.ok, 'response body should indicate ok').toBe(true);
    expect(body.queued?.skill, 'queued line should record the skill name').toBe('test-plan');
  });
});
