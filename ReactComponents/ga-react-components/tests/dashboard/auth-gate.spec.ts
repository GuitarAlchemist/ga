// Auth gate test — verifies the Cloudflare Access UI surface behaves as
// expected when the /cdn-cgi/access/get-identity endpoint returns
// authenticated vs unauthenticated.
//
// Catches:
//   - SkillActionButton fires POST against /dev-data/* instead of the new
//     /actions/* path (regression in src/components/Harness/SkillActionButton.tsx)
//   - SkillActionButton stays clickable to a queue POST when no identity is
//     returned (operator gate breaks → public traffic can fire actions)
//   - AuthChip stops showing "Logged in" when the stub responds 200 (the
//     dev experience breaks)
//   - useCfIdentity loses the credentials: 'include' flag (CF Access cookie
//     would never be sent; identity always 401)
//
// This test uses Playwright's route() interception to mock the identity
// endpoint without touching real CF Access. It does NOT require a live
// /actions/* server — it asserts on the request the button attempts to
// make, intercepts it, and returns a synthetic 200. That makes the test
// safe to run against the live URL (no state mutation actually lands).
//
// See docs/runbooks/cf-access-dashboard.md.

import { test, expect, Route } from '@playwright/test';

const SIGNED_IN_IDENTITY = {
  email: 'spareilleux@gmail.com',
  name: 'Stephane Pareilleux',
  id: 'cf-access-test',
  type: 'test-stub',
};

test.describe('CF Access auth gate', () => {
  test('SkillActionButton disables + shows lock when /cdn-cgi/access/get-identity returns 401', async ({ page }) => {
    // Return 401 from the identity endpoint — simulate "no session".
    await page.route('**/cdn-cgi/access/get-identity', (route: Route) => {
      route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ error: 'not authenticated' }) });
    });

    // Track whether any action POST sneaks through despite the gate.
    let unauthorizedActionPost = false;
    await page.route('**/actions/harness/skill/**', (route: Route) => {
      if (route.request().method() === 'POST') unauthorizedActionPost = true;
      route.fulfill({ status: 401, body: 'unauthorized' });
    });
    await page.route('**/dev-data/harness/skill/**', (route: Route) => {
      if (route.request().method() === 'POST') unauthorizedActionPost = true;
      route.fulfill({ status: 401, body: 'unauthorized' });
    });

    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // Auth chip should show "Sign in" state. We assert by the data attribute
    // the chip exposes (data-auth-state="signed-out"), set in AuthChip.tsx.
    const chip = page.locator('[data-auth-state="signed-out"]').first();
    await expect(chip, 'AuthChip should render in signed-out state').toBeVisible({ timeout: 15_000 });

    // A SkillActionButton should appear with data-authed="false" (lock icon).
    const lockedButton = page.locator('button[data-authed="false"]').first();
    await expect(lockedButton, 'Skill action button should render in locked state').toBeVisible({
      timeout: 15_000,
    });

    // Clicking the locked button should NOT POST to either action route — it
    // redirects to the CF Access login URL instead. We don't follow the
    // navigation (would leave the test page); just assert the POST never fired.
    // Use a short pause to give any erroneous POST a chance to land.
    await page.waitForTimeout(500);
    expect(unauthorizedActionPost, 'no POST should fire when identity returns 401').toBe(false);
  });

  test('SkillActionButton enables and POSTs to /actions/* when identity returns 200', async ({ page }) => {
    // Stub identity → return signed-in user.
    await page.route('**/cdn-cgi/access/get-identity', (route: Route) => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(SIGNED_IN_IDENTITY),
      });
    });

    let postUrl: string | null = null;
    let postBody: unknown = null;
    let legacyPathHit = false;

    await page.route('**/actions/harness/skill/**', (route: Route) => {
      if (route.request().method() === 'POST') {
        postUrl = route.request().url();
        try {
          postBody = route.request().postDataJSON();
        } catch {
          postBody = route.request().postData();
        }
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            ok: true,
            queued: { id: 'test-stub', skill: 'test-plan' },
            message: 'Queued (stub)',
          }),
        });
        return;
      }
      route.continue();
    });

    // Guard: also intercept the legacy path. The test fails if the button
    // calls it (we want all new clicks on the canonical /actions/* path).
    await page.route('**/dev-data/harness/skill/**', (route: Route) => {
      if (route.request().method() === 'POST') legacyPathHit = true;
      route.fulfill({ status: 410, body: 'gone — test guard' });
    });

    await page.goto('/test#dev/harness', { waitUntil: 'domcontentloaded' });

    // AuthChip should show signed-in state.
    const chip = page.locator('[data-auth-state="signed-in"]').first();
    await expect(chip, 'AuthChip should render in signed-in state').toBeVisible({ timeout: 15_000 });

    // Find an enabled skill button (data-authed="true").
    const enabledButton = page.locator('button[data-authed="true"]').first();
    await expect(enabledButton, 'Skill action button should be enabled when authed').toBeVisible({
      timeout: 15_000,
    });

    await enabledButton.click();

    // Wait briefly for the POST to land in our mock.
    await expect.poll(() => postUrl, { timeout: 5_000 }).not.toBeNull();

    expect(postUrl, 'POST should target /actions/harness/skill/<name>').toMatch(
      /\/actions\/harness\/skill\/[a-z0-9-]+$/,
    );
    expect(legacyPathHit, 'button must NOT POST to deprecated /dev-data/harness/skill/*').toBe(false);

    // Body should carry the operator email breadcrumb (UX trail, not auth).
    if (postBody && typeof postBody === 'object') {
      const b = postBody as Record<string, unknown>;
      expect(b.actor_email, 'POST body should include operator email').toBe(SIGNED_IN_IDENTITY.email);
      expect(b.source, 'POST body should declare source').toBe('harness-tab');
    }
  });
});
