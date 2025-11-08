import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'jotai';
import ChatInput from '../components/Chat/ChatInput';

describe('ChatInput Component', () => {
  const renderWithProvider = (component: React.ReactElement) => {
    return render(<Provider>{component}</Provider>);
  };

  it('should render input field', () => {
    renderWithProvider(<ChatInput onSend={vi.fn()} />);
    const input = screen.getByPlaceholderText(/Ask about chords/i);
    expect(input).toBeInTheDocument();
  });

  it('should render send button', () => {
    renderWithProvider(<ChatInput onSend={vi.fn()} />);
    const button = screen.getByRole('button', { name: /send/i });
    expect(button).toBeInTheDocument();
  });

  it('should update input value on typing', async () => {
    const user = userEvent.setup();
    renderWithProvider(<ChatInput onSend={vi.fn()} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);
    await user.type(input, 'Test message');

    expect(input).toHaveValue('Test message');
  });

  it('should call onSend when send button clicked', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);
    const button = screen.getByRole('button', { name: /send/i });

    await user.type(input, 'Test message');
    await user.click(button);

    expect(onSend).toHaveBeenCalledWith('Test message');
  });

  it('should call onSend when Enter key pressed', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);

    await user.type(input, 'Test message');
    await user.keyboard('{Enter}');

    expect(onSend).toHaveBeenCalledWith('Test message');
  });

  it('should not call onSend when Shift+Enter pressed', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);

    await user.type(input, 'Test message');
    await user.keyboard('{Shift>}{Enter}{/Shift}');

    expect(onSend).not.toHaveBeenCalled();
  });

  it('should clear input after sending', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);
    const button = screen.getByRole('button', { name: /send/i });

    await user.type(input, 'Test message');
    await user.click(button);

    expect(input).toHaveValue('');
  });

  it('should not send empty messages', async () => {
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const button = screen.getByRole('button', { name: /send/i });

    // Button should be disabled when input is empty
    expect(button).toBeDisabled();
    expect(onSend).not.toHaveBeenCalled();
  });

  it('should not send whitespace-only messages', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);
    const button = screen.getByRole('button', { name: /send/i });

    await user.type(input, '   ');

    // Button should be disabled when input contains only whitespace
    expect(button).toBeDisabled();
    expect(onSend).not.toHaveBeenCalled();
  });

  it('should disable send button when loading', () => {
    renderWithProvider(<ChatInput onSend={vi.fn()} isLoading={true} />);
    const button = screen.getByRole('button', { name: /send/i });
    expect(button).toBeDisabled();
  });

  it('should disable input when loading', () => {
    renderWithProvider(<ChatInput onSend={vi.fn()} isLoading={true} />);
    const input = screen.getByPlaceholderText(/Ask about chords/i);
    expect(input).toBeDisabled();
  });

  it('should support multiline input', async () => {
    const user = userEvent.setup();
    renderWithProvider(<ChatInput onSend={vi.fn()} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);

    await user.type(input, 'Line 1{Shift>}{Enter}{/Shift}Line 2');

    expect(input).toHaveValue('Line 1\nLine 2');
  });

  it('should auto-focus input on mount', () => {
    renderWithProvider(<ChatInput onSend={vi.fn()} />);
    const input = screen.getByPlaceholderText(/Ask about chords/i);
    expect(input).toHaveFocus();
  });

  it('should trim whitespace from messages', async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();
    renderWithProvider(<ChatInput onSend={onSend} />);

    const input = screen.getByPlaceholderText(/Ask about chords/i);
    const button = screen.getByRole('button', { name: /send/i });

    await user.type(input, '  Test message  ');
    await user.click(button);

    expect(onSend).toHaveBeenCalledWith('Test message');
  });
});
