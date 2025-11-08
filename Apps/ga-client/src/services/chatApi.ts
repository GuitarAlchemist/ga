/**
 * Chat API Service
 * Handles communication with GaApi backend chatbot endpoints
 */

export interface ChatApiMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export interface ChatRequest {
  message: string;
  conversationHistory?: ChatApiMessage[];
  useSemanticSearch?: boolean;
}

export interface ChatResponse {
  message: string;
  timestamp: string;
}

export interface ChatbotStatus {
  isAvailable: boolean;
  message: string;
  timestamp: string;
}

export class ChatApiService {
  private baseUrl: string;
  private abortController: AbortController | null = null;

  constructor(baseUrl: string = 'https://localhost:7001') {
    this.baseUrl = baseUrl;
  }

  /**
   * Check if the chatbot backend is available
   */
  async checkStatus(): Promise<ChatbotStatus> {
    try {
      const response = await fetch(`${this.baseUrl}/api/chatbot/status`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Status check failed: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error('Failed to check chatbot status:', error);
      return {
        isAvailable: false,
        message: 'Failed to connect to chatbot service',
        timestamp: new Date().toISOString(),
      };
    }
  }

  /**
   * Send a message and receive streaming response via Server-Sent Events
   */
  async *streamChat(
    request: ChatRequest,
    onChunk?: (chunk: string) => void
  ): AsyncGenerator<string, void, unknown> {
    // Cancel any existing stream
    this.cancelStream();

    // Create new abort controller
    this.abortController = new AbortController();

    try {
      const response = await fetch(`${this.baseUrl}/api/chatbot/chat/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'text/event-stream',
        },
        body: JSON.stringify({
          message: request.message,
          conversationHistory: request.conversationHistory || [],
          useSemanticSearch: request.useSemanticSearch ?? true,
        }),
        signal: this.abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`Stream request failed: ${response.statusText}`);
      }

      if (!response.body) {
        throw new Error('Response body is null');
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      try {
        while (true) {
          const { done, value } = await reader.read();

          if (done) {
            break;
          }

          // Decode the chunk
          buffer += decoder.decode(value, { stream: true });

          // Process complete SSE messages
          const lines = buffer.split('\n');
          buffer = lines.pop() || ''; // Keep incomplete line in buffer

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const data = line.slice(6); // Remove 'data: ' prefix

              // Check for error
              try {
                const parsed = JSON.parse(data);
                if (parsed.error) {
                  throw new Error(parsed.error);
                }
              } catch {
                // Not JSON, treat as text chunk
                if (data && data !== '[DONE]') {
                  if (onChunk) {
                    onChunk(data);
                  }
                  yield data;
                }
              }
            }
          }
        }
      } finally {
        reader.releaseLock();
      }
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        console.log('Stream cancelled by user');
      } else {
        console.error('Stream error:', error);
        throw error;
      }
    } finally {
      this.abortController = null;
    }
  }

  /**
   * Send a message and get complete response (non-streaming)
   */
  async sendMessage(request: ChatRequest): Promise<ChatResponse> {
    try {
      const response = await fetch(`${this.baseUrl}/api/chatbot/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: request.message,
          conversationHistory: request.conversationHistory || [],
          useSemanticSearch: request.useSemanticSearch ?? true,
        }),
      });

      if (!response.ok) {
        throw new Error(`Chat request failed: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error('Failed to send message:', error);
      throw error;
    }
  }

  /**
   * Cancel the current streaming request
   */
  cancelStream(): void {
    if (this.abortController) {
      this.abortController.abort();
      this.abortController = null;
    }
  }

  /**
   * Get example queries
   */
  async getExamples(): Promise<string[]> {
    try {
      const response = await fetch(`${this.baseUrl}/api/chatbot/examples`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Examples request failed: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error('Failed to get examples:', error);
      // Return default examples
      return [
        'Show me some easy beginner chords',
        'What are the modes of the major scale?',
        'Explain voice leading in jazz',
        'How do I play a barre chord?',
      ];
    }
  }
}

// Singleton instance
export const chatApi = new ChatApiService();

