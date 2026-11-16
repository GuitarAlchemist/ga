export interface ChatbotStatusResponse {
  isAvailable: boolean;
  message: string;
  timestamp: string;
}

const buildUrl = (baseUrl: string, path: string) => {
  const normalizedBase = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  return `${normalizedBase}${path.startsWith('/') ? '' : '/'}${path}`;
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

export interface ChatbotDemoPrompt {
  prompt: string;
  description: string;
}

export interface ChatbotDemoCategory {
  id: string;
  name: string;
  icon: string;
  description: string;
  prompts: ChatbotDemoPrompt[];
}

export interface ChatbotDemoScript {
  version: string;
  categories: ChatbotDemoCategory[];
}

export const fetchChatDemo = async (
  baseUrl: string,
  signal?: AbortSignal,
): Promise<ChatbotDemoScript> => {
  const response = await fetch(buildUrl(baseUrl, '/api/chatbot/demo'), {
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to retrieve chatbot demo script: ${response.statusText}`);
  }

  return response.json();
};
