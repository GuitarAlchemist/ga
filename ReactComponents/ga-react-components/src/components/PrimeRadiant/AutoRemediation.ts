// src/components/PrimeRadiant/AutoRemediation.ts
// Auto-remediation engine — collects console errors, classifies them,
// and attempts self-healing before surfacing to the user.
// The governance system healing itself.

import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ConsoleIssue {
  level: 'error' | 'warn';
  message: string;
  source: string | null;
  timestamp: number;
  count: number;            // how many times this error repeated
  autoFixed: boolean;
  fixApplied: string | null;
}

export interface RemediationAction {
  pattern: string;          // substring to match in error message
  fix: () => void;
  description: string;
  maxRetries: number;
}

// ---------------------------------------------------------------------------
// Known auto-fixes
// ---------------------------------------------------------------------------

const REMEDIATION_ACTIONS: RemediationAction[] = [
  {
    pattern: 'Failed to fetch',
    fix: () => {
      // Clear stale signals that may reference failed fetch results
      signalBus.clearAll();
      signalBus.publish('remediation:applied', {
        action: 'clear-signals',
        reason: 'Failed to fetch — cleared stale signals',
      }, '__autoRemediation__');
    },
    description: 'Clear stale signals after fetch failure',
    maxRetries: 3,
  },
  {
    pattern: '429',
    fix: () => {
      // Rate limited — back off by publishing a slow-down signal
      signalBus.publish('remediation:rate-limited', {
        action: 'backoff',
        reason: '429 Too Many Requests — backing off',
      }, '__autoRemediation__');
    },
    description: 'Back off on rate limit',
    maxRetries: 1,
  },
  {
    pattern: 'ResizeObserver loop',
    fix: () => {
      // Benign browser warning — suppress
    },
    description: 'Suppress benign ResizeObserver warning',
    maxRetries: 0, // suppress forever
  },
  {
    pattern: 'WebSocket',
    fix: () => {
      signalBus.publish('remediation:websocket', {
        action: 'reconnect-hint',
        reason: 'WebSocket error — hinting reconnection',
      }, '__autoRemediation__');
    },
    description: 'Publish WebSocket reconnection hint',
    maxRetries: 5,
  },
];

// ---------------------------------------------------------------------------
// Auto-Remediation Engine
// ---------------------------------------------------------------------------

class AutoRemediationEngine {
  private issues = new Map<string, ConsoleIssue>();
  private retryCount = new Map<string, number>();
  private originalConsoleError: typeof console.error | null = null;
  private originalConsoleWarn: typeof console.warn | null = null;
  private active = false;
  private suppressPatterns = new Set<string>();

  /** Start intercepting console errors and warnings */
  start(): void {
    if (this.active || typeof console === 'undefined') return;
    this.active = true;

    this.originalConsoleError = console.error;
    this.originalConsoleWarn = console.warn;

    console.error = (...args: unknown[]) => {
      this.handleConsoleMessage('error', args);
      this.originalConsoleError?.apply(console, args);
    };

    console.warn = (...args: unknown[]) => {
      this.handleConsoleMessage('warn', args);
      this.originalConsoleWarn?.apply(console, args);
    };

    // Also catch unhandled errors
    if (typeof window !== 'undefined') {
      window.addEventListener('error', (event) => {
        this.handleConsoleMessage('error', [event.message]);
      });
    }
  }

  /** Stop intercepting */
  stop(): void {
    if (!this.active) return;
    this.active = false;
    if (this.originalConsoleError) console.error = this.originalConsoleError;
    if (this.originalConsoleWarn) console.warn = this.originalConsoleWarn;
  }

  /** Get all collected issues */
  getIssues(): ConsoleIssue[] {
    return Array.from(this.issues.values())
      .sort((a, b) => b.timestamp - a.timestamp);
  }

  /** Get issue count by level */
  getCounts(): { errors: number; warnings: number; autoFixed: number } {
    let errors = 0, warnings = 0, autoFixed = 0;
    for (const issue of this.issues.values()) {
      if (issue.level === 'error') errors += issue.count;
      else warnings += issue.count;
      if (issue.autoFixed) autoFixed++;
    }
    return { errors, warnings, autoFixed };
  }

  /** Clear all collected issues */
  clear(): void {
    this.issues.clear();
    this.retryCount.clear();
  }

  /** Reset for test isolation */
  reset(): void {
    this.stop();
    this.clear();
    this.suppressPatterns.clear();
  }

  // ── Internal ──

  private handleConsoleMessage(level: 'error' | 'warn', args: unknown[]): void {
    const message = args.map(a => {
      if (a instanceof Error) return a.message;
      if (typeof a === 'string') return a;
      try { return String(a); } catch { return '[object]'; }
    }).join(' ');

    // Check suppress patterns
    for (const pattern of this.suppressPatterns) {
      if (message.indexOf(pattern) >= 0) return;
    }

    // Deduplicate by message prefix (first 100 chars)
    const key = message.substring(0, 100);
    const existing = this.issues.get(key);
    if (existing) {
      existing.count++;
      existing.timestamp = Date.now();
    } else {
      this.issues.set(key, {
        level,
        message: message.substring(0, 500),
        source: null,
        timestamp: Date.now(),
        count: 1,
        autoFixed: false,
        fixApplied: null,
      });
    }

    // Attempt auto-remediation
    this.attemptFix(key, message);

    // Publish to signal bus for QA panel / algedonic
    signalBus.publish('console:issue', {
      level,
      message: message.substring(0, 200),
      timestamp: Date.now(),
    }, '__autoRemediation__');

    // Cap collected issues at 100
    if (this.issues.size > 100) {
      const oldest = Array.from(this.issues.keys())[0];
      if (oldest) this.issues.delete(oldest);
    }
  }

  private attemptFix(key: string, message: string): void {
    for (const action of REMEDIATION_ACTIONS) {
      if (message.indexOf(action.pattern) < 0) continue;

      const retries = this.retryCount.get(key) ?? 0;

      // Suppress if maxRetries is 0 (benign warning)
      if (action.maxRetries === 0) {
        this.suppressPatterns.add(action.pattern);
        const issue = this.issues.get(key);
        if (issue) {
          issue.autoFixed = true;
          issue.fixApplied = action.description + ' (suppressed)';
        }
        return;
      }

      if (retries >= action.maxRetries) return; // exhausted retries

      this.retryCount.set(key, retries + 1);

      try {
        action.fix();
        const issue = this.issues.get(key);
        if (issue) {
          issue.autoFixed = true;
          issue.fixApplied = action.description;
        }
      } catch {
        // Fix itself failed — don't crash
      }
      return; // apply first matching fix only
    }
  }
}

// ---------------------------------------------------------------------------
// Singleton
// ---------------------------------------------------------------------------

export const autoRemediation = new AutoRemediationEngine();
