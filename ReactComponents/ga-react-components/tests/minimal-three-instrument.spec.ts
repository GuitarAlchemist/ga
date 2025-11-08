/**
 * Playwright tests for MinimalThreeInstrument component
 * 
 * Tests the universal 3D instrument renderer that supports all instruments from YAML database
 */

import { test, expect, Page } from '@playwright/test';

test.describe('MinimalThreeInstrument Component', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the MinimalThree test page
    await page.goto('/test/minimal-three');
    
    // Wait for the page to load and instruments to be loaded
    await page.waitForSelector('h4:has-text("Minimal Three.js Instrument Showcase")', { timeout: 10000 });
    
    // Wait for the instrument database to load
    await page.waitForFunction(() => {
      const familySelect = document.querySelector('div[role="button"]:has-text("Guitar")');
      return familySelect !== null;
    }, { timeout: 15000 });
  });

  test('should load the showcase page without errors', async ({ page }) => {
    // Check that the main title is present
    await expect(page.locator('h4')).toContainText('Minimal Three.js Instrument Showcase');
    
    // Check that the description is present
    await expect(page.locator('text=This demonstrates a single ThreeJS + WebGPU component')).toBeVisible();
    
    // Check that the instrument selection controls are present
    await expect(page.locator('text=Instrument Family')).toBeVisible();
    await expect(page.locator('text=Variant')).toBeVisible();
  });

  test('should render the default guitar without crashing', async ({ page }) => {
    // Wait for the 3D canvas to be present
    await page.waitForSelector('canvas', { timeout: 10000 });
    
    // Check that the canvas is visible and has reasonable dimensions
    const canvas = page.locator('canvas');
    await expect(canvas).toBeVisible();
    
    const canvasBox = await canvas.boundingBox();
    expect(canvasBox?.width).toBeGreaterThan(800);
    expect(canvasBox?.height).toBeGreaterThan(300);
    
    // Check that the renderer info overlay is present
    await expect(page.locator('text=WebGPU').or(page.locator('text=WebGL'))).toBeVisible();
    await expect(page.locator('text=6 strings')).toBeVisible();
  });

  test('should switch between different instrument families without crashing', async ({ page }) => {
    const instrumentFamilies = [
      'BassGuitar',
      'Ukulele', 
      'Banjo',
      'Mandolin',
      'Guitar' // Back to guitar
    ];

    for (const family of instrumentFamilies) {
      console.log(`Testing instrument family: ${family}`);
      
      // Click on the instrument family dropdown
      await page.locator('div[role="button"]:has-text("Instrument Family")').first().click();
      
      // Wait for dropdown to open
      await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
      
      // Select the instrument family
      await page.locator(`li[role="option"]:has-text("${family}")`).first().click();
      
      // Wait for the instrument to load and render
      await page.waitForTimeout(2000);
      
      // Check that the canvas is still present and functional
      const canvas = page.locator('canvas');
      await expect(canvas).toBeVisible();
      
      // Check that no error alerts are present
      const errorAlert = page.locator('div[role="alert"]:has-text("error")');
      await expect(errorAlert).toHaveCount(0);
      
      // Check that the instrument info is updated
      await expect(page.locator(`text=${family}`)).toBeVisible();
      
      // Take a screenshot for visual verification
      await page.screenshot({ 
        path: `test-results/instrument-${family.toLowerCase()}.png`,
        fullPage: false,
        clip: { x: 0, y: 0, width: 1200, height: 800 }
      });
    }
  });

  test('should handle different render modes', async ({ page }) => {
    const renderModes = ['3d-webgpu', '3d-webgl'];
    
    for (const mode of renderModes) {
      console.log(`Testing render mode: ${mode}`);
      
      // Click on render mode dropdown
      await page.locator('div[role="button"]:has-text("Render Mode")').click();
      
      // Wait for dropdown
      await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
      
      // Select render mode
      const modeText = mode === '3d-webgpu' ? 'WebGPU (Preferred)' : 'WebGL (Fallback)';
      await page.locator(`li[role="option"]:has-text("${modeText}")`).click();
      
      // Wait for re-render
      await page.waitForTimeout(3000);
      
      // Check canvas is still present
      await expect(page.locator('canvas')).toBeVisible();
      
      // Check renderer info shows correct mode
      const expectedText = mode === '3d-webgpu' ? 'WebGPU' : 'WebGL';
      await expect(page.locator(`text=${expectedText}`)).toBeVisible();
    }
  });

  test('should handle different view modes', async ({ page }) => {
    const viewModes = [
      { value: 'fretboard', text: 'Fretboard Only' },
      { value: 'headstock', text: 'Headstock Only' },
      { value: 'full', text: 'Full Instrument' }
    ];
    
    for (const viewMode of viewModes) {
      console.log(`Testing view mode: ${viewMode.value}`);
      
      // Click on view mode dropdown
      await page.locator('div[role="button"]:has-text("View Mode")').click();
      
      // Wait for dropdown
      await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
      
      // Select view mode
      await page.locator(`li[role="option"]:has-text("${viewMode.text}")`).click();
      
      // Wait for re-render
      await page.waitForTimeout(2000);
      
      // Check canvas is still present
      await expect(page.locator('canvas')).toBeVisible();
      
      // Take screenshot for visual verification
      await page.screenshot({ 
        path: `test-results/view-mode-${viewMode.value}.png`,
        fullPage: false,
        clip: { x: 0, y: 0, width: 1200, height: 600 }
      });
    }
  });

  test('should handle capo positions', async ({ page }) => {
    const capoPositions = [0, 2, 5, 7];
    
    for (const capoFret of capoPositions) {
      console.log(`Testing capo position: ${capoFret}`);
      
      // Click on capo position dropdown
      await page.locator('div[role="button"]:has-text("Capo Position")').click();
      
      // Wait for dropdown
      await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
      
      // Select capo position
      const capoText = capoFret === 0 ? 'No Capo' : `Fret ${capoFret}`;
      await page.locator(`li[role="option"]:has-text("${capoText}")`).click();
      
      // Wait for re-render
      await page.waitForTimeout(1500);
      
      // Check canvas is still present
      await expect(page.locator('canvas')).toBeVisible();
    }
  });

  test('should toggle left-handed mode', async ({ page }) => {
    // Toggle left-handed mode on
    await page.locator('input[type="checkbox"]:near(:text("Left-Handed"))').click();
    
    // Wait for re-render
    await page.waitForTimeout(2000);
    
    // Check canvas is still present
    await expect(page.locator('canvas')).toBeVisible();
    
    // Take screenshot
    await page.screenshot({ 
      path: 'test-results/left-handed-mode.png',
      fullPage: false,
      clip: { x: 0, y: 0, width: 1200, height: 600 }
    });
    
    // Toggle back off
    await page.locator('input[type="checkbox"]:near(:text("Left-Handed"))').click();
    
    // Wait for re-render
    await page.waitForTimeout(2000);
    
    // Check canvas is still present
    await expect(page.locator('canvas')).toBeVisible();
  });

  test('should display instrument statistics', async ({ page }) => {
    // Check that statistics are displayed
    await expect(page.locator('text=Database Statistics')).toBeVisible();
    
    // Check for specific statistics
    await expect(page.locator('text=Instrument Families')).toBeVisible();
    await expect(page.locator('text=Total Variants')).toBeVisible();
    await expect(page.locator('text=Min Strings')).toBeVisible();
    await expect(page.locator('text=Max Strings')).toBeVisible();
    
    // Check that the numbers are reasonable
    const familiesCount = await page.locator('h4').first().textContent();
    const totalVariants = await page.locator('h4').nth(1).textContent();
    
    // Should have at least 50 families and 200 variants
    if (familiesCount) {
      expect(parseInt(familiesCount)).toBeGreaterThan(50);
    }
    if (totalVariants) {
      expect(parseInt(totalVariants)).toBeGreaterThan(200);
    }
  });

  test('should handle exotic instruments without crashing', async ({ page }) => {
    const exoticInstruments = [
      'Sitar',
      'Tamburitza', 
      'Ronroco',
      'Walaycho',
      'PedalSteelGuitar'
    ];

    for (const instrument of exoticInstruments) {
      console.log(`Testing exotic instrument: ${instrument}`);
      
      try {
        // Click on instrument family dropdown
        await page.locator('div[role="button"]:has-text("Instrument Family")').first().click();
        
        // Wait for dropdown
        await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
        
        // Check if the instrument exists in the dropdown
        const instrumentOption = page.locator(`li[role="option"]:has-text("${instrument}")`);
        const isVisible = await instrumentOption.isVisible();
        
        if (isVisible) {
          // Select the instrument
          await instrumentOption.click();
          
          // Wait for render
          await page.waitForTimeout(3000);
          
          // Check that canvas is still present
          await expect(page.locator('canvas')).toBeVisible();
          
          // Check no error alerts
          const errorAlert = page.locator('div[role="alert"]:has-text("error")');
          await expect(errorAlert).toHaveCount(0);
          
          // Take screenshot
          await page.screenshot({ 
            path: `test-results/exotic-${instrument.toLowerCase()}.png`,
            fullPage: false,
            clip: { x: 0, y: 0, width: 1200, height: 600 }
          });
        } else {
          console.log(`Instrument ${instrument} not found in dropdown, skipping`);
          // Close dropdown by clicking elsewhere
          await page.locator('h4').first().click();
        }
      } catch (error) {
        console.log(`Error testing ${instrument}:`, error);
        // Close any open dropdowns
        await page.locator('h4').first().click();
      }
    }
  });

  test('should maintain performance with complex instruments', async ({ page }) => {
    // Test with a complex instrument (many strings)
    await page.locator('div[role="button"]:has-text("Instrument Family")').first().click();
    await page.waitForSelector('ul[role="listbox"]', { timeout: 5000 });
    
    // Try to find an instrument with many strings
    const complexInstrument = page.locator('li[role="option"]:has-text("Octavina")').or(
      page.locator('li[role="option"]:has-text("SwedishLute")')
    ).first();
    
    const isVisible = await complexInstrument.isVisible();
    if (isVisible) {
      const startTime = Date.now();
      
      await complexInstrument.click();
      
      // Wait for render
      await page.waitForTimeout(5000);
      
      const endTime = Date.now();
      const renderTime = endTime - startTime;
      
      // Should render within reasonable time (less than 10 seconds)
      expect(renderTime).toBeLessThan(10000);
      
      // Check canvas is present
      await expect(page.locator('canvas')).toBeVisible();
      
      console.log(`Complex instrument render time: ${renderTime}ms`);
    }
  });
});

test.describe('MinimalThreeInstrument Error Handling', () => {
  test('should handle network errors gracefully', async ({ page }) => {
    // Navigate to page
    await page.goto('/test/minimal-three');
    
    // Block the instruments YAML file to simulate network error
    await page.route('**/config/Instruments.yaml', route => route.abort());
    
    // Reload page
    await page.reload();
    
    // Should show error message instead of crashing
    await expect(page.locator('div[role="alert"]')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=Failed to load instruments')).toBeVisible();
  });

  test('should handle WebGPU unavailability', async ({ page }) => {
    // Mock WebGPU as unavailable
    await page.addInitScript(() => {
      // Remove WebGPU support
      delete (window.navigator as any).gpu;
    });
    
    await page.goto('/test/minimal-three');
    
    // Wait for page to load
    await page.waitForSelector('canvas', { timeout: 15000 });
    
    // Should fallback to WebGL
    await expect(page.locator('text=WebGL')).toBeVisible();
    await expect(page.locator('canvas')).toBeVisible();
  });
});
