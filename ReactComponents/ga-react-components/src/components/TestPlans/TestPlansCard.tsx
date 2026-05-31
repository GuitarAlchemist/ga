// TestPlansCard — renders test plan proposals produced by the /test-plan
// skill (.claude/skills/test-plan/SKILL.md). Source of truth is
// state/quality/test-plans/*.{md,meta.json} aggregated by the Vite
// middleware at /dev-data/test-plans.
//
// Why this card exists:
//   The /test-plan skill writes diff-driven test proposals for each PR,
//   but until now they were only visible via PR comments or by browsing
//   the artifact directory. Surfacing them on /test#dev/qa gives a single
//   pane of glass for "what tests should I write next?" alongside the
//   existing quality + process-health cards.
//
// Footer surfaces the chatbot eval pass rate from
// state/quality/chatbot-qa/last.json (or the most recent dated snapshot)
// so the QA tab also answers "is the chatbot loop healthy right now?".
//
// Auto-refresh every 60s. The "Generate for chatbot" button POSTs to the
// existing /dev-data/harness/skill/test-plan endpoint (reuses the
// SkillActionButton component) with context="gachatbot" so an agent
// picks it up on the next pass.

import React, { useCallback, useEffect, useState } from 'react';
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Badge,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import AssignmentIcon from '@mui/icons-material/Assignment';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import SkillActionButton from '../Harness/SkillActionButton';

// ── Types ────────────────────────────────────────────────────────────────
// Mirror what the Vite middleware at /dev-data/test-plans emits. Keep
// these in sync if the middleware shape changes — there is no shared
// schema file because the endpoint is local-only dev surface.

export interface TestPlanSummary {
  id: string;
  path: string;
  title: string;
  target: string;
  generated_at: string | null;
  step_count: number;
  status: 'draft' | 'reviewed' | 'executed' | 'unknown';
  markdown: string;
}

export interface ChatbotQaSummary {
  pass_pct: number | null;
  fail_count: number | null;
  total_prompts: number | null;
  last_run_at: string | null;
  degraded?: boolean;
  degraded_reason?: string | null;
  last_known_good_pass_pct?: number | null;
}

export interface TestPlansPayload {
  generated_at: string;
  total: number;
  plans: TestPlanSummary[];
  chatbot_qa: ChatbotQaSummary | null;
}

// ── Helpers ──────────────────────────────────────────────────────────────

function statusColor(status: TestPlanSummary['status']): 'default' | 'info' | 'success' | 'warning' {
  switch (status) {
    case 'executed': return 'success';
    case 'reviewed': return 'info';
    case 'draft':    return 'warning';
    default:         return 'default';
  }
}

function timeAgo(iso: string | null): string {
  if (!iso) return '—';
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso;
  const delta = Date.now() - t;
  const min = Math.round(delta / 60_000);
  if (min < 1) return 'just now';
  if (min < 60) return `${min}m ago`;
  const h = Math.round(min / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.round(h / 24);
  return `${d}d ago`;
}

// ── Component ────────────────────────────────────────────────────────────

export const TestPlansCard: React.FC = () => {
  const [data, setData] = useState<TestPlansPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const loadPlans = useCallback(async () => {
    try {
      const resp = await fetch('/dev-data/test-plans', { cache: 'no-store' });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const payload = (await resp.json()) as TestPlansPayload;
      setData(payload);
      setError(null);
    } catch (e) {
      setError(String((e as Error).message ?? e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPlans();
    const interval = setInterval(loadPlans, 60_000);
    return () => clearInterval(interval);
  }, [loadPlans]);

  const renderChatbotFooter = (cb: ChatbotQaSummary | null) => {
    if (!cb) {
      return (
        <Typography variant="caption" color="text.secondary">
          Chatbot loop: no <code>state/quality/chatbot-qa/last.json</code> yet.
        </Typography>
      );
    }
    if (cb.degraded || cb.pass_pct == null) {
      // last_known_good_pass_pct is emitted by run-prompt-corpus.ps1 as a
      // 0..100 percent (it converts the 0..1 baseline.primary_baseline via
      // *100 at the producer). Do NOT multiply by 100 here — pass_pct is the
      // 0..1 fraction; last_known_good_pass_pct is already a percent.
      const lkg = cb.last_known_good_pass_pct;
      return (
        <Typography variant="caption" color="warning.main">
          <SmartToyIcon fontSize="inherit" sx={{ verticalAlign: 'middle', mr: 0.5 }} />
          Chatbot loop: degraded ({cb.degraded_reason ?? 'environment unavailable'})
          {lkg != null && <> · last known good: {lkg.toFixed(0)}%</>}
          {cb.last_run_at && <> · last run {timeAgo(cb.last_run_at)}</>}
        </Typography>
      );
    }
    const pass = Math.round(cb.pass_pct * 100);
    const fail = cb.fail_count ?? 0;
    const total = cb.total_prompts ?? 0;
    const passing = total - fail;
    return (
      <Typography variant="caption" color="text.secondary">
        <SmartToyIcon fontSize="inherit" sx={{ verticalAlign: 'middle', mr: 0.5 }} />
        Chatbot loop: <strong>{passing} passing</strong>{total > 0 && <> / {fail} failing ({pass}%)</>}
        {cb.last_run_at && <> · last run {timeAgo(cb.last_run_at)}</>}
      </Typography>
    );
  };

  const renderPlanCard = (plan: TestPlanSummary) => (
    <Accordion key={plan.id} disableGutters elevation={0} sx={{ border: '1px solid', borderColor: 'divider', '&:before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ width: '100%' }}>
          <AssignmentIcon fontSize="small" color="action" />
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {plan.title}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
              target: <code>{plan.target}</code> · {plan.step_count} step{plan.step_count === 1 ? '' : 's'}
              {plan.generated_at && <> · {timeAgo(plan.generated_at)}</>}
              <> · <code>{plan.path}</code></>
            </Typography>
          </Box>
          <Chip
            label={plan.status}
            size="small"
            color={statusColor(plan.status)}
            sx={{ fontSize: '0.7rem', height: 20, textTransform: 'capitalize' }}
          />
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        <Box
          sx={{
            maxHeight: 400,
            overflow: 'auto',
            fontSize: '0.85rem',
            '& h1': { fontSize: '1.05rem', mt: 0 },
            '& h2': { fontSize: '0.95rem', mt: 1.5, mb: 0.5 },
            '& h3': { fontSize: '0.88rem' },
            '& ul, & ol': { pl: 3, my: 0.5 },
            '& code': { bgcolor: 'action.hover', px: 0.5, borderRadius: 0.5, fontSize: '0.85em' },
            '& pre': { bgcolor: 'action.hover', p: 1, borderRadius: 1, overflow: 'auto', fontSize: '0.78rem' },
            '& table': { borderCollapse: 'collapse', width: '100%', my: 1, fontSize: '0.78rem' },
            '& th, & td': { border: '1px solid', borderColor: 'divider', px: 0.75, py: 0.4, textAlign: 'left' },
          }}
        >
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{plan.markdown}</ReactMarkdown>
        </Box>
      </AccordionDetails>
    </Accordion>
  );

  return (
    <Paper sx={{ p: 2 }} data-testid="test-plans-card">
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Typography variant="h6">Test Plans</Typography>
          <Badge
            badgeContent={data?.total ?? 0}
            color="primary"
            showZero
            max={99}
            sx={{ '& .MuiBadge-badge': { position: 'static', transform: 'none' } }}
          />
        </Stack>
        <SkillActionButton
          skill="test-plan"
          label="Generate for chatbot"
          context="gachatbot"
          tooltip="Queues a /test-plan invocation tagged for the GA chatbot surface — an agent picks it up and writes to state/quality/test-plans/."
        />
      </Stack>

      {error && (
        <Alert severity="warning" sx={{ mb: 1, py: 0, fontSize: '0.8rem' }}>
          Failed to load /dev-data/test-plans: {error}
        </Alert>
      )}

      {loading && !data && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
          <CircularProgress size={20} />
        </Box>
      )}

      {data && data.plans.length === 0 && (
        <Card variant="outlined" sx={{ bgcolor: 'action.hover' }}>
          <CardContent>
            <Typography variant="body2" color="text.secondary">
              No test plans generated yet. Click <strong>Generate for chatbot</strong> to create one —
              the <code>/test-plan</code> skill writes to <code>state/quality/test-plans/</code>.
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
              See <code>.claude/skills/test-plan/SKILL.md</code> for the diff-driven heuristic. The skill
              also fires automatically on PR open via
              {' '}<code>.github/workflows/test-plan-suggester.yml</code>.
            </Typography>
          </CardContent>
        </Card>
      )}

      {data && data.plans.length > 0 && (
        <Stack spacing={1}>
          {data.plans.map(renderPlanCard)}
        </Stack>
      )}

      <Divider sx={{ my: 1.5 }} />
      <Box>{renderChatbotFooter(data?.chatbot_qa ?? null)}</Box>
    </Paper>
  );
};

export default TestPlansCard;
