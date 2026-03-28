// src/components/PrimeRadiant/VisualCriticLoop.ts
// IXQL-driven visual self-healing loop powered by Claude vision
// Captures canvas → sends to /api/governance/visual-critic → executes IXQL commands
// Emits algedonic pain/pleasure signals based on visual quality

import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';

export interface VisualCriticResult {
  quality: number;
  issues: string[];
  ixql_commands: string[];
  signal_type: 'pain' | 'pleasure';
  signal_severity: 'critical' | 'warning' | 'info';
  signal_description: string;
  suggestions: string[];
}

export type CriticPhase = 'idle' | 'capturing' | 'analyzing' | 'executing' | 'complete';

export interface VisualCriticConfig {
  apiBaseUrl?: string;        // default: '' (same origin)
  intervalMs?: number;        // default: 60000 (60s)
  enabled?: boolean;          // default: false (opt-in)
  autoFix?: boolean;          // default: true (execute IXQL commands automatically)
  onResult?: (result: VisualCriticResult) => void;
  onIxqlCommand?: (result: IxqlParseResult) => void;
  onPhaseChange?: (phase: CriticPhase) => void;
}

/**
 * Start the visual critic loop.
 * Captures the Three.js canvas, sends to Claude vision via the backend,
 * and executes returned IXQL commands to fix visual issues.
 *
 * Returns a cleanup function to stop the loop.
 */
export function startVisualCriticLoop(
  canvas: HTMLCanvasElement,
  config: VisualCriticConfig = {},
): () => void {
  const {
    apiBaseUrl = '',
    intervalMs = 60_000,
    enabled = false,
    autoFix = true,
    onResult,
    onIxqlCommand,
    onPhaseChange,
  } = config;

  if (!enabled) {
    console.info('[VisualCritic] Disabled — set enabled: true to activate');
    return () => {};
  }

  let running = true;

  async function analyze() {
    if (!running) return;

    try {
      // Phase 1: Capture
      onPhaseChange?.('capturing');
      const dataUrl = canvas.toDataURL('image/png');
      const base64 = dataUrl.split(',')[1];
      if (!base64) { onPhaseChange?.('idle'); return; }

      // Phase 2: Analyze
      onPhaseChange?.('analyzing');
      console.info('[VisualCritic] Capturing screenshot for analysis...');

      const response = await fetch(`${apiBaseUrl}/api/governance/visual-critic`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ image: base64, mediaType: 'image/png' }),
      });

      if (!response.ok) {
        console.warn(`[VisualCritic] API error: ${response.status}`);
        return;
      }

      const result: VisualCriticResult = await response.json();

      // Log results
      const emoji = result.quality >= 7 ? '✓' : result.quality >= 4 ? '!' : '✗';
      console.info(
        `[VisualCritic] ${emoji} Quality: ${result.quality}/10 | Issues: ${result.issues?.length ?? 0} | Signal: ${result.signal_type}/${result.signal_severity}`
      );
      if (result.issues?.length) {
        result.issues.forEach(issue => console.info(`  - ${issue}`));
      }
      if (result.suggestions?.length) {
        result.suggestions.forEach(s => console.info(`  → ${s}`));
      }

      // Callback
      onResult?.(result);

      // Phase 3: Execute
      onPhaseChange?.('executing');

      // Execute IXQL commands
      if (autoFix && result.ixql_commands?.length && onIxqlCommand) {
        for (const cmd of result.ixql_commands) {
          const parsed = parseIxqlCommand(cmd);
          if (parsed.ok) {
            console.info(`[VisualCritic] Executing IXQL: ${cmd}`);
            onIxqlCommand(parsed);
          } else {
            console.warn(`[VisualCritic] Invalid IXQL: ${cmd} — ${parsed.error}`);
          }
        }
      }
      // Phase 4: Complete
      onPhaseChange?.('complete');
      setTimeout(() => onPhaseChange?.('idle'), 5000); // back to idle after 5s
    } catch (err) {
      console.warn('[VisualCritic] Analysis error:', err);
      onPhaseChange?.('idle');
    }
  }

  // Initial analysis after a short delay (let scene render first)
  const initialTimeout = setTimeout(analyze, 5000);

  // Periodic analysis
  const interval = setInterval(analyze, intervalMs);

  return () => {
    running = false;
    clearTimeout(initialTimeout);
    clearInterval(interval);
  };
}
