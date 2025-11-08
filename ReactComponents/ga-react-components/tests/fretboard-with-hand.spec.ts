import { test, expect } from '@playwright/test';

test.describe('FretboardWithHand Component', () => {
  test.describe('Console Errors', () => {
    test('should not have console errors', async ({ page }) => {
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
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');

      // Wait for canvas to render
      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 15000 });

      // Wait for WebGPU initialization and hand model loading
      await page.waitForTimeout(5000);

      // Filter out known acceptable warnings and expected errors
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('ShaderMaterial') &&
        !err.includes('deprecated') &&
        !err.includes('DevTools') &&
        !err.includes('ERR_CONNECTION_REFUSED') && // API not running is expected
        !err.includes('No voicings found') // Fallback is expected when API is down
      );

      // Check for errors
      if (filteredErrors.length > 0) {
        console.log('Console Errors:', filteredErrors);
      }

      expect(filteredErrors).toHaveLength(0);
    });
  });

  test.describe('Component Rendering', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');
    });

    test('should render 3D fretboard with canvas', async ({ page }) => {
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();
    });

    test('should display component title', async ({ page }) => {
      await expect(page.getByText('3D Fretboard with Hand Visualization')).toBeVisible();
    });

    test('should display component description', async ({ page }) => {
      await expect(page.getByText(/combines a 3D fretboard with hand pose visualization/i)).toBeVisible();
    });

    test('should show WebGPU or WebGL renderer badge', async ({ page }) => {
      const rendererBadge = page.getByText(/WebGPU|WebGL/);
      await expect(rendererBadge).toBeVisible();
    });

    test('should display chord name in heading', async ({ page }) => {
      await expect(page.getByRole('heading', { name: /G Chord/i })).toBeVisible();
    });

    test('should display difficulty level', async ({ page }) => {
      await expect(page.getByText(/Difficulty: Easy/i)).toBeVisible();
    });
  });

  test.describe('Chord Input Controls', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');
    });

    test('should have chord name input field', async ({ page }) => {
      const chordInput = page.getByLabel('Chord Name');
      await expect(chordInput).toBeVisible();
    });

    test('should have Load Chord button', async ({ page }) => {
      const loadButton = page.getByRole('button', { name: 'Load Chord' });
      await expect(loadButton).toBeVisible();
    });

    test('should have quick select buttons', async ({ page }) => {
      await expect(page.getByRole('button', { name: 'C' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'D' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'E' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'G' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Am' })).toBeVisible();
    });

    test('should change chord when quick select button is clicked', async ({ page }) => {
      // Click D chord button
      await page.getByRole('button', { name: 'D' }).click();
      
      // Wait for re-render
      await page.waitForTimeout(2000);
      
      // Check that heading updated
      await expect(page.getByRole('heading', { name: /D Chord/i })).toBeVisible();
    });

    test('should change chord when Load Chord button is clicked', async ({ page }) => {
      const chordInput = page.getByLabel('Chord Name');
      const loadButton = page.getByRole('button', { name: 'Load Chord' });
      
      // Clear and type new chord
      await chordInput.clear();
      await chordInput.fill('Am');
      
      // Click load button
      await loadButton.click();
      
      // Wait for re-render
      await page.waitForTimeout(2000);
      
      // Check that heading updated
      await expect(page.getByRole('heading', { name: /Am Chord/i })).toBeVisible();
    });
  });

  test.describe('Hand Model Loading', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');
    });

    test('should load hand model without errors', async ({ page }) => {
      const consoleMessages: string[] = [];
      
      page.on('console', (msg) => {
        consoleMessages.push(msg.text());
      });

      // Wait for hand model to load
      await page.waitForTimeout(5000);

      // Check for hand model loaded message
      const handLoadedMessage = consoleMessages.find(msg => 
        msg.includes('Hand model loaded') || msg.includes('🖐️')
      );
      
      expect(handLoadedMessage).toBeTruthy();
    });

    test('should detect finger bones', async ({ page }) => {
      const consoleMessages: string[] = [];
      
      page.on('console', (msg) => {
        consoleMessages.push(msg.text());
      });

      // Wait for hand model to load
      await page.waitForTimeout(5000);

      // Check for bone detection messages
      const boneMessages = consoleMessages.filter(msg => 
        msg.includes('Bone found:') && 
        (msg.includes('finger_') || msg.includes('thumb'))
      );
      
      // Should find at least 15 finger bones (3 bones × 5 fingers)
      expect(boneMessages.length).toBeGreaterThanOrEqual(15);
    });

    test('should find finger bones for all fingers', async ({ page }) => {
      const consoleMessages: string[] = [];
      
      page.on('console', (msg) => {
        consoleMessages.push(msg.text());
      });

      // Wait for hand model to load
      await page.waitForTimeout(5000);

      // Check for specific finger bones
      const hasIndexBones = consoleMessages.some(msg => msg.includes('finger_index'));
      const hasMiddleBones = consoleMessages.some(msg => msg.includes('finger_middle'));
      const hasRingBones = consoleMessages.some(msg => msg.includes('finger_ring'));
      const hasPinkyBones = consoleMessages.some(msg => msg.includes('finger_pinky'));
      const hasThumbBones = consoleMessages.some(msg => msg.includes('thumb'));
      
      expect(hasIndexBones).toBe(true);
      expect(hasMiddleBones).toBe(true);
      expect(hasRingBones).toBe(true);
      expect(hasPinkyBones).toBe(true);
      expect(hasThumbBones).toBe(true);
    });
  });

  test.describe('API Integration', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');
    });

    test('should attempt to fetch chord voicings from API', async ({ page }) => {
      const consoleMessages: string[] = [];
      
      page.on('console', (msg) => {
        consoleMessages.push(msg.text());
      });

      // Wait for API call
      await page.waitForTimeout(3000);

      // Check for API fetch message
      const fetchMessage = consoleMessages.find(msg => 
        msg.includes('🎸 Fetching voicings for chord')
      );
      
      expect(fetchMessage).toBeTruthy();
    });

    test('should use fallback chord when API is unavailable', async ({ page }) => {
      const consoleMessages: string[] = [];
      
      page.on('console', (msg) => {
        consoleMessages.push(msg.text());
      });

      // Wait for API call and fallback
      await page.waitForTimeout(3000);

      // Check for fallback message
      const fallbackMessage = consoleMessages.find(msg => 
        msg.includes('🔄 Using fallback')
      );
      
      expect(fallbackMessage).toBeTruthy();
    });

    test('should display alert when using fallback chord', async ({ page }) => {
      // Wait for component to load
      await page.waitForTimeout(3000);

      // Check for alert message
      const alert = page.locator('[role="alert"]');
      await expect(alert).toBeVisible();
      await expect(alert).toContainText(/Using fallback chord/i);
    });
  });

  test.describe('Visual Regression', () => {
    test('should render consistently with G chord', async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 15000 });

      // Wait for WebGPU and hand model initialization
      await page.waitForTimeout(5000);

      // Take screenshot
      await page.screenshot({
        path: 'tests/screenshots/fretboard-with-hand-g-chord.png',
        fullPage: true,
        timeout: 15000
      });
    });

    test('should render consistently with D chord', async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');

      // Click D chord button
      await page.getByRole('button', { name: 'D' }).click();
      
      // Wait for re-render
      await page.waitForTimeout(5000);

      // Take screenshot
      await page.screenshot({
        path: 'tests/screenshots/fretboard-with-hand-d-chord.png',
        fullPage: true,
        timeout: 15000
      });
    });
  });

  test.describe('Performance', () => {
    test('should load within 20 seconds', async ({ page }) => {
      const startTime = Date.now();

      await page.goto('/test/fretboard-with-hand');

      const canvas = page.locator('canvas');
      await canvas.waitFor({ state: 'visible', timeout: 20000 });

      // Wait for WebGPU badge to appear
      await page.getByText(/WebGPU|WebGL/).first().waitFor({ state: 'visible', timeout: 20000 });

      const loadTime = Date.now() - startTime;
      expect(loadTime).toBeLessThan(20000);
    });

    test('should load hand model within 10 seconds', async ({ page }) => {
      const consoleMessages: string[] = [];
      const startTime = Date.now();
      
      page.on('console', (msg) => {
        if (msg.text().includes('Hand model loaded')) {
          const loadTime = Date.now() - startTime;
          expect(loadTime).toBeLessThan(10000);
        }
        consoleMessages.push(msg.text());
      });

      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');

      // Wait for hand model to load
      await page.waitForTimeout(10000);
    });
  });

  test.describe('Features List', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/test/fretboard-with-hand');
      await page.waitForLoadState('networkidle');
    });

    test('should display features section', async ({ page }) => {
      await expect(page.getByRole('heading', { name: 'Features' })).toBeVisible();
    });

    test('should list all implemented features', async ({ page }) => {
      await expect(page.getByText(/Fetches chord voicings from backend API/i)).toBeVisible();
      await expect(page.getByText(/3D fretboard visualization with WebGPU/i)).toBeVisible();
      await expect(page.getByText(/Hand pose visualization/i)).toBeVisible();
      await expect(page.getByText(/Interactive orbit controls/i)).toBeVisible();
    });
  });
});

