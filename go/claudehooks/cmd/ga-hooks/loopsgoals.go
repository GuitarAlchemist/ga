package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"time"
	"unicode/utf8"
)

// loopsGoalsTracker records /loop and /goal invocations so a second Claude window can see what
// is looping or which goal is open (was .claude/hooks/loops-goals-tracker.ps1; see
// .claude/hooks/README.md). The mode is the first argument:
//
//	prompt (UserPromptSubmit): a prompt opening with /loop or /goal appends a `start` record;
//	stop (Stop): every active record of this session gets a `turn` record, turn_count + 1.
//
// Records are appended to state/.runtime-loops-goals.jsonl (read by the Vite dev server) and to
// $USERPROFILE/.claude/projects/<encoded repo>/state/runtime-loops-goals.jsonl. It prints
// nothing and never blocks.
func loopsGoalsTracker(e *env) int {
	mode := "prompt"
	if len(e.args) > 0 {
		mode = e.args[0]
	}
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	now, _ := timestamps(e.sys.now())
	p, _ := e.payload()
	session := psString(p["session_id"])
	if session == "" {
		session = "unknown"
	}
	switch {
	case strings.EqualFold(mode, "prompt"):
		prompt := psString(p["prompt"])
		if prompt == "" {
			return 0
		}
		kind, captured := "", ""
		if m := loopCommand.FindStringSubmatch(prompt); m != nil {
			kind, captured = "loop", m[1]
		} else if m := goalCommand.FindStringSubmatch(prompt); m != nil {
			kind, captured = "goal", m[1]
		} else {
			return 0
		}
		branch := e.git(root, "rev-parse", "--abbrev-ref", "HEAD")
		e.appendLoopRecord(root, loopRecord{
			ID: newLoopID(), Kind: kind, StartedAt: now, SessionID: session,
			PromptOrCondition: safeText(strings.TrimSpace(captured), 280), TurnCount: 0,
			LastActivityAt: now, Status: "active", Event: "start", Branch: strings.TrimSpace(branch),
		})
	case strings.EqualFold(mode, "stop"):
		for _, r := range activeLoopRecords(loopPaths(e, root).repo) {
			if !strings.EqualFold(r.SessionID, session) {
				continue
			}
			r.TurnCount++
			r.LastActivityAt = now
			r.Status, r.Event = "active", "turn"
			e.appendLoopRecord(root, r)
		}
	}
	return 0
}

// The script's [regex]::Match patterns, Singleline and case-sensitive.
var (
	loopCommand = regexp.MustCompile(`(?s)^\s*/loop\b\s*(.*)$`)
	goalCommand = regexp.MustCompile(`(?s)^\s*/goal\b\s*(.*)$`)
)

type loopRecord struct {
	ID                string `json:"id"`
	Kind              string `json:"kind"`
	StartedAt         string `json:"started_at"`
	SessionID         string `json:"session_id"`
	PromptOrCondition string `json:"prompt_or_condition"`
	TurnCount         int    `json:"turn_count"`
	LastActivityAt    string `json:"last_activity_at"`
	Status            string `json:"status"`
	Event             string `json:"event"`
	Branch            string `json:"branch"`
}

type loopFiles struct{ user, repo string }

// loopPaths: Claude Code's encoding of the repository path (separators and colons as dashes,
// leading dashes dropped) names the per-user directory; no USERPROFILE, no per-user copy (the
// script's Join-Path failed there too).
func loopPaths(e *env, root string) loopFiles {
	f := loopFiles{repo: filepath.Join(root, "state", ".runtime-loops-goals.jsonl")}
	if home := e.sys.getenv("USERPROFILE"); home != "" {
		encoded := strings.TrimLeft(strings.NewReplacer(`\`, "-", "/", "-", ":", "-").Replace(root), "-")
		f.user = filepath.Join(home, ".claude", "projects", encoded, "state", "runtime-loops-goals.jsonl")
	}
	return f
}

// appendLoopRecord appends one JSON line to both files, best effort.
func (e *env) appendLoopRecord(root string, r loopRecord) {
	line := compactJSON(r) + "\n"
	paths := loopPaths(e, root)
	for _, path := range []string{paths.user, paths.repo} {
		if path == "" {
			continue
		}
		_ = os.MkdirAll(filepath.Dir(path), 0o755)
		f, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o644)
		if err != nil {
			continue
		}
		_, _ = f.WriteString(line)
		_ = f.Close()
	}
}

// activeLoopRecords is the latest record per id (a later or equal last_activity_at wins) whose
// status is active, in the order the ids first appear.
func activeLoopRecords(path string) []loopRecord {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil
	}
	byID := map[string]loopRecord{}
	var order []string
	for _, line := range lines(string(data)) {
		if line == "" {
			continue
		}
		var raw map[string]any
		if json.Unmarshal([]byte(line), &raw) != nil {
			continue
		}
		r := loopRecord{
			ID: psString(raw["id"]), Kind: psString(raw["kind"]), StartedAt: psString(raw["started_at"]),
			SessionID: psString(raw["session_id"]), PromptOrCondition: psString(raw["prompt_or_condition"]),
			LastActivityAt: psString(raw["last_activity_at"]), Status: psString(raw["status"]),
			Event: psString(raw["event"]), Branch: psString(raw["branch"]),
		}
		if n, ok := raw["turn_count"].(float64); ok {
			r.TurnCount = int(n)
		}
		if r.ID == "" {
			continue
		}
		prev, seen := byID[r.ID]
		if !seen {
			order = append(order, r.ID)
		}
		if !seen || !activityBefore(r.LastActivityAt, prev.LastActivityAt) {
			byID[r.ID] = r
		}
	}
	var active []loopRecord
	for _, id := range order {
		if strings.EqualFold(byID[id].Status, "active") {
			active = append(active, byID[id])
		}
	}
	return active
}

// activityBefore: timestamp a is strictly earlier than b, compared as instants (the hooks write
// whole seconds, the dashboard toISOString() milliseconds, so text order is not time order), as
// the script compared deserialized dates; as text when either does not parse.
func activityBefore(a, b string) bool {
	ta, errA := time.Parse(time.RFC3339Nano, a)
	tb, errB := time.Parse(time.RFC3339Nano, b)
	if errA != nil || errB != nil {
		return a < b
	}
	return ta.Before(tb)
}

// safeText: CR, LF and tabs as spaces, cut to max characters with "..." appended.
func safeText(v string, max int) string {
	v = strings.NewReplacer("\r", " ", "\n", " ", "\t", " ").Replace(v)
	if utf8.RuneCountInString(v) > max {
		v = string([]rune(v)[:max]) + "..."
	}
	return v
}

// newLoopID is 16 hex characters, as the script's GUID cut to 16.
func newLoopID() string {
	b := make([]byte, 8)
	_, _ = rand.Read(b) // crypto/rand.Read never fails
	return hex.EncodeToString(b)
}
