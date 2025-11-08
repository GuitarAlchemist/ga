import { test, expect } from '@playwright/test';

/**
 * Floor 0 Navigation Test - End-to-End
 * 
 * Tests the complete flow:
 * 1. Backend API connection
 * 2. Room data loading
 * 3. 3D scene rendering
 * 4. Navigation controls
 */

test.describe('Floor 0 Navigation E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the test page
    await page.goto('/test/floor0-navigation');
  });

  test('should load the page without errors', async ({ page }) => {
    // Check for console errors
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        errors.push(msg.text());
      }
    });

    // Wait for page to load
    await page.waitForLoadState('networkidle');

    // Check title
    await expect(page.locator('h4')).toContainText('Floor 0 Navigation Test');

    // Should not have critical errors (allow warnings)
    const criticalErrors = errors.filter(e => 
      !e.includes('Warning') && 
      !e.includes('DevTools') &&
      !e.includes('favicon')
    );
    expect(criticalErrors).toHaveLength(0);
  });

  test('should connect to backend API and load floor data', async ({ page }) => {
    // Wait for loading indicator to appear
    const loadingIndicator = page.locator('text=Loading Floor 0...');
    
    // Wait for loading to complete (max 10 seconds)
    await expect(loadingIndicator).toBeVisible({ timeout: 2000 });
    await expect(loadingIndicator).toBeHidden({ timeout: 10000 });

    // Check that stats chips are displayed
    await expect(page.locator('text=/\\d+ Rooms/')).toBeVisible();
    await expect(page.locator('text=/\\d+ Corridors/')).toBeVisible();
    await expect(page.locator('text=/\\d+ Categories/')).toBeVisible();
    await expect(page.locator('text=/Seed: \\d+/')).toBeVisible();
  });

  test('should display correct number of rooms', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForSelector('text=/\\d+ Rooms/', { timeout: 10000 });

    // Get the room count from the chip
    const roomsChip = await page.locator('text=/\\d+ Rooms/').textContent();
    const roomCount = parseInt(roomsChip?.match(/(\d+) Rooms/)?.[1] || '0');

    // Should have generated rooms (typically 30-40 for floor 0 with seed 42)
    expect(roomCount).toBeGreaterThan(20);
    expect(roomCount).toBeLessThan(50);

    console.log(`✓ Generated ${roomCount} rooms for Floor 0`);
  });

  test('should render 3D viewport', async ({ page }) => {
    // Wait for viewport to be visible
    const viewport = page.locator('[data-testid="floor0-viewport"]');
    await expect(viewport).toBeVisible();

    // Check viewport has canvas (Three.js renderer)
    const canvas = viewport.locator('canvas');
    await expect(canvas).toBeVisible({ timeout: 10000 });

    // Check canvas dimensions
    const box = await canvas.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeGreaterThan(500);
    expect(box!.height).toBeGreaterThan(400);
  });

  test('should handle API errors gracefully', async ({ page }) => {
    // Mock API to return error
    await page.route('**/api/music-rooms/**', (route) => {
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          error: 'Internal server error',
        }),
      });
    });

    // Reload page
    await page.reload();

    // Should show error message
    await expect(page.locator('text=/error/i')).toBeVisible({ timeout: 10000 });
  });

  test('should load data from backend API', async ({ page }) => {
    // Intercept API call
    const apiResponse = page.waitForResponse(
      (response) => response.url().includes('/api/music-rooms/floor/0'),
      { timeout: 10000 }
    );

    // Wait for API response
    const response = await apiResponse;
    expect(response.status()).toBe(200);

    // Parse response
    const data = await response.json();
    expect(data.success).toBe(true);
    expect(data.data).toBeDefined();
    expect(data.data.floor).toBe(0);
    expect(data.data.floorName).toBe('Set Classes');
    expect(data.data.rooms).toBeDefined();
    expect(data.data.corridors).toBeDefined();

    console.log(`✓ API returned ${data.data.rooms.length} rooms and ${data.data.corridors.length} corridors`);
  });

  test('should render rooms with correct categories', async ({ page }) => {
    // Wait for loading to complete
    await page.waitForSelector('text=/\\d+ Categories/', { timeout: 10000 });

    // Get categories count
    const categoriesChip = await page.locator('text=/\\d+ Categories/').textContent();
    const categoryCount = parseInt(categoriesChip?.match(/(\d+) Categories/)?.[1] || '0');

    // Floor 0 should have 8 categories (Set Classes)
    expect(categoryCount).toBeGreaterThan(5);
    expect(categoryCount).toBeLessThan(12);

    console.log(`✓ Floor 0 has ${categoryCount} categories`);
  });

  test('should have working orbit controls', async ({ page }) => {
    // Wait for canvas to be ready
    const canvas = page.locator('[data-testid="floor0-viewport"] canvas');
    await expect(canvas).toBeVisible({ timeout: 10000 });

    // Get initial canvas state
    await page.waitForTimeout(1000); // Let scene render

    // Simulate mouse drag (orbit)
    const box = await canvas.boundingBox();
    if (box) {
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.down();
      await page.mouse.move(box.x + box.width / 2 + 100, box.y + box.height / 2);
      await page.mouse.up();
    }

    // Wait for animation
    await page.waitForTimeout(500);

    // Canvas should still be visible (no crashes)
    await expect(canvas).toBeVisible();
  });

  test('should display seed parameter', async ({ page }) => {
    // Wait for seed chip
    await page.waitForSelector('text=/Seed: \\d+/', { timeout: 10000 });

    // Get seed value
    const seedChip = await page.locator('text=/Seed: \\d+/').textContent();
    const seed = parseInt(seedChip?.match(/Seed: (\d+)/)?.[1] || '0');

    // Should be seed 42 (default in component)
    expect(seed).toBe(42);

    console.log(`✓ Using seed: ${seed}`);
  });

  test('should render without memory leaks', async ({ page }) => {
    // Wait for initial render
    await page.waitForSelector('[data-testid="floor0-viewport"] canvas', { timeout: 10000 });

    // Get initial memory (if available)
    const initialMetrics = await page.evaluate(() => {
      if ('memory' in performance) {
        return (performance as any).memory.usedJSHeapSize;
      }
      return null;
    });

    // Interact with scene
    const canvas = page.locator('[data-testid="floor0-viewport"] canvas');
    const box = await canvas.boundingBox();
    if (box) {
      // Simulate multiple interactions
      for (let i = 0; i < 5; i++) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.mouse.down();
        await page.mouse.move(box.x + box.width / 2 + 50, box.y + box.height / 2 + 50);
        await page.mouse.up();
        await page.waitForTimeout(200);
      }
    }

    // Get final memory
    const finalMetrics = await page.evaluate(() => {
      if ('memory' in performance) {
        return (performance as any).memory.usedJSHeapSize;
      }
      return null;
    });

    // Memory should not grow excessively (allow 50MB increase)
    if (initialMetrics && finalMetrics) {
      const memoryIncrease = finalMetrics - initialMetrics;
      expect(memoryIncrease).toBeLessThan(50 * 1024 * 1024); // 50MB
      console.log(`✓ Memory increase: ${(memoryIncrease / 1024 / 1024).toFixed(2)}MB`);
    }
  });

  test('should cleanup on unmount', async ({ page }) => {
    // Wait for canvas
    await page.waitForSelector('[data-testid="floor0-viewport"] canvas', { timeout: 10000 });

    // Navigate away
    await page.goto('/test');

    // Wait a bit
    await page.waitForTimeout(500);

    // Navigate back
    await page.goto('/test/floor0-navigation');

    // Should render again without errors
    await expect(page.locator('[data-testid="floor0-viewport"] canvas')).toBeVisible({ timeout: 10000 });
  });

  test('should have responsive viewport', async ({ page }) => {
    // Wait for canvas
    const canvas = page.locator('[data-testid="floor0-viewport"] canvas');
    await expect(canvas).toBeVisible({ timeout: 10000 });

    // Get initial size
    const initialBox = await canvas.boundingBox();
    expect(initialBox).not.toBeNull();

    // Resize window
    await page.setViewportSize({ width: 1200, height: 800 });
    await page.waitForTimeout(500);

    // Get new size
    const newBox = await canvas.boundingBox();
    expect(newBox).not.toBeNull();

    // Canvas should have resized
    expect(newBox!.width).not.toBe(initialBox!.width);
  });

  test('should display floor name in header', async ({ page }) => {
    // Wait for header
    await expect(page.locator('h4')).toContainText('Set Classes');
  });

  test('should have proper lighting in scene', async ({ page }) => {
    // Wait for canvas
    await page.waitForSelector('[data-testid="floor0-viewport"] canvas', { timeout: 10000 });

    // Take screenshot to verify lighting
    const screenshot = await page.locator('[data-testid="floor0-viewport"]').screenshot();
    expect(screenshot.length).toBeGreaterThan(1000); // Should have content

    // Check that canvas is not completely black (has lighting)
    const isBlack = await page.evaluate(() => {
      const canvas = document.querySelector('[data-testid="floor0-viewport"] canvas') as HTMLCanvasElement;
      if (!canvas) return true;

      const ctx = canvas.getContext('2d');
      if (!ctx) return true;

      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const data = imageData.data;

      // Check if all pixels are black
      for (let i = 0; i < data.length; i += 4) {
        if (data[i] > 10 || data[i + 1] > 10 || data[i + 2] > 10) {
          return false; // Found non-black pixel
        }
      }
      return true;
    });

    expect(isBlack).toBe(false); // Should have visible content
  });
});

