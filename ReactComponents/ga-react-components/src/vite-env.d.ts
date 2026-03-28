/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_CLAUDE_PROXY_URL?: string;
  readonly VITE_OPENAI_CONFIGURED?: string;
  readonly VITE_GEMINI_CONFIGURED?: string;
  readonly VITE_VOXTRAL_CONFIGURED?: string;
  readonly VITE_CODEX_CONFIGURED?: string;
  readonly DEV: boolean;
  readonly PROD: boolean;
  readonly MODE: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

