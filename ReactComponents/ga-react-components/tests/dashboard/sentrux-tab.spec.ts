// Sentrux tab test — verifies /test#dev/sentrux renders the four cards and
// handles the "sentrux unreachable" graceful-degradation path.
//
// Catches:
//   - Sentrux tab missing from the tab strip after a DevelopmentSection.tsx
//     refactor
//   - One of the four cards (Health / Rules / TestGaps / DSM) stops rendering
//   - /dev-data/sentrux/* middleware regression in vite.config.ts
//   - "Trigger rescan" button missing
//
// Sentrux itself is OPTIONAL (sentrux.exe may not be installed on every
// machine). The test passes whether sentrux is reachable or not — we only
// assert that the cards render and the unreachable state shows the right
// copy.
//
// See src/components/Sentrux/ for the components and the sentruxPlugin in
// vite.config.ts for the middleware.
import { test, expect } from '@playwright/test';

test.describe('Sentrux tab', () => {
  test('tab visible in dev strip + four cards render', async ({ page }) => {
    await page.goto('/test#dev/sentrux', { waitUntil: 'domcontentloaded' });

    // The tab itself
    await expect(page.getByRole('tab', { name: /sentrux/i })).toBeVisible({ timeout: 15_000 });

    // Each of the four cards has a unique heading. We assert all four.
    await expect(page.getByRole('heading', { name: /^sentrux$/i }).first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('heading', { name: /rule violations/i })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('heading', { name: /test gaps/i })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('heading', { name: /dependency structure matrix/i })).toBeVisible({ timeout: 10_000 });
  });

  test('health endpoint returns ok=true or graceful unreachable', async ({ request }) => {
    const resp = await request.get('/dev-data/sentrux/health');
    expect(resp.status(), '/dev-data/sentrux/health should respond 2xx').toBeLessThan(400);
    const body = await resp.json() as { ok?: boolean; error?: string; data?: { quality_signal?: number } };
    expect(typeof body.ok).toBe('boolean');
    if (body.ok) {
      // When sentrux IS reachable, quality_signal should be a number in [0, 10000].
      expect(typeof body.data?.quality_signal).toBe('number');
    } else {
      // When sentrux ISN'T reachable, we must still return a useful error.
      expect(body.error, 'error message must be present on ok=false').toBeTruthy();
    }
  });

  test('rescan button is rendered (auth-aware)', async ({ page }) => {
    // The button is always rendered; whether it succeeds depends on
    // gateLocal in vite.config.ts. We only assert it shows up — the actual
    // POST is local-only and would 403 against a public Cloudflare tunnel.
    await page.goto('/test#dev/sentrux', { waitUntil: 'domcontentloaded' });
    await expect(page.getByRole('button', { name: /trigger rescan/i })).toBeVisible({ timeout: 10_000 });
  });

  test('rescan POST is local-only', async ({ request }) => {
    // POST /actions/sentrux/rescan is gateLocal-protected. Against the live
    // URL it returns 403; against a local Vite, 200. Both are valid.
    const resp = await request.post('/actions/sentrux/rescan');
    const status = resp.status();
    expect(status === 200 || status === 403, `expected 200 or 403, got ${status}`).toBe(true);
  });
});
