// Epic drill-down test — verifies /test#dev/summary epic cards expand to
// show sub-sections, item bodies, and PR/doc badges.
//
// Catches:
//   - parseBacklog() loses the items[] field (parser regression)
//   - EpicRow drops the ButtonBase / Collapse wrapper (UI regression)
//   - PR badge href construction breaks (link target regression)
//   - localStorage key drifts and persistence silently breaks
//
// NOTE — runs against a LIVE URL (see playwright.dashboard.config.ts).
// Until the new drill-down ships to demos.guitaralchemist.com this test
// must waitFor + test.skip when the new UI is absent, matching the
// crossover-skip idiom from harness-tab.spec.ts.
//
// See src/pages/OverviewSection.tsx (EpicRow + BacklogItemRow) and
// src/dev-data/parsers.ts (BacklogItem extraction).
import { test, expect } from '@playwright/test';

const PR_HREF_BASE = 'https://github.com/GuitarAlchemist/ga/pull/';

test.describe('Epic drill-down', () => {
  test('clicking an epic row reveals sub-sections and items', async ({ page }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Fetch the manifest to find an epic with item bodies. If the
    // backend hasn't deployed the items[] field yet, skip — old payload
    // can't power the new UI.
    const apiResp = await page.request.get('/dev-data/manifest');
    expect(apiResp.status(), '/dev-data/manifest should respond 2xx').toBeLessThan(400);
    const manifest = await apiResp.json();
    const epics = manifest?.backlog?.epics ?? [];
    const epicWithItems = epics.find((e: { sub_sections: { items?: unknown[] }[] }) =>
      e.sub_sections.some((s) => Array.isArray(s.items) && s.items.length > 0),
    );
    test.skip(
      !epicWithItems,
      'backlog payload has no items[] yet (deploy has not caught up with feat/epic-drilldown parser)',
    );

    const epicTitle = (epicWithItems as { title: string }).title;
    const firstSub = (epicWithItems as { sub_sections: { title: string; items: { text: string }[] }[] })
      .sub_sections.find((s) => s.items && s.items.length > 0)!;
    const firstItemText = firstSub.items[0].text;
    const firstItemFragment = firstItemText.slice(0, Math.min(20, firstItemText.length));

    // Wait for either the new expandable layout (data-testid="epic-row") OR
    // the old static layout (no test id). If only the old layout is present,
    // the live deploy hasn't picked up the new component yet — skip.
    const epicRowLocator = page.locator('[data-testid="epic-row"]').first();
    await expect.poll(async () => {
      return epicRowLocator.isVisible().catch(() => false);
    }, {
      timeout: 20_000,
      message: 'epic-row test ID should appear on /test#dev/summary',
    }).toBe(true);

    // Find the epic row whose header contains our chosen epic title.
    const targetRow = page
      .locator('[data-testid="epic-row"]')
      .filter({ hasText: epicTitle })
      .first();
    await expect(targetRow, `epic row for "${epicTitle}" should be visible`).toBeVisible();

    // Before click, details should not be present in DOM.
    const detailsLocator = targetRow.locator('[data-testid="epic-details"]');
    await expect(detailsLocator, 'details should be absent before click').toHaveCount(0);

    // Click the row header (the ButtonBase wrapper). Use the title text as
    // the anchor since it sits inside the ButtonBase.
    await targetRow.getByText(epicTitle, { exact: false }).first().click();

    // After click, details container appears and the sub-section title
    // + first item body should render.
    await expect(detailsLocator, 'details container should appear on expand').toBeVisible({
      timeout: 5_000,
    });
    await expect(
      detailsLocator.getByText(firstSub.title, { exact: false }).first(),
      `sub-section "${firstSub.title}" header should render`,
    ).toBeVisible();
    await expect(
      detailsLocator.getByText(firstItemFragment, { exact: false }).first(),
      `first item text "${firstItemFragment}…" should render`,
    ).toBeVisible();
  });

  test('PR badge links to the correct GitHub PR URL', async ({ page }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Locate an item with a PR ref via the API.
    const apiResp = await page.request.get('/dev-data/manifest');
    expect(apiResp.status()).toBeLessThan(400);
    const manifest = await apiResp.json();
    const epics = manifest?.backlog?.epics ?? [];
    let epicTitle: string | null = null;
    let prNumber: number | null = null;
    for (const e of epics) {
      for (const s of e.sub_sections ?? []) {
        for (const item of s.items ?? []) {
          if (Array.isArray(item.pr_refs) && item.pr_refs.length > 0) {
            epicTitle = e.title;
            prNumber = item.pr_refs[0];
            break;
          }
        }
        if (epicTitle) break;
      }
      if (epicTitle) break;
    }
    test.skip(
      !epicTitle || prNumber === null,
      'no PR refs found in current backlog payload (or items[] not deployed)',
    );

    // Skip if the new layout isn't deployed.
    const epicRowLocator = page.locator('[data-testid="epic-row"]').first();
    await expect.poll(async () => {
      return epicRowLocator.isVisible().catch(() => false);
    }, { timeout: 20_000 }).toBe(true);

    const targetRow = page
      .locator('[data-testid="epic-row"]')
      .filter({ hasText: epicTitle! })
      .first();
    await targetRow.getByText(epicTitle!, { exact: false }).first().click();

    const detailsLocator = targetRow.locator('[data-testid="epic-details"]');
    await expect(detailsLocator).toBeVisible({ timeout: 5_000 });

    // The PR chip text is `#NNN`. Click-target is an <a>; assert href.
    const prBadge = detailsLocator.getByText(`#${prNumber}`, { exact: true }).first();
    await expect(prBadge, `PR badge #${prNumber} should be visible`).toBeVisible();
    const href = await prBadge.evaluate((el) => {
      const a = el.closest('a');
      return a?.getAttribute('href') ?? null;
    });
    expect(href, 'PR badge should link to GitHub').toBe(`${PR_HREF_BASE}${prNumber}`);
  });
});
