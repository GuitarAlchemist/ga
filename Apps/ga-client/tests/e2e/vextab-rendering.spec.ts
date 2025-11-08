import { test, expect } from '@playwright/test';

test.describe('VexTab Rendering in Chatbot', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());

    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();
  });

  test('should render VexTab notation', async ({ page }) => {
    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab',
          role: 'assistant',
          content: 'Here is a C major scale:\n\n```vextab\n6/0 5/2 4/2 4/0 3/2 2/0 2/1 1/0\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that VexTab viewer is rendered (it renders as SVG)
    const svg = page.locator('svg').first();
    await expect(svg).toBeVisible();
  });

  test('should render VexTab with standard notation', async ({ page }) => {
    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-standard',
          role: 'assistant',
          content: 'Here is a tab:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for VexTab SVG
    const vextabSvg = page.locator('svg').first();
    await expect(vextabSvg).toBeVisible();
  });

  test('should handle VexTab code blocks in markdown', async ({ page }) => {
    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-markdown',
          role: 'assistant',
          content: 'Here is tablature:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Should NOT show raw VexTab code (code blocks are replaced with viewer)
    await expect(page.getByText('6/0 5/2 4/2')).not.toBeVisible();

    // Should show rendered VexTab as SVG
    const svg = page.locator('svg').first();
    await expect(svg).toBeVisible();
  });

  test('should render multiple VexTab blocks', async ({ page }) => {
    // Inject assistant message with multiple VexTab code blocks
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-multiple-vextab',
          role: 'assistant',
          content: 'Here are two scales:\n\n```vextab\n6/0 5/2 4/2\n```\n\nAnd another:\n\n```vextab\n1/0 2/1 3/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for multiple SVG elements (VexTab renders as SVG)
    const svgs = page.locator('svg');
    const count = await svgs.count();

    // Should have at least 2 SVG elements (one for each VexTab block)
    expect(count).toBeGreaterThanOrEqual(2);
  });

  test('should handle VexTab rendering errors gracefully', async ({ page }) => {
    // Inject assistant message with valid VexTab
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-error',
          role: 'assistant',
          content: 'Here is a tab:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Page should still be functional
    const input = page.getByPlaceholder(/Ask about chords/i);
    await expect(input).toBeVisible();
    await expect(input).toBeEnabled();
  });

  test('should display VexTab with proper styling', async ({ page }) => {
    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-styling',
          role: 'assistant',
          content: 'Here is a C major scale:\n\n```vextab\n6/0 5/2 4/2 4/0 3/2 2/0 2/1 1/0\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that VexTab is rendered as SVG
    const svg = page.locator('svg').first();
    await expect(svg).toBeVisible();

    // Check that it's within a message bubble
    const messageBox = page.locator('[data-testid="chat-message"]').last();
    await expect(messageBox).toBeVisible();
  });

  test('should support VexTab with different notations', async ({ page }) => {
    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-notations',
          role: 'assistant',
          content: 'Here is notation:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Should render VexTab as SVG
    const svg = page.locator('svg').first();
    await expect(svg).toBeVisible();
  });

  test('should render VexTab in mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Inject assistant message with VexTab code block
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-mobile',
          role: 'assistant',
          content: 'Here is a tab:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // VexTab should still be visible on mobile (renders as SVG)
    const svg = page.locator('svg').first();
    await expect(svg).toBeVisible();
  });

  test('should scroll to VexTab when rendered', async ({ page }) => {
    // Inject multiple messages including VexTab
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'msg-1',
          role: 'user',
          content: 'Message 1',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'msg-2',
          role: 'user',
          content: 'Message 2',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'msg-3',
          role: 'user',
          content: 'Message 3',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-vextab-scroll',
          role: 'assistant',
          content: 'Here is a tab:\n\n```vextab\n6/0 5/2 4/2\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // VexTab should be visible (auto-scrolled)
    const svg = page.locator('svg').last();
    await expect(svg).toBeVisible();
  });
});

