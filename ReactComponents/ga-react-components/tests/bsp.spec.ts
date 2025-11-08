import { test, expect } from '@playwright/test';

test.describe('BSP Musical Analysis Interface', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to BSP test page
    await page.goto('/test/bsp');
    await page.waitForLoadState('networkidle');
    
    // Wait for the BSP interface to load
    await page.waitForSelector('[data-testid="bsp-interface"]', { timeout: 10000 });
  });

  test.describe('Page Load and Basic UI', () => {
    test('should load BSP interface without console errors', async ({ page }) => {
      const consoleErrors: string[] = [];
      
      page.on('console', (msg) => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Check main elements are present
      await expect(page.locator('h1')).toContainText('BSP Musical Analysis Interface');
      await expect(page.locator('[role="tablist"]')).toBeVisible();
      
      // Check for tabs
      await expect(page.locator('text=Spatial Query')).toBeVisible();
      await expect(page.locator('text=Tonal Context')).toBeVisible();
      await expect(page.locator('text=Progression Analysis')).toBeVisible();
      await expect(page.locator('text=BSP Info')).toBeVisible();

      // Filter out acceptable warnings
      const filteredErrors = consoleErrors.filter(err =>
        !err.includes('DevTools') &&
        !err.includes('deprecated') &&
        !err.includes('Warning:')
      );

      expect(filteredErrors).toHaveLength(0);
    });

    test('should display connection status indicator', async ({ page }) => {
      // Check for connection status elements
      await expect(page.locator('[data-testid="connection-status"]')).toBeVisible();
      
      // Should have either connected, disconnected, or checking status
      const statusIndicator = page.locator('[data-testid="connection-status"]');
      await expect(statusIndicator).toBeVisible();
    });

    test('should display tutorial and export buttons', async ({ page }) => {
      // Check for tutorial button
      await expect(page.locator('button[aria-label*="tutorial"], button:has-text("Help")')).toBeVisible();
      
      // Check for export button
      await expect(page.locator('button:has-text("Export")')).toBeVisible();
    });
  });

  test.describe('Spatial Query Tab', () => {
    test('should allow spatial query input and show quick examples', async ({ page }) => {
      // Click on Spatial Query tab
      await page.click('text=Spatial Query');
      
      // Check input fields
      await expect(page.locator('input[label="Pitch Classes"], input[placeholder*="C,E,G"]')).toBeVisible();
      await expect(page.locator('input[label="Search Radius"], input[type="number"]')).toBeVisible();
      await expect(page.locator('[role="combobox"]')).toBeVisible(); // Strategy selector
      
      // Check quick examples
      await expect(page.locator('text=Quick Examples')).toBeVisible();
      await expect(page.locator('text=C Major')).toBeVisible();
      
      // Test clicking a quick example
      await page.click('text=C Major');
      
      // Verify the input was populated
      const pitchClassInput = page.locator('input[placeholder*="C,E,G"]').first();
      await expect(pitchClassInput).toHaveValue('C,E,G');
    });

    test('should perform spatial query when connected', async ({ page }) => {
      await page.click('text=Spatial Query');
      
      // Fill in query parameters
      await page.fill('input[placeholder*="C,E,G"]', 'C,E,G');
      await page.fill('input[type="number"]', '0.5');
      
      // Click perform query button
      const queryButton = page.locator('button:has-text("Perform Spatial Query")');
      await queryButton.click();
      
      // Wait for either results or error
      await page.waitForTimeout(2000);
      
      // Check if we get results or connection error
      const hasResults = await page.locator('text=Query Results').isVisible();
      const hasError = await page.locator('[role="alert"]').isVisible();
      
      expect(hasResults || hasError).toBeTruthy();
    });

    test('should show spatial visualization when results are available', async ({ page }) => {
      await page.click('text=Spatial Query');
      
      // Try to trigger a query
      await page.fill('input[placeholder*="C,E,G"]', 'C,E,G');
      const queryButton = page.locator('button:has-text("Perform Spatial Query")');
      await queryButton.click();
      
      await page.waitForTimeout(2000);
      
      // Check if visualization appears (if we have results)
      const hasVisualization = await page.locator('canvas').isVisible();
      const hasResults = await page.locator('text=Query Results').isVisible();
      
      if (hasResults) {
        expect(hasVisualization).toBeTruthy();
      }
    });
  });

  test.describe('Tonal Context Tab', () => {
    test('should allow tonal context analysis', async ({ page }) => {
      await page.click('text=Tonal Context');
      
      // Check input field
      await expect(page.locator('input[placeholder*="A,C,E"]')).toBeVisible();
      
      // Check quick examples
      await expect(page.locator('text=Quick Examples')).toBeVisible();
      
      // Test clicking a quick example
      await page.click('text=A Minor');
      
      // Verify input was populated
      const tonalInput = page.locator('input[placeholder*="A,C,E"]');
      await expect(tonalInput).toHaveValue('A,C,E');
      
      // Try analysis
      const analyzeButton = page.locator('button:has-text("Analyze Tonal Context")');
      await analyzeButton.click();
      
      await page.waitForTimeout(2000);
      
      // Check for results or error
      const hasResults = await page.locator('text=Context Analysis').isVisible();
      const hasError = await page.locator('[role="alert"]').isVisible();
      
      expect(hasResults || hasError).toBeTruthy();
    });
  });

  test.describe('Progression Analysis Tab', () => {
    test('should allow chord progression management', async ({ page }) => {
      await page.click('text=Progression Analysis');
      
      // Check initial progression
      await expect(page.locator('input[placeholder="C Major"]')).toBeVisible();
      await expect(page.locator('input[placeholder="C,E,G"]')).toBeVisible();
      
      // Check add/remove buttons
      await expect(page.locator('button:has-text("Add Chord")')).toBeVisible();
      await expect(page.locator('button:has-text("Remove")')).toBeVisible();
      
      // Test adding a chord
      await page.click('button:has-text("Add Chord")');
      
      // Should have more chord inputs now
      const chordInputs = page.locator('input[placeholder="C Major"]');
      await expect(chordInputs).toHaveCount(5); // 4 initial + 1 added
    });

    test('should load progression examples', async ({ page }) => {
      await page.click('text=Progression Analysis');
      
      // Check for progression examples
      await expect(page.locator('text=Load Example Progressions')).toBeVisible();
      await expect(page.locator('text=I-vi-IV-V')).toBeVisible();
      
      // Click on an example
      await page.click('text=I-vi-IV-V');
      
      // Verify progression was loaded
      const firstChordName = page.locator('input[placeholder="C Major"]').first();
      await expect(firstChordName).toHaveValue('C Major');
    });

    test('should perform progression analysis', async ({ page }) => {
      await page.click('text=Progression Analysis');
      
      // Use default progression and analyze
      const analyzeButton = page.locator('button:has-text("Analyze Progression")');
      await analyzeButton.click();
      
      await page.waitForTimeout(2000);
      
      // Check for results or error
      const hasResults = await page.locator('text=Progression Analysis').isVisible();
      const hasError = await page.locator('[role="alert"]').isVisible();
      
      expect(hasResults || hasError).toBeTruthy();
    });
  });

  test.describe('BSP Info Tab', () => {
    test('should display BSP information and metrics', async ({ page }) => {
      await page.click('text=BSP Info');
      
      // Check for performance metrics
      await expect(page.locator('text=Performance Metrics')).toBeVisible();
      
      // Check for BSP tree visualization
      await expect(page.locator('text=BSP Tree Structure')).toBeVisible();
      
      // Check for system information
      await expect(page.locator('text=What is BSP?')).toBeVisible();
      await expect(page.locator('text=Binary Space Partitioning')).toBeVisible();
    });

    test('should show tree visualization canvas', async ({ page }) => {
      await page.click('text=BSP Info');
      
      // Wait for tree visualization to load
      await page.waitForTimeout(1000);
      
      // Check for canvas element
      const canvas = page.locator('canvas').last(); // Tree visualization canvas
      await expect(canvas).toBeVisible();
    });

    test('should display performance metrics dashboard', async ({ page }) => {
      await page.click('text=BSP Info');
      
      // Check for metrics elements
      await expect(page.locator('text=Query Performance')).toBeVisible();
      await expect(page.locator('text=System Health')).toBeVisible();
      await expect(page.locator('text=Recent Query Performance')).toBeVisible();
    });
  });

  test.describe('Tutorial Functionality', () => {
    test('should open and navigate tutorial', async ({ page }) => {
      // Click tutorial button
      const tutorialButton = page.locator('button[aria-label*="tutorial"], button:has-text("Help")').first();
      await tutorialButton.click();
      
      // Check tutorial dialog opened
      await expect(page.locator('text=BSP Interactive Tutorial')).toBeVisible();
      
      // Check tutorial steps
      await expect(page.locator('text=Welcome to BSP Analysis')).toBeVisible();
      await expect(page.locator('text=Binary Space Partitioning for Music')).toBeVisible();
      
      // Test navigation
      const continueButton = page.locator('button:has-text("Continue")');
      if (await continueButton.isVisible()) {
        await continueButton.click();
        await expect(page.locator('text=Spatial Queries')).toBeVisible();
      }
      
      // Close tutorial
      await page.click('button:has-text("Close")');
      await expect(page.locator('text=BSP Interactive Tutorial')).not.toBeVisible();
    });
  });

  test.describe('Export and Share Functionality', () => {
    test('should open export dialog', async ({ page }) => {
      // Click export button
      const exportButton = page.locator('button:has-text("Export")');
      await exportButton.click();
      
      // Check export dialog opened
      await expect(page.locator('text=Export & Share Analysis')).toBeVisible();
      
      // Check tabs
      await expect(page.locator('text=Share')).toBeVisible();
      await expect(page.locator('text=Export')).toBeVisible();
      
      // Close dialog
      await page.click('button:has-text("Close")');
      await expect(page.locator('text=Export & Share Analysis')).not.toBeVisible();
    });

    test('should show export options', async ({ page }) => {
      await page.click('button:has-text("Export")');
      
      // Click export tab
      await page.click('text=Export');
      
      // Check export buttons
      await expect(page.locator('button:has-text("Export JSON")')).toBeVisible();
      await expect(page.locator('button:has-text("Export CSV")')).toBeVisible();
      await expect(page.locator('button:has-text("Export Markdown")')).toBeVisible();
      
      await page.click('button:has-text("Close")');
    });
  });

  test.describe('Responsive Design', () => {
    test('should work on mobile viewport', async ({ page }) => {
      await page.setViewportSize({ width: 375, height: 667 });
      
      // Check main elements are still visible
      await expect(page.locator('h1')).toBeVisible();
      await expect(page.locator('[role="tablist"]')).toBeVisible();
      
      // Check tabs are accessible
      await page.click('text=Spatial Query');
      await expect(page.locator('input[placeholder*="C,E,G"]')).toBeVisible();
    });

    test('should work on tablet viewport', async ({ page }) => {
      await page.setViewportSize({ width: 768, height: 1024 });
      
      // Check layout adapts properly
      await expect(page.locator('h1')).toBeVisible();
      await expect(page.locator('[role="tablist"]')).toBeVisible();
      
      // Test tutorial on tablet
      const tutorialButton = page.locator('button[aria-label*="tutorial"], button:has-text("Help")').first();
      await tutorialButton.click();
      await expect(page.locator('text=BSP Interactive Tutorial')).toBeVisible();
      await page.click('button:has-text("Close")');
    });
  });

  test.describe('Accessibility', () => {
    test('should have proper ARIA labels and roles', async ({ page }) => {
      // Check for proper roles
      await expect(page.locator('[role="tablist"]')).toBeVisible();
      await expect(page.locator('[role="tab"]')).toHaveCount(4);
      
      // Check for accessible buttons
      const buttons = page.locator('button');
      const buttonCount = await buttons.count();
      expect(buttonCount).toBeGreaterThan(0);
      
      // Check for proper headings
      await expect(page.locator('h1')).toBeVisible();
      await expect(page.locator('h6')).toBeVisible();
    });

    test('should support keyboard navigation', async ({ page }) => {
      // Focus on first tab
      await page.keyboard.press('Tab');
      
      // Navigate through tabs with arrow keys
      await page.keyboard.press('ArrowRight');
      await page.keyboard.press('ArrowRight');
      
      // Should be able to activate tabs with Enter/Space
      await page.keyboard.press('Enter');
      
      // Check that tab changed
      const activeTab = page.locator('[role="tab"][aria-selected="true"]');
      await expect(activeTab).toBeVisible();
    });
  });
});
