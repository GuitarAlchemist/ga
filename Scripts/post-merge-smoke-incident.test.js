const assert = require('node:assert/strict');
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

test('an identical open incident gets a recurrence comment instead of a new issue', async () => {
  const fingerprint = fingerprintFailures([failure()]);
  const calls = { lists: [], comments: [], creates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async (args) => {
          calls.lists.push(args);
          return { data: [{
            number: 712,
            body: `<!-- post-merge-smoke-fingerprint:${fingerprint} -->`,
            labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }],
          }] };
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
          number: 712,
          body: [
            '## Live demo regression detected',
            '',
            '### Failing URLs',
            '- `https://demos.guitaralchemist.com/dev-data/manifest` -> status=0, marker_found=false, unreachable=true',
          ].join('\n'),
          labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }],
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
  const calls = { comments: [], updates: [] };
  const github = {
    rest: {
      issues: {
        listForRepo: async () => ({
          data: [
            { number: 712, labels: [{ name: 'post-merge-smoke' }, { name: 'regression' }] },
            { number: 800, labels: [{ name: 'post-merge-smoke' }] },
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

  assert.deepEqual(result, { action: 'recovered', issueNumbers: [712] });
  assert.equal(calls.comments.length, 1);
  assert.match(calls.comments[0].body, /Recovery observed/);
  assert.deepEqual(calls.updates, [{
    owner: 'GuitarAlchemist',
    repo: 'ga',
    issue_number: 712,
    state: 'closed',
    state_reason: 'completed',
  }]);
});
