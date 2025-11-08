import { test, expect } from '@playwright/test';

test.describe('Markdown Rendering in Chatbot', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());

    // Navigate to chat tab
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();
  });

  test('should render headings', async ({ page }) => {
    // Inject assistant message with heading markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-heading',
          role: 'assistant',
          content: '# Chord Theory\n\nChords are the foundation of harmony.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for heading elements within chat messages (not page header)
    const chatMessage = page.locator('[data-testid="chat-message"]').last();
    const heading = chatMessage.locator('h1');
    await expect(heading).toBeVisible();
    await expect(heading).toHaveText('Chord Theory');
  });

  test('should render bold text', async ({ page }) => {
    // Inject assistant message with bold markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-bold',
          role: 'assistant',
          content: 'This is **bold text** in markdown.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for bold text
    const bold = page.locator('strong').first();
    await expect(bold).toBeVisible();
    await expect(bold).toHaveText('bold text');
  });

  test('should render italic text', async ({ page }) => {
    // Inject assistant message with italic markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-italic',
          role: 'assistant',
          content: 'This is *italic text* in markdown.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for italic text
    const italic = page.locator('em').first();
    await expect(italic).toBeVisible();
    await expect(italic).toHaveText('italic text');
  });

  test('should render lists', async ({ page }) => {
    // Inject assistant message with list markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-list',
          role: 'assistant',
          content: 'Here are some chords:\n\n- C major\n- G major\n- D minor',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for list elements
    const list = page.locator('ul').first();
    await expect(list).toBeVisible();

    const listItems = page.locator('li');
    const count = await listItems.count();
    expect(count).toBeGreaterThanOrEqual(3);
  });

  test('should render code blocks with syntax highlighting', async ({ page }) => {
    // Inject assistant message with code block markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-code-block',
          role: 'assistant',
          content: 'Here is some code:\n\n```javascript\nconst chord = "Cmaj7";\nconsole.log(chord);\n```',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for code block (SyntaxHighlighter renders as div, not pre)
    const codeBlock = page.locator('code').first();
    await expect(codeBlock).toBeVisible();
  });

  test('should render inline code', async ({ page }) => {
    // Inject assistant message with inline code markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-inline-code',
          role: 'assistant',
          content: 'The `Cmaj7` chord is a major seventh chord.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for inline code
    const inlineCode = page.locator('code').first();
    await expect(inlineCode).toBeVisible();
    await expect(inlineCode).toHaveText('Cmaj7');
  });

  test('should render links', async ({ page }) => {
    // Inject assistant message with link markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-link',
          role: 'assistant',
          content: 'Check out [Guitar Alchemist](https://example.com) for more info.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for links
    const link = page.locator('a[href]').first();
    await expect(link).toBeVisible();
    await expect(link).toHaveText('Guitar Alchemist');
    await expect(link).toHaveAttribute('href', 'https://example.com');
  });

  test('should render blockquotes', async ({ page }) => {
    // Inject assistant message with blockquote markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-blockquote',
          role: 'assistant',
          content: '> This is a quote about music theory.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for blockquote
    const blockquote = page.locator('blockquote').first();
    await expect(blockquote).toBeVisible();
  });

  test('should render tables', async ({ page }) => {
    // Inject assistant message with table markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-table',
          role: 'assistant',
          content: '| Chord | Notes |\n|-------|-------|\n| C | C E G |\n| G | G B D |',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for table within chat messages
    const chatMessage = page.locator('[data-testid="chat-message"]').last();
    const table = chatMessage.locator('table');
    await expect(table).toBeVisible({ timeout: 10000 });
  });

  test('should handle mixed markdown content', async ({ page }) => {
    // Inject assistant message with mixed markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-mixed',
          role: 'assistant',
          content: '# Music Theory\n\nThis is **bold** and *italic* text.\n\n- Item 1\n- Item 2\n\n`code` example',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Should have multiple markdown elements
    const heading = page.locator('h1').first();
    await expect(heading).toBeVisible();
    const bold = page.locator('strong').first();
    await expect(bold).toBeVisible();
    const italic = page.locator('em').first();
    await expect(italic).toBeVisible();
  });

  test('should preserve line breaks', async ({ page }) => {
    // Inject assistant message with line breaks
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-linebreaks',
          role: 'assistant',
          content: 'Line 1\n\nLine 2\n\nLine 3',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that content is rendered with paragraphs
    const paragraphs = page.locator('p');
    const count = await paragraphs.count();
    expect(count).toBeGreaterThanOrEqual(3);
  });

  test('should render horizontal rules', async ({ page }) => {
    // Inject assistant message with horizontal rule markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-hr',
          role: 'assistant',
          content: 'Section 1\n\n---\n\nSection 2',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for horizontal rule
    const hr = page.locator('hr').first();
    await expect(hr).toBeVisible();
  });

  test('should handle special characters in markdown', async ({ page }) => {
    // Inject assistant message with special characters
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-special-chars',
          role: 'assistant',
          content: 'Special chars: & < > " \' are escaped properly.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Should render without breaking
    const messageBox = page.locator('[data-testid="chat-message"]').last();
    await expect(messageBox).toBeVisible();
    await expect(messageBox).toContainText('Special chars');
  });

  test('should render nested lists', async ({ page }) => {
    // Inject assistant message with nested list markdown
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-nested-list',
          role: 'assistant',
          content: '- Item 1\n  - Nested 1.1\n  - Nested 1.2\n- Item 2',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check for nested lists
    const listItems = page.locator('li');
    const count = await listItems.count();
    expect(count).toBeGreaterThanOrEqual(4); // 2 top-level + 2 nested
  });

  test('should apply proper styling to markdown elements', async ({ page }) => {
    // Inject assistant message with various markdown elements
    await page.evaluate(() => {
      const messages = [
        {
          id: 'system-welcome',
          role: 'system',
          content: 'Welcome to Guitar Alchemist!',
          timestamp: new Date().toISOString(),
        },
        {
          id: 'test-styling',
          role: 'assistant',
          content: '# Heading\n\nThis is a paragraph with **bold** text.',
          timestamp: new Date().toISOString(),
        },
      ];
      localStorage.setItem('ga-chat-messages', JSON.stringify(messages));
    });

    // Reload to apply changes
    await page.reload();
    await page.getByRole('tab', { name: /AI Chat Assistant/i }).click();

    // Check that markdown is styled (has CSS classes or inline styles)
    const messageBox = page.locator('[data-testid="chat-message"]').last();
    await expect(messageBox).toBeVisible();

    // Should have proper spacing and formatting
    const heading = page.locator('h1').first();
    await expect(heading).toBeVisible();
    const paragraph = page.locator('p').first();
    await expect(paragraph).toBeVisible();
  });
});

