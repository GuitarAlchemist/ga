import type { ChatMessage as ConversationMessage } from '../store/chatAtoms';

export interface ChatApiMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface SendChatOptions {
  baseUrl: string;
  message: string;
  history?: ChatApiMessage[];
  useSemanticSearch?: boolean;
  signal?: AbortSignal;
  onChunk?: (partialResponse: string) => void;
  onComplete?: (finalResponse: string) => void;
}

export interface ChatbotStatusResponse {
  isAvailable: boolean;
  message: string;
  timestamp: string;
}

const buildUrl = (baseUrl: string, path: string) => {
  const normalizedBase = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  return `${normalizedBase}${path.startsWith('/') ? '' : '/'}${path}`;
};

const parseSseBuffer = (
  buffer: string,
  handlers: {
    onChunk?: (chunk: string) => void;
    onComplete?: () => void;
  },
) => {
  const events = buffer.split('\n\n');
  const remainder = events.pop() ?? '';
  let hasCompleted = false;

  for (const event of events) {
    const dataLine = event
      .split('\n')
      .find((line) => line.startsWith('data:'));
    if (!dataLine) {
      continue;
    }

    const payload = dataLine.slice(5).trim();
    if (!payload) {
      continue;
    }

    if (payload === '[DONE]') {
      handlers.onComplete?.();
      hasCompleted = true;
      continue;
    }

    if (payload.startsWith('{')) {
      try {
        const parsed = JSON.parse(payload);
        if (parsed.error) {
          throw new Error(parsed.error);
        }
      } catch (error) {
        throw error instanceof Error
          ? error
          : new Error('Failed to parse chat stream payload.');
      }
      continue;
    }

    handlers.onChunk?.(payload);
  }

  return { remainder, hasCompleted };
};

export const sendChatMessageStream = async ({
  baseUrl,
  message,
  history = [],
  useSemanticSearch = true,
  signal,
  onChunk,
  onComplete,
}: SendChatOptions): Promise<string> => {
  const requestBody = JSON.stringify({
    message,
    conversationHistory: history,
    useSemanticSearch,
  });

  const response = await fetch(buildUrl(baseUrl, '/api/chatbot/chat/stream'), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: requestBody,
    signal,
  });

  if (!response.ok) {
    throw new Error(`Chat request failed: ${response.status} ${response.statusText}`);
  }

  if (!response.body) {
    const fallbackText = await response.text();
    if (fallbackText) {
      let finalResponse = '';
      parseSseBuffer(`${fallbackText}\n\n`, {
        onChunk: (chunk) => {
          finalResponse = chunk;
          onChunk?.(finalResponse);
        },
      });

      if (finalResponse) {
        onComplete?.(finalResponse);
        return finalResponse;
      }
    }

    throw new Error('Chat response stream was not available.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let fullResponse = '';

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    try {
      const { remainder, hasCompleted } = parseSseBuffer(buffer, {
        onChunk: (chunk) => {
          fullResponse += chunk;
          onChunk?.(fullResponse);
        },
      });

      buffer = remainder;
      if (hasCompleted) {
        break;
      }
    } catch (error) {
      reader.cancel();
      throw error instanceof Error ? error : new Error('Chat stream error.');
    }
  }

  if (buffer.trim()) {
    fullResponse += buffer.trim();
    onChunk?.(fullResponse);
  }

  onComplete?.(fullResponse);

  return fullResponse;
};

export const fetchChatStatus = async (
  baseUrl: string,
  signal?: AbortSignal,
): Promise<ChatbotStatusResponse> => {
  const response = await fetch(buildUrl(baseUrl, '/api/chatbot/status'), {
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to retrieve chatbot status: ${response.statusText}`);
  }

  return response.json();
};

export const fetchChatExamples = async (
  baseUrl: string,
  signal?: AbortSignal,
): Promise<string[]> => {
  const response = await fetch(buildUrl(baseUrl, '/api/chatbot/examples'), {
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to retrieve chatbot examples: ${response.statusText}`);
  }

  return response.json();
};

export const mapMessagesToApi = (messages: ConversationMessage[]): ChatApiMessage[] =>
  messages
    .filter((message) => message.role !== 'system')
    .slice(-20)
    .map((message) => ({
      role: message.role,
      content: message.content,
    }));

