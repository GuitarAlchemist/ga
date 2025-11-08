import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import ChatMessage from '../components/Chat/ChatMessage';

describe('ChatMessage Component', () => {
  const mockMessage = {
    id: '1',
    role: 'user' as const,
    content: 'Test message',
    timestamp: new Date(),
  };

  // Mock clipboard API
  beforeEach(() => {
    Object.defineProperty(navigator, 'clipboard', {
      value: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
      writable: true,
      configurable: true,
    });
  });

  it('should render user message', () => {
    render(<ChatMessage message={mockMessage} />);
    expect(screen.getByText('Test message')).toBeInTheDocument();
  });

  it('should render assistant message', () => {
    const assistantMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Assistant response',
    };

    render(<ChatMessage message={assistantMessage} />);
    expect(screen.getByText('Assistant response')).toBeInTheDocument();
  });

  it('should render markdown content', () => {
    const markdownMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '# Heading\n\nThis is **bold** text.',
    };

    render(<ChatMessage message={markdownMessage} />);
    expect(screen.getByText('Heading')).toBeInTheDocument();
    expect(screen.getByText(/bold/)).toBeInTheDocument();
  });

  it('should render code blocks', () => {
    const codeMessage = {
      ...mockMessage,
      content: '```javascript\nconst x = 42;\n```',
    };

    render(<ChatMessage message={codeMessage} />);
    const matches = screen.getAllByText(
      (_, element) => element?.textContent?.includes('const x = 42') ?? false,
    );
    expect(matches.length).toBeGreaterThan(0);
  });

  // VexFlow requires real DOM SVG support (getBBox) which jsdom doesn't provide
  // This test passes in real browsers (E2E tests) but fails in unit tests
  it.skip('should detect VexTab code blocks', () => {
    const vextabMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '```vextab\ntabstave notation=true\nnotes :q (0/6.3/6)\n```',
    };

    const { container } = render(<ChatMessage message={vextabMessage} />);
    // VexTabViewer should be rendered - check for the component or its container
    const vextabElement = container.querySelector('[class*="vextab"]') || container.querySelector('svg');
    expect(vextabElement).toBeInTheDocument();
  });

  it('should render inline code', () => {
    const inlineCodeMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Use the `Cmaj7` chord here.',
    };

    render(<ChatMessage message={inlineCodeMessage} />);
    expect(screen.getByText(/Cmaj7/)).toBeInTheDocument();
  });

  it('should render lists', () => {
    const listMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '- Item 1\n- Item 2\n- Item 3',
    };

    render(<ChatMessage message={listMessage} />);
    expect(screen.getByText(/Item 1/)).toBeInTheDocument();
    expect(screen.getByText(/Item 2/)).toBeInTheDocument();
    expect(screen.getByText(/Item 3/)).toBeInTheDocument();
  });

  it('should render links', () => {
    const linkMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Check out [Guitar Alchemist](https://example.com)',
    };

    render(<ChatMessage message={linkMessage} />);
    const link = screen.getByRole('link', { name: /Guitar Alchemist/ });
    expect(link).toHaveAttribute('href', 'https://example.com');
  });

  it('should apply correct styling for user messages', () => {
    const { container } = render(<ChatMessage message={mockMessage} />);
    const messageBox = container.querySelector('[data-testid="chat-message"]');
    expect(messageBox).toHaveStyle({ justifyContent: 'flex-end' });
  });

  it('should apply correct styling for assistant messages', () => {
    const assistantMessage = {
      ...mockMessage,
      role: 'assistant' as const,
    };

    const { container } = render(<ChatMessage message={assistantMessage} />);
    const messageBox = container.querySelector('[data-testid="chat-message"]');
    expect(messageBox).toHaveStyle({ justifyContent: 'flex-start' });
  });

  it('should handle empty content', () => {
    const emptyMessage = {
      ...mockMessage,
      content: '',
    };

    const { container } = render(<ChatMessage message={emptyMessage} />);
    expect(container).toBeInTheDocument();
  });

  it('should handle multiline content', () => {
    const multilineMessage = {
      ...mockMessage,
      content: 'Line 1\nLine 2\nLine 3',
    };

    render(<ChatMessage message={multilineMessage} />);
    expect(screen.getByText(/Line 1/)).toBeInTheDocument();
    expect(screen.getByText(/Line 2/)).toBeInTheDocument();
    expect(screen.getByText(/Line 3/)).toBeInTheDocument();
  });

  it('should render system messages', () => {
    const systemMessage = {
      ...mockMessage,
      role: 'system' as const,
      content: 'System notification',
    };

    render(<ChatMessage message={systemMessage} />);
    expect(screen.getByText('System notification')).toBeInTheDocument();
  });

  it('should render chord diagram code blocks', () => {
    const chordMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '```chord-diagram\nname: Cmaj7\nfrets: x32000\n```',
    };

    const { container } = render(<ChatMessage message={chordMessage} />);
    // Check for code element with chord-diagram class
    const codeElement = container.querySelector('code.language-chord-diagram') || container.querySelector('code.language-chord');
    expect(codeElement).toBeInTheDocument();
    expect(codeElement?.textContent).toContain('Cmaj7');
  });

  it('should render code blocks with syntax highlighting', () => {
    const codeMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '```typescript\nconst greeting: string = "Hello";\n```',
    };

    const { container } = render(<ChatMessage message={codeMessage} />);
    // Check for code element with typescript class
    const codeElement = container.querySelector('code.language-typescript');
    expect(codeElement).toBeInTheDocument();
    expect(codeElement?.textContent).toContain('const');
    expect(codeElement?.textContent).toContain('greeting');
  });

  it('should render tables with remark-gfm', () => {
    const tableMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '| Chord | Notes |\n|-------|-------|\n| C | C E G |',
    };

    const { container } = render(<ChatMessage message={tableMessage} />);
    const table = container.querySelector('table');
    expect(table).toBeInTheDocument();
  });

  it('should render blockquotes', () => {
    const quoteMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '> This is a quote',
    };

    const { container } = render(<ChatMessage message={quoteMessage} />);
    const blockquote = container.querySelector('blockquote');
    expect(blockquote).toBeInTheDocument();
  });

  it('should render headings at different levels', () => {
    const headingMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '# H1\n## H2\n### H3',
    };

    const { container } = render(<ChatMessage message={headingMessage} />);
    expect(container.querySelector('h1')).toBeInTheDocument();
    expect(container.querySelector('h2')).toBeInTheDocument();
    expect(container.querySelector('h3')).toBeInTheDocument();
  });

  it('should render horizontal rules', () => {
    const hrMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Text above\n\n---\n\nText below',
    };

    const { container } = render(<ChatMessage message={hrMessage} />);
    const hr = container.querySelector('hr');
    expect(hr).toBeInTheDocument();
  });

  it('should handle streaming messages', () => {
    const streamingMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Streaming...',
      isStreaming: true,
    };

    const { container } = render(<ChatMessage message={streamingMessage} />);
    expect(container).toBeInTheDocument();
    expect(screen.getByText('Streaming...')).toBeInTheDocument();
  });

  it('should render copy button for assistant messages', () => {
    const testMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'Content to copy',
    };

    render(<ChatMessage message={testMessage} />);

    // Find the copy button
    const copyButton = screen.getByRole('button', { name: /copy message/i });
    expect(copyButton).toBeInTheDocument();
    expect(copyButton).toHaveAttribute('type', 'button');
  });

  it('should render emphasized text', () => {
    const emphasisMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: 'This is *italic* and **bold** text.',
    };

    const { container } = render(<ChatMessage message={emphasisMessage} />);
    const em = container.querySelector('em');
    const strong = container.querySelector('strong');
    expect(em).toBeInTheDocument();
    expect(strong).toBeInTheDocument();
  });

  it('should render strikethrough text with remark-gfm', () => {
    const strikeMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '~~strikethrough~~',
    };

    const { container } = render(<ChatMessage message={strikeMessage} />);
    const del = container.querySelector('del');
    expect(del).toBeInTheDocument();
  });

  it('should render task lists with remark-gfm', () => {
    const taskMessage = {
      ...mockMessage,
      role: 'assistant' as const,
      content: '- [ ] Task 1\n- [x] Task 2',
    };

    const { container } = render(<ChatMessage message={taskMessage} />);
    const checkboxes = container.querySelectorAll('input[type="checkbox"]');
    expect(checkboxes.length).toBeGreaterThan(0);
  });
});
