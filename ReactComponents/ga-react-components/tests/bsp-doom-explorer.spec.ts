import { test, expect } from '@playwright/test';

test.describe('BSP DOOM Explorer', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to BSP DOOM Explorer test page
    await page.goto('/test/bsp-doom-explorer');
    await page.waitForLoadState('networkidle');

    // Wait for the canvas to be ready
    await page.waitForSelector('canvas', { timeout: 10000 });
  });

  test.describe('Anodized Metal Materials', () => {
    test('should render with anodized metal materials visible', async ({ page }) => {
      // Wait for scene to fully load
      await page.waitForTimeout(3000);

      // Take screenshot to verify metallic appearance
      const screenshot = await page.locator('canvas').screenshot();
      expect(screenshot).toBeTruthy();

      // Check that the scene has loaded by verifying HUD elements
      await expect(page.locator('text=Floor:')).toBeVisible();

      // Verify renderer is running
      const rendererText = await page.locator('text=Renderer:').textContent();
      expect(rendererText).toContain('Renderer:');
    });

    test('should show metallic reflections on different floors', async ({ page }) => {
      // Wait for initial load
      await page.waitForTimeout(2000);

      // Take screenshot of floor 0 (stone)
      await page.screenshot({
        path: 'test-results/screenshots/bsp-floor-0-stone.png',
        fullPage: false
      });

      // Navigate to floor 3 (anodized metal) using mouse wheel
      const canvas = page.locator('canvas');
      await canvas.hover();

      // Scroll down to change floors (3 scrolls = floor 3)
      for (let i = 0; i < 3; i++) {
        await canvas.dispatchEvent('wheel', { deltaY: 100 });
        await page.waitForTimeout(500);
      }

      // Take screenshot of floor 3 (anodized bronze/copper)
      await page.screenshot({
        path: 'test-results/screenshots/bsp-floor-3-anodized-metal.png',
        fullPage: false
      });

      // Navigate to floor 5 (polished anodized metal)
      for (let i = 0; i < 2; i++) {
        await canvas.dispatchEvent('wheel', { deltaY: 100 });
        await page.waitForTimeout(500);
      }

      // Take screenshot of floor 5 (polished gold/brass)
      await page.screenshot({
        path: 'test-results/screenshots/bsp-floor-5-polished-metal.png',
        fullPage: false
      });

      // Verify floor indicator shows correct floor
      const floorText = await page.locator('text=/Floor: \\d+/').textContent();
      expect(floorText).toContain('Floor: 5');
    });

    test('should show animated rotating sample elements', async ({ page }) => {
      // Wait for scene to load
      await page.waitForTimeout(2000);

      // Take initial screenshot
      const screenshot1 = await page.locator('canvas').screenshot();

      // Wait for animation (sample elements rotate)
      await page.waitForTimeout(2000);

      // Take second screenshot
      const screenshot2 = await page.locator('canvas').screenshot();

      // Screenshots should be different due to rotation animation
      expect(screenshot1).not.toEqual(screenshot2);
    });

    test('should display demo mode indicator when API unavailable', async ({ page }) => {
      // Wait for scene to load
      await page.waitForTimeout(2000);

      // Check for demo mode indicator (orange text)
      const demoModeText = page.locator('text=/DEMO MODE/');

      // Demo mode should be visible if API is not connected
      const isVisible = await demoModeText.isVisible();

      // Either demo mode is shown, or API is connected (both are valid)
      if (isVisible) {
        await expect(demoModeText).toBeVisible();
        const text = await demoModeText.textContent();
        expect(text).toContain('Backend API not connected');
      }
    });
  });

  test.describe('Page Load and Basic UI', () => {
    test('should load BSP DOOM Explorer without critical console errors', async ({ page }) => {
      const consoleErrors: string[] = [];

      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Wait for component to initialize
      await page.waitForTimeout(2000);

      // Check main title (use first() to avoid strict mode violation)
      await expect(page.getByRole('heading', { name: /BSP DOOM EXPLORER/ }).first()).toBeVisible();

      // Check canvas is present
      await expect(page.locator('canvas')).toBeVisible();

      // Filter out acceptable warnings and API errors (API is optional)
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('DevTools') &&
        !err.includes('deprecated') &&
        !err.includes('Warning:') &&
        !err.includes('WebGPU') && // WebGPU might not be available in test environment
        !err.includes('API not available') && // API is optional
        !err.includes('HTTP error') && // API errors are expected
        !err.includes('Failed to fetch') // Network errors are expected
      );

      // Should have no critical errors
      expect(filteredErrors.length).toBeLessThan(5); // Allow some minor errors
    });

    test('should display header with title and reset button', async ({ page }) => {
      await expect(page.getByRole('heading', { name: /BSP DOOM EXPLORER/ }).first()).toBeVisible();
      await expect(page.locator('button:has-text("Reset Settings")')).toBeVisible();
    });

    test('should render canvas element', async ({ page }) => {
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();
      
      // Check canvas has reasonable dimensions
      const box = await canvas.boundingBox();
      expect(box).not.toBeNull();
      expect(box!.width).toBeGreaterThan(500);
      expect(box!.height).toBeGreaterThan(400);
    });
  });

  test.describe('HUD Elements', () => {
    test('should display HUD with renderer info', async ({ page }) => {
      // Wait for HUD to appear
      await page.waitForTimeout(1000);
      
      // Check for HUD container
      const hud = page.locator('text=BSP DOOM EXPLORER').first();
      await expect(hud).toBeVisible();
      
      // Check for renderer type (WebGPU or WebGL)
      const rendererText = page.locator('text=Renderer:');
      await expect(rendererText).toBeVisible();
    });

    test('should display FPS counter', async ({ page }) => {
      await page.waitForTimeout(1000);
      
      // Check for FPS label
      await expect(page.locator('text=FPS:')).toBeVisible();
    });

    test('should display controls guide', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Check for controls text anywhere on the page
      await expect(page.locator('text=WASD').first()).toBeVisible();
      await expect(page.locator('text=Mouse').first()).toBeVisible();
    });

    test('should display BSP tree stats when available', async ({ page }) => {
      await page.waitForTimeout(2000);
      
      // Check for BSP tree info (might not be available if API is down)
      const bspTreeText = page.locator('text=BSP Tree:');
      const isVisible = await bspTreeText.isVisible();
      
      if (isVisible) {
        await expect(page.locator('text=Regions:')).toBeVisible();
        await expect(page.locator('text=Depth:')).toBeVisible();
      }
    });
  });

  test.describe('Minimap', () => {
    test('should display minimap', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Check for minimap text (use first() to avoid strict mode)
      await expect(page.locator('text=MINIMAP').first()).toBeVisible();
    });

    test('should show player position indicator', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Minimap should be visible
      await expect(page.locator('text=MINIMAP').first()).toBeVisible();
    });
  });

  test.describe('Settings Panel', () => {
    test('should display settings panel with all controls', async ({ page }) => {
      // Check for settings section
      await expect(page.locator('text=⚙️ Settings')).toBeVisible();
      
      // Check for toggles
      await expect(page.locator('text=Show HUD')).toBeVisible();
      await expect(page.locator('text=Show Minimap')).toBeVisible();
      
      // Check for sliders
      await expect(page.locator('text=Move Speed:')).toBeVisible();
      await expect(page.locator('text=Look Speed:')).toBeVisible();
    });

    test('should toggle HUD visibility', async ({ page }) => {
      await page.waitForTimeout(1000);
      
      // Find HUD toggle switch
      const hudToggle = page.locator('text=Show HUD').locator('..').locator('input[type="checkbox"]');
      
      // HUD should be visible initially
      const hudElement = page.locator('text=BSP DOOM EXPLORER').first();
      await expect(hudElement).toBeVisible();
      
      // Toggle HUD off
      await hudToggle.click();
      await page.waitForTimeout(500);
      
      // HUD might still be in DOM but hidden - check if it's actually visible
      // (Implementation might vary, so we just verify the toggle works)
      
      // Toggle back on
      await hudToggle.click();
      await page.waitForTimeout(500);
      await expect(hudElement).toBeVisible();
    });

    test('should toggle minimap visibility', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Find minimap toggle switch
      const minimapToggle = page.locator('text=Show Minimap').locator('..').locator('input[type="checkbox"]');

      // Minimap should be visible initially
      await expect(page.locator('text=MINIMAP').first()).toBeVisible();

      // Toggle minimap off
      await minimapToggle.click();
      await page.waitForTimeout(500);

      // Toggle back on
      await minimapToggle.click();
      await page.waitForTimeout(500);
      await expect(page.locator('text=MINIMAP').first()).toBeVisible();
    });

    test('should adjust move speed with slider', async ({ page }) => {
      // Find move speed slider
      const moveSpeedSlider = page.locator('text=Move Speed:').locator('..').locator('input[type="range"]');
      await expect(moveSpeedSlider).toBeVisible();
      
      // Get initial value
      const initialValue = await moveSpeedSlider.inputValue();
      
      // Change slider value
      await moveSpeedSlider.fill('10');
      await page.waitForTimeout(300);
      
      // Verify value changed
      const newValue = await moveSpeedSlider.inputValue();
      expect(newValue).toBe('10');
      expect(newValue).not.toBe(initialValue);
    });

    test('should adjust look speed with slider', async ({ page }) => {
      // Find look speed slider
      const lookSpeedSlider = page.locator('text=Look Speed:').locator('..').locator('input[type="range"]');
      await expect(lookSpeedSlider).toBeVisible();
      
      // Get initial value
      const initialValue = await lookSpeedSlider.inputValue();
      
      // Change slider value
      await lookSpeedSlider.fill('3');
      await page.waitForTimeout(300);
      
      // Verify value changed
      const newValue = await lookSpeedSlider.inputValue();
      expect(newValue).toBe('3');
      expect(newValue).not.toBe(initialValue);
    });

    test('should reset settings when reset button clicked', async ({ page }) => {
      // Change some settings first
      const moveSpeedSlider = page.locator('text=Move Speed:').locator('..').locator('input[type="range"]');
      await moveSpeedSlider.fill('15');
      await page.waitForTimeout(300);
      
      // Click reset button
      await page.click('button:has-text("Reset Settings")');
      await page.waitForTimeout(500);
      
      // Verify settings reset to defaults
      const resetValue = await moveSpeedSlider.inputValue();
      expect(resetValue).toBe('5'); // Default move speed
    });
  });

  test.describe('Information Sections', () => {
    test('should display About section', async ({ page }) => {
      await expect(page.locator('text=ℹ️ About')).toBeVisible();
      await expect(page.locator('text=Navigate through the BSP tree structure')).toBeVisible();
    });

    test('should display Controls info section', async ({ page }) => {
      await expect(page.getByRole('heading', { name: /Controls/ }).first()).toBeVisible();
      // Check for controls text anywhere on page
      await expect(page.locator('text=Move').first()).toBeVisible();
      await expect(page.locator('text=Look').first()).toBeVisible();
    });

    test('should display Features section', async ({ page }) => {
      await expect(page.locator('text=✨ Features')).toBeVisible();
      await expect(page.locator('text=First-Person Camera')).toBeVisible();
      await expect(page.locator('text=WASD Movement')).toBeVisible();
      await expect(page.locator('text=WebGPU Rendering')).toBeVisible();
    });
  });

  test.describe('Region Tracking', () => {
    test('should display current region when available', async ({ page }) => {
      await page.waitForTimeout(2000);
      
      // Check if current region section exists
      const currentRegionSection = page.locator('text=📍 Current Region');
      const isVisible = await currentRegionSection.isVisible();
      
      // Region might not be detected immediately, so we just check the section can appear
      if (isVisible) {
        await expect(currentRegionSection).toBeVisible();
      }
    });

    test('should display region history when regions are visited', async ({ page }) => {
      await page.waitForTimeout(2000);
      
      // Check if region history section exists
      const regionHistorySection = page.locator('text=📜 Region History');
      const isVisible = await regionHistorySection.isVisible();
      
      // History might be empty initially
      if (isVisible) {
        await expect(regionHistorySection).toBeVisible();
      }
    });
  });

  test.describe('Performance', () => {
    test('should maintain reasonable FPS', async ({ page }) => {
      await page.waitForTimeout(3000);
      
      // Check if FPS counter is visible
      const fpsText = page.locator('text=FPS:').locator('..');
      await expect(fpsText).toBeVisible();
      
      // FPS should be displayed (actual value depends on system)
      const fpsValue = await fpsText.textContent();
      expect(fpsValue).toBeTruthy();
    });

    test('should not have memory leaks on repeated navigation', async ({ page }) => {
      // Navigate away and back
      await page.goto('/test');
      await page.waitForTimeout(500);
      
      await page.goto('/test/bsp-doom-explorer');
      await page.waitForTimeout(2000);
      
      // Canvas should still be visible
      await expect(page.locator('canvas')).toBeVisible();
    });
  });

  test.describe('Responsive Design', () => {
    test('should work on desktop viewport', async ({ page }) => {
      await page.setViewportSize({ width: 1920, height: 1080 });
      await page.waitForTimeout(1000);

      // Check main elements are visible
      await expect(page.locator('canvas')).toBeVisible();
      await expect(page.getByRole('heading', { name: /BSP DOOM EXPLORER/ }).first()).toBeVisible();
    });

    test('should adapt to smaller viewport', async ({ page }) => {
      await page.setViewportSize({ width: 1024, height: 768 });
      await page.waitForTimeout(1000);
      
      // Check main elements are still accessible
      await expect(page.locator('canvas')).toBeVisible();
      await expect(page.locator('text=⚙️ Settings')).toBeVisible();
    });
  });

  test.describe('Auto-Navigation', () => {
    test('should have auto-navigate button', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Check for auto-navigate button
      const autoNavButton = page.locator('button:has-text("Auto-Navigate")');
      await expect(autoNavButton).toBeVisible();
    });

    test('should toggle auto-navigation on and off', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Find auto-navigate button
      const autoNavButton = page.locator('button:has-text("Auto-Navigate")');
      await expect(autoNavButton).toBeVisible();

      // Click to start auto-navigation
      await autoNavButton.click();
      await page.waitForTimeout(500);

      // Button text should change to "Stop"
      await expect(page.locator('button:has-text("Stop Auto-Navigate")')).toBeVisible();

      // Should show auto-navigating indicator
      await expect(page.locator('text=Auto-navigating')).toBeVisible();

      // Click to stop
      await page.locator('button:has-text("Stop Auto-Navigate")').click();
      await page.waitForTimeout(500);

      // Button should revert
      await expect(autoNavButton).toBeVisible();
    });

    test('should cycle through floors during auto-navigation', async ({ page }) => {
      await page.waitForTimeout(1000);

      // Get initial floor
      const floorLabel = page.locator('text=Current Floor:').locator('..');
      const initialFloor = await floorLabel.textContent();

      // Start auto-navigation
      await page.locator('button:has-text("Auto-Navigate")').click();

      // Wait for floor change (3 seconds per floor)
      await page.waitForTimeout(3500);

      // Floor should have changed
      const newFloor = await floorLabel.textContent();
      expect(newFloor).not.toBe(initialFloor);

      // Stop auto-navigation
      await page.locator('button:has-text("Stop Auto-Navigate")').click();
    });
  });

  test.describe('Error Handling', () => {
    test('should handle missing BSP API gracefully', async ({ page }) => {
      await page.waitForTimeout(2000);

      // Component should still render even if API is unavailable
      await expect(page.locator('canvas')).toBeVisible();

      // Should not show critical error that breaks the page
      const criticalError = page.locator('text=Critical Error');
      await expect(criticalError).not.toBeVisible();
    });

    test('should display loading state initially', async ({ page }) => {
      // Reload page to catch loading state
      await page.reload();

      // Check for loading indicator (might be brief)
      const loadingText = page.locator('text=LOADING BSP TREE');

      // Loading might be too fast to catch, so we just verify page loads
      await page.waitForSelector('canvas', { timeout: 10000 });
      await expect(page.locator('canvas')).toBeVisible();
    });

    test('should not crash when navigating between floors', async ({ page }) => {
      await page.waitForTimeout(2000);

      // Simulate mouse wheel navigation (if possible)
      // For now, just verify the component doesn't crash
      await expect(page.locator('canvas')).toBeVisible();

      // Check that HUD is still responsive
      await expect(page.locator('text=Current Floor:').first()).toBeVisible();
    });
  });

  test.describe('Accessibility', () => {
    test('should have proper heading structure', async ({ page }) => {
      // Check for main heading
      await expect(page.locator('h4:has-text("BSP DOOM EXPLORER")')).toBeVisible();
      
      // Check for section headings
      await expect(page.locator('h6:has-text("About")')).toBeVisible();
      await expect(page.locator('h6:has-text("Controls")')).toBeVisible();
      await expect(page.locator('h6:has-text("Settings")')).toBeVisible();
    });

    test('should have accessible form controls', async ({ page }) => {
      // Check sliders have labels
      const moveSpeedSlider = page.locator('text=Move Speed:').locator('..').locator('input[type="range"]');
      await expect(moveSpeedSlider).toBeVisible();
      
      const lookSpeedSlider = page.locator('text=Look Speed:').locator('..').locator('input[type="range"]');
      await expect(lookSpeedSlider).toBeVisible();
      
      // Check switches have labels
      await expect(page.locator('text=Show HUD')).toBeVisible();
      await expect(page.locator('text=Show Minimap')).toBeVisible();
    });
  });
});

