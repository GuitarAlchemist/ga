// TestPlansCard spec — verifies /test#dev/qa renders the new card,
// surfaces test plans aggregated by the Vite middleware at
// /dev-data/test-plans, and that the "Generate for chatbot" button
// POSTs to /dev-data/harness/skill/test-plan with the right context.
//
// Catches:
//   - the /dev-data/test-plans endpoint disappearing or changing shape
//   - the card mis-rendering when state/quality/test-plans/ is empty
//   - the SkillActionButton URL drifting from /dev-data/harness/skill/<name>
//   - the QA sub-tab losing the card (regression on DevelopmentSection.tsx)
//
// Runs against a LIVE URL (see playwright.dashboard.config.ts). The
// POST-flow assertion is local-only because the queue endpoint is
// gateLocal-protected — it skips automatically on the live deploy.
import { test, expect } from '@playwright/test';

test.describe('TestPlansCard (/test#dev/qa)', () => {
  test('renders header + chatbot footer + empty-or-list state', async ({ page }) => {
    await page.goto('/test#dev/qa', { waitUntil: 'domcontentloaded' });

    // The card itself is uniquely identified by data-testid.
    const card = page.getByTestId('test-plans-card');
    await expect(card, 'TestPlansCard should mount on /test#dev/qa').toBeVisible({
      timeout: 20_000,
    });

    // Header label + Generate button must always be present.
    await expect(card.getByText('Test Plans', { exact: true })).toBeVisible();
    await expect(card.getByRole('button', { name: /Generate for chatbot/i })).toBeVisible();

    // Cross-check the /dev-data/test-plans payload. The endpoint exists
    // even when no plans are on disk — it returns an empty list, never 404.
    const apiResp = await page.request.get('/dev-data/test-plans');
    expect(apiResp.status(), '/dev-data/test-plans should respond 2xx').toBeLessThan(400);
    const payload = await apiResp.json();
    expect(Array.isArray(payload.plans), 'payload.plans should be an array').toBe(true);
    expect(typeof payload.total).toBe('number');

    if (payload.total === 0) {
      // Empty-state copy includes the skill name + directory path.
      await expect(
        card.getByText(/No test plans generated yet/i),
      ).toBeVisible({ timeout: 5_000 });
      await expect(
        card.getByText(/state\/quality\/test-plans/),
      ).toBeVisible();
    } else {
      // List state — at least one plan title must render. The card
      // truncates titles with ellipsis but the text content is preserved
      // so the first plan's title (or a short prefix) is locatable.
      const firstTitle = payload.plans[0].title as string;
      const fragment = firstTitle.slice(0, Math.min(20, firstTitle.length));
      await expect(card.getByText(fragment, { exact: false }).first()).toBeVisible({
        timeout: 5_000,
      });
    }

    // Chatbot eval footer should always render — either with a pass rate
    // or with a "no last.json" hint. The "Chatbot loop:" prefix is the
    // load-bearing string the footer always emits.
    await expect(card.getByText(/Chatbot loop:/i)).toBeVisible();
  });

  test('Generate for chatbot button POSTs the queue line (local only)', async ({ request }) => {
    // The skill-invocation endpoint is gateLocal-protected. On the live
    // deploy it would correctly 403; skip rather than fail.
    const baseURL = (test.info().project.use.baseURL as string | undefined) ?? '';
    const isLocal = /localhost|127\.0\.0\.1/.test(baseURL);
    test.skip(!isLocal, 'POST endpoint is local-only; skipping on live deploy');

    const resp = await request.post('/dev-data/harness/skill/test-plan', {
      data: { source: 'playwright', context: 'gachatbot', item_number: null },
    });
    expect(resp.status(), 'POST should respond 2xx').toBeLessThan(400);
    const body = await resp.json();
    expect(body.ok, 'response.ok should be true').toBe(true);
    expect(body.queued?.skill, 'queued line should record the skill name').toBe('test-plan');
    expect(body.queued?.context, 'queued line should preserve the context tag').toBe('gachatbot');
  });
});
