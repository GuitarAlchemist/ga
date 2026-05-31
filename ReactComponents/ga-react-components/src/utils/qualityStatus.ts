// qualityStatus — shared derivation of the "tile" state from a
// state/quality/<domain>/<date>.json snapshot.
//
// Why this exists:
//   The chatbot-qa producer (Scripts/run-prompt-corpus.ps1) correctly emits
//   `degraded:true` + `last_known_good_pass_pct` when the hosted runner can't
//   reach Ollama/OPTIC-K — but the dashboard tiles only ever read
//   `oracle_status`. Result: a degraded snapshot rendered as either gray
//   (no oracle_status) or red (when downstream code coerced "no oracle_status
//   + no pass_pct" into an error). Neither tells the operator the truthful
//   state: "degraded today on the hosted runner, here's last-known-good".
//
//   This helper centralises that derivation so every tile (OverviewSection,
//   DevelopmentSection, ManifestViewer, TestPlansCard footer) renders the
//   same trip-wire matrix.
//
// Units note:
//   `pass_pct`                  is a 0..1 fraction (per vite.config.ts
//                               gatherQuality / gatherChatbotQa).
//   `last_known_good_pass_pct`  is a 0..100 percent (per
//                               Scripts/run-prompt-corpus.ps1 _FindLastKnownGood
//                               which multiplies the 0..1 baseline by 100).
//   Schema mismatch is inherited from the producer; don't double-convert here.
//
// Backward-compat: `degraded` + `last_known_good_pass_pct` were added by
// chatbot-qa producer change #327. Older snapshots that pre-date #327 won't
// carry them — the helper treats `undefined` as "not degraded".

/**
 * Minimum subset of fields the helper needs. Both the manifest "domain"
 * payload (Record<string, unknown>) and the TestPlansCard footer
 * (ChatbotQaSummary) can be projected into this shape.
 */
export interface QualitySnapshotLike {
    oracle_status?: string | null;
    pass_pct?: number | null;            // 0..1 fraction
    degraded?: boolean | null;
    degraded_reason?: string | null;
    last_known_good_pass_pct?: number | null;  // 0..100 percent
}

/** MUI palette color the tile chip / border should use. */
export type TileColor = 'success' | 'warning' | 'error' | 'default';

export interface TileStatus {
    /** MUI color name — drives chip / border. */
    color: TileColor;
    /**
     * Stable kebab-case label used by MUI Chips, screen readers, and tests.
     * One of: 'ok' | 'warn' | 'error' | 'degraded' | 'unknown'.
     */
    label: 'ok' | 'warn' | 'error' | 'degraded' | 'unknown';
    /** Short headline string for tooltip / footer (no markup). */
    headline: string;
}

const PASS_THRESHOLD_FRACTION = 0.7;   // pass_pct in 0..1

/**
 * Derive the truthful tile status from a snapshot. Precedence:
 *   1. Explicit `oracle_status` (envelope contract).
 *   2. `degraded:true` → amber, with last-known-good context.
 *   3. `pass_pct == null` → unknown (gray) — honest "no data", not red.
 *   4. `pass_pct < threshold` → red.
 *   5. Otherwise → green.
 */
export function deriveTileStatus(snap: QualitySnapshotLike | null | undefined): TileStatus {
    if (!snap) return { color: 'default', label: 'unknown', headline: 'no data' };

    // 1. Explicit oracle_status wins (envelope contract).
    const explicit = typeof snap.oracle_status === 'string' ? snap.oracle_status : null;
    if (explicit === 'ok')   return { color: 'success', label: 'ok',    headline: 'ok' };
    if (explicit === 'warn') return { color: 'warning', label: 'warn',  headline: 'warn' };
    if (explicit)            return { color: 'error',   label: 'error', headline: explicit };

    // 2. Degraded — amber with last-known-good when available.
    if (snap.degraded === true) {
        const lkg = typeof snap.last_known_good_pass_pct === 'number' ? snap.last_known_good_pass_pct : null;
        const reason = snap.degraded_reason ?? 'environment unavailable';
        const headline = lkg != null
            ? `degraded · last good ${lkg.toFixed(0)}% (${reason})`
            : `degraded · no last-known-good (${reason})`;
        return { color: 'warning', label: 'degraded', headline };
    }

    // 3. Honest unknown — null pass_pct without a degraded marker is "no data",
    //    not a regression. Gray, not red.
    if (snap.pass_pct == null) {
        return { color: 'default', label: 'unknown', headline: 'no data' };
    }

    // 4 / 5. Real pass-rate verdict (0..1 fraction).
    const pct = (snap.pass_pct * 100).toFixed(0);
    if (snap.pass_pct < PASS_THRESHOLD_FRACTION) {
        return { color: 'error', label: 'error', headline: `${pct}% passing` };
    }
    return { color: 'success', label: 'ok', headline: `${pct}% passing` };
}
