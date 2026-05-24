// Manifest page test — verifies the /test/manifest viewer loads and
// surfaces the manifest schema_version + curl-copy affordance.
//
// Catches:
//   - /test/manifest 404 (route deleted or renamed)
//   - "Copy curl" button removed (AI-tool onboarding regression)
//   - schema_version field missing from the manifest payload itself
//     (server-side breaks at /dev-data/manifest)
//
// See src/pages/ManifestViewer.tsx (label="Copy curl", line ~211).
import { test, expect } from '@playwright/test';

test.describe('Manifest viewer', () => {
  test('/test/manifest loads with copy-curl + schema_version', async ({ page }) => {
    const response = await page.goto('/test/manifest', { waitUntil: 'domcontentloaded' });
    expect(response, 'navigation response should exist').not.toBeNull();
    expect(response!.status()).toBeLessThan(400);

    // The button label is "Copy curl" (CopyButton in ManifestViewer.tsx).
    await expect(
      page.getByRole('button', { name: /Copy curl/i }),
      'Copy curl button should be visible',
    ).toBeVisible({ timeout: 15_000 });

    // schema_version is rendered inline ("schema_version <chip>X.Y.Z</chip>").
    // We assert against the manifest payload directly because the inline
    // rendering inserts whitespace/MUI markup that's brittle to grep.
    const apiResp = await page.request.get('/dev-data/manifest');
    expect(apiResp.status(), '/dev-data/manifest should respond 2xx').toBeLessThan(400);
    const manifest = await apiResp.json();
    expect(
      manifest.schema_version,
      'manifest payload must declare schema_version (consumed by AI tools per CLAUDE.md "where to find things")',
    ).toBeTruthy();
    expect(typeof manifest.schema_version).toBe('string');
  });
});
