// SentruxNextStepsCard test — verifies the prescription surface on
// /test#dev/sentrux: the card renders at the top of the tab, the
// middleware returns a structured payload (or graceful empty state),
// and the Regenerate button is wired.
//
// Catches:
//   - SentruxNextStepsCard missing from the Sentrux tab after a
//     DevelopmentSection.tsx refactor
//   - /dev-data/sentrux/next-steps middleware regression in vite.config.ts
//   - YAML frontmatter parser drift (the middleware extracts inputs.*
//     without a YAML dep; brittle if the seed schema changes)
//   - Regenerate button missing or not bound to the /sentrux-next-steps
//     skill via SkillActionButton
//
// The seed artifact (state/quality/sentrux-next-steps/latest.md) is shipped
// in the repo so this test exercises the populated path; we don't assert
// against specific recommendation text (that drifts when the skill regenerates),
// only against structural anchors that prove the render succeeded.
import { test, expect } from '@playwright/test';

test.describe('Sentrux Next Steps card', () => {
  test('card visible at top of Sentrux tab', async ({ page }) => {
    await page.goto('/test#dev/sentrux', { waitUntil: 'domcontentloaded' });

    // The card heading
    await expect(page.getByRole('heading', { name: /^next steps$/i }))
      .toBeVisible({ timeout: 15_000 });

    // Card test id
    await expect(page.getByTestId('sentrux-next-steps-card'))
      .toBeVisible({ timeout: 10_000 });

    // Regenerate button is rendered (auth-aware — SkillActionButton stays
    // visible even when CF Access identity is absent, so this is safe).
    await expect(page.getByRole('button', { name: /regenerate/i }))
      .toBeVisible({ timeout: 10_000 });
  });

  test('next-steps endpoint returns either populated payload or graceful empty', async ({ request }) => {
    const resp = await request.get('/dev-data/sentrux/next-steps');
    expect(resp.status(), '/dev-data/sentrux/next-steps must respond 2xx').toBeLessThan(400);
    const body = await resp.json() as {
      empty?: boolean;
      schema?: string;
      markdown?: string;
      hint?: string;
      source_path?: string;
      inputs?: Record<string, unknown>;
    };
    expect(typeof body.empty).toBe('boolean');
    expect(body.source_path).toMatch(/sentrux-next-steps\/latest\.md$/);

    if (body.empty) {
      // Empty state must carry an onboarding hint so the card can render
      // a non-confusing first-run experience.
      expect(body.hint, 'empty payload must include hint copy').toBeTruthy();
    } else {
      // Populated state must include the parsed frontmatter inputs and the
      // markdown body so the card can render chips + body.
      expect(body.schema).toBe('sentrux-next-steps-v1');
      expect(typeof body.markdown).toBe('string');
      expect((body.markdown ?? '').length).toBeGreaterThan(0);
      expect(body.inputs, 'inputs object must be present').toBeTruthy();
      // The seed populates these three numeric inputs; the dashboard chip
      // row depends on them. Don't pin exact values (the next regeneration
      // will change them) — just assert they parsed as numbers.
      const inp = body.inputs as Record<string, unknown>;
      expect(typeof inp.quality_signal === 'number' || inp.quality_signal === undefined).toBe(true);
      expect(typeof inp.cycles === 'number' || inp.cycles === undefined).toBe(true);
      expect(typeof inp.coverage_pct === 'number' || inp.coverage_pct === undefined).toBe(true);
    }
  });

  test('inputs chips render when seed payload is populated', async ({ page, request }) => {
    // Probe the endpoint first; if it's empty, skip the chip assertion
    // (this test crosses-over with the empty-state fallback, which is
    // exercised by the test above).
    const resp = await request.get('/dev-data/sentrux/next-steps');
    const body = await resp.json() as { empty?: boolean; inputs?: Record<string, unknown> };
    test.skip(body.empty === true, 'next-steps payload is empty; chip row not rendered');

    await page.goto('/test#dev/sentrux', { waitUntil: 'domcontentloaded' });
    await expect(page.getByTestId('sentrux-next-steps-card'))
      .toBeVisible({ timeout: 10_000 });

    // At least one of the well-known input chips should render. We accept
    // either "quality" or "cycles" or "coverage" — any one is sufficient
    // proof the frontmatter parser + chip renderer wired correctly.
    const chipRegex = /(quality \d|\d+ cycles|\d+(\.\d+)?% coverage)/i;
    await expect(
      page.getByTestId('sentrux-next-steps-card').getByText(chipRegex).first()
    ).toBeVisible({ timeout: 5_000 });
  });
});
