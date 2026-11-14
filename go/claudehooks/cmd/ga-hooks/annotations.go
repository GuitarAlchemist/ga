package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"unicode/utf8"
)

// The @ai: annotation hooks (docs/contracts/2026-05-24-ai-annotation.contract.md in ix): a scan
// of each edited file into state/quality/ai-annotations.jsonl, and a gate on `gh pr merge` while
// the reconciliation report holds contradicted or refuted annotations in the changed files.

// scanExts mirrors the Rust walker's SCANNABLE_EXTS.
var scanExts = map[string]bool{}

func init() {
	for _, x := range strings.Fields(".rs .cs .fs .fsx .fsi .ts .tsx .js .jsx .py .rb .go .java .swift .c .h .cpp .hpp .lua .sql .hs .sh .ps1 .psm1 .yml .yaml .toml .html .htm .xml .md") {
		scanExts[x] = true
	}
}

// The script's patterns (crates/ix-ai-annotations/src/parser.rs syntax). PowerShell's -match
// ignores case, hence (?i): a lowercase truth value or an uppercase certainty is read as the
// script read it.
var (
	annComment = regexp.MustCompile(`(?i)^\s*(?:///|//!|//|#!|#|--|/\*|<!--)\s*`)
	annMarker  = regexp.MustCompile(`(?i)@ai:([a-zA-Z_-]+)\s+(.+?)\s+\[([TPUDFC]):([a-z][a-z\-]*)(?:\s+conf:([0-9]*\.?[0-9]+))?(?:\s+src:([^\]]+?))?\]`)
	annKinds   = []string{"invariant", "assumption", "hypothesis", "contract", "smell", "decision", "hint"}
	annCerts   = []string{"test", "formal-proof", "manually-reviewed", "assumed", "uncertain", "inferred", "dismissed"}
)

// containsFold is PowerShell's -contains on strings: equality without case.
func containsFold(list []string, s string) bool {
	for _, x := range list {
		if strings.EqualFold(x, s) {
			return true
		}
	}
	return false
}

type annotation struct {
	SchemaVersion int        `json:"schema_version"`
	ID            string     `json:"id"`
	Kind          string     `json:"kind"`
	Claim         string     `json:"claim"`
	TruthValue    string     `json:"truth_value"`
	Certainty     string     `json:"certainty"`
	Confidence    float64    `json:"confidence"`
	Source        annSource  `json:"source"`
	Location      annLocator `json:"location"`
	CreatedAt     string     `json:"created_at"`
	UpdatedAt     string     `json:"updated_at"`
}

type annSource struct {
	Author   string `json:"author"`
	Evidence string `json:"evidence,omitempty"`
}

type annLocator struct {
	Path      string `json:"path"`
	LineStart int    `json:"line_start"`
	LineEnd   int    `json:"line_end"`
}

// aiAnnotationScan (PostToolUse on Edit|Write) replaces the edited file's entries in
// state/quality/ai-annotations.jsonl with the @ai: markers it now holds (was
// Scripts/ai-annotation-scan.ps1). Best effort: every failure exits 0, the next full ix scan
// reconciles. A file without markers leaves the JSONL untouched, as the script did.
func aiAnnotationScan(e *env) int {
	p, _ := e.payload()
	filePath := field(p, "tool_input", "file_path")
	if filePath == "" {
		return 0
	}
	if _, err := os.Stat(filePath); err != nil {
		return 0
	}
	repo := e.projectDir()
	outDir := filepath.Join(repo, "state", "quality")
	outFile := filepath.Join(outDir, "ai-annotations.jsonl")
	_ = os.MkdirAll(outDir, 0o755) // the script created it before looking at the extension
	if !scanExts[strings.ToLower(filepath.Ext(filePath))] {
		return 0
	}
	relPath := filePath
	if len(filePath) >= len(repo) && strings.EqualFold(filePath[:len(repo)], repo) {
		relPath = strings.ReplaceAll(strings.TrimLeft(filePath[len(repo):], `\/`), `\`, "/")
	}
	data, err := os.ReadFile(filePath)
	if err != nil {
		return 0
	}
	now := e.sys.now().UTC().Format("2006-01-02T15:04:05.0000000Z") // .NET's "o"
	var fresh []string
	for i, line := range lines(string(data)) {
		if !annComment.MatchString(line) {
			continue
		}
		m := annMarker.FindStringSubmatch(line)
		if m == nil || !containsFold(annKinds, m[1]) || !containsFold(annCerts, m[4]) {
			continue
		}
		a := annotation{SchemaVersion: 1, Kind: m[1], Claim: strings.TrimSpace(m[2]), TruthValue: m[3], Certainty: m[4],
			Confidence: 0.5, Source: annSource{Author: "claude", Evidence: strings.TrimSpace(m[6])},
			Location: annLocator{Path: relPath, LineStart: i + 1, LineEnd: i + 1}, CreatedAt: now, UpdatedAt: now}
		if m[5] != "" {
			if c, err := strconv.ParseFloat(m[5], 64); err == nil {
				a.Confidence = c
			}
		}
		a.ID = annotationID(relPath, i+1, m[1], a.Claim)
		fresh = append(fresh, compactJSON(a))
	}
	if len(fresh) == 0 {
		return 0
	}
	var kept []string
	if old, err := os.ReadFile(outFile); err == nil {
		for _, line := range lines(string(old)) {
			if strings.TrimSpace(line) == "" {
				continue
			}
			var v any
			if json.Unmarshal([]byte(line), &v) != nil {
				continue // the script dropped a line it could not parse
			}
			obj, _ := v.(map[string]any)
			loc, _ := obj["location"].(map[string]any)
			if path, ok := loc["path"].(string); ok && strings.EqualFold(path, relPath) {
				continue // -ne ignores case
			}
			kept = append(kept, line)
		}
	}
	_ = writeText(outFile, strings.Join(append(kept, fresh...), "\n")) // best effort
	return 0
}

// annotationID mirrors types::annotation_id: sha256 of "path:line:Kind:claim", Kind being the
// Rust Debug name (first letter upper, the rest lower, dashes dropped).
func annotationID(path string, line int, kind, claim string) string {
	pascal := strings.ToUpper(kind[:1]) + strings.ToLower(kind[1:])
	pascal = strings.ReplaceAll(pascal, "-", "")
	sum := sha256.Sum256([]byte(fmt.Sprintf("%s:%d:%s:%s", path, line, pascal, claim)))
	return "sha256:" + hex.EncodeToString(sum[:])
}

// compactJSON is ConvertTo-Json -Compress: < > & written as they are.
func compactJSON(v any) string {
	var b bytes.Buffer
	enc := json.NewEncoder(&b)
	enc.SetEscapeHTML(false)
	_ = enc.Encode(v) // a struct of strings and numbers always encodes
	return strings.TrimRight(b.String(), "\n")
}

var ghPRMerge = regexp.MustCompile(`(?i)\bgh\s+pr\s+merge\b`)

// aiAnnotationMergeGate (PreToolUse on Bash) blocks `gh pr merge` (exit 2) while
// state/quality/ai-annotations-reconciliation.json holds a contradictory (C) or a refuted,
// non-dismissed (F) annotation in a file the branch changed against origin/main (every file when
// that diff is unavailable). It fails open: no report, a malformed one or an internal error
// lets the merge through. AI_ANNOTATIONS_GATE_OVERRIDE=1 allows one merge.
//
// The script wrote its reason with Write-Host, which reached stdout, so Claude saw a blocked
// call without the reason; it goes to stderr here, which Claude Code feeds back on exit 2.
func aiAnnotationMergeGate(e *env) int {
	p, _ := e.payload()
	command := field(p, "tool_input", "command")
	if command == "" || !ghPRMerge.MatchString(command) {
		return 0
	}
	if e.sys.getenv("AI_ANNOTATIONS_GATE_OVERRIDE") == "1" {
		fmt.Fprintln(e.stdout, "[ai-annotation gate] override active (AI_ANNOTATIONS_GATE_OVERRIDE=1) — allowing merge")
		return 0
	}
	raw, err := os.ReadFile(filepath.Join(e.projectDir(), "state", "quality", "ai-annotations-reconciliation.json"))
	if err != nil {
		return 0
	}
	dec := json.NewDecoder(bytes.NewReader(bytes.TrimPrefix(raw, []byte(bom))))
	dec.UseNumber()
	var data map[string]any
	if dec.Decode(&data) != nil {
		return 0
	}
	var annotations []any
	switch a := data["annotations"].(type) {
	case []any:
		annotations = a
	case map[string]any:
		annotations = []any{a} // foreach over a single object visits it once
	}
	if len(annotations) == 0 {
		return 0
	}

	changed := map[string]bool{} // a PowerShell hashtable: keys without case
	if out, ok := e.sys.output("", "git", "diff", "--name-only", "origin/main...HEAD"); ok {
		for _, f := range lines(out) {
			if f = strings.ReplaceAll(strings.TrimSpace(f), `\`, "/"); f != "" {
				changed[strings.ToLower(f)] = true
			}
		}
	}

	var bad []string
	for _, item := range annotations {
		a, _ := item.(map[string]any)
		loc, _ := a["location"].(map[string]any)
		if len(changed) > 0 {
			if loc["path"] == nil {
				return 0 // ContainsKey($null) threw, and the script's catch let the merge through
			}
			if !changed[strings.ToLower(psString(loc["path"]))] {
				continue
			}
		}
		kind := ""
		switch {
		case strings.EqualFold(psString(a["truth_value"]), "C"):
			kind = "C"
		case strings.EqualFold(psString(a["truth_value"]), "F") && !strings.EqualFold(psString(a["certainty"]), "dismissed"):
			kind = "F"
		default:
			continue
		}
		line := fmt.Sprintf("  [%s] %s:%s -- %s", kind, psString(loc["path"]), psString(loc["line_start"]), psString(a["claim"]))
		if utf8.RuneCountInString(line) > 220 {
			line = string([]rune(line)[:220]) + "..."
		}
		bad = append(bad, line)
	}
	if len(bad) == 0 {
		return 0
	}
	w := e.stderr
	fmt.Fprintf(w, "\n[ai-annotation gate] Blocking 'gh pr merge' -- %d unresolved annotation(s) in changed files:\n", len(bad))
	for i, l := range bad {
		if i == 10 {
			break
		}
		fmt.Fprintln(w, l)
	}
	if len(bad) > 10 {
		fmt.Fprintf(w, "  ... and %d more\n", len(bad)-10)
	}
	fmt.Fprint(w, "\nResolve by:\n"+
		"  - fixing the underlying code so the next reconciler run flips the truth value\n"+
		"  - editing the @ai: marker (e.g., promote F -> C with [C:dismissed] if intentionally retired)\n"+
		"  - one-shot override: $env:AI_ANNOTATIONS_GATE_OVERRIDE=1; gh pr merge ...  (use only with reviewer sign-off)\n\n")
	return 2
}

// psString is a JSON value as PowerShell interpolated it into a string: null empty, booleans
// True and False, numbers as written.
func psString(v any) string {
	switch x := v.(type) {
	case nil:
		return ""
	case string:
		return x
	case json.Number:
		return x.String()
	case bool:
		if x {
			return "True"
		}
		return "False"
	default:
		return fmt.Sprint(x)
	}
}
