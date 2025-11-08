# GA Client Testing Guide

Comprehensive testing documentation for the Guitar Alchemist React/TypeScript/Jotai chatbot.

---

## 📋 **Test Coverage**

### **Unit Tests (Vitest)**

Located in `src/test/`:

1. **chatAtoms.test.ts** - Jotai state management
   - Chat messages atom (persistence, initialization)
   - Chat input atom
   - Loading state atom
   - Streaming message atom
   - Visible messages derived atom
   - Add message action atom
   - Clear messages action atom

2. **ChatMessage.test.tsx** - Message rendering component
   - User vs assistant message styling
   - Markdown rendering (headings, bold, italic, lists, links)
   - Code block rendering with syntax highlighting
   - VexTab code block detection and rendering
   - Inline code rendering
   - Empty and multiline content handling

3. **ChatInput.test.tsx** - Input component
   - Input field rendering and updates
   - Send button functionality
   - Enter key to send
   - Shift+Enter for multiline
   - Empty message prevention
   - Whitespace trimming
   - Loading state handling
   - Auto-focus behavior

### **E2E Tests (Playwright)**

Located in `tests/e2e/`:

1. **chatbot.spec.ts** - Core chatbot functionality
   - Tab navigation
   - Welcome message display
   - Sending messages (button and Enter key)
   - Empty message prevention
   - Input clearing after send
   - Quick suggestions
   - AI response handling
   - Multiline input support
   - Auto-scroll to latest message
   - Chat history persistence
   - Clear chat functionality

2. **vextab-rendering.spec.ts** - VexTab music notation
   - VexTab notation rendering
   - Standard notation support
   - VexTab code block detection
   - Multiple VexTab blocks
   - Error handling
   - Styling and layout
   - Different notation types
   - Mobile viewport support
   - Auto-scroll to rendered VexTab

3. **markdown-rendering.spec.ts** - Markdown features
   - Headings (h1-h6)
   - Bold and italic text
   - Lists (ordered, unordered, nested)
   - Code blocks with syntax highlighting
   - Inline code
   - Links (with target="_blank")
   - Blockquotes
   - Tables
   - Horizontal rules
   - Special character handling
   - Mixed content rendering
   - Proper styling application

---

## 🚀 **Running Tests**

### **Quick Start**

```bash
# Run all tests (unit + E2E)
pwsh ./run-tests.ps1

# Run only unit tests
pwsh ./run-tests.ps1 -Unit

# Run only E2E tests
pwsh ./run-tests.ps1 -E2E

# Run E2E tests with visible browser
pwsh ./run-tests.ps1 -E2E -Headed
```

### **Unit Tests (Vitest)**

```bash
# Run once
npm run test

# Watch mode (auto-rerun on changes)
npm run test -- --watch

# UI mode (interactive)
npm run test:ui

# Coverage report
npm run test:coverage
```

### **E2E Tests (Playwright)**

```bash
# Run all E2E tests (headless)
npm run test:e2e

# Run with visible browser
npm run test:e2e:headed

# Interactive UI mode
npm run test:e2e:ui

# Run specific test file
npx playwright test tests/e2e/chatbot.spec.ts

# Run specific test
npx playwright test -g "should send a message"

# Debug mode
npx playwright test --debug
```

### **Cross-Browser Testing**

```bash
# Run on all browsers (Chromium, Firefox, WebKit)
npm run test:e2e

# Run on specific browser
npx playwright test --project=chromium
npx playwright test --project=firefox
npx playwright test --project=webkit

# Mobile browsers
npx playwright test --project="Mobile Chrome"
npx playwright test --project="Mobile Safari"
```

---

## 🛠️ **Test Setup**

### **Dependencies**

All testing dependencies are installed:

- `vitest` - Unit test runner
- `@vitest/ui` - Interactive UI for Vitest
- `@testing-library/react` - React testing utilities
- `@testing-library/jest-dom` - DOM matchers
- `@testing-library/user-event` - User interaction simulation
- `jsdom` - DOM implementation for Node.js
- `@playwright/test` - E2E testing framework

### **Configuration Files**

- `vite.config.ts` - Vitest configuration
- `playwright.config.ts` - Playwright configuration
- `src/test/setup.ts` - Test setup (mocks, cleanup)

---

## 📊 **Test Reports**

### **Unit Test Reports**

Coverage reports are generated in `coverage/` directory:

```bash
npm run test:coverage
open coverage/index.html
```

### **E2E Test Reports**

Playwright HTML reports are generated in `playwright-report/`:

```bash
npm run test:e2e
npx playwright show-report
```

Screenshots and videos of failed tests are saved in `test-results/`.

---

## ✅ **Writing New Tests**

### **Unit Test Example**

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'jotai';
import MyComponent from '../components/MyComponent';

describe('MyComponent', () => {
  it('should render correctly', () => {
    render(
      <Provider>
        <MyComponent />
      </Provider>
    );
    
    expect(screen.getByText('Hello')).toBeInTheDocument();
  });
});
```

### **E2E Test Example**

```typescript
import { test, expect } from '@playwright/test';

test('should do something', async ({ page }) => {
  await page.goto('/');
  
  await page.getByRole('button', { name: /click me/i }).click();
  
  await expect(page.getByText('Success')).toBeVisible();
});
```

---

## 🐛 **Debugging Tests**

### **Unit Tests**

```bash
# Run specific test file
npm run test -- src/test/chatAtoms.test.ts

# Run tests matching pattern
npm run test -- -t "should add message"

# UI mode for debugging
npm run test:ui
```

### **E2E Tests**

```bash
# Debug mode (opens inspector)
npx playwright test --debug

# Headed mode (see browser)
npm run test:e2e:headed

# Slow motion
npx playwright test --headed --slow-mo=1000

# Trace viewer
npx playwright show-trace test-results/.../trace.zip
```

---

## 📝 **Best Practices**

1. **Test Isolation**: Each test should be independent
2. **Clear Descriptions**: Use descriptive test names
3. **Arrange-Act-Assert**: Structure tests clearly
4. **Mock External Dependencies**: Don't rely on real APIs
5. **Test User Behavior**: Focus on what users do, not implementation
6. **Use Data Attributes**: Add `data-testid` for reliable selectors
7. **Clean Up**: Use `beforeEach` and `afterEach` hooks
8. **Async Handling**: Properly await async operations
9. **Accessibility**: Use semantic queries (getByRole, getByLabelText)
10. **Coverage Goals**: Aim for >80% coverage on critical paths

---

## 🔄 **CI/CD Integration**

Tests are designed to run in CI environments:

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: |
    npm ci
    npm run test -- --run
    npm run test:e2e
```

Environment variables:
- `CI=true` - Enables CI mode (no retries, parallel workers)

---

## 📚 **Resources**

- [Vitest Documentation](https://vitest.dev/)
- [Playwright Documentation](https://playwright.dev/)
- [Testing Library](https://testing-library.com/)
- [Jotai Testing](https://jotai.org/docs/guides/testing)

---

## 🎯 **Test Checklist**

Before committing:

- [ ] All unit tests pass
- [ ] All E2E tests pass
- [ ] Coverage is maintained or improved
- [ ] New features have tests
- [ ] Tests are documented
- [ ] No flaky tests
- [ ] Tests run in CI

---

## 🚨 **Troubleshooting**

### **Tests Fail Locally**

1. Clear node_modules and reinstall: `rm -rf node_modules && npm install`
2. Clear test cache: `npm run test -- --clearCache`
3. Update Playwright browsers: `npx playwright install`

### **Flaky E2E Tests**

1. Add explicit waits: `await page.waitForTimeout(1000)`
2. Use better selectors: `getByRole` instead of `getByText`
3. Increase timeout: `test.setTimeout(60000)`
4. Check for race conditions

### **Coverage Issues**

1. Check ignored files in `vite.config.ts`
2. Ensure all branches are tested
3. Add tests for edge cases

---

**Happy Testing! 🧪**

