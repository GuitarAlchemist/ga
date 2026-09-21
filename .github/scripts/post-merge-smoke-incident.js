const { createHash } = require('node:crypto');

const FINGERPRINT_MARKER = 'post-merge-smoke-fingerprint';

function normalizeFailure(failure) {
  return {
    url: String(failure.url || ''),
    status: Number(failure.status || 0),
    marker_found: failure.marker_found === true,
    unreachable: failure.unreachable === true,
    gateway_error: failure.gateway_error === true,
  };
}

function fingerprintFailures(failures) {
  const canonical = failures
    .map(normalizeFailure)
    .sort((left, right) => JSON.stringify(left).localeCompare(JSON.stringify(right)));

  return createHash('sha256').update(JSON.stringify(canonical)).digest('hex');
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

async function reportFailure({ github, context, failures, artifactPath }) {
  if (!Array.isArray(failures) || failures.length === 0) {
    throw new Error('Cannot report a smoke incident without failures.');
  }

  const fingerprint = fingerprintFailures(failures);
  const marker = `${FINGERPRINT_MARKER}:${fingerprint}`;
  const existingIssues = await listOpenSmokeRegressions(github, context);
  const existingIssue = existingIssues.find((issue) => {
    if (issue.pull_request || typeof issue.body !== 'string') {
      return false;
    }
    if (issue.body.includes(`<!-- ${marker} -->`)) {
      return true;
    }
    const legacyFailures = failuresFromLegacyBody(issue.body);
    return legacyFailures.length > 0 && fingerprintFailures(legacyFailures) === fingerprint;
  });

  if (existingIssue) {
    await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: existingIssue.number,
      body: [
        '## Incident recurred',
        '',
        `**Incident fingerprint:** \`${fingerprint}\``,
        `**Merge commit:** ${context.sha}`,
        `**Workflow run:** ${workflowRunUrl(context)}`,
        `**Artifact:** \`${artifactPath}\``,
        '',
        '### Failing URLs',
        formatFailures(failures),
      ].join('\n'),
    });
    return { action: 'deduplicated', issueNumber: existingIssue.number, fingerprint };
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
      'This issue was opened automatically by `.github/workflows/post-merge-smoke.yml`.',
      'See [docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md](../blob/main/docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md) item #4 for the rationale.',
    ].join('\n'),
    labels: ['post-merge-smoke', 'regression'],
  });

  return { action: 'created', issueNumber: created.data.number, fingerprint };
}

async function listOpenSmokeRegressions(github, context) {
  const issues = [];
  let page = 1;
  while (true) {
    const response = await github.rest.issues.listForRepo({
      owner: context.repo.owner,
      repo: context.repo.repo,
      state: 'open',
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
  const candidates = await listOpenSmokeRegressions(github, context);
  const smokeRegressions = candidates.filter((issue) => {
    if (issue.pull_request) {
      return false;
    }
    const labels = new Set(issue.labels.map((label) => typeof label === 'string' ? label : label.name));
    return labels.has('post-merge-smoke') && labels.has('regression');
  });

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
