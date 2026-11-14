package main

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"testing"
	"time"
)

// Tests of the hooks ga had beyond the digest family that ix and tars share.

func withEnv(sys *system, vars map[string]string) *system {
	sys.getenv = func(name string) string { return vars[name] }
	return sys
}

func TestSessionStartBanner(t *testing.T) {
	now := time.Now()
	root := t.TempDir()
	writeDigest(t, root, "digest-skill", time.Minute, now)
	sys := fakeSystem(root, now)
	sys.fetch = func(url string, limit time.Duration) (int, []byte, error) {
		if limit != 3*time.Second {
			t.Errorf("%s: timeout %v", url, limit)
		}
		switch url {
		case "http://localhost:5176":
			return 200, []byte("<html>"), nil
		case "http://localhost:5232/health":
			return 200, []byte("Healthy"), nil
		}
		return 0, nil, errors.New("unexpected " + url)
	}
	const up = "\nGuitar Alchemist Session\n  Branch: feat/x\n  Vite: OK (200)\n  API: Healthy\n\n"
	// A fresh session: the banner only; the digest was injected on compact|resume alone.
	for _, in := range []string{`{"source":"startup"}`, `{"source":"clear"}`, ""} {
		if code, out, _ := runHook(t, sys, "session-start", in); code != 0 || out != up {
			t.Errorf("%s: exit %d, %q", in, code, out)
		}
	}
	if _, out, _ := runHook(t, sys, "session-start", `{"source":"compact"}`); !strings.HasPrefix(out, up+"\n=== Session digest") {
		t.Errorf("compact: %q", out)
	}
	if _, out, _ := runHook(t, sys, "session-banner", ""); out != up {
		t.Errorf("session-banner: %q", out)
	}
	// Both down, and a non-2xx answer reads as down; no branch prints an empty one.
	sys.fetch = func(url string, _ time.Duration) (int, []byte, error) {
		if strings.Contains(url, "5232") {
			return 503, nil, &httpStatus{503}
		}
		return 0, nil, errors.New("refused")
	}
	sys.output = func(string, string, ...string) (string, bool) { return "", false }
	want := "\nGuitar Alchemist Session\n  Branch: \n  Vite: DOWN (port 5176) - run: pwsh Scripts/start-dev.ps1\n  API: DOWN (port 5232)\n\n"
	if _, out, _ := runHook(t, sys, "session-banner", ""); out != want {
		t.Errorf("down: %q", out)
	}
}

// A digest the /digest skill wrote is never replaced at Stop, whatever its age.
func TestStopFinalizeKeepsASkillDigest(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	for _, c := range []struct {
		name, head string
		kept       bool
	}{
		{"skill", "---\nschema_version: 1\ntrigger: digest-skill\n---\n", true},
		{"skill, other case, CRLF", "---\r\nschema_version: 1\r\n  Trigger:  Digest-Skill  \r\n---\r\n", true},
		{"fallback stub", "---\ntrigger: stop-hook-finalize\n---\n", false},
		{"first trigger line decides", "---\ntrigger: precompact-hook-fallback\ntrigger: digest-skill\n---\n", false},
		{"beyond line 12", strings.Repeat("x: y\n", 12) + "trigger: digest-skill\n", false},
		{"trailing words", "---\ntrigger: digest-skill now\n---\n", false},
	} {
		root := t.TempDir()
		d := digestsOf(root)
		if err := os.MkdirAll(d.dir, 0o755); err != nil {
			t.Fatal(err)
		}
		body := c.head + "\nDIGEST-MARKER\n"
		if err := os.WriteFile(d.latest, []byte(body), 0o644); err != nil {
			t.Fatal(err)
		}
		old := now.Add(-5 * time.Hour)
		if err := os.Chtimes(d.latest, old, old); err != nil {
			t.Fatal(err)
		}
		runHook(t, fakeSystem(root, now), "digest-stop-finalize", "")
		after, _ := os.ReadFile(d.latest)
		if kept := string(after) == body; kept != c.kept {
			t.Errorf("%s: kept %v, want %v: %q", c.name, kept, c.kept, after)
		}
	}
}

func TestDSLChanged(t *testing.T) {
	root := t.TempDir()
	for file, want := range map[string]string{
		`C:\r\Common\GA.Business.DSL\Parser.fs`: "DSL file changed\n",
		"/r/common/ga.business.dsl/x.fs":        "DSL file changed\n", // -match ignores case
		"/r/Common/GA.Business.Core/x.cs":       "",
		"/r/Common/GA-Business-DSL/x.cs":        "",
		"":                                      "",
	} {
		if code, out, _ := runHook(t, fakeSystem(root, time.Now()), "dsl-changed", editPayload("Edit", file)); code != 0 || out != want {
			t.Errorf("%q: exit %d, %q", file, code, out)
		}
	}
	if code, out, _ := runHook(t, fakeSystem(root, time.Now()), "dsl-changed", "not json"); code != 0 || out != "" {
		t.Errorf("bad payload: %d %q", code, out)
	}
}

func readJSONL(t *testing.T, path string) []map[string]any {
	t.Helper()
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	var out []map[string]any
	for _, l := range strings.Split(strings.TrimSpace(string(data)), "\n") {
		var m map[string]any
		if json.Unmarshal([]byte(l), &m) != nil {
			continue // a test may append junk lines; the hooks must skip them too
		}
		out = append(out, m)
	}
	return out
}

func TestAIAnnotationScan(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 1234567, time.UTC)
	root := t.TempDir()
	sys := withEnv(fakeSystem(root, now), map[string]string{"CLAUDE_PROJECT_DIR": root})
	src := filepath.Join(root, "src", "a.rs")
	if err := os.MkdirAll(filepath.Dir(src), 0o755); err != nil {
		t.Fatal(err)
	}
	content := "fn main() {}\r\n" +
		"// @ai:invariant arr is sorted ascending [T:test conf:0.95 src:test_search.rs:42]\r\n" +
		"let x = 1; // @ai:invariant not a comment line [T:test]\r\n" +
		"  # @ai:assumption the <cache> & co are warm [P:assumed]\r\n" +
		"/// @ai:bogus unknown kind [T:test]\r\n" +
		"//! @ai:hint unknown certainty [T:guessed]\r\n" +
		"<!-- @ai:Hypothesis mixed case [u:Uncertain conf:1] -->\r\n"
	if err := os.WriteFile(src, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}
	out := filepath.Join(root, "state", "quality", "ai-annotations.jsonl")
	if err := os.MkdirAll(filepath.Dir(out), 0o755); err != nil {
		t.Fatal(err)
	}
	other := `{"schema_version":2,"id":"x","location":{"path":"other.cs","line_start":1}}`
	existing := other + "\n" + `{"id":"stale","location":{"path":"SRC/A.RS"}}` + "\n\nnot json\n" + `{"id":"nolocation"}` + "\n"
	if err := os.WriteFile(out, []byte(existing), 0o644); err != nil {
		t.Fatal(err)
	}
	if code, stdout, _ := runHook(t, sys, "ai-annotation-scan", editPayload("Edit", src)); code != 0 || stdout != "" {
		t.Fatalf("exit %d, %q", code, stdout)
	}
	data, _ := os.ReadFile(out)
	got := readJSONL(t, out)
	if len(got) != 5 || strings.Contains(string(data), "not json") || got[0]["id"] != "x" || got[1]["id"] != "nolocation" || !strings.HasPrefix(string(data), other+"\n") {
		t.Fatalf("kept lines wrong:\n%s", data)
	}
	sum := sha256.Sum256([]byte("src/a.rs:2:Invariant:arr is sorted ascending"))
	want := `{"schema_version":1,"id":"sha256:` + hex.EncodeToString(sum[:]) + `","kind":"invariant","claim":"arr is sorted ascending","truth_value":"T","certainty":"test","confidence":0.95,"source":{"author":"claude","evidence":"test_search.rs:42"},"location":{"path":"src/a.rs","line_start":2,"line_end":2},"created_at":"2026-11-05T12:00:00.0012345Z","updated_at":"2026-11-05T12:00:00.0012345Z"}`
	if !strings.Contains(string(data), want+"\n") {
		t.Errorf("first entry: want\n%s\nin\n%s", want, data)
	}
	if !strings.Contains(string(data), `"claim":"the <cache> & co are warm","truth_value":"P","certainty":"assumed","confidence":0.5,"source":{"author":"claude"},"location":{"path":"src/a.rs","line_start":4,`) {
		t.Errorf("second entry (no conf, no src, HTML characters as they are):\n%s", data)
	}
	h := got[4]
	if h["kind"] != "Hypothesis" || h["truth_value"] != "u" || h["certainty"] != "Uncertain" || h["confidence"] != 1.0 {
		t.Errorf("case-insensitive entry: %v", h)
	}
	sum = sha256.Sum256([]byte("src/a.rs:7:Hypothesis:mixed case"))
	if h["id"] != "sha256:"+hex.EncodeToString(sum[:]) {
		t.Errorf("id of a capitalised kind: %v", h["id"])
	}

	// A file whose markers are gone leaves the JSONL as it is; so do an unscannable extension
	// and a missing file.
	before, _ := os.ReadFile(out)
	if err := os.WriteFile(src, []byte("fn main() {}\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	png := filepath.Join(root, "x.png")
	if err := os.WriteFile(png, []byte("// @ai:invariant x [T:test]\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	for _, f := range []string{src, png, filepath.Join(root, "none.rs")} {
		runHook(t, sys, "ai-annotation-scan", editPayload("Write", f))
	}
	if after, _ := os.ReadFile(out); string(after) != string(before) {
		t.Errorf("JSONL changed:\n%s", after)
	}

	// A one-line file is scanned (the script indexed the characters of a lone line and found
	// nothing); a file outside the project keeps its path as given.
	outside := filepath.Join(t.TempDir(), "One.CS")
	if err := os.WriteFile(outside, []byte("// @ai:smell one line only [F:inferred]"), 0o644); err != nil {
		t.Fatal(err)
	}
	runHook(t, sys, "ai-annotation-scan", editPayload("Edit", outside))
	got = readJSONL(t, out)
	last := got[len(got)-1]
	if loc := last["location"].(map[string]any); loc["path"] != outside || last["claim"] != "one line only" {
		t.Errorf("one-line file: %v", last)
	}
}

func writeRecon(t *testing.T, root string, annotations any) {
	t.Helper()
	p := filepath.Join(root, "state", "quality", "ai-annotations-reconciliation.json")
	if err := os.MkdirAll(filepath.Dir(p), 0o755); err != nil {
		t.Fatal(err)
	}
	var data []byte
	if s, ok := annotations.(string); ok {
		data = []byte(s)
	} else {
		data, _ = json.Marshal(map[string]any{"annotations": annotations})
	}
	if err := os.WriteFile(p, data, 0o644); err != nil {
		t.Fatal(err)
	}
}

func ann(path string, line int, tv, cert, claim string) map[string]any {
	return map[string]any{"truth_value": tv, "certainty": cert, "claim": claim,
		"location": map[string]any{"path": path, "line_start": line}}
}

func TestAIAnnotationMergeGate(t *testing.T) {
	const merge = `{"tool_input":{"command":"gh pr merge 42 --squash"}}`
	gate := func(root, stdin, diff string, diffOK bool, vars map[string]string) (int, string, string) {
		sys := fakeSystem(root, time.Now())
		if vars == nil {
			vars = map[string]string{}
		}
		vars["CLAUDE_PROJECT_DIR"] = root
		withEnv(sys, vars)
		sys.output = func(dir, name string, args ...string) (string, bool) {
			if dir != "" || name+" "+strings.Join(args, " ") != "git diff --name-only origin/main...HEAD" {
				t.Errorf("unexpected process %s %v in %q", name, args, dir)
			}
			return diff, diffOK
		}
		return runHook(t, sys, "ai-annotation-merge-gate", stdin)
	}

	root := t.TempDir()
	if code, _, _ := gate(root, merge, "", false, nil); code != 0 {
		t.Errorf("no report blocks: %d", code)
	}
	writeRecon(t, root, "{not json")
	if code, _, _ := gate(root, merge, "", false, nil); code != 0 {
		t.Errorf("malformed report blocks: %d", code)
	}
	writeRecon(t, root, []any{})
	if code, _, _ := gate(root, merge, "", false, nil); code != 0 {
		t.Errorf("empty report blocks: %d", code)
	}

	writeRecon(t, root, []any{
		ann("src/A.cs", 10, "C", "test", "contradicted claim"),
		ann("src/b.cs", 20, "F", "dismissed", "retired claim"),
		ann("src/b.cs", 21, "T", "test", "fine"),
		ann("src/untouched.cs", 1, "F", "inferred", "refuted elsewhere"),
	})
	code, stdout, stderr := gate(root, merge, "src\\a.cs\nsrc/b.cs\n", true, nil)
	if code != 2 || stdout != "" {
		t.Fatalf("contradiction in a changed file: exit %d, stdout %q", code, stdout)
	}
	want := "\n[ai-annotation gate] Blocking 'gh pr merge' -- 1 unresolved annotation(s) in changed files:\n" +
		"  [C] src/A.cs:10 -- contradicted claim\n\nResolve by:\n"
	if !strings.HasPrefix(stderr, want) || !strings.Contains(stderr, "AI_ANNOTATIONS_GATE_OVERRIDE=1; gh pr merge ...  (use only with reviewer sign-off)\n\n") {
		t.Errorf("stderr:\n%s", stderr)
	}
	// Without the branch diff every annotation counts: the refuted one elsewhere too.
	if code, _, stderr := gate(root, merge, "", false, nil); code != 2 || !strings.Contains(stderr, "-- 2 unresolved") || !strings.Contains(stderr, "  [F] src/untouched.cs:1 -- refuted elsewhere\n") {
		t.Errorf("no diff: %d\n%s", code, stderr)
	}
	// The diff names only clean files: the merge goes through.
	if code, _, _ := gate(root, merge, "src/b.cs", true, nil); code != 0 {
		t.Errorf("clean changed files: %d", code)
	}
	// Other commands and the override pass; the gate matches without case.
	if code, _, _ := gate(root, `{"tool_input":{"command":"gh pr view 42"}}`, "", false, nil); code != 0 {
		t.Errorf("gh pr view: %d", code)
	}
	if code, _, _ := gate(root, `{"tool_input":{"command":"echo x && GH PR MERGE 1"}}`, "", false, nil); code != 2 {
		t.Errorf("GH PR MERGE: %d", code)
	}
	if code, stdout, _ := gate(root, merge, "", false, map[string]string{"AI_ANNOTATIONS_GATE_OVERRIDE": "1"}); code != 0 || !strings.Contains(stdout, "override active") {
		t.Errorf("override: %d %q", code, stdout)
	}
	if code, _, _ := gate(root, "", "", false, nil); code != 0 {
		t.Errorf("empty stdin: %d", code)
	}

	// More than ten: ten listed, the rest counted; a long line is cut at 220 characters.
	var many []any
	for i := 0; i < 12; i++ {
		many = append(many, ann("f.cs", i, "c", "test", strings.Repeat("y", 300)))
	}
	writeRecon(t, root, many)
	_, _, stderr = gate(root, merge, "", false, nil)
	if !strings.Contains(stderr, "-- 12 unresolved") || !strings.Contains(stderr, "  ... and 2 more\n") || strings.Count(stderr, "  [C] f.cs:") != 10 {
		t.Errorf("many:\n%s", stderr)
	}
	first := strings.Split(stderr, "\n")[2]
	if len(first) != 223 || !strings.HasSuffix(first, "y...") {
		t.Errorf("cut line (%d): %q", len(first), first)
	}

	// An annotation without a path while the diff is known: the script's ContainsKey($null)
	// threw and its catch let the merge through.
	writeRecon(t, root, []any{ann("src/a.cs", 1, "C", "test", "x"), map[string]any{"truth_value": "C"}})
	if code, _, _ := gate(root, merge, "src/a.cs", true, nil); code != 0 {
		t.Errorf("pathless annotation: %d", code)
	}
}

func TestLoopsGoalsTracker(t *testing.T) {
	now := time.Date(2026, 11, 5, 12, 0, 0, 0, time.UTC)
	root := t.TempDir()
	home := t.TempDir()
	sys := withEnv(fakeSystem(root, now), map[string]string{"USERPROFILE": home})
	repoFile := filepath.Join(root, "state", ".runtime-loops-goals.jsonl")
	encoded := strings.TrimLeft(strings.NewReplacer(`\`, "-", "/", "-", ":", "-").Replace(root), "-")
	userFile := filepath.Join(home, ".claude", "projects", encoded, "state", "runtime-loops-goals.jsonl")

	for _, in := range []string{`{"prompt":"continue","session_id":"s1"}`, `{"prompt":"/loopy x","session_id":"s1"}`, `{"prompt":"run /loop 5m","session_id":"s1"}`, `{"prompt":"/LOOP 5m","session_id":"s1"}`, ""} {
		runHook(t, sys, "loops-goals-tracker", in, "prompt")
	}
	if _, err := os.Stat(repoFile); err == nil {
		t.Fatal("a non-command prompt was recorded")
	}

	runHook(t, sys, "loops-goals-tracker", `{"prompt":"  /loop 5m check\tthe deploy\n","session_id":"s1"}`, "prompt")
	runHook(t, sys, "loops-goals-tracker", `{"prompt":"/goal `+strings.Repeat("g", 300)+`","session_id":"s2"}`, "prompt")
	runHook(t, sys, "loops-goals-tracker", `{"prompt":"/goal"}`, "prompt")
	recs := readJSONL(t, repoFile)
	if len(recs) != 3 {
		t.Fatalf("records: %v", recs)
	}
	loop := recs[0]
	for k, v := range map[string]any{"kind": "loop", "started_at": "2026-11-05T12:00:00Z", "session_id": "s1", "prompt_or_condition": "5m check the deploy",
		"turn_count": 0.0, "last_activity_at": "2026-11-05T12:00:00Z", "status": "active", "event": "start", "branch": "feat/x"} {
		if loop[k] != v {
			t.Errorf("loop %s = %v, want %v", k, loop[k], v)
		}
	}
	if id, _ := loop["id"].(string); len(id) != 16 || strings.Trim(id, "0123456789abcdef") != "" {
		t.Errorf("id %q", id)
	}
	if recs[1]["prompt_or_condition"] != strings.Repeat("g", 280)+"..." || recs[2]["session_id"] != "unknown" || recs[2]["prompt_or_condition"] != "" {
		t.Errorf("goal records: %v %v", recs[1], recs[2])
	}
	if data, err := os.ReadFile(userFile); err != nil || len(strings.Split(strings.TrimSpace(string(data)), "\n")) != 3 {
		t.Errorf("per-user copy: %v %q", err, data)
	}
	raw, _ := os.ReadFile(repoFile)
	if !strings.HasPrefix(string(raw), `{"id":"`) || !strings.Contains(string(raw), `","kind":"loop","started_at":`) {
		t.Errorf("key order: %s", raw)
	}

	// Stop bumps the active records of its own session, twice; a completed one stays.
	later := now.Add(5 * time.Minute)
	stopSys := withEnv(fakeSystem(root, later), map[string]string{"USERPROFILE": home})
	completed := fmt.Sprintf(`{"id":"%s","kind":"goal","started_at":"x","session_id":"s2","status":"completed","last_activity_at":"2026-11-05T12:01:00Z"}`, recs[1]["id"])
	f, _ := os.OpenFile(repoFile, os.O_APPEND|os.O_WRONLY, 0o644)
	_, _ = f.WriteString(completed + "\nnot json\n\n")
	_ = f.Close()
	var calls []string
	runHook(t, recording(stopSys, &calls), "loops-goals-tracker", `{"session_id":"S1"}`, "stop") // -ne ignores case
	runHook(t, stopSys, "loops-goals-tracker", `{"session_id":"s2"}`, "stop")
	runHook(t, stopSys, "loops-goals-tracker", `{"session_id":"s1"}`, "stop")
	if len(calls) != 0 {
		t.Errorf("stop started %v", calls)
	}
	recs = readJSONL(t, repoFile)
	var turns []map[string]any
	for _, r := range recs {
		if r["event"] == "turn" {
			turns = append(turns, r)
		}
	}
	if len(turns) != 2 || turns[0]["turn_count"] != 1.0 || turns[1]["turn_count"] != 2.0 || turns[1]["id"] != loop["id"] ||
		turns[1]["started_at"] != "2026-11-05T12:00:00Z" || turns[1]["last_activity_at"] != "2026-11-05T12:05:00Z" || turns[1]["prompt_or_condition"] != "5m check the deploy" {
		t.Errorf("turns: %v", turns)
	}

	// Without USERPROFILE only the repository copy is written.
	root2 := t.TempDir()
	runHook(t, fakeSystem(root2, now), "loops-goals-tracker", `{"prompt":"/goal ship","session_id":"s"}`)
	if recs := readJSONL(t, filepath.Join(root2, "state", ".runtime-loops-goals.jsonl")); len(recs) != 1 || recs[0]["kind"] != "goal" {
		t.Errorf("default mode is prompt: %v", recs)
	}
}

func TestSessionPRCheck(t *testing.T) {
	check := func(branch, gh string) (int, string, []string) {
		var calls []string
		sys := fakeSystem(t.TempDir(), time.Now())
		sys.output = func(dir, name string, args ...string) (string, bool) {
			key := name + " " + strings.Join(args, " ")
			calls = append(calls, key)
			switch key {
			case "git branch --show-current":
				return branch, branch != ""
			case "gh pr view --json number,statusCheckRollup,mergeable,isDraft":
				return gh, gh != ""
			}
			t.Errorf("unexpected %s", key)
			return "", false
		}
		code, out, _ := runHook(t, sys, "session-pr-check", "")
		return code, out, calls
	}
	if code, out, calls := check("", `{"number":1}`); code != 0 || out != "" || len(calls) != 1 {
		t.Errorf("no branch: %d %q %v", code, out, calls)
	}
	for _, gh := range []string{"", "not json"} {
		if code, out, _ := check("feat/x", gh); code != 0 || out != "" {
			t.Errorf("gh %q: %d %q", gh, code, out)
		}
	}
	if _, out, _ := check("feat/x", `{"number":7,"statusCheckRollup":[],"isDraft":false}`); out != "\nPR #7 on feat/x — no CI checks reported yet.\n\n" {
		t.Errorf("no checks: %q", out)
	}
	red := `{"number":7,"isDraft":true,"statusCheckRollup":[
		{"name":"build","conclusion":"FAILURE","status":"COMPLETED"},
		{"name":"build","conclusion":"failure","status":"COMPLETED"},
		{"name":"lint","conclusion":"TIMED_OUT"},
		{"name":"test","conclusion":"SUCCESS"},
		{"name":"docs","conclusion":"NEUTRAL"},
		{"name":"e2e","conclusion":"","status":"IN_PROGRESS"},
		{"context":"ci/legacy","state":"SUCCESS"},
		{"name":"skip","conclusion":"SKIPPED"}]}`
	want := "\nPR #7 on feat/x — CI is red (draft)\n" +
		"  2 green / 3 red / 1 pending / 2 other (of 8)\n" +
		"  ✗ build\n  ✗ lint\n" +
		"  → gh pr view 7 --web   for the full report\n\n"
	if code, out, _ := check("feat/x", red); code != 0 || out != want {
		t.Errorf("red:\n%q\nwant\n%q", out, want)
	}
	for gh, state := range map[string]string{
		`{"number":8,"statusCheckRollup":[{"conclusion":"SUCCESS"},{"status":"QUEUED"}]}`:      "CI is pending\n  1 green / 0 red / 1 pending / 0 other (of 2)\n\n",
		`{"number":8,"statusCheckRollup":[{"conclusion":"SUCCESS"},{"conclusion":"NEUTRAL"}]}`: "CI is green\n  2 green / 0 red / 0 pending / 0 other (of 2)\n\n",
		`{"number":8,"statusCheckRollup":[{"conclusion":"SUCCESS"},{"conclusion":"SKIPPED"}]}`: "CI is mixed\n  1 green / 0 red / 0 pending / 1 other (of 2)\n\n",
	} {
		if _, out, _ := check("main", gh); out != "\nPR #8 on main — "+state {
			t.Errorf("%s: %q", gh, out)
		}
	}
}

// Every hook command in .claude/settings.json runs a ga-hooks subcommand that exists, and none
// runs a shell or a script (the CI step checks the same with grep).
func TestSettingsWireOnlyGaHooks(t *testing.T) {
	data, err := os.ReadFile(filepath.Join("..", "..", "..", "..", ".claude", "settings.json"))
	if err != nil {
		t.Skip("no settings.json above the module:", err)
	}
	var s struct {
		Hooks map[string][]struct {
			Hooks []struct {
				Command string `json:"command"`
			} `json:"hooks"`
		} `json:"hooks"`
	}
	if err := json.Unmarshal(data, &s); err != nil {
		t.Fatal(err)
	}
	shell := regexp.MustCompile(`(?i)\b(bash|sh|pwsh|powershell|python3?|node)\b|\.(sh|ps1|py|js)\b`)
	n := 0
	for event, groups := range s.Hooks {
		for _, g := range groups {
			for _, h := range g.Hooks {
				n++
				if shell.MatchString(h.Command) {
					t.Errorf("%s: runs a shell or a script: %s", event, h.Command)
				}
				var sub string
				switch {
				case strings.HasPrefix(h.Command, `"$CLAUDE_PROJECT_DIR/.claude/hooks/bin/ga-hooks" `):
					sub = strings.Fields(strings.TrimPrefix(h.Command, `"$CLAUDE_PROJECT_DIR/.claude/hooks/bin/ga-hooks" `))[0]
				case h.Command == `go -C "$CLAUDE_PROJECT_DIR/go/claudehooks" run ./cmd/ga-hooks session-start`:
					sub = "session-start"
				}
				if hooks[sub] == nil {
					t.Errorf("%s: not a ga-hooks subcommand: %s", event, h.Command)
				}
			}
		}
	}
	if n != 13 {
		t.Errorf("%d hook commands, want 13", n)
	}
}
