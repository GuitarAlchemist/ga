package main

import (
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"unicode"
)

// prRationaleCapture snapshots a `gh pr create`'s title, body and diff stats to
// state/digests/pr-<num>-<slug>.md, so the rationale survives later edits of the pull request
// (was Scripts/pr-rationale-capture.ps1; Cherny's PR rationale capture). ga wires it as a
// PreToolUse hook on Bash, as the script was: there is no tool_response yet, so the number reads
// "unknown" unless a PostToolUse payload carries the gh output.
func prRationaleCapture(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	p, _ := e.payload()
	if p == nil {
		return 0
	}
	cmd := field(p, "tool_input", "command")
	if cmd == "" || !ghPRCreate.MatchString(cmd) {
		return 0
	}
	output := ""
	switch r := p["tool_response"].(type) {
	case map[string]any:
		output = field(r, "output")
	case string:
		output = r
	}

	title := ""
	if m := titleFlag.FindStringSubmatch(cmd); m != nil {
		title = m[1] + m[2]
	}
	body := bodyFlag(cmd)
	num := "unknown"
	if m := prURL.FindStringSubmatch(output); m != nil {
		num = m[1]
	}

	dir := digestsOf(root).dir
	_ = os.MkdirAll(dir, 0o755) // a failure surfaces in writeText
	ts, _ := timestamps(e.sys.now())
	branch := e.git(root, "rev-parse", "--abbrev-ref", "HEAD")
	shortStat := e.git(root, "diff", "--shortstat", "HEAD~1")
	if shortStat != "" {
		shortStat = " " + shortStat // git prints it with a leading space; the script kept it
	}
	doc := fmt.Sprintf(`---
schema_version: 1
trigger: pr-rationale-capture
captured_at: %s
branch: %s
pr_number: %s
diff_shortstat: %s
---

# PR #%s — %s

**Branch:** %s
**Captured:** %s
**Diff:** %s

## Title

%s

## Body

%s`, ts, branch, num, shortStat, num, title, branch, ts, shortStat, title, body)
	if err := writeText(filepath.Join(dir, "pr-"+num+"-"+prSlug(title)+".md"), doc); err != nil {
		fmt.Fprintf(e.stderr, "[pr-rationale-capture] could not save the rationale of PR #%s: %v\n", num, err)
	}
	return 0
}

var (
	ghPRCreate = regexp.MustCompile(`(?i)gh\s+pr\s+create`) // -notmatch ignores case
	titleFlag  = regexp.MustCompile(`--title\s+(?:"([^"]*)"|'([^']*)')`)
	bodyStart  = regexp.MustCompile(`--body\s+["']`)
	prURL      = regexp.MustCompile(`pull/(\d+)`)
	nonSlug    = regexp.MustCompile(`[^a-z0-9]+`)
)

// bodyFlag is the script's `--body\s+(?:"([\s\S]*?)"(?=\s|$)|'([\s\S]*?)'(?=\s|$))`: the text up
// to the first matching quote that ends the command or is followed by whitespace. RE2 has no
// look-ahead, so the closing quote is searched by hand.
func bodyFlag(cmd string) string {
	for _, loc := range bodyStart.FindAllStringIndex(cmd, -1) {
		quote := cmd[loc[1]-1]
		rest := cmd[loc[1]:]
		for i := 0; i < len(rest); i++ {
			if rest[i] != quote {
				continue
			}
			if i+1 == len(rest) || unicode.IsSpace(rune(rest[i+1])) {
				return rest[:i]
			}
		}
	}
	return ""
}

// prSlug: lowercased, every run of other characters one dash, no dash at either end, cut to 40
// characters and trimmed of dashes again; "untitled" when nothing is left.
func prSlug(title string) string {
	s := strings.Trim(nonSlug.ReplaceAllString(strings.ToLower(title), "-"), "-")
	if len(s) > 40 {
		s = strings.Trim(s[:40], "-")
	}
	if s == "" {
		return "untitled"
	}
	return s
}
