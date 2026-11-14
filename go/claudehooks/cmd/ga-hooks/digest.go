package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"io/fs"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"time"
)

// The digest hooks keep state/digests/latest.md: a nudge when it goes stale, a metadata stub
// after enough activity, at Stop and before a compaction, and its injection at SessionStart.
// Karpathy R1+R4, Cherny's session continuity (see .claude/skills/digest/SKILL.md).

type digestPaths struct {
	dir, archive, latest string
}

func digestsOf(root string) digestPaths {
	dir := filepath.Join(root, "state", "digests")
	return digestPaths{dir, filepath.Join(dir, "archive"), filepath.Join(dir, "latest.md")}
}

// safeYAML is the scripts' Get-SafeYaml: a single-quoted YAML scalar, newlines as spaces, cut
// to max characters with "..." appended, quotes doubled; null when empty.
func safeYAML(v string, max int) string {
	if v == "" {
		return "null"
	}
	c := []rune(strings.NewReplacer("\r", " ", "\n", " ").Replace(v))
	s := string(c)
	if len(c) > max {
		s = string(c[:max]) + "..."
	}
	return "'" + strings.ReplaceAll(s, "'", "''") + "'"
}

var safeIDChars = regexp.MustCompile(`^[A-Za-z0-9._-]+$`)

// safeID is the scripts' Get-SafeId: v stripped of CR, LF and tabs and cut to max characters
// when it is then a plain identifier, fallback otherwise (no path traversal in a file name).
func safeID(v, fallback string, max int) string {
	if v == "" {
		return fallback
	}
	c := []rune(strings.NewReplacer("\r", "", "\n", "", "\t", "").Replace(v))
	if len(c) > max {
		c = c[:max]
	}
	if !safeIDChars.MatchString(string(c)) {
		return fallback
	}
	return string(c)
}

// head is the branch, short sha and subject of HEAD, each "" when git prints nothing.
func (e *env) head(root string) (branch, sha, subject string) {
	return e.git(root, "rev-parse", "--abbrev-ref", "HEAD"),
		e.git(root, "rev-parse", "--short", "HEAD"),
		e.git(root, "log", "-1", "--format=%s")
}

// openPR is "#<n>" for the branch's pull request, "" without gh or without one.
func (e *env) openPR(root string) string {
	out, ok := e.sys.output(root, "gh", "pr", "view", "--json", "number")
	if !ok || out == "" {
		return ""
	}
	var v struct {
		Number json.Number `json:"number"`
	}
	if json.Unmarshal([]byte(out), &v) != nil || v.Number == "" {
		return ""
	}
	return "#" + v.Number.String()
}

func timestamps(now time.Time) (iso, file string) {
	now = now.UTC()
	return now.Format("2006-01-02T15:04:05Z"), now.Format("2006-01-02T15-04-05Z")
}

// archiveLatest copies latest.md into the archive before a hook replaces it, when there is one.
// On an error the caller keeps latest.md as it is rather than lose the previous digest.
func archiveLatest(e *env, d digestPaths, name, hook string) bool {
	data, err := os.ReadFile(d.latest)
	if errors.Is(err, fs.ErrNotExist) {
		return true
	}
	if err == nil {
		err = os.WriteFile(filepath.Join(d.archive, name), data, 0o644)
	}
	if err != nil {
		fmt.Fprintf(e.stderr, "[%s] could not archive latest.md, kept as it is: %v\n", hook, err)
		return false
	}
	return true
}

// replaceLatest writes latest.md through a temporary file and a rename, so a failed write never
// leaves it truncated; a failure keeps the previous latest.md and is said on stderr.
func replaceLatest(e *env, d digestPaths, body, hook string) {
	tmp, err := os.CreateTemp(d.dir, ".latest-*.tmp")
	if err == nil {
		_, err = tmp.WriteString(body + "\n")
		if cerr := tmp.Close(); err == nil {
			err = cerr
		}
		if err == nil {
			err = os.Rename(tmp.Name(), d.latest)
		}
		if err != nil {
			_ = os.Remove(tmp.Name()) // a leftover temporary file is harmless
		}
	}
	if err != nil {
		fmt.Fprintf(e.stderr, "[%s] could not write latest.md, kept as it is: %v\n", hook, err)
	}
}

func prLine(pr string) string {
	if pr == "" {
		return ""
	}
	return "**Open PR:** " + pr + "\n"
}

// precompactDigest (PreCompact) archives latest.md under the session's id and, when /digest has
// not written it in the last 30 minutes, replaces it with a metadata-only fallback.
func precompactDigest(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	d := digestsOf(root)
	_ = os.MkdirAll(d.archive, 0o755) // a failure surfaces in archiveLatest or replaceLatest
	p, _ := e.payload()
	session := field(p, "session_id")
	if session == "" {
		session = "unknown"
	}
	ts, tsFile := timestamps(e.sys.now())
	if !archiveLatest(e, d, tsFile+"-"+safeID(session, "unknown", 64)+".md", "precompact-digest") {
		return 0
	}
	if age, ok := e.age(d.latest); ok && age.Minutes() < 30 {
		return 0
	}
	branch, sha, subject := e.head(root)
	pr := e.openPR(root)
	body := fmt.Sprintf(`---
schema_version: 1
session_id: %s
written_at: %s
trigger: precompact-hook-fallback
branch: %s
head_sha: %s
head_subject: %s
open_pr: %s
---

# Session digest (fallback — /digest was not invoked before compaction)

**Branch:** %s @ %s — %s
%s
## Model-driven sections

_No `+"`/digest`"+` invocation was captured before this compaction. Re-orient from
`+"`git log`"+` and the open PR. Invoke `+"`/digest`"+` mid-session to populate the
**Next action**, **In-flight**, **Live hypotheses**, **Open questions**, and
**Do NOT carry forward** sections before the next compaction event._`,
		safeYAML(session, 200), ts, safeYAML(branch, 200), safeYAML(sha, 200), safeYAML(subject, 200), safeYAML(pr, 200),
		branch, sha, subject, prLine(pr))
	replaceLatest(e, d, body, "precompact-digest")
	return 0
}

// digestStalenessNudge (UserPromptSubmit) says when the digest is missing or has drifted;
// silent when it is fresh.
func digestStalenessNudge(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	d := digestsOf(root)
	age, exists := e.age(d.latest)
	ageMin := psInt(age.Minutes())
	mutations := counter(filepath.Join(d.dir, ".activity-counter"))

	var reason string
	switch {
	case !exists:
		reason = "No session digest exists yet. Invoke /digest at your next natural breakpoint to seed it."
	case ageMin > 30 && mutations > 10:
		reason = fmt.Sprintf("Last digest %d min ago; %d mutations since. Karpathy R4: task complete != goal achieved — consider /digest to mark success criteria status before continuing.", ageMin, mutations)
	case ageMin > 90:
		reason = fmt.Sprintf("Last digest %d min ago. Session drifting — invoke /digest before the next compaction or session boundary.", ageMin)
	default:
		return 0
	}
	writeContext(e.stdout, "[digest-nudge] "+reason)
	return 0
}

// digestActivityTracker (PostToolUse on Edit|Write|Bash) counts mutations, and after
// GA_DIGEST_MID_COUNT of them (20), or GA_DIGEST_MID_MIN minutes (30) with at least 3,
// writes a metadata-only mid-session digest, so a crash or a network drop does not lose the
// session's place before Stop or PreCompact.
func digestActivityTracker(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	d := digestsOf(root)
	_ = os.MkdirAll(d.archive, 0o755) // a failure surfaces in archiveLatest

	// The counters are best effort: a lost count only delays the next stub.
	totalPath, midPath := filepath.Join(d.dir, ".activity-counter"), filepath.Join(d.dir, ".activity-count")
	_ = writeText(totalPath, strconv.Itoa(counter(totalPath)+1))
	mid := counter(midPath) + 1
	_ = writeText(midPath, strconv.Itoa(mid))

	thresholdCount := envCount(e, "GA_DIGEST_MID_COUNT", 20)
	thresholdMin := envCount(e, "GA_DIGEST_MID_MIN", 30)
	ageMin := int64(99999)
	if a, ok := e.age(d.latest); ok {
		ageMin = psInt(a.Minutes())
	}
	if !(mid >= thresholdCount || (ageMin >= int64(thresholdMin) && mid >= 3)) {
		return 0
	}
	_ = writeText(midPath, "0")

	tsIso, tsFile := timestamps(e.sys.now())
	if !archiveLatest(e, d, tsFile+"-pre-mid.md", "digest-activity-tracker") {
		return 0
	}
	branch, sha, subject := e.head(root)
	body := fmt.Sprintf(`---
schema_version: 1
session_id: mid-session-auto
written_at: %s
trigger: activity-tracker-mid-session
branch: %s
head_sha: %s
head_subject: %s
mutations_since_last: %d
---

# Session digest (mid-session auto — activity threshold reached)

**Branch:** %s @ %s — %s

## Model-driven sections

_Auto-written by digest-activity-tracker after %d mutations / %dm since last digest.
Invoke `+"`/digest`"+` at your next natural breakpoint to populate **Next action**,
**In-flight**, **Live hypotheses**, **Open questions**, **Do NOT carry forward**,
and **Success criteria**._`, tsIso, branch, sha, subject, mid, branch, sha, subject, mid, ageMin)
	if err := writeText(filepath.Join(d.dir, "mid-"+tsFile+".md"), body); err != nil {
		fmt.Fprintf(e.stderr, "[digest-activity-tracker] could not write the mid-session digest: %v\n", err)
	}
	replaceLatest(e, d, body, "digest-activity-tracker")
	return 0
}

// digestStopFinalize (Stop) writes a metadata digest when /digest has not written one in the
// last 10 minutes, archiving the previous one first. A digest the /digest skill wrote
// (`trigger: digest-skill` in its first 12 lines) is never replaced, whatever its age: four
// stomps in one session on 2026-05-16 came from the 10-minute guard alone.
func digestStopFinalize(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	d := digestsOf(root)
	if age, ok := e.age(d.latest); ok {
		if age.Minutes() < 10 || writtenBySkill(d.latest) {
			return 0
		}
	}
	_ = os.MkdirAll(d.archive, 0o755) // a failure surfaces in archiveLatest or replaceLatest
	tsIso, tsFile := timestamps(e.sys.now())
	if !archiveLatest(e, d, tsFile+"-stop.md", "digest-stop-finalize") {
		return 0
	}
	branch, sha, subject := e.head(root)
	pr := e.openPR(root)
	body := fmt.Sprintf(`---
schema_version: 1
session_id: stop-finalize
written_at: %s
trigger: stop-hook-finalize
branch: %s
head_sha: %s
head_subject: %s
open_pr: %s
---

# Session digest (Stop-hook finalize — /digest not invoked in last 10 min)

**Branch:** %s @ %s — %s
%s
## Model-driven sections

_Session ended without a recent `+"`/digest`"+`. Next session: re-orient from
`+"`git log`"+` + open PR + `+"`docs/solutions/`"+` recent entries. Prior digests
(if any) are in `+"`state/digests/archive/`"+`._`,
		tsIso, safeYAML(branch, 200), safeYAML(sha, 200), safeYAML(subject, 200), safeYAML(pr, 200),
		branch, sha, subject, prLine(pr))
	replaceLatest(e, d, body, "digest-stop-finalize")
	validateDigest(d.latest, io.Discard) // the script ran digest-validate.ps1 2>$null and ignored its verdict
	return 0
}

var (
	triggerLine  = regexp.MustCompile(`(?i)^\s*trigger:\s*(\S+)`)
	skillTrigger = regexp.MustCompile(`(?i)^\s*trigger:\s*digest-skill\s*$`)
)

// writtenBySkill: the first `trigger:` line among the first 12 lines of the digest says
// digest-skill (the script's -match, which ignores case). An unreadable file is not, so a
// corrupt digest still gets replaced.
func writtenBySkill(path string) bool {
	data, err := os.ReadFile(path)
	if err != nil {
		return false
	}
	head := lines(string(data))
	if len(head) > 12 {
		head = head[:12]
	}
	for _, l := range head {
		if triggerLine.MatchString(l) {
			return skillTrigger.MatchString(l)
		}
	}
	return false
}

// digestValidate checks a digest's frontmatter against the required fields of
// docs/contracts/digest-schema.json (Karpathy R11). Argument: the digest path (optionally after
// -DigestPath, as the script took it), state/digests/latest.md by default. Exit 1 with the
// reason on stderr when it does not conform.
func digestValidate(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	args := e.args
	if len(args) > 0 && strings.EqualFold(args[0], "-DigestPath") {
		args = args[1:]
	}
	path := digestsOf(root).latest
	if len(args) > 0 && args[0] != "" {
		path = args[0]
	}
	if _, err := os.Stat(path); err != nil {
		return 0
	}
	return validateDigest(path, e.stderr)
}

var (
	frontmatter   = regexp.MustCompile(`(?s)^---\r?\n(.*?)\r?\n---`)
	frontmatterKV = regexp.MustCompile(`^\s*([\w_]+)\s*:\s*(.*?)\s*$`)
	lineBreak     = regexp.MustCompile(`\r?\n`)
	validTriggers = []string{"digest-skill", "precompact-hook-fallback", "stop-hook-finalize", "auto-write-routine"}
)

func validateDigest(path string, stderr io.Writer) int {
	data, err := os.ReadFile(path)
	if err != nil {
		return 0
	}
	m := frontmatter.FindStringSubmatch(strings.TrimPrefix(string(data), bom))
	if m == nil {
		fmt.Fprintf(stderr, "digest-validate: missing or malformed YAML frontmatter in %s\n", path)
		return 1
	}
	fm := map[string]*string{} // nil: null or empty, as the script's $null
	for _, line := range lineBreak.Split(m[1], -1) {
		if kv := frontmatterKV.FindStringSubmatch(line); kv != nil {
			v := kv[2]
			if v == "" || strings.EqualFold(v, "null") {
				fm[kv[1]] = nil
			} else {
				fm[kv[1]] = &v
			}
		}
	}
	for _, k := range []string{"schema_version", "session_id", "written_at", "trigger", "branch", "head_sha", "head_subject"} {
		if v, ok := fm[k]; !ok || v == nil {
			fmt.Fprintf(stderr, "digest-validate: required field '%s' missing or null\n", k)
			return 1
		}
	}
	if sv := *fm["schema_version"]; sv != "1" {
		fmt.Fprintf(stderr, "digest-validate: schema_version must be 1 (got '%s')\n", sv)
		return 1
	}
	tr := *fm["trigger"]
	for _, t := range validTriggers {
		if strings.EqualFold(tr, t) { // PowerShell's -in compares without case
			return 0
		}
	}
	fmt.Fprintf(stderr, "digest-validate: trigger '%s' not in %s\n", tr, strings.Join(validTriggers, ", "))
	return 1
}
