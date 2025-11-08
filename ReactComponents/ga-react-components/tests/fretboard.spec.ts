import { test, expect } from '@playwright/test';

test.describe('Fretboard Components', () => {
  test.describe('Console Errors', () => {
    test('ThreeFretboard should not have console errors', async ({ page }) => {
      const consoleErrors: string[] = [];
      const consoleWarnings: string[] = [];

      // Capture console messages
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        } else if (msg.type() === 'warning') {
          consoleWarnings.push(msg.text());
        }
      });

      // Navigate and wait for load
      await page.goto('/test/three-fretboard');
      await page.waitForLoadState('networkidle');

      // Wait for canvas to render
      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 15000 });

      // Wait for WebGPU initialization
      await page.waitForTimeout(3000);

      // Filter out known acceptable warnings (like ShaderMaterial compatibility)
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('ShaderMaterial') &&
        !err.includes('deprecated') &&
        !err.includes('DevTools')
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });

    test('RealisticFretboard should not have console errors', async ({ page }) => {
      const consoleErrors: string[] = [];
      const consoleWarnings: string[] = [];

      // Capture console messages
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        } else if (msg.type() === 'warning') {
          consoleWarnings.push(msg.text());
        }
      });

      // Navigate and wait for load
      await page.goto('/test/realistic-fretboard');
      await page.waitForLoadState('networkidle');

      // Wait for canvas to render
      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 10000 });

      // Wait for Pixi.js initialization
      await page.waitForTimeout(2000);

      // Filter out known acceptable warnings
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('deprecated') &&
        !err.includes('DevTools')
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });

    test('GuitarFretboard should not have console errors', async ({ page }) => {
      const consoleErrors: string[] = [];

      // Capture console messages
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Navigate and wait for load
      await page.goto('/test/guitar-fretboard');
      await page.waitForLoadState('networkidle');

      // Wait for SVG to render
      await page.waitForTimeout(1000);

      // Filter out known acceptable warnings
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('deprecated') &&
        !err.includes('DevTools')
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });

    test('WebGPUFretboard should not have console errors', async ({ page }) => {
      const consoleErrors: string[] = [];

      // Capture console messages
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Navigate and wait for load
      await page.goto('/test/webgpu-fretboard');
      await page.waitForLoadState('networkidle');

      // Wait for canvas to render
      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 10000 });

      // Wait for WebGPU initialization
      await page.waitForTimeout(2000);

      // Filter out known acceptable warnings
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('deprecated') &&
        !err.includes('DevTools')
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });

    test('Capo Test page should not have console errors', async ({ page }) => {
      const consoleErrors: string[] = [];

      // Capture console messages
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Navigate and wait for load
      await page.goto('/test/capo');
      await page.waitForLoadState('networkidle');

      // Wait for both canvases to render
      await page.waitForTimeout(3000);

      // Filter out known acceptable warnings
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('ShaderMaterial') &&
        !err.includes('deprecated') &&
        !err.includes('DevTools')
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });
  });

  test.describe('Network Errors', () => {
    test('ThreeFretboard should not have failed network requests', async ({ page }) => {
      const failedRequests: string[] = [];

      // Capture failed requests
      page.on('requestfailed', (request) => {
        failedRequests.push(`${request.url()} - ${request.failure()?.errorText}`);
      });

      await page.goto('/test/three-fretboard');
      await page.waitForLoadState('networkidle');

      // Wait for initialization
      await page.waitForTimeout(3000);

      // Check for failed requests
      if (failedRequests.length > 0) {
        console.log('Failed Requests:', failedRequests);
      }

      expect(failedRequests).toHaveLength(0);
    });

    test('RealisticFretboard should not have failed network requests', async ({ page }) => {
      const failedRequests: string[] = [];

      page.on('requestfailed', (request) => {
        failedRequests.push(`${request.url()} - ${request.failure()?.errorText}`);
      });

      await page.goto('/test/realistic-fretboard');
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      if (failedRequests.length > 0) {
        console.log('Failed Requests:', failedRequests);
      }

      expect(failedRequests).toHaveLength(0);
    });

    test('All test pages should load without 404 errors', async ({ page }) => {
      const pages = [
        '/test',
        '/test/three-fretboard',
        '/test/realistic-fretboard',
        '/test/guitar-fretboard',
        '/test/webgpu-fretboard',
        '/test/capo',
      ];

      for (const testPage of pages) {
        const response = await page.goto(testPage);
        expect(response?.status()).toBe(200);
      }
    });
  });

  test.describe('Test Index Page', () => {
    test('should display test index with all components', async ({ page }) => {
      await page.goto('/test');
      await page.waitForLoadState('networkidle');

      // Check for title
      await expect(page.getByText('Fretboard Component Test Suite')).toBeVisible();

      // Check for all component cards (use role to be more specific)
      await expect(page.getByRole('heading', { name: 'ThreeFretboard' })).toBeVisible();
      await expect(page.getByRole('heading', { name: 'RealisticFretboard' })).toBeVisible();
      await expect(page.getByRole('heading', { name: 'WebGPUFretboard' })).toBeVisible();
      await expect(page.getByRole('heading', { name: 'GuitarFretboard' })).toBeVisible();

      // Check for feature comparison matrix
      await expect(page.getByText('Feature Comparison Matrix')).toBeVisible();
    });
  });

  test.describe('ThreeFretboard (3D WebGPU)', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/three-fretboard');
      await page.waitForLoadState('networkidle');
    });

    test('should render 3D fretboard', async ({ page }) => {
      // Wait for canvas to be present
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();

      // Check for WebGPU badge
      const webgpuBadge = page.getByText(/⚡ WebGPU|🔧 WebGL/).first();
      await expect(webgpuBadge).toBeVisible();
    });

    test('should have fullscreen button', async ({ page }) => {
      // Check for fullscreen icon button (SVG icon)
      const fullscreenButton = page.locator('button').filter({ has: page.locator('svg[data-testid="FullscreenIcon"], svg[data-testid="FullscreenExitIcon"]') });
      await expect(fullscreenButton).toBeVisible();
    });

    test('should have capo selector', async ({ page }) => {
      const capoSelect = page.getByLabel('Capo Position');
      await expect(capoSelect).toBeVisible();
    });

    test('should change capo position', async ({ page }) => {
      const capoSelect = page.getByLabel('Capo Position');

      // Open dropdown
      await capoSelect.click({ timeout: 10000 });

      // Select Fret 3
      await page.getByRole('option', { name: 'Fret 3' }).click({ timeout: 10000 });

      // Wait for re-render
      await page.waitForTimeout(1000);
    });

    test('should have left-handed toggle', async ({ page }) => {
      const leftHandedSwitch = page.getByLabel('Left-Handed');
      await expect(leftHandedSwitch).toBeVisible();
    });

    test('should toggle left-handed mode', async ({ page }) => {
      const leftHandedSwitch = page.getByLabel('Left-Handed');

      // Toggle on
      await leftHandedSwitch.click({ timeout: 10000 });

      // Wait for re-render
      await page.waitForTimeout(1000);
    });

    test('should have guitar type selector', async ({ page }) => {
      const guitarTypeSelect = page.getByLabel('Guitar Type');
      await expect(guitarTypeSelect).toBeVisible();
    });

    test('should change guitar type', async ({ page }) => {
      const guitarTypeSelect = page.getByLabel('Guitar Type');

      // Open dropdown
      await guitarTypeSelect.click({ timeout: 10000 });

      // Select Acoustic
      await page.getByRole('option', { name: 'Acoustic' }).click({ timeout: 10000 });

      // Wait for re-render
      await page.waitForTimeout(1000);
    });

    test('should display C major chord positions', async ({ page }) => {
      // The demo should show C major chord by default
      // We can't easily verify 3D objects, but we can check the canvas is rendering
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();

      // Take a screenshot for visual verification
      await page.screenshot({ path: 'tests/screenshots/threefretboard-c-major.png', fullPage: true });
    });

    test('should have orbit controls info', async ({ page }) => {
      // Use caption variant to target the specific info text
      const info = page.locator('span.MuiTypography-caption').filter({ hasText: /drag to rotate|scroll to zoom/i });
      await expect(info).toBeVisible();
    });

    test('should render capo when capo position is set', async ({ page }) => {
      // Set capo to fret 3
      const capoSelect = page.getByLabel('Capo Position');
      await capoSelect.waitFor({ state: 'visible', timeout: 15000 });

      // Click to open dropdown
      await capoSelect.click({ timeout: 15000 });

      // Wait for dropdown to open
      await page.waitForTimeout(500);

      // Select Fret 3
      const fret3Option = page.getByRole('option', { name: 'Fret 3' });
      await fret3Option.waitFor({ state: 'visible', timeout: 10000 });
      await fret3Option.click({ timeout: 10000 });

      // Wait for capo to render in 3D scene
      await page.waitForTimeout(2000);

      // Take screenshot to verify capo is visible
      await page.screenshot({
        path: 'tests/screenshots/threefretboard-capo-fret3.png',
        fullPage: true,
        timeout: 15000
      });

      // Verify the select shows Fret 3
      await expect(capoSelect).toContainText('Fret 3');
    });

    test('should render capo at different positions', async ({ page }) => {
      // Test capo at fret 5
      const capoSelect = page.getByLabel('Capo Position');
      await capoSelect.waitFor({ state: 'visible', timeout: 15000 });

      await capoSelect.click({ timeout: 15000 });
      await page.waitForTimeout(500);

      const fret5Option = page.getByRole('option', { name: 'Fret 5' });
      await fret5Option.waitFor({ state: 'visible', timeout: 10000 });
      await fret5Option.click({ timeout: 10000 });

      // Wait for capo to render
      await page.waitForTimeout(2000);

      // Take screenshot
      await page.screenshot({
        path: 'tests/screenshots/threefretboard-capo-fret5.png',
        fullPage: true,
        timeout: 15000
      });

      await expect(capoSelect).toContainText('Fret 5');
    });

    test('should remove capo when set to No Capo', async ({ page }) => {
      // First set capo to fret 3
      const capoSelect = page.getByLabel('Capo Position');
      await capoSelect.waitFor({ state: 'visible', timeout: 15000 });

      await capoSelect.click({ timeout: 15000 });
      await page.waitForTimeout(500);

      const fret3Option = page.getByRole('option', { name: 'Fret 3' });
      await fret3Option.waitFor({ state: 'visible', timeout: 10000 });
      await fret3Option.click({ timeout: 10000 });

      await page.waitForTimeout(2000);

      // Now remove capo
      await capoSelect.click({ timeout: 15000 });
      await page.waitForTimeout(500);

      const noCapoOption = page.getByRole('option', { name: 'No Capo' });
      await noCapoOption.waitFor({ state: 'visible', timeout: 10000 });
      await noCapoOption.click({ timeout: 10000 });

      // Wait for scene to update
      await page.waitForTimeout(2000);

      // Take screenshot to verify no capo
      await page.screenshot({
        path: 'tests/screenshots/threefretboard-no-capo.png',
        fullPage: true,
        timeout: 15000
      });

      await expect(capoSelect).toContainText('No Capo');
    });
  });

  test.describe('RealisticFretboard (Pixi.js)', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/realistic-fretboard');
      await page.waitForLoadState('networkidle');
    });

    test('should render realistic fretboard', async ({ page }) => {
      // Check for canvas (Pixi.js renders to canvas)
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();
    });

    test('should have capo selector', async ({ page }) => {
      const capoSelect = page.getByLabel('Capo Position');
      await expect(capoSelect).toBeVisible();
    });

    test('should have left-handed toggle', async ({ page }) => {
      const leftHandedSwitch = page.getByLabel('Left-Handed');
      await expect(leftHandedSwitch).toBeVisible();
    });

    test('should have guitar type selector', async ({ page }) => {
      const guitarTypeSelect = page.getByLabel('Guitar Type');
      await expect(guitarTypeSelect).toBeVisible();
    });

    test('should display wood grain texture', async ({ page }) => {
      // Wait for canvas to render
      await page.waitForTimeout(1000);

      // Take screenshot to verify wood grain
      await page.screenshot({ path: 'tests/screenshots/realisticfretboard-wood-grain.png', fullPage: true });
    });

    test('should render capo when capo position is set', async ({ page }) => {
      // Set capo to fret 3
      const capoSelect = page.getByLabel('Capo Position');
      await capoSelect.waitFor({ state: 'visible', timeout: 10000 });

      await capoSelect.click({ timeout: 10000 });
      await page.waitForTimeout(500);

      const fret3Option = page.getByRole('option', { name: 'Fret 3' });
      await fret3Option.waitFor({ state: 'visible', timeout: 10000 });
      await fret3Option.click({ timeout: 10000 });

      // Wait for capo to render
      await page.waitForTimeout(1000);

      // Take screenshot to verify capo is visible
      await page.screenshot({
        path: 'tests/screenshots/realisticfretboard-capo-fret3.png',
        fullPage: true
      });

      await expect(capoSelect).toContainText('Fret 3');
    });

    test('should render capo with metallic appearance', async ({ page }) => {
      // Set capo to fret 5
      const capoSelect = page.getByLabel('Capo Position');
      await capoSelect.waitFor({ state: 'visible', timeout: 10000 });

      await capoSelect.click({ timeout: 10000 });
      await page.waitForTimeout(500);

      const fret5Option = page.getByRole('option', { name: 'Fret 5' });
      await fret5Option.waitFor({ state: 'visible', timeout: 10000 });
      await fret5Option.click({ timeout: 10000 });

      // Wait for capo to render
      await page.waitForTimeout(1000);

      // Take screenshot to verify metallic appearance
      await page.screenshot({
        path: 'tests/screenshots/realisticfretboard-capo-fret5.png',
        fullPage: true
      });

      await expect(capoSelect).toContainText('Fret 5');
    });
  });

  test.describe('Feature Comparison', () => {
    test('ThreeFretboard should have capo support', async ({ page }) => {
      await page.goto('/test/three-fretboard');
      const capoSelect = page.getByLabel('Capo Position');
      await expect(capoSelect).toBeVisible();
    });

    test('RealisticFretboard should have capo support', async ({ page }) => {
      await page.goto('/test/realistic-fretboard');
      const capoSelect = page.getByLabel('Capo Position');
      await expect(capoSelect).toBeVisible();
    });

    test('ThreeFretboard should have fullscreen', async ({ page }) => {
      await page.goto('/test/three-fretboard');
      const fullscreenButton = page.locator('button').filter({
        has: page.locator('svg[data-testid="FullscreenIcon"], svg[data-testid="FullscreenExitIcon"]')
      });
      await expect(fullscreenButton).toBeVisible();
    });

    test('RealisticFretboard should NOT have fullscreen', async ({ page }) => {
      await page.goto('/test/realistic-fretboard');
      const fullscreenButton = page.locator('button').filter({
        has: page.locator('svg[data-testid="FullscreenIcon"], svg[data-testid="FullscreenExitIcon"]')
      });
      await expect(fullscreenButton).toHaveCount(0);
    });
  });

  test.describe('Visual Regression', () => {
    test('ThreeFretboard should render consistently', async ({ page }) => {
      await page.goto('/test/three-fretboard');
      await page.waitForLoadState('networkidle');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 15000 });

      // Wait for WebGPU initialization
      await page.waitForTimeout(3000);

      // Take screenshot with increased timeout
      await page.screenshot({
        path: 'tests/screenshots/threefretboard-visual-regression.png',
        timeout: 15000
      });
    });

    test('RealisticFretboard should render consistently', async ({ page }) => {
      await page.goto('/test/realistic-fretboard');
      await page.waitForLoadState('networkidle');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible' });

      // Wait for Pixi.js initialization
      await page.waitForTimeout(1000);

      // Take screenshot
      await page.screenshot({ path: 'tests/screenshots/realisticfretboard-visual-regression.png' });
    });
  });

  test.describe('Performance', () => {
    test('ThreeFretboard should load within 20 seconds', async ({ page }) => {
      const startTime = Date.now();

      await page.goto('/test/three-fretboard');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 20000 });

      // Wait for WebGPU badge to appear (indicates initialization complete)
      await page.getByText(/⚡ WebGPU|🔧 WebGL/).first().waitFor({ state: 'visible', timeout: 20000 });

      const loadTime = Date.now() - startTime;
      expect(loadTime).toBeLessThan(20000);
    });

    test('RealisticFretboard should load within 5 seconds', async ({ page }) => {
      const startTime = Date.now();

      await page.goto('/test/realistic-fretboard');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible' });

      const loadTime = Date.now() - startTime;
      expect(loadTime).toBeLessThan(5000);
    });
  });
});

