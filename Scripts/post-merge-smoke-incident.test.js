const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const {
  fingerprintFailures,
  reportFailure,
  reportRecovery,
} = require('../.github/scripts/post-merge-smoke-incident');

function failure(overrides = {}) {
  return {
    url: 'https://demos.guitaralchemist.com/dev-data/manifest',
    status: 0,
    marker_found: false,
    unreachable: true,
    gateway_error: false,
    ...overrides,
  };
}

function context(sha = 'b363c3f086608f850be026546f85ef13c6e6bfb8') {
  return {
    repo: { owner: 'GuitarAlchemist', repo: 'ga' },
    serverUrl: 'https://github.com',
    runId: 35654652028,
    sha,
  };
}

const generatedByWorkflow = 'This issue was opened automatically by `.github/workflows/post-merge-smoke.yml`.';

function currentIncident({ number = 712, fingerprint, state = 'open' }) {
  return {
    number,
    state,
    title: `[post-merge-smoke] Live demo incident ${fingerprint.substring(0, 12)}`,
    body: [
      `<!-- post-merge-smoke-fingerprint:${fingerprint} -->`,
      '## Live demo regression detected',
      '',
      generatedByWorkflow,
    ].join('\n'),
    labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }],
  };
}

function legacyIncident({ number = 711, state = 'open' } = {}) {
  return {
    number,
    state,
    title: '[post-merge-smoke] Live demo regression on 2efaf72',
    body: [
      '## Live demo regression detected',
      '',
      '### Failing URLs',
      '- `https://demos.guitaralchemist.com/dev-data/manifest` -> status=0, marker_found=false, unreachable=true',
      '',
      generatedByWorkflow,
    ].join('\n'),
    labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }],
  };
}

test('fingerprint is stable across check order and ignores run-specific data', () => {
  const first = [
    failure(),
    failure({ url: 'https://demos.guitaralchemist.com/chatbot/', status: 404, unreachable: false }),
  ];
  const reordered = [
    { ...first[1], latency_ms: 912, size_bytes: 14 },
    { ...first[0], latency_ms: 3, size_bytes: 0 },
  ];

  assert.equal(fingerprintFailures(first), fingerprintFailures(reordered));
  assert.match(fingerprintFailures(first), /^[a-f0-9]{64}$/);
  assert.notEqual(fingerprintFailures(first), fingerprintFailures([failure({ status: 404, unreachable: false })]));
});

test('content fingerprints ignore co-occurring transient infrastructure failures but preserve distinct regressions', () => {
  const missingManifestMarker = failure({
    status: 200,
    marker_found: false,
    unreachable: false,
  });
  const transientGateway = failure({
    url: 'https://demos.guitaralchemist.com/chatbot/',
    status: 503,
    unreachable: false,
    gateway_error: true,
  });

  assert.equal(
    fingerprintFailures([missingManifestMarker]),
    fingerprintFailures([missingManifestMarker, transientGateway]),
  );
  assert.notEqual(
    fingerprintFailures([missingManifestMarker]),
    fingerprintFailures([{
      ...missingManifestMarker,
      url: 'https://demos.guitaralchemist.com/chatbot/',
    }]),
  );
  assert.notEqual(
    fingerprintFailures([missingManifestMarker]),
    fingerprintFailures([{ ...missingManifestMarker, status: 404 }]),
  );
});

test('an identical open incident gets a recurrence comment instead of a new issue', async () => {
  const fingerprint = fingerprintFailures([failure()]);
  const calls = { lists: [], comments: [], creates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async (args) => {
          calls.lists.push(args);
          return { data: [currentIncident({ fingerprint })] };
        },
        createComment: async (args) => calls.comments.push(args),
        create: async (args) => calls.creates.push(args),
      },
    },
  };

  const result = await reportFailure({
    github,
    context: context('2efaf72000000000000000000000000000000000'),
    failures: [failure()],
    artifactPath: 'state/quality/e2e/repeat.json',
  });

  assert.equal(result.action, 'deduplicated');
  assert.equal(result.issueNumber, 712);
  assert.equal(calls.creates.length, 0);
  assert.equal(calls.comments.length, 1);
  assert.equal(calls.comments[0].issue_number, 712);
  assert.match(calls.comments[0].body, /Incident recurred/);
  assert.equal(calls.lists[0].labels, 'post-merge-smoke,regression');
});

test('a matching closed current incident is reopened and reused', async () => {
  const fingerprint = fingerprintFailures([failure()]);
  const calls = { comments: [], creates: [], updates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({ data: [currentIncident({ fingerprint, state: 'closed' })] }),
        createComment: async (args) => calls.comments.push(args),
        create: async (args) => calls.creates.push(args),
        update: async (args) => calls.updates.push(args),
      },
    },
  };

  const result = await reportFailure({
    github,
    context: context(),
    failures: [failure()],
    artifactPath: 'state/quality/e2e/recurrence.json',
  });

  assert.equal(result.action, 'reopened');
  assert.equal(result.issueNumber, 712);
  assert.equal(calls.creates.length, 0);
  assert.equal(calls.comments.length, 1);
  assert.deepEqual(calls.updates, [{
    owner: 'GuitarAlchemist',
    repo: 'ga',
    issue_number: 712,
    state: 'open',
  }]);
});

test('a matching closed legacy incident is reopened and reused', async () => {
  const calls = { comments: [], creates: [], updates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({ data: [legacyIncident({ state: 'closed' })] }),
        createComment: async (args) => calls.comments.push(args),
        create: async (args) => calls.creates.push(args),
        update: async (args) => calls.updates.push(args),
      },
    },
  };

  const result = await reportFailure({
    github,
    context: context(),
    failures: [failure()],
    artifactPath: 'state/quality/e2e/legacy-recurrence.json',
  });

  assert.equal(result.action, 'reopened');
  assert.equal(result.issueNumber, 711);
  assert.equal(calls.creates.length, 0);
  assert.equal(calls.comments.length, 1);
  assert.equal(calls.updates[0].state, 'open');
});

test('a new failure fingerprint opens one tracking issue carrying the marker', async () => {
  const calls = { creates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({ data: [] }),
        createComment: async () => assert.fail('unexpected comment'),
        create: async (args) => {
          calls.creates.push(args);
          return { data: { number: 900 } };
        },
      },
    },
  };

  const result = await reportFailure({
    github,
    context: context(),
    failures: [failure()],
    artifactPath: 'state/quality/e2e/first.json',
  });

  const fingerprint = fingerprintFailures([failure()]);
  assert.equal(result.action, 'created');
  assert.equal(result.issueNumber, 900);
  assert.equal(calls.creates.length, 1);
  assert.match(calls.creates[0].body, new RegExp(`<!-- post-merge-smoke-fingerprint:${fingerprint} -->`));
  assert.deepEqual(calls.creates[0].labels, ['post-merge-smoke', 'regression']);
});

test('an equivalent pre-fingerprint issue is adopted instead of duplicated', async () => {
  const calls = { comments: [], creates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({ data: [{
          ...legacyIncident({ number: 712 }),
        }] }),
        createComment: async (args) => calls.comments.push(args),
        create: async (args) => calls.creates.push(args),
      },
    },
  };

  const result = await reportFailure({
    github,
    context: context('2efaf72000000000000000000000000000000000'),
    failures: [failure()],
    artifactPath: 'state/quality/e2e/repeat.json',
  });

  assert.equal(result.action, 'deduplicated');
  assert.equal(result.issueNumber, 712);
  assert.equal(calls.creates.length, 0);
  assert.equal(calls.comments.length, 1);
});

test('a healthy run records recovery and closes only open smoke regression issues', async () => {
  const fingerprint = fingerprintFailures([failure()]);
  const calls = { comments: [], updates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({
          data: [
            currentIncident({ number: 712, fingerprint }),
            legacyIncident({ number: 713 }),
            { number: 800, title: 'Human tracker', body: 'Not generated by smoke.', labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }] },
            { number: 801, pull_request: {}, labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }] },
          ],
        }),
        createComment: async (args) => calls.comments.push(args),
        update: async (args) => calls.updates.push(args),
      },
    },
  };

  const result = await reportRecovery({
    github,
    context: context('healthy00000000000000000000000000000000000'),
    artifactPath: 'state/quality/e2e/healthy.json',
  });

  assert.deepEqual(result, { action: 'recovered', issueNumbers: [712, 713] });
  assert.equal(calls.comments.length, 2);
  assert.match(calls.comments[0].body, /Recovery observed/);
  assert.deepEqual(calls.updates.map(({ issue_number }) => issue_number), [712, 713]);
});

test('workflow lifecycle creates, recovers, then reopens one incident through the wired transport', async () => {
  const repoRoot = path.resolve(__dirname, '..');
  const smokeWorkflow = fs.readFileSync(path.join(repoRoot, '.github/workflows/post-merge-smoke.yml'), 'utf8');
  const ciWorkflow = fs.readFileSync(path.join(repoRoot, '.github/workflows/ci.yml'), 'utf8');

  assert.match(smokeWorkflow, /require\(`\$\{process\.env\.GITHUB_WORKSPACE\}\/\.github\/scripts\/post-merge-smoke-incident\.js`\)/);
  assert.match(smokeWorkflow, /reportFailure\(\{[\s\S]*github,[\s\S]*context,[\s\S]*failures,[\s\S]*artifactPath:/);
  assert.match(smokeWorkflow, /reportRecovery\(\{[\s\S]*github,[\s\S]*context,[\s\S]*artifactPath:/);
  assert.match(ciWorkflow, /node --test Scripts\/post-merge-smoke-incident\.test\.js/);
  assert.doesNotMatch(ciWorkflow, /npm test/);

  const issues = [];
  const calls = { creates: 0, comments: [], updates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async ({ state }) => ({
          data: issues.filter((issue) => state === 'all' || issue.state === state),
        }),
        create: async (args) => {
          calls.creates += 1;
          const issue = {
            number: 900,
            state: 'open',
            title: args.title,
            body: args.body,
            labels: args.labels,
          };
          issues.push(issue);
          return { data: issue };
        },
        createComment: async (args) => calls.comments.push(args),
        update: async (args) => {
          calls.updates.push(args);
          const issue = issues.find(({ number }) => number === args.issue_number);
          issue.state = args.state;
          return { data: issue };
        },
      },
    },
  };
  const failures = [failure({ status: 200, unreachable: false })];

  const first = await reportFailure({ github, context: context('first'), failures, artifactPath: 'first.json' });
  const recovery = await reportRecovery({ github, context: context('healthy'), artifactPath: 'healthy.json' });
  const recurrence = await reportFailure({ github, context: context('repeat'), failures, artifactPath: 'repeat.json' });

  assert.equal(first.action, 'created');
  assert.deepEqual(recovery.issueNumbers, [900]);
  assert.equal(recurrence.action, 'reopened');
  assert.equal(recurrence.issueNumber, 900);
  assert.equal(calls.creates, 1);
  assert.equal(issues[0].state, 'open');
  assert.deepEqual(calls.updates.map(({ state }) => state), ['closed', 'open']);
});
