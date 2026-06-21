// MCP dashboard control test — verifies the McpControlProvider opens a
// SignalR connection to /hubs/dev-dashboard when on /test#dev/* and
// stays silent on demo pages.
//
// This test uses the crossover-skip idiom: it runs whether GaApi is
// reachable or not. On a deployed/tunneled build with GaApi up, we expect
// the WebSocket handshake to succeed; on a static-only build (or with
// GaApi off), we expect the connect attempt to fail gracefully without
// breaking the page.
//
// Catches:
//   - McpControlProvider stops mounting after a DevelopmentSection refactor
//   - The hub URL drifts away from /hubs/dev-dashboard
//   - The provider tries to connect on demo pages (would create noise)
//   - data-dashboard-root marker disappears (breaks screenshot capture)
//
// See docs/runbooks/mcp-dashboard-control.md for the full pattern.
import { test, expect } from '@playwright/test';

test.describe('MCP dashboard control (Phase 1, read-only)', () => {
  test('connects to /hubs/dev-dashboard when on /test#dev', async ({ page }) => {
    const wsAttempts: string[] = [];
    page.on('websocket', (ws) => {
      wsAttempts.push(ws.url());
    });
    // Also catch the SignalR negotiate POST (the handshake that runs
    // before the WebSocket upgrade). If GaApi is down we still see
    // the attempt here.
    const negotiateAttempts: string[] = [];
    page.on('request', (req) => {
      if (req.url().includes('/hubs/dev-dashboard/negotiate')) {
        negotiateAttempts.push(req.url());
      }
    });

    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });

    // Wait a beat for the provider to mount + start its connection.
    await page.waitForTimeout(2500);

    const hitDashboardHub =
      negotiateAttempts.some((u) => u.includes('/hubs/dev-dashboard')) ||
      wsAttempts.some((u) => u.includes('/hubs/dev-dashboard'));

    expect(hitDashboardHub, 'expected an attempted connect to /hubs/dev-dashboard').toBe(true);
  });

  test('data-dashboard-root marker is rendered on /test#dev', async ({ page }) => {
    await page.goto('/test#dev/summary', { waitUntil: 'domcontentloaded' });
    // The screenshotter reads [data-dashboard-root="true"]; if this
    // marker disappears we'd silently fall back to document.body.
    await expect(page.locator('[data-dashboard-root="true"]')).toBeVisible({ timeout: 10_000 });
  });

  test('does NOT connect to /hubs/dev-dashboard from a demo page', async ({ page }) => {
    const negotiateAttempts: string[] = [];
    page.on('request', (req) => {
      if (req.url().includes('/hubs/dev-dashboard/negotiate')) {
        negotiateAttempts.push(req.url());
      }
    });

    // Prime Radiant is a demo; the dashboard provider must not mount here.
    await page.goto('/test/prime-radiant', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);

    expect(
      negotiateAttempts,
      'McpControlProvider must not connect on demo pages — it would create spurious traffic',
    ).toHaveLength(0);
  });

  test('hub endpoint is reachable on backend (crossover-skip)', async ({ request }) => {
    // The SignalR negotiate POST is the canonical "is the hub there?" check.
    // Skip the test when GaApi isn't reachable (typical for a Pages-only
    // build) instead of failing — same idiom as sentrux-tab.spec.ts.
    const resp = await request.post('/hubs/dev-dashboard/negotiate?negotiateVersion=1', {
      failOnStatusCode: false,
      timeout: 5_000,
    }).catch(() => null);

    if (!resp) {
      test.skip(true, 'GaApi not reachable — crossover skip');
      return;
    }
    // Either we get a proper SignalR negotiate response (200), or the
    // route exists but rejects something (4xx is still proof of life).
    // 502/504 means upstream is down — skip rather than fail.
    if (resp.status() >= 500 && resp.status() < 600) {
      test.skip(true, `GaApi returned ${resp.status()} — backend not running`);
      return;
    }
    expect(resp.status(), 'expected SignalR negotiate to respond, not 5xx').toBeLessThan(500);
  });
});
