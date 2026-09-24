const { createHash } = require('node:crypto');

const FINGERPRINT_MARKER = 'post-merge-smoke-fingerprint';
const GENERATED_BY_WORKFLOW = 'This issue was opened automatically by `.github/workflows/post-merge-smoke.yml`.';
const LEGACY_TITLE = /^\[post-merge-smoke\] Live demo regression on [0-9a-f]{7}$/;
const CURRENT_MARKER = /<!-- post-merge-smoke-fingerprint:[a-f0-9]{64} -->/;

function isGatewayStatus(status) {
  return status === 502 || status === 503 || status === 504 || (status >= 520 && status <= 530);
}

function normalizeFailure(failure) {
  const status = Number(failure.status || 0);
  return {
    url: String(failure.url || ''),
    status,
    marker_found: failure.marker_found === true,
    unreachable: failure.unreachable === true,
    gateway_error: failure.gateway_error === true || isGatewayStatus(status),
  };
}

function fingerprintMaterial(failures) {
  const normalized = failures.map(normalizeFailure);
  const contentFailures = normalized.filter((failure) => !failure.unreachable && !failure.gateway_error);

  // When a persistent content regression and transient infrastructure noise
  // happen in the same run, the durable content defect owns the incident.
  // URL + status + marker result remain in the material so distinct content
  // regressions do not collapse into one issue. Infra-only runs retain their
  // complete failure set.
  const scope = contentFailures.length > 0 ? 'content' : 'infrastructure';
  const canonical = (contentFailures.length > 0 ? contentFailures : normalized)
    .sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)));

  return { scope, failures: canonical };
}

function fingerprintFailures(failures) {
  const material = {
    schema: 'post-merge-smoke-fingerprint-v2',
    ...fingerprintMaterial(failures),
  };
  return createHash('sha256').update(JSON.stringify(material)).digest('hex');
}

function workflowRunUrl(context) {
  return `${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId}`;
}

function formatFailures(failures) {
  return failures.map((failure) =>
    `- \`${failure.url}\` -> status=${failure.status}, marker_found=${failure.marker_found}, unreachable=${failure.unreachable}`
  ).join('\n');
}

function failuresFromLegacyBody(body) {
  const failures = [];
  const line = /^- `([^`]+)` -> status=(\d+), marker_found=(true|false), unreachable=(true|false)$/gm;
  for (const match of body.matchAll(line)) {
    const status = Number(match[2]);
    failures.push({
      url: match[1],
      status,
      marker_found: match[3] === 'true',
      unreachable: match[4] === 'true',
      gateway_error: status === 502 || status === 503 || status === 504 || (status >= 520 && status <= 530),
    });
  }
  return failures;
}

function labelsFor(issue) {
  return new Set((issue.labels || []).map((label) => typeof label === 'string' ? label : label.name));
}

function isOwnedSmokeIncident(issue) {
  if (issue.pull_request || typeof issue.body !== 'string') {
    return false;
  }

  const labels = labelsFor(issue);
  if (!labels.has('post-merge-smoke') || !labels.has('regression')) {
    return false;
  }

  const generatedBody = issue.body.includes('## Live demo regression detected')
    && issue.body.includes(GENERATED_BY_WORKFLOW);
  if (!generatedBody) {
    return false;
  }

  const current = CURRENT_MARKER.test(issue.body);
  const legacy = LEGACY_TITLE.test(String(issue.title || ''))
    && failuresFromLegacyBody(issue.body).length > 0;
  return current || legacy;
}

async function reportFailure({ github, context, failures, artifactPath }) {
  if (!Array.isArray(failures) || failures.length === 0) {
    throw new Error('Cannot report a smoke incident without failures.');
  }

  const fingerprint = fingerprintFailures(failures);
  const fingerprintScope = fingerprintMaterial(failures).scope;
  const marker = `${FINGERPRINT_MARKER}:${fingerprint}`;
  const existingIssues = await listSmokeRegressions(github, context, 'all');
  const existingIssue = existingIssues.find((issue) => {
    if (!isOwnedSmokeIncident(issue)) {
      return false;
    }
    if (issue.body.includes(`<!-- ${marker} -->`)) {
      return true;
    }
    const legacyFailures = failuresFromLegacyBody(issue.body);
    return legacyFailures.length > 0 && fingerprintFailures(legacyFailures) === fingerprint;
  });

  if (existingIssue) {
    let action = 'deduplicated';
    if (existingIssue.state === 'closed') {
      await github.rest.issues.update({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: existingIssue.number,
        state: 'open',
      });
      action = 'reopened';
    }
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: existingIssue.number,
      body: [
        '## Incident recurred',
        '',
        `**Incident fingerprint:** \`${fingerprint}\``,
        `**Fingerprint scope:** ${fingerprintScope}`,
        `**Merge commit:** ${context.sha}`,
        `**Workflow run:** ${workflowRunUrl(context)}`,
        `**Artifact:** \`${artifactPath}\``,
        '',
        '### Failing URLs',
        formatFailures(failures),
      ].join('\n'),
    });
    return { action, issueNumber: existingIssue.number, fingerprint };
  }

  const created = await github.rest.issues.create({
    owner: context.repo.owner,
    repo: context.repo.repo,
    title: `[post-merge-smoke] Live demo incident ${fingerprint.substring(0, 12)}`,
    body: [
      `<!-- ${marker} -->`,
      '## Live demo regression detected',
      '',
      `**Incident fingerprint:** \`${fingerprint}\``,
      `**Fingerprint scope:** ${fingerprintScope}`,
      `**First observed merge:** ${context.sha}`,
      `**Workflow run:** ${workflowRunUrl(context)}`,
      `**Artifact:** \`${artifactPath}\``,
      '',
      '### Failing URLs',
      formatFailures(failures),
      '',
      '### Repro locally',
      '```powershell',
      'pwsh Scripts/post-merge-smoke.ps1',
      '```',
      '',
      GENERATED_BY_WORKFLOW,
      'See [docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md](../blob/main/docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md) item #4 for the rationale.',
    ].join('\n'),
    labels: ['post-merge-smoke', 'regression'],
  });

  return { action: 'created', issueNumber: created.data.number, fingerprint };
}

async function listSmokeRegressions(github, context, state) {
  const issues = [];
  let page = 1;
  while (true) {
    const response = await github.rest.issues.listForRepo({
      owner: context.repo.owner,
      repo: context.repo.repo,
      state,
      labels: 'post-merge-smoke,regression',
      per_page: 100,
      page,
    });
    issues.push(...response.data);
    if (response.data.length < 100) {
      break;
    }
    page += 1;
  }
  return issues;
}

async function reportRecovery({ github, context, artifactPath }) {
  const candidates = await listSmokeRegressions(github, context, 'open');
  const smokeRegressions = candidates.filter(isOwnedSmokeIncident);

  const issueNumbers = [];
  for (const issue of smokeRegressions) {
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issue.number,
      body: [
        '## Recovery observed',
        '',
        'All post-merge smoke checks passed.',
        `**Healthy merge:** ${context.sha}`,
        `**Workflow run:** ${workflowRunUrl(context)}`,
        `**Artifact:** \`${artifactPath}\``,
      ].join('\n'),
    });
    await github.rest.issues.update({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issue.number,
      state: 'closed',
      state_reason: 'completed',
    });
    issueNumbers.push(issue.number);
  }

  return { action: 'recovered', issueNumbers };
}

module.exports = {
  fingerprintFailures,
  reportFailure,
  reportRecovery,
};
