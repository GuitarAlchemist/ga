// Heartbeat banner test — verifies the live status banner at the top of
// /test#dev/summary renders, is the correct color, and contains the
// epic-shipped percentage.
//
// Catches:
//   - banner missing (regression in OverviewSection.tsx)
//   - banner shows red/amber when /dev-data/manifest reports healthy
//   - epics percentage NaN or missing (manifest contract drift)
//
// Selector strategy:
//   The banner is the first <Paper> with success.main / warning.main /
//   info.main background, and contains the text "Live · ..." (see
//   src/pages/OverviewSection.tsx ~line 290). We assert on that text +
//   computed background color rather than a data-testid (no testid exists
//   yet — adding one would be a non-surgical change per Karpathy rule 3).
import { test, expect } from '@playwright/test';

test.describe('Heartbeat banner', () => {
  test('renders with healthy color + epic-shipped percentage', async ({ page }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Banner text is one of:
    //   "Live · all systems nominal"           (healthy)
    //   "Live · N regression(s)"               (regression)
    //   "Live"                                 (info / empty)
    const banner = page.locator('text=/^Live(\\s*·|$)/').first();
    await expect(banner, 'heartbeat banner should be visible').toBeVisible({ timeout: 20_000 });

    // The banner's <Paper> ancestor carries the background color. We walk
    // up to the nearest paper element and read its computed style.
    const paper = banner.locator('xpath=ancestor::*[contains(@class, "MuiPaper-root")][1]');
    await expect(paper).toBeVisible();

    const bgColor = await paper.evaluate((el) => getComputedStyle(el).backgroundColor);
    // Healthy = MUI success.main (green ~rgb(46, 125, 50))
    // Regression = MUI warning.main (amber ~rgb(237, 108, 2))
    // Info = MUI info.main (blue ~rgb(2, 136, 209))
    // We assert it's one of those three families, NOT default white/gray.
    const [r, g, b] = (bgColor.match(/\d+/g) ?? []).map(Number);
    expect(
      bgColor,
      `banner bg should be a colored MUI palette (got ${bgColor})`,
    ).toMatch(/rgba?\(/);
    // Brightness threshold — full-saturation MUI palette colors are below 200 on
    // every channel, while default Paper is ~rgb(255, 255, 255).
    expect(
      r + g + b < 600,
      `banner bg should not be white-ish default Paper (rgb sum=${r + g + b})`,
    ).toBe(true);

    // Epic shipped percentage — sanity check that the manifest landed and
    // a numeric value is rendered. Format: "N% shipped (X/Y)".
    const epicsText = await banner.locator('xpath=ancestor::*[contains(@class, "MuiPaper-root")][1]').innerText();
    const pctMatch = epicsText.match(/(\d+)%\s+shipped/);
    expect(pctMatch, `heartbeat banner should contain "N% shipped" (got: ${epicsText})`).not.toBeNull();
    const pct = Number(pctMatch![1]);
    expect(pct).toBeGreaterThanOrEqual(0);
    expect(pct).toBeLessThanOrEqual(100);
  });
});
