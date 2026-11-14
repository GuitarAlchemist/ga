package main

import (
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
)

// correctionDetector (UserPromptSubmit), Cherny's auto /correct trigger: when the prompt opens
// with correction language, nudge Claude to invoke /correct so the rule lands in CLAUDE.md.
// Throttled: fires on the first match, then on every GA_CORRECTION_THROTTLE-th (5).
func correctionDetector(e *env) int {
	root := e.repoRoot()
	if root == "" {
		return 0
	}
	dir := digestsOf(root).dir
	_ = os.MkdirAll(dir, 0o755) // a failure only loses the throttle count

	p, _ := e.payload()
	prompt := field(p, "prompt")
	if prompt == "" {
		prompt = field(p, "user_prompt")
	}
	if prompt == "" || !isCorrection(prompt) {
		return 0
	}

	throttle := envCount(e, "GA_CORRECTION_THROTTLE", 5)
	counterPath := filepath.Join(dir, ".correction-counter")
	count := counter(counterPath) + 1
	_ = writeText(counterPath, strconv.Itoa(count)) // best effort, as the script's Set-Content
	// A throttle of 0 made the script's `$count % 0` fail; here it fires on the first match only.
	if count != 1 && (throttle == 0 || count%throttle != 0) {
		return 0
	}
	writeContext(e.stdout, "[correction-nudge] Detected correction language. Consider invoking /correct to formalize this into CLAUDE.md so the rule persists across sessions (Cherny self-improvement loop).")
	return 0
}

// The script matched the lowercased first 200 characters against this, `^` being the start of
// the prompt (.NET without Multiline).
var correctionOpener = regexp.MustCompile(`^(no|don't|dont|stop|wait|actually|that's wrong|thats wrong|incorrect)\b`)

func isCorrection(prompt string) bool {
	head := []rune(prompt)
	if len(head) > 200 {
		head = head[:200]
	}
	return correctionOpener.MatchString(strings.ToLower(string(head)))
}
