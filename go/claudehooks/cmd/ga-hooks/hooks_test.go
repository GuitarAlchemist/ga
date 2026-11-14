package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

// fakeSystem: a repository at root (git answers from a table), no gh, no HTTP server, a fixed
// clock, the working directory at root. Tests never start a real program or touch the network.
func fakeSystem(root string, now time.Time) *system {
	return &system{
		now:    func() time.Time { return now },
		getenv: func(string) string { return "" },
		getwd:  func() (string, error) { return root, nil },
		output: func(dir, name string, args ...string) (string, bool) {
			key := name + " " + strings.Join(args, " ")
			switch {
			case strings.HasSuffix(key, "rev-parse --abbrev-ref HEAD"):
				return "feat/x", true
			case strings.HasSuffix(key, "rev-parse --short HEAD"):
				return "abc1234", true
			case strings.HasSuffix(key, "log -1 --format=%s"):
				return "feat: it's done", true
			case strings.HasSuffix(key, "diff --shortstat HEAD~1"):
				return "2 files changed, 3 insertions(+)", true
			case key == "git branch --show-current":
				return "feat/x", true
			}
			return "", false
		},
		fetch: func(url string, _ time.Duration) (int, []byte, error) {
			return 0, nil, errors.New("connection refused: " + url)
		},
		executable: func() (string, error) { return "", errors.New("no executable in tests") },
		toplevel:   func() (string, bool) { return root, root != "" },
	}
}

// recording wraps sys so that every process it would start and every URL it would fetch is
// appended to *calls.
func recording(sys *system, calls *[]string) *system {
	output, fetch := sys.output, sys.fetch
	sys.output = func(dir, name string, args ...string) (string, bool) {
		*calls = append(*calls, name+" "+strings.Join(args, " "))
		return output(dir, name, args...)
	}
	sys.fetch = func(url string, limit time.Duration) (int, []byte, error) {
		*calls = append(*calls, "fetch "+url)
		return fetch(url, limit)
	}
	return sys
}

func runHook(t *testing.T, sys *system, name, stdin string, args ...string) (int, string, string) {
	t.Helper()
	var out, errb bytes.Buffer
	code := dispatch(append([]string{name}, args...), &env{stdin: strings.NewReader(stdin), stdout: &out, stderr: &errb, sys: sys})
	return code, out.String(), errb.String()
}

func editPayload(tool, file string) string {
	b, _ := json.Marshal(map[string]any{"tool_name": tool, "tool_input": map[string]any{"file_path": file}})
	return string(b)
}

func writeDigest(t *testing.T, root, trigger string, age time.Duration, now time.Time) string {
	t.Helper()
	d := digestsOf(root)
	if err := os.MkdirAll(d.dir, 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(d.latest, []byte("---\nschema_version: 1\ntrigger: "+trigger+"\n---\n\nDIGEST-MARKER\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	mt := now.Add(-age)
	if err := os.Chtimes(d.latest, mt, mt); err != nil {
		t.Fatal(err)
	}
	return d.latest
}

// The hooks that fire on every prompt or every tool call start no process when they have
// nothing to do: each one cost 1 to 2.5 s on a loaded Windows machine.
func TestHotHooksStartNoProcess(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	writeDigest(t, root, "digest-skill", 5*time.Minute, now)
	for _, c := range []struct{ hook, stdin string }{
		{"correction-detector", `{"prompt":"no, not that file"}`},
		{"digest-staleness-nudge", `{"prompt":"continue"}`},
		{"digest-activity-tracker", ""},
		{"pr-rationale-capture", `{"tool_input":{"command":"gh pr view 1"}}`},
		{"dsl-changed", editPayload("Edit", "/r/Common/GA.Business.DSL/Parser.fs")},
		{"ai-annotation-scan", editPayload("Edit", filepath.Join(root, "missing.cs"))},
		{"ai-annotation-merge-gate", `{"tool_input":{"command":"gh pr view 1"}}`},
		{"ai-annotation-merge-gate", `{"tool_input":{"command":"gh pr merge 1"}}`}, // no report
		{"loops-goals-tracker", `{"prompt":"continue","session_id":"s"}`},
	} {
		var calls []string
		args := []string(nil)
		if c.hook == "loops-goals-tracker" {
			args = []string{"prompt"}
		}
		runHook(t, recording(fakeSystem(root, now), &calls), c.hook, c.stdin, args...)
		if len(calls) != 0 {
			t.Errorf("%s %s started %v", c.hook, c.stdin, calls)
		}
	}
}

// The repository root is found without starting git, from a nested directory, for a checkout
// (.git directory) and a worktree (.git file).
func TestFindTopLevel(t *testing.T) {
	for _, worktree := range []bool{false, true} {
		root := t.TempDir()
		var err error
		if worktree {
			err = os.WriteFile(filepath.Join(root, ".git"), []byte("gitdir: elsewhere\n"), 0o644)
		} else {
			err = os.Mkdir(filepath.Join(root, ".git"), 0o755)
		}
		nested := filepath.Join(root, "a", "b")
		if err != nil || os.MkdirAll(nested, 0o755) != nil {
			t.Fatal("setup")
		}
		for _, from := range []string{root, nested} {
			got, ok := findTopLevel(from)
			if !ok || got != filepath.ToSlash(root) {
				t.Errorf("worktree=%v from %s: got %q %v, want %q", worktree, from, got, ok, filepath.ToSlash(root))
			}
		}
	}
}

func TestCorrectionDetector(t *testing.T) {
	now := time.Now()
	fires := []string{
		"no, use the worktree",
		"No, don't amend after CI passed.",
		"Don't rewrite the README.",
		"dont touch main",
		"Stop rewriting the README.",
		"Wait, that is not what I asked.",
		"Actually we discussed this before.",
		"That's wrong, the port is 5176.",
		"thats wrong",
		"Incorrect: it is F#.",
	}
	for _, prompt := range fires {
		root := t.TempDir() // a fresh throttle counter: the first match fires
		in, _ := json.Marshal(map[string]string{"prompt": prompt})
		code, out, _ := runHook(t, fakeSystem(root, now), "correction-detector", string(in))
		var v map[string]string
		if code != 0 || json.Unmarshal([]byte(out), &v) != nil || !strings.HasPrefix(v["additionalContext"], "[correction-nudge] Detected correction language.") || strings.Contains(out, "\n  ") {
			t.Errorf("fires: %q: exit %d, out %q", prompt, code, out)
		}
	}
	silent := map[string]string{
		"a request":                   `{"prompt": "Can you run the tests on Windows?"}`,
		"no as part of a word":        `{"prompt": "Nothing to change, go on."}`,
		"an opener after the start":   `{"prompt": "Thanks.\nno, not this file"}`,
		"an opener after 200 chars":   `{"prompt": "` + strings.Repeat("x", 200) + ` no"}`,
		"french is not matched":       `{"prompt": "Non, pas comme ça."}`,
		"empty prompt":                `{"prompt": ""}`,
		"empty stdin":                 ``,
		"raw text is not read":        `no, raw text`,
		"user_prompt is read, no hit": `{"user_prompt": "The build passes."}`,
	}
	for desc, in := range silent {
		if code, out, _ := runHook(t, fakeSystem(t.TempDir(), now), "correction-detector", in); code != 0 || out != "" {
			t.Errorf("silent: %s: exit %d, out %q", desc, code, out)
		}
	}
	if _, out, _ := runHook(t, fakeSystem(t.TempDir(), now), "correction-detector", `{"user_prompt": "stop that"}`); out == "" {
		t.Error("user_prompt is not read")
	}
	if _, out, _ := runHook(t, fakeSystem("", now), "correction-detector", `{"prompt": "stop"}`); out != "" {
		t.Errorf("outside a repository: %q", out)
	}
}

// Fires on the 1st match, then on every GA_CORRECTION_THROTTLE-th (5 by default).
func TestCorrectionDetectorThrottle(t *testing.T) {
	for _, c := range []struct {
		throttle string
		want     string
	}{
		{"", "1000100001"},  // matches 1..10 with the default 5
		{"3", "1010010010"}, // 1, 3, 6, 9
		{"x", "1000100001"}, // not a number: the default
		{"0", "1000000000"}, // the first match only
	} {
		root := t.TempDir()
		sys := fakeSystem(root, time.Now())
		sys.getenv = func(name string) string {
			if name == "GA_CORRECTION_THROTTLE" {
				return c.throttle
			}
			return ""
		}
		got := ""
		for i := 0; i < 10; i++ {
			if _, out, _ := runHook(t, sys, "correction-detector", `{"prompt":"no"}`); out != "" {
				got += "1"
			} else {
				got += "0"
			}
		}
		if got != c.want {
			t.Errorf("throttle %q: fired %s, want %s", c.throttle, got, c.want)
		}
		if n := counter(filepath.Join(digestsOf(root).dir, ".correction-counter")); n != 10 {
			t.Errorf("throttle %q: counter %d", c.throttle, n)
		}
	}
}

func TestSessionstartDigest(t *testing.T) {
	now := time.Now()
	root := t.TempDir()
	if code, out, _ := runHook(t, fakeSystem(root, now), "sessionstart-digest", ""); code != 0 || out != "" {
		t.Errorf("no digest: %d %q", code, out)
	}
	for _, c := range []struct {
		age  time.Duration
		want string
	}{
		{5 * time.Minute, "=== Session digest (last written 5 min ago) ===\n---\nschema_version: 1"},
		{90 * time.Minute, "last written 1h 30m ago"},
		{23*time.Hour + 59*time.Minute, "last written 23h 59m ago"},
	} {
		writeDigest(t, root, "activity-tracker-mid-session", c.age, now) // stubs are injected too
		_, out, _ := runHook(t, fakeSystem(root, now), "sessionstart-digest", `{"source":"compact"}`)
		if !strings.Contains(out, c.want) || !strings.Contains(out, "DIGEST-MARKER\n=== End digest ===\n\n") {
			t.Errorf("age %v: %q", c.age, out)
		}
	}
	writeDigest(t, root, "digest-skill", 25*time.Hour, now)
	if _, out, _ := runHook(t, fakeSystem(root, now), "sessionstart-digest", ""); strings.Contains(out, "DIGEST-MARKER") || !strings.Contains(out, ">24h old") {
		t.Errorf("a day-old digest is injected: %q", out)
	}
}

func TestSessionStartInstallsTheBinaryThenInjects(t *testing.T) {
	now := time.Now()
	root := t.TempDir()
	writeDigest(t, root, "digest-skill", time.Minute, now)
	exe := filepath.Join(t.TempDir(), "built")
	if err := os.WriteFile(exe, []byte("v1"), 0o755); err != nil {
		t.Fatal(err)
	}
	sys := fakeSystem(root, now)
	sys.executable = func() (string, error) { return exe, nil }
	if _, out, _ := runHook(t, sys, "session-start", `{"source":"resume"}`); !strings.Contains(out, "DIGEST-MARKER") {
		t.Errorf("digest not injected: %q", out)
	}
	if got, _ := os.ReadFile(binPath(root)); string(got) != "v1" {
		t.Fatalf("installed %q", got)
	}
	if err := os.WriteFile(exe, []byte("v2"), 0o755); err != nil {
		t.Fatal(err)
	}
	runHook(t, sys, "session-start", "{}")
	if got, _ := os.ReadFile(binPath(root)); string(got) != "v2" {
		t.Fatalf("not refreshed: %q", got)
	}
	left, _ := filepath.Glob(filepath.Join(filepath.Dir(binPath(root)), ".ga-hooks-*.tmp"))
	if len(left) != 0 {
		t.Fatalf("temporary files left: %v", left)
	}
	// A failed install says so and still injects the digest.
	sys.executable = func() (string, error) { return "", errors.New("gone") }
	if code, out, errs := runHook(t, sys, "session-start", `{"source":"compact"}`); code != 0 || !strings.Contains(errs, "could not install") || !strings.Contains(out, "DIGEST-MARKER") {
		t.Errorf("failed install: %d %q %q", code, out, errs)
	}
}

func TestDigestStalenessNudge(t *testing.T) {
	now := time.Now()
	root := t.TempDir()
	d := digestsOf(root)
	nudge := func() string {
		_, out, _ := runHook(t, fakeSystem(root, now), "digest-staleness-nudge", "")
		return out
	}
	if out := nudge(); out != `{"additionalContext":"[digest-nudge] No session digest exists yet. Invoke /digest at your next natural breakpoint to seed it."}`+"\n" {
		t.Errorf("missing digest: %q", out)
	}
	writeDigest(t, root, "digest-skill", 45*time.Minute, now)
	if out := nudge(); out != "" {
		t.Errorf("no mutations, 45 min: nudged %q", out)
	}
	if err := os.WriteFile(filepath.Join(d.dir, ".activity-counter"), []byte("11\r\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	if out := nudge(); !strings.Contains(out, "Last digest 45 min ago; 11 mutations since. Karpathy R4: task complete != goal achieved — consider") {
		t.Errorf("drifted digest: %q", out)
	}
	// [int] rounds: 30.5 min reads 30 (halves to even), not over the threshold.
	writeDigest(t, root, "digest-skill", 30*time.Minute+30*time.Second, now)
	if out := nudge(); out != "" {
		t.Errorf("30.5 min nudged: %q", out)
	}
	writeDigest(t, root, "digest-skill", 30*time.Minute+31*time.Second, now)
	if out := nudge(); !strings.Contains(out, "Last digest 31 min ago") {
		t.Errorf("30.52 min rounds to 31: %q", out)
	}
	writeDigest(t, root, "digest-skill", 100*time.Minute, now)
	if err := os.WriteFile(filepath.Join(d.dir, ".activity-counter"), []byte("x"), 0o644); err != nil {
		t.Fatal(err)
	}
	if out := nudge(); !strings.Contains(out, "Last digest 100 min ago. Session drifting") {
		t.Errorf("old digest: %q", out)
	}
	if _, out, _ := runHook(t, fakeSystem("", now), "digest-staleness-nudge", ""); out != "" {
		t.Errorf("outside a repository: %q", out)
	}
}

func TestDigestActivityTrackerWritesAStubAtTheThreshold(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	writeDigest(t, root, "digest-skill", time.Minute, now)
	sys := fakeSystem(root, now)
	for i := 0; i < 19; i++ {
		runHook(t, sys, "digest-activity-tracker", "")
	}
	d := digestsOf(root)
	if data, _ := os.ReadFile(d.latest); !strings.Contains(string(data), "DIGEST-MARKER") {
		t.Fatal("stub written before the threshold")
	}
	runHook(t, sys, "digest-activity-tracker", "")
	data, _ := os.ReadFile(d.latest)
	for _, want := range []string{"session_id: mid-session-auto", "trigger: activity-tracker-mid-session", "branch: feat/x", "mutations_since_last: 20",
		"**Branch:** feat/x @ abc1234 — feat: it's done", "after 20 mutations / 1m since last digest."} {
		if !strings.Contains(string(data), want) {
			t.Errorf("stub lacks %q:\n%s", want, data)
		}
	}
	if mid, _ := os.ReadFile(filepath.Join(d.dir, "mid-2026-11-05T12-00-00Z.md")); !bytes.Equal(mid, data) {
		t.Error("mid-<ts>.md is not the copy of latest.md")
	}
	if counter(filepath.Join(d.dir, ".activity-count")) != 0 || counter(filepath.Join(d.dir, ".activity-counter")) != 20 {
		t.Fatal("counters not reset/kept")
	}
	if _, err := os.Stat(filepath.Join(d.archive, "2026-11-05T12-00-00Z-pre-mid.md")); err != nil {
		t.Fatal("previous digest not archived")
	}
}

// After GA_DIGEST_MID_MIN minutes, 3 mutations are enough; 2 are not.
func TestDigestActivityTrackerByAge(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	writeDigest(t, root, "digest-skill", 10*time.Minute, now)
	sys := fakeSystem(root, now)
	sys.getenv = func(name string) string {
		if name == "GA_DIGEST_MID_MIN" {
			return "10"
		}
		return ""
	}
	runHook(t, sys, "digest-activity-tracker", "")
	runHook(t, sys, "digest-activity-tracker", "")
	if data, _ := os.ReadFile(digestsOf(root).latest); !strings.Contains(string(data), "DIGEST-MARKER") {
		t.Fatal("stub after 2 mutations")
	}
	runHook(t, sys, "digest-activity-tracker", "")
	if data, _ := os.ReadFile(digestsOf(root).latest); !strings.Contains(string(data), "after 3 mutations / 10m since last digest") {
		t.Fatalf("no stub after 3 mutations and 10 min: %s", data)
	}
	// No digest at all reads as 99999 minutes old.
	root = t.TempDir()
	sys = fakeSystem(root, now)
	for i := 0; i < 3; i++ {
		runHook(t, sys, "digest-activity-tracker", "")
	}
	if data, _ := os.ReadFile(digestsOf(root).latest); !strings.Contains(string(data), "/ 99999m since") {
		t.Fatalf("no stub without a digest: %s", data)
	}
}

func TestStopAndPrecompactDigests(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	sys := fakeSystem(root, now)
	runHook(t, sys, "digest-stop-finalize", "")
	d := digestsOf(root)
	data, _ := os.ReadFile(d.latest)
	for _, want := range []string{"session_id: stop-finalize\nwritten_at: 2026-11-05T12:00:00Z\ntrigger: stop-hook-finalize\nbranch: 'feat/x'\nhead_sha: 'abc1234'\nhead_subject: 'feat: it''s done'\nopen_pr: null\n---\n",
		"**Branch:** feat/x @ abc1234 — feat: it's done\n\n## Model-driven sections", "are in `state/digests/archive/`._\n"} {
		if !strings.Contains(string(data), want) {
			t.Errorf("stop digest lacks %q:\n%s", want, data)
		}
	}
	if code, _, errs := runHook(t, sys, "digest-validate", ""); code != 0 {
		t.Fatalf("stop digest invalid: %s", errs)
	}
	// Written 9 minutes ago: Stop leaves it.
	if err := os.Chtimes(d.latest, now.Add(-9*time.Minute), now.Add(-9*time.Minute)); err != nil {
		t.Fatal(err)
	}
	runHook(t, sys, "digest-stop-finalize", "")
	if _, err := os.Stat(filepath.Join(d.archive, "2026-11-05T12-00-00Z-stop.md")); err == nil {
		t.Fatal("a 9-minute-old digest was finalized")
	}
	// PreCompact archives it under the session id and keeps it while it is under 30 minutes.
	runHook(t, sys, "precompact-digest", `{"session_id":"../../evil"}`)
	if _, err := os.Stat(filepath.Join(d.archive, "2026-11-05T12-00-00Z-unknown.md")); err != nil {
		t.Fatal("a traversing session id was not replaced by 'unknown'")
	}
	if data2, _ := os.ReadFile(d.latest); !bytes.Equal(data, data2) {
		t.Fatal("a fresh digest was overwritten before compaction")
	}
	if err := os.Chtimes(d.latest, now.Add(-time.Hour), now.Add(-time.Hour)); err != nil {
		t.Fatal(err)
	}
	// An open pull request is named; a long session id is quoted and cut with "...".
	sys.output = func(dir, name string, args ...string) (string, bool) {
		if name == "gh" {
			return `{"number":42}`, true
		}
		return fakeSystem(root, now).output(dir, name, args...)
	}
	long := strings.Repeat("s", 210)
	runHook(t, sys, "precompact-digest", `{"session_id":"`+long+`"}`)
	data, _ = os.ReadFile(d.latest)
	for _, want := range []string{"session_id: '" + strings.Repeat("s", 200) + "...'", "trigger: precompact-hook-fallback", "open_pr: '#42'",
		"feat: it's done\n**Open PR:** #42\n\n## Model-driven sections", "compaction event._\n"} {
		if !strings.Contains(string(data), want) {
			t.Errorf("precompact digest lacks %q:\n%s", want, data)
		}
	}
	if _, err := os.Stat(filepath.Join(d.archive, "2026-11-05T12-00-00Z-"+strings.Repeat("s", 64)+".md")); err != nil {
		t.Error("the archive name does not carry the session id cut to 64")
	}
}

// When the archive cannot be written (a file where the archive directory should be), each hook
// that replaces latest.md keeps the previous digest instead of losing it, says so on stderr and
// still exits 0.
func TestAFailedArchiveKeepsTheLatestDigest(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	for _, c := range []struct{ hook, stdin, env string }{
		{"digest-activity-tracker", "", "GA_DIGEST_MID_COUNT"},
		{"digest-stop-finalize", "", ""},
		{"precompact-digest", `{"session_id":"s-1"}`, ""},
	} {
		root := t.TempDir()
		latest := writeDigest(t, root, "precompact-hook-fallback", 2*time.Hour, now) // not a /digest write: Stop replaces it
		before, _ := os.ReadFile(latest)
		if err := os.WriteFile(digestsOf(root).archive, []byte("not a directory\n"), 0o644); err != nil {
			t.Fatal(err)
		}
		sys := fakeSystem(root, now)
		if c.env != "" {
			sys.getenv = func(name string) string {
				if name == c.env {
					return "1"
				}
				return ""
			}
		}
		code, _, errs := runHook(t, sys, c.hook, c.stdin)
		after, _ := os.ReadFile(latest)
		if code != 0 || !bytes.Equal(before, after) || !strings.Contains(errs, "could not archive latest.md, kept as it is") {
			t.Errorf("%s: exit %d, stderr %q, latest.md now %q", c.hook, code, errs, after)
		}
	}
}

// With a writable archive the same three hooks archive, then replace latest.md, leaving no
// temporary file behind.
func TestAnArchivedDigestIsReplaced(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	for _, c := range []struct{ hook, stdin, archived string }{
		{"digest-activity-tracker", "", "2026-11-05T12-00-00Z-pre-mid.md"},
		{"digest-stop-finalize", "", "2026-11-05T12-00-00Z-stop.md"},
		{"precompact-digest", `{"session_id":"s-1"}`, "2026-11-05T12-00-00Z-s-1.md"},
	} {
		root := t.TempDir()
		latest := writeDigest(t, root, "precompact-hook-fallback", 2*time.Hour, now) // not a /digest write: Stop replaces it
		sys := fakeSystem(root, now)
		sys.getenv = func(name string) string {
			if name == "GA_DIGEST_MID_COUNT" {
				return "1"
			}
			return ""
		}
		runHook(t, sys, c.hook, c.stdin)
		archived, _ := os.ReadFile(filepath.Join(digestsOf(root).archive, c.archived))
		after, _ := os.ReadFile(latest)
		left, _ := filepath.Glob(filepath.Join(digestsOf(root).dir, ".latest-*.tmp"))
		if !strings.Contains(string(archived), "DIGEST-MARKER") || strings.Contains(string(after), "DIGEST-MARKER") || len(left) != 0 {
			t.Errorf("%s: archived %q, latest %q, temporary files %v", c.hook, archived, after, left)
		}
	}
}

func TestDigestValidate(t *testing.T) {
	root := t.TempDir()
	p := filepath.Join(root, "d.md")
	const ok = "---\nschema_version: 1\nsession_id: s\nwritten_at: t\ntrigger: digest-skill\nbranch: b\nhead_sha: h\nhead_subject: x\n---\n"
	for body, want := range map[string]string{
		ok:                                   "",
		strings.ReplaceAll(ok, "\n", "\r\n"): "",
		"\ufeff" + ok:                        "",
		strings.Replace(ok, "digest-skill", "Digest-Skill", 1): "", // -in ignores case
		"no frontmatter\n": "missing or malformed YAML frontmatter in " + p,
		strings.Replace(ok, "head_subject: x\n", "", 1):                        "required field 'head_subject' missing or null",
		strings.Replace(ok, "branch: b", "branch: null", 1):                    "required field 'branch' missing or null",
		strings.Replace(ok, "branch: b", "branch:   ", 1):                      "required field 'branch' missing or null",
		strings.Replace(ok, "schema_version: 1", "schema_version: 2", 1):       "schema_version must be 1 (got '2')",
		strings.Replace(ok, "digest-skill", "other", 1):                        "trigger 'other' not in digest-skill, precompact-hook-fallback, stop-hook-finalize, auto-write-routine",
		strings.Replace(ok, "digest-skill", "activity-tracker-mid-session", 1): "not in",
	} {
		if err := os.WriteFile(p, []byte(body), 0o644); err != nil {
			t.Fatal(err)
		}
		code, _, errs := runHook(t, fakeSystem(root, time.Now()), "digest-validate", "", p)
		if (want == "") != (code == 0) || !strings.Contains(errs, want) || (want != "" && code != 1) {
			t.Errorf("%q: exit %d, stderr %q, want %q", body, code, errs, want)
		}
	}
	// -DigestPath as the script took it; a missing file and no repository both pass.
	if err := os.WriteFile(p, []byte("no frontmatter\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	if code, _, _ := runHook(t, fakeSystem(root, time.Now()), "digest-validate", "", "-DigestPath", p); code != 1 {
		t.Errorf("-DigestPath ignored: %d", code)
	}
	if code, _, _ := runHook(t, fakeSystem(root, time.Now()), "digest-validate", "", filepath.Join(root, "none.md")); code != 0 {
		t.Errorf("missing file: %d", code)
	}
	if code, _, _ := runHook(t, fakeSystem("", time.Now()), "digest-validate", "", p); code != 0 {
		t.Errorf("outside a repository: %d", code)
	}
}

func TestPRRationaleCapture(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	in, _ := json.Marshal(map[string]any{
		"tool_input":    map[string]any{"command": `gh pr create --title "Fix: the Thing!" --body 'why it''s "here" matters' --base main`},
		"tool_response": map[string]any{"output": "https://github.com/o/r/pull/42\n"},
	})
	runHook(t, fakeSystem(root, now), "pr-rationale-capture", string(in))
	data, err := os.ReadFile(filepath.Join(root, "state", "digests", "pr-42-fix-the-thing.md"))
	if err != nil {
		t.Fatal(err)
	}
	for _, want := range []string{"captured_at: 2026-11-05T12:00:00Z\nbranch: feat/x\npr_number: 42\ndiff_shortstat:  2 files changed, 3 insertions(+)\n---",
		"# PR #42 — Fix: the Thing!", "## Body\n\nwhy it''s \"here\" matters\n"} {
		if !strings.Contains(string(data), want) {
			t.Errorf("lacks %q:\n%s", want, data)
		}
	}
	// A string tool_response, a body ending the command, no number in the output.
	in, _ = json.Marshal(map[string]any{
		"tool_input":    map[string]any{"command": `GH PR CREATE --title '` + strings.Repeat("ab ", 20) + `' --body "last"`},
		"tool_response": "created",
	})
	runHook(t, fakeSystem(root, now), "pr-rationale-capture", string(in))
	data, err = os.ReadFile(filepath.Join(root, "state", "digests", "pr-unknown-"+strings.Repeat("ab-", 13)+"a.md"))
	if err != nil || !strings.Contains(string(data), "## Body\n\nlast\n") {
		t.Fatalf("%v %s", err, data)
	}
	other, _ := json.Marshal(map[string]any{"tool_input": map[string]any{"command": "gh pr view 1"}})
	runHook(t, fakeSystem(root, now), "pr-rationale-capture", string(other))
	if all, _ := filepath.Glob(filepath.Join(root, "state", "digests", "pr-*.md")); len(all) != 2 {
		t.Fatalf("captured a non-create command: %v", all)
	}
}

func TestPRSlugAndBody(t *testing.T) {
	for title, want := range map[string]string{
		"":                             "untitled",
		"!!!":                          "untitled",
		"Été — déjà vu":                "t-d-j-vu",
		strings.Repeat("a", 39) + " b": strings.Repeat("a", 39), // cut at 40, then the dash trimmed
	} {
		if got := prSlug(title); got != want {
			t.Errorf("prSlug(%q) = %q, want %q", title, got, want)
		}
	}
	for cmd, want := range map[string]string{
		`gh pr create --body "a" --title x`:      "a",
		`gh pr create --body "a"b" --x`:          `a"b`,
		`gh pr create --body "unterminated`:      "",
		`gh pr create --body "$(cat <<'EOF'`:     "",
		`gh pr create --body 'one' --body 'two'`: "one",
	} {
		if got := bodyFlag(cmd); got != want {
			t.Errorf("bodyFlag(%q) = %q, want %q", cmd, got, want)
		}
	}
}

func TestUnknownHookDoesNotBlock(t *testing.T) {
	if code, _, errs := runHook(t, fakeSystem("", time.Now()), "no-such-hook", ""); code == 2 || !strings.Contains(errs, "usage") {
		t.Fatalf("exit %d %q", code, errs)
	}
}
