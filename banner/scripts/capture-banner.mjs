// Capture banner.png from the built site for use in the org README.
//
// Runs headless Chromium with WebGPU enabled (SwiftShader fallback so
// it works on GitHub-hosted Linux runners without a real GPU). If
// WebGPU still fails to initialise the script exits 0 so the deploy
// keeps whatever banner.png was previously published — stale beats
// broken.

import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const url = process.argv[2] ?? 'http://localhost:4173/ga/';
const here = path.dirname(fileURLToPath(import.meta.url));
const outPath = path.resolve(here, '..', 'dist', 'banner.png');

console.log(`[capture] loading ${url}`);

const browser = await chromium.launch({
  args: [
    '--enable-unsafe-webgpu',
    '--use-vulkan=swiftshader',
    '--enable-features=Vulkan',
    '--no-sandbox',
  ],
});

try {
  const ctx = await browser.newContext({
    // 1600×600 reads well as a GitHub README banner; 2× scale for retina.
    viewport: { width: 1600, height: 600 },
    deviceScaleFactor: 2,
  });
  const page = await ctx.newPage();
  page.on('console', msg => console.log(`[browser ${msg.type()}]`, msg.text()));
  page.on('pageerror', err => console.error('[browser error]', err));

  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30_000 });

  const hasWebGPU = await page.evaluate(() => 'gpu' in navigator);
  if (!hasWebGPU) {
    console.warn('[capture] WebGPU unavailable — skipping (previous banner.png stays published)');
    process.exit(0);
  }

  await page.waitForSelector('canvas', { timeout: 15_000 });

  // Wait for the Warship-loaded console line, but don't block forever
  // if the model is cached or the log format drifts.
  const warshipLoaded = page.waitForEvent('console', {
    predicate: msg => msg.text().includes('Warship loaded'),
    timeout: 10_000,
  }).catch(() => null);
  await Promise.race([warshipLoaded, page.waitForTimeout(8_000)]);

  // Night preset shows the Milky Way + moon + ship — the showcase shot.
  const nightBtn = page.locator('button', { hasText: /^night$/i });
  if (await nightBtn.count()) {
    await nightBtn.first().click();
    // Let the Milky Way swap in and the ring spin settle.
    await page.waitForTimeout(3_500);
  } else {
    console.warn('[capture] NIGHT preset button not found — capturing current scene');
  }

  await page.screenshot({ path: outPath, type: 'png', fullPage: false });
  console.log(`[capture] wrote ${outPath}`);
} catch (err) {
  console.error('[capture] failed:', err);
  // Don't fail the workflow — keep whatever banner.png was previously deployed.
  process.exit(0);
} finally {
  await browser.close();
}
