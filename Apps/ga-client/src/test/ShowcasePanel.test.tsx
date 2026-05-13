import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import ShowcasePanel from '../components/Chat/ShowcasePanel';
import type { ChatbotDemoScript } from '../services/chatService';

const FAKE_SCRIPT: ChatbotDemoScript = {
  version: '1.0',
  categories: [
    {
      id: 'theory',
      name: 'Music Theory',
      icon: 'music_note',
      description: 'Foundational questions.',
      prompts: [
        { prompt: 'Explain the circle of fifths', description: 'Diagram.' },
        { prompt: 'What are the modes?', description: 'Ionian to Locrian.' },
      ],
    },
    {
      id: 'voicings',
      name: 'Chord Voicings',
      icon: 'queue_music',
      description: 'OPTIC-K vector search.',
      prompts: [{ prompt: 'Show me chord voicings for Cmaj7', description: 'Index hit.' }],
    },
  ],
};

describe('ShowcasePanel', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      statusText: 'OK',
      json: async () => FAKE_SCRIPT,
    } as Response);
  });

  afterEach(() => {
    global.fetch = originalFetch;
    vi.clearAllMocks();
  });

  it('renders nothing while closed', () => {
    render(
      <ShowcasePanel
        open={false}
        apiBaseUrl="http://test"
        onClose={() => {}}
        onSelectPrompt={() => {}}
      />,
    );
    expect(screen.queryByTestId('showcase-panel')).not.toBeInTheDocument();
  });

  it('loads and renders categories when opened', async () => {
    render(
      <ShowcasePanel
        open
        apiBaseUrl="http://test"
        onClose={() => {}}
        onSelectPrompt={() => {}}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Music Theory')).toBeInTheDocument();
    });

    expect(screen.getByText('Chord Voicings')).toBeInTheDocument();
    expect(screen.getByText('Explain the circle of fifths')).toBeInTheDocument();
    expect(screen.getByTestId('showcase-category-theory')).toBeInTheDocument();
    expect(screen.getByTestId('showcase-category-voicings')).toBeInTheDocument();
  });

  it('invokes onSelectPrompt and closes when a prompt chip is clicked', async () => {
    const onSelectPrompt = vi.fn();
    const onClose = vi.fn();

    render(
      <ShowcasePanel
        open
        apiBaseUrl="http://test"
        onClose={onClose}
        onSelectPrompt={onSelectPrompt}
      />,
    );

    const chip = await screen.findByText('Explain the circle of fifths');
    fireEvent.click(chip);

    expect(onSelectPrompt).toHaveBeenCalledWith('Explain the circle of fifths');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('surfaces an error if the fetch fails', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      statusText: 'Service Unavailable',
      json: async () => ({}),
    } as Response);

    render(
      <ShowcasePanel
        open
        apiBaseUrl="http://test"
        onClose={() => {}}
        onSelectPrompt={() => {}}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText(/Could not load showcase/i)).toBeInTheDocument();
    });
  });

  it('triggers onClose when the close button is clicked', async () => {
    const onClose = vi.fn();
    render(
      <ShowcasePanel
        open
        apiBaseUrl="http://test"
        onClose={onClose}
        onSelectPrompt={() => {}}
      />,
    );

    const closeButton = await screen.findByRole('button', { name: /close showcase/i });
    fireEvent.click(closeButton);
    expect(onClose).toHaveBeenCalled();
  });
});
