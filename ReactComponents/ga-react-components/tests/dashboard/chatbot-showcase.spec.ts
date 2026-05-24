// Chatbot showcase test — opens the showcase modal, clicks a prompt, and
// asserts the response arrives within 5s.
//
// Catches:
//   - /chatbot/ 404 (deploy didn't publish wwwroot)
//   - Showcase button missing (regression in Apps/GaChatbot.Api/wwwroot/index.html)
//   - api/chatbot/demo broken (modal stuck on "Loading…")
//   - QA badges all gone (qa-summary endpoint broken)
//   - response time > 5s (slow LLM / GPU thermal throttle / no warmup)
//
// Selectors are pinned to the class names in wwwroot/index.html, which is
// hand-written HTML (not Vite). The showcase opens via the
// `<button class="showcase-button" onclick="openShowcase()">✨ Showcase</button>`.
import { test, expect } from '@playwright/test';

test.describe('Chatbot showcase', () => {
  test('opens modal, clicks a prompt, response < 5s', async ({ page }) => {
    test.setTimeout(45_000); // chatbot warmup can take a few seconds

    const response = await page.goto('/chatbot/', { waitUntil: 'domcontentloaded' });
    expect(response, 'navigation response should exist').not.toBeNull();
    expect(response!.status()).toBeLessThan(400);

    // Click the showcase button
    const showcaseBtn = page.locator('button.showcase-button, #showcaseButton').first();
    await expect(showcaseBtn, 'showcase button should be visible').toBeVisible({ timeout: 15_000 });
    await showcaseBtn.click();

    // Modal opens — content loads via fetch(api/chatbot/demo)
    const modal = page.locator('#showcaseModal');
    await expect(modal).toHaveClass(/open/, { timeout: 5_000 });

    // Wait for at least one prompt button to render (loading → loaded)
    const firstPrompt = page.locator('.showcase-prompt').first();
    await expect(firstPrompt, 'at least one showcase prompt should render').toBeVisible({ timeout: 15_000 });

    // At least one QA badge should exist (verified/warning/unverified).
    // We don't require a specific verdict — just that the QA wiring works.
    const qaBadgeCount = await page.locator('.qa-badge').count();
    expect(qaBadgeCount, 'at least one QA badge should render on showcase prompts').toBeGreaterThan(0);

    // Click the first prompt → modal closes, prompt is sent.
    // The chat form posts and renders a `.message.assistant` (or the existing
    // message list grows). We don't depend on the exact response text — we
    // just measure round-trip latency.
    const sentAt = Date.now();
    await firstPrompt.click();
    await expect(modal, 'modal should close after clicking a prompt').not.toHaveClass(/open/, { timeout: 5_000 });

    // Wait for an assistant message to appear. The wwwroot chat renders
    // streamed assistant content; we look for any element marking a reply.
    // Selector candidates: `.message.assistant`, `[data-role="assistant"]`,
    // or simply the chat container gaining new children. We use a broad
    // selector and require it to render in < 5s (the SLO).
    await page
      .locator('.message.assistant, [data-role="assistant"], .chat-message.assistant')
      .first()
      .waitFor({ state: 'visible', timeout: 5_000 })
      .catch(async () => {
        // Fallback: if no class matches, the live wwwroot may have evolved.
        // In that case we assert *some* new chat content appeared rather
        // than failing on a selector miss.
        const html = await page.locator('.chat, main').first().innerText();
        expect(
          html.length,
          'chat container should have grown after sending a prompt',
        ).toBeGreaterThan(50);
      });
    const elapsedMs = Date.now() - sentAt;
    expect(elapsedMs, `showcase response SLO is 5000ms (got ${elapsedMs}ms)`).toBeLessThan(5_000);
  });
});
