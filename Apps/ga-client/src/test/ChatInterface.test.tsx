// The /ai-copilot page must mount. ChatInterface referred to VIRTUALIZATION_THRESHOLD and
// VirtualizedMessageList, neither of which exists in the repository, so its first render threw
// "VIRTUALIZATION_THRESHOLD is not defined" and the route showed nothing.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'jotai';
import ChatInterface from '../components/Chat/ChatInterface';

describe('ChatInterface', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    // jsdom has no layout, so no scrollIntoView; the page calls it to follow new messages
    Element.prototype.scrollIntoView = vi.fn();
    // Status, suggestions and showcase requests: an empty answer is enough to mount
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      statusText: 'OK',
      json: async () => ({}),
    } as Response);
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('mounts, with the message input', () => {
    render(
      <Provider>
        <ChatInterface />
      </Provider>,
    );

    expect(screen.getByRole('textbox')).toBeInTheDocument();
  });
});
