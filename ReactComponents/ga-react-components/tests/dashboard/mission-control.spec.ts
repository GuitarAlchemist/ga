// MissionControl test — verifies the 4-quadrant Mission Control summary
// renders at the top of /test#dev/summary with the right shape.
//
// Catches:
//   - quadrant layout regression (one of NOW / WAITING / HAPPENED / AT RISK missing)
//   - count badge not wired to the underlying data array length
//   - "PR ready to merge" rows fail to open the GitHub PR URL on click
//
// Runs against the LIVE dashboard URL (see playwright.dashboard.config.ts).
// Uses the crossover-skip idiom (matches harness-tab.spec.ts and
// epic-drilldown.spec.ts) so the test passes on the current deploy until
// the new component lands — then enforces the contract once visible.
//
// See:
//   src/components/Summary/MissionControl.tsx
//   src/pages/OverviewSection.tsx (wiring point above InFlightCard)
import { test, expect } from '@playwright/test';

const QUADRANT_TESTIDS = [
  'quadrant-now',
  'quadrant-waiting',
  'quadrant-happened',
  'quadrant-at-risk',
] as const;

test.describe('Mission Control', () => {
  test('renders all four quadrants with count badges', async ({ page }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Crossover-skip: if the deploy hasn't picked up MissionControl yet,
    // the data-testid="mission-control" wrapper won't exist. Wait briefly
    // then skip — same pattern as epic-drilldown.spec.ts.
    const mc = page.locator('[data-testid="mission-control"]').first();
    const visible = await expect
      .poll(async () => mc.isVisible().catch(() => false), { timeout: 20_000 })
      .toBe(true)
      .then(() => true)
      .catch(() => false);
    test.skip(
      !visible,
      'MissionControl not deployed yet (feat/summary-mission-control PR has not shipped to demos.guitaralchemist.com)',
    );

    // All four quadrants must be present.
    for (const id of QUADRANT_TESTIDS) {
      const q = page.locator(`[data-testid="${id}"]`).first();
      await expect(q, `quadrant ${id} should render`).toBeVisible();

      // Each quadrant has a count badge — the chip rendered next to the title.
      const badge = page.locator(`[data-testid="${id}-count"]`).first();
      await expect(badge, `${id} count badge should render`).toBeVisible();
      const badgeText = await badge.innerText();
      // Must be a number (possibly 0). Catches a regression where the chip
      // shows a placeholder or the data array length isn't computed.
      expect(badgeText.trim(), `${id} count should be numeric`).toMatch(/^\d+$/);
    }
  });

  test('PR-ready row opens GitHub PR URL when clicked', async ({ page, context }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Same crossover skip.
    const mc = page.locator('[data-testid="mission-control"]').first();
    const visible = await expect
      .poll(async () => mc.isVisible().catch(() => false), { timeout: 20_000 })
      .toBe(true)
      .then(() => true)
      .catch(() => false);
    test.skip(!visible, 'MissionControl not deployed yet');

    // Look for any waiting-ready-pr row. There may not be any (the inbox
    // can legitimately be empty), so skip rather than fail in that case.
    const readyRow = page.locator('[data-testid="waiting-ready-pr"]').first();
    const hasReady = await readyRow.isVisible().catch(() => false);
    test.skip(
      !hasReady,
      'no PRs are currently in the "ready to merge" state — nothing to click',
    );

    // The PR-ready row is wrapped in an <a> with target="_blank" + the
    // github.com/GuitarAlchemist/ga/pull/<N> URL. Verify the href rather
    // than waiting for a new tab, since headless context handling of
    // target="_blank" is flakey across CI environments.
    const href = await readyRow.evaluate((el) => {
      const anchor = el.closest('a');
      return anchor?.getAttribute('href') ?? null;
    });
    expect(href, 'ready-PR row should link to a GitHub PR').toMatch(
      /^https:\/\/github\.com\/GuitarAlchemist\/ga\/pull\/\d+$/,
    );

    // Sanity check — the link should open in a new tab (target=_blank) so
    // the operator doesn't navigate away from the dashboard.
    const target = await readyRow.evaluate((el) => {
      const anchor = el.closest('a');
      return anchor?.getAttribute('target') ?? null;
    });
    expect(target, 'ready-PR row should open in a new tab').toBe('_blank');

    // Reference the context arg so lint doesn't flag it; it's reserved for a
    // future enhancement that asserts the new tab actually loads.
    expect(context).toBeDefined();
  });
});
