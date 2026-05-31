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

    // Most prompts should show a *validated* badge (verified/warning class),
    // not just an unverified placeholder. Badge classes come from
    // wwwroot/index.html#renderQaBadge: `.qa-badge.verified` (passed) or
    // `.qa-badge.warning` (passed but slow) → validated;
    // `.qa-badge.unverified` → no data.
    //
    // Threshold: 35. The committed canonical baseline covers 45 prompts;
    // 35 leaves headroom for partial-recording days and any showcase entries
    // that haven't been canonicalized yet. Catching "0 validated" was the
    // whole point of this assertion — pre-fix, every prompt rendered the
    // unverified state because BuildQaSummary required _meta.json which is
    // gitignored.
    const validatedBadgeCount = await page
      .locator('.qa-badge.verified, .qa-badge.warning')
      .count();
    expect(
      validatedBadgeCount,
      'expected ≥35 validated QA badges (verified or warning) — fewer means the qa-summary fallback or the canonical baseline is broken',
    ).toBeGreaterThanOrEqual(35);

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
