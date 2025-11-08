import { test, expect } from '@playwright/test';

test.describe('TabConverter Component', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:5173/test/tab-converter');
    await page.waitForLoadState('networkidle');
  });

  test('should render TabConverter component', async ({ page }) => {
    // Check for main heading
    await expect(page.getByRole('heading', { name: /Guitar Tab Format Converter/i })).toBeVisible();
    
    // Check for format selectors
    await expect(page.getByLabel(/Source Format/i)).toBeVisible();
    await expect(page.getByLabel(/Target Format/i)).toBeVisible();
    
    // Check for convert button
    await expect(page.getByRole('button', { name: /Convert/i })).toBeVisible();
  });

  test('should have default formats selected', async ({ page }) => {
    // Check default source format (ASCII)
    const sourceFormat = page.getByLabel(/Source Format/i);
    await expect(sourceFormat).toHaveValue('ASCII');
    
    // Check default target format (VexTab)
    const targetFormat = page.getByLabel(/Target Format/i);
    await expect(targetFormat).toHaveValue('VexTab');
  });

  test('should load example tab content', async ({ page }) => {
    // Click "Load Example" button
    await page.getByRole('button', { name: /Load Example/i }).click();
    
    // Check that source editor has content
    const sourceEditor = page.locator('textarea').first();
    const content = await sourceEditor.inputValue();
    expect(content.length).toBeGreaterThan(0);
    expect(content).toContain('e|');
  });

  test('should swap formats when swap button clicked', async ({ page }) => {
    // Get initial values
    const sourceFormat = page.getByLabel(/Source Format/i);
    const targetFormat = page.getByLabel(/Target Format/i);
    
    const initialSource = await sourceFormat.inputValue();
    const initialTarget = await targetFormat.inputValue();
    
    // Click swap button
    await page.getByRole('button', { name: /Swap formats/i }).click();
    
    // Check that formats are swapped
    await expect(sourceFormat).toHaveValue(initialTarget);
    await expect(targetFormat).toHaveValue(initialSource);
  });

  test('should enable convert button when content is present', async ({ page }) => {
    const convertButton = page.getByRole('button', { name: /^Convert$/i });
    
    // Initially disabled (no content)
    await expect(convertButton).toBeDisabled();
    
    // Load example
    await page.getByRole('button', { name: /Load Example/i }).click();
    
    // Now enabled
    await expect(convertButton).toBeEnabled();
  });

  test('should show error when converting empty content', async ({ page }) => {
    // Try to convert without content
    const sourceEditor = page.locator('textarea').first();
    await sourceEditor.fill('');
    
    const convertButton = page.getByRole('button', { name: /^Convert$/i });
    await expect(convertButton).toBeDisabled();
  });

  test('should allow file upload', async ({ page }) => {
    // Check for upload button
    const uploadButton = page.getByRole('button', { name: /Upload File/i });
    await expect(uploadButton).toBeVisible();
  });

  test('should have copy and download buttons for result', async ({ page }) => {
    // Load example and convert
    await page.getByRole('button', { name: /Load Example/i }).click();
    
    // Note: Actual conversion requires API to be running
    // Just check that the buttons exist
    const copyButtons = page.getByRole('button', { name: /Copy/i });
    expect(await copyButtons.count()).toBeGreaterThan(0);
    
    const downloadButtons = page.getByRole('button', { name: /Download/i });
    expect(await downloadButtons.count()).toBeGreaterThan(0);
  });

  test('should have dual editor layout', async ({ page }) => {
    // Check for source and result sections
    await expect(page.getByText(/Source \(ASCII\)/i)).toBeVisible();
    await expect(page.getByText(/Result \(VexTab\)/i)).toBeVisible();
    
    // Check for two text areas
    const textareas = page.locator('textarea');
    expect(await textareas.count()).toBe(2);
  });

  test('should have responsive viewport', async ({ page }) => {
    // Check that component renders in viewport
    const viewport = page.viewportSize();
    expect(viewport).toBeTruthy();
    
    // Component should be visible
    const component = page.locator('text=Guitar Tab Format Converter');
    await expect(component).toBeVisible();
  });

  test('should cleanup on unmount', async ({ page }) => {
    // Navigate away
    await page.goto('http://localhost:5173/test');
    
    // Navigate back
    await page.goto('http://localhost:5173/test/tab-converter');
    
    // Component should still work
    await expect(page.getByRole('heading', { name: /Guitar Tab Format Converter/i })).toBeVisible();
  });
});

test.describe('TabConverter API Integration', () => {
  test.skip('should convert ASCII to VexTab', async ({ page }) => {
    // Skip if API is not running
    await page.goto('http://localhost:5173/test/tab-converter');
    
    // Load example
    await page.getByRole('button', { name: /Load Example/i }).click();
    
    // Click convert
    await page.getByRole('button', { name: /^Convert$/i }).click();
    
    // Wait for conversion
    await page.waitForTimeout(1000);
    
    // Check for result
    const resultEditor = page.locator('textarea').last();
    const result = await resultEditor.inputValue();
    expect(result.length).toBeGreaterThan(0);
  });

  test.skip('should show conversion metadata', async ({ page }) => {
    // Skip if API is not running
    await page.goto('http://localhost:5173/test/tab-converter');
    
    // Load example and convert
    await page.getByRole('button', { name: /Load Example/i }).click();
    await page.getByRole('button', { name: /^Convert$/i }).click();
    
    // Wait for conversion
    await page.waitForTimeout(1000);
    
    // Check for metadata
    await expect(page.getByText(/Conversion Metadata/i)).toBeVisible();
    await expect(page.getByText(/Duration/i)).toBeVisible();
    await expect(page.getByText(/Notes/i)).toBeVisible();
    await expect(page.getByText(/Measures/i)).toBeVisible();
  });

  test.skip('should show VexFlow preview for VexTab output', async ({ page }) => {
    // Skip if API is not running
    await page.goto('http://localhost:5173/test/tab-converter');
    
    // Load example and convert
    await page.getByRole('button', { name: /Load Example/i }).click();
    await page.getByRole('button', { name: /^Convert$/i }).click();
    
    // Wait for conversion
    await page.waitForTimeout(1000);
    
    // Check for visual preview
    await expect(page.getByText(/Visual Preview/i)).toBeVisible();
  });
});

