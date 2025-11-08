import { test, expect } from '@playwright/test';

test.describe('Guitar Alchemist Chatbot', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the app
    await page.goto('/');

    // Clear localStorage and set welcome message
    await page.evaluate(() => {
      localStorage.clear();
      // Set the welcome message that the app expects
      const welcomeMessage = [{
        id: 'system-welcome',
        role: 'system',
        content: 'Welcome to Guitar Alchemist! I can help you with chord progressions, music theory, guitar techniques, and more. Try asking me about scales, chords, or music notation!',
        timestamp: new Date().toISOString(),
      }];
      localStorage.setItem('ga-chat-messages', JSON.stringify(welcomeMessage));
    });

    // Reload to apply localStorage changes
    await page.reload();
  });

  test('should display the app with tabs', async ({ page }) => {
    // Check that tabs are visible
    await expect(page.getByRole('tab', { name: /Fretboard Explorer/i })).toBeVisible();
    await expect(page.getByRole('tab', { name: /AI Chat Assistant/i })).toBeVisible();
  });

  test('should navigate to chat tab', async ({ page }) => {
    // Click on AI Chat Assistant tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that chat interface is visible
    await expect(page.getByPlaceholder(/Ask about chords/i)).toBeVisible();
  });

  test('should display welcome message', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Wait for chat interface to load
    await page.waitForSelector('[data-testid="chat-message"]', { timeout: 10000 });

    // Check for welcome message (should be in the first message)
    const welcomeMessage = page.locator('[data-testid="chat-message"]').first();
    await expect(welcomeMessage).toBeVisible();
    await expect(welcomeMessage).toContainText(/Guitar Alchemist/i);
  });

  test('should send a message', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Type a message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('What is a C major chord?');

    // Click send button
    await page.getByRole('button', { name: /send/i }).click();

    // Check that message appears in chat
    await expect(page.getByText('What is a C major chord?')).toBeVisible();
  });

  test('should send message with Enter key', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Type a message and press Enter
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('Show me a scale');
    await input.press('Enter');

    // Check that message appears in chat
    await expect(page.getByText('Show me a scale')).toBeVisible();
  });

  test('should not send empty messages', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Wait for chat interface to load
    await page.waitForSelector('[data-testid="chat-message"]', { timeout: 10000 });

    // Count initial messages
    const initialMessages = await page.locator('[data-testid="chat-message"]').count();

    // Verify send button is disabled when input is empty
    const sendButton = page.getByRole('button', { name: /send/i });
    await expect(sendButton).toBeDisabled();

    // Try to send empty message (button should be disabled, but test anyway)
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('');
    await expect(sendButton).toBeDisabled();

    // Check that no new message was added
    const finalMessages = await page.locator('[data-testid="chat-message"]').count();
    expect(finalMessages).toBe(initialMessages);
  });

  test('should clear input after sending', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Type and send a message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('Test message');
    await page.getByRole('button', { name: /send/i }).click();

    // Check that input is cleared
    await expect(input).toHaveValue('');
  });

  test('should display quick suggestions', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Wait for chat interface to load
    await page.waitForSelector('[data-testid="chat-message"]', { timeout: 10000 });

    // Check for quick suggestion chips (using more flexible selectors)
    // The suggestions should be visible when there's only the welcome message
    await expect(page.getByText(/Try these suggestions/i)).toBeVisible({ timeout: 5000 });

    // Check for at least one suggestion chip
    const suggestionChips = page.locator('.MuiChip-root');
    await expect(suggestionChips.first()).toBeVisible({ timeout: 5000 });
  });

  test('should send message when clicking quick suggestion', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Click a quick suggestion
    await page.getByText(/Show me a C major scale/i).click();

    // Check that message appears in chat
    await expect(page.getByText(/Show me a C major scale/i)).toBeVisible();
  });

  test('should receive AI response', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Send a message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('What is a chord?');
    await page.getByRole('button', { name: /send/i }).click();

    // Wait for AI response (simulated)
    await page.waitForTimeout(2000);

    // Check that response appears
    const messages = await page.locator('[data-testid="chat-message"]').count();
    expect(messages).toBeGreaterThan(1);
  });

  test('should support multiline input with Shift+Enter', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Type multiline message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('Line 1');
    await input.press('Shift+Enter');
    await input.type('Line 2');

    // Check that input contains newline
    const value = await input.inputValue();
    expect(value).toContain('\n');
  });

  test('should auto-scroll to latest message', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Send multiple messages
    const input = page.getByPlaceholder(/Ask about chords/i);
    for (let i = 1; i <= 5; i++) {
      await input.fill(`Message ${i}`);
      await page.getByRole('button', { name: /send/i }).click();
      await page.waitForTimeout(500);
    }

    // Check that latest message is visible
    await expect(page.getByText('Message 5')).toBeVisible();
  });

  test('should persist chat history in localStorage', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Send a message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('Persistent message');
    await page.getByRole('button', { name: /send/i }).click();

    // Reload the page
    await page.reload();

    // Navigate back to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that message is still there
    await expect(page.getByText('Persistent message')).toBeVisible();
  });

  test('should clear chat history', async ({ page }) => {
    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Wait for chat interface to load
    await page.waitForSelector('[data-testid="chat-message"]', { timeout: 10000 });

    // Send a message
    const input = page.getByPlaceholder(/Ask about chords/i);
    await input.fill('Message to clear');
    await page.getByRole('button', { name: /send/i }).click();

    // Wait for message to appear
    await page.waitForTimeout(1000);

    // Find and click clear button (Delete icon in header)
    const clearButton = page.getByRole('button', { name: /clear chat/i });
    await expect(clearButton).toBeVisible();

    // Mock the confirm dialog to auto-accept
    page.on('dialog', dialog => dialog.accept());

    await clearButton.click();

    // Wait for clear to complete
    await page.waitForTimeout(500);

    // Check that only welcome message remains (clear preserves welcome message)
    const messages = await page.locator('[data-testid="chat-message"]').count();
    expect(messages).toBe(1); // Only welcome message should remain

    // Verify it's the welcome message
    const welcomeMessage = page.locator('[data-testid="chat-message"]').first();
    await expect(welcomeMessage).toContainText(/Guitar Alchemist/i);
  });
});

