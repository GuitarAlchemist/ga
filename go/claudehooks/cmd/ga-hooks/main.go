// ga-hooks is every Claude Code hook of this repository, one subcommand per hook. It replaces the
// PowerShell scripts (Scripts/*.ps1, .claude/hooks/loops-goals-tracker.ps1) and the inline
// `pwsh -Command` hooks that .claude/settings.json ran (the user, 2026-11-05: "jamais de shell
// dans les hooks"). It mirrors ix's go/claudehooks (ix PR #512) and tars's (tars PR #419), from
// which the digest, correction and PR-rationale code comes: ga's scripts were the originals of
// that family.
//
// .claude/settings.json runs `session-start` through `go run` (Go caches the built executable),
// and that subcommand installs the binary as .claude/hooks/bin/ga-hooks[.exe], which every other
// hook calls directly. The bin directory is gitignored.
//
// Each subcommand keeps its script's contract: the same inputs, the same exit codes, the same
// files written, the same text on stdout, except where the commit that ported them says
// otherwise. Programs (git, gh) are executed directly, never through a shell.
package main

import (
	"fmt"
	"io"
	"os"
	"sort"
	"strings"
)

// env is what a hook may touch: its streams, its arguments and the outside world. Tests replace it.
type env struct {
	stdin  io.Reader
	stdout io.Writer
	stderr io.Writer
	args   []string
	sys    *system
}

var hooks = map[string]func(*env) int{
	"session-start":            sessionStart,
	"session-banner":           sessionBanner,
	"sessionstart-digest":      sessionstartDigest,
	"precompact-digest":        precompactDigest,
	"digest-staleness-nudge":   digestStalenessNudge,
	"correction-detector":      correctionDetector,
	"digest-activity-tracker":  digestActivityTracker,
	"pr-rationale-capture":     prRationaleCapture,
	"digest-stop-finalize":     digestStopFinalize,
	"digest-validate":          digestValidate,
	"dsl-changed":              dslChanged,
	"ai-annotation-scan":       aiAnnotationScan,
	"ai-annotation-merge-gate": aiAnnotationMergeGate,
	"loops-goals-tracker":      loopsGoalsTracker,
	"session-pr-check":         sessionPRCheck,
}

func main() {
	var stdin io.Reader = os.Stdin
	// A hook run by hand from a terminal does not wait for input.
	if fi, err := os.Stdin.Stat(); err == nil && fi.Mode()&os.ModeCharDevice != 0 {
		stdin = strings.NewReader("")
	}
	os.Exit(dispatch(os.Args[1:], &env{stdin: stdin, stdout: os.Stdout, stderr: os.Stderr, sys: realSystem()}))
}

func dispatch(args []string, e *env) int {
	if len(args) == 0 || hooks[args[0]] == nil {
		names := make([]string, 0, len(hooks))
		for n := range hooks {
			names = append(names, n)
		}
		sort.Strings(names)
		fmt.Fprintf(e.stderr, "usage: ga-hooks <hook> [args]\nhooks: %v\n", names)
		return 1 // never 2: a misnamed PreToolUse hook must not block every tool call
	}
	e.args = args[1:]
	return hooks[args[0]](e)
}
