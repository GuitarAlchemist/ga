package main

import (
	"encoding/json"
	"fmt"
	"slices"
	"strings"
)

// sessionPRCheck (Stop) prints the CI state of the current branch's open pull request: the
// "shipped on local green, red on CI" gap (was Scripts/session-pr-check.ps1). Silent outside a
// repository, without gh or without a pull request; always exits 0, red CI included.
// Write-Host's colours are not printed: the hook's stdout is a pipe, where PowerShell 7 dropped
// them too.
func sessionPRCheck(e *env) int {
	branch, _ := e.sys.output("", "git", "branch", "--show-current")
	if branch == "" {
		return 0
	}
	// A missing gh fails to start, which reads as no pull request (the script's Get-Command).
	out, _ := e.sys.output("", "gh", "pr", "view", "--json", "number,statusCheckRollup,mergeable,isDraft")
	if out == "" {
		return 0
	}
	var pr struct {
		Number  json.Number `json:"number"`
		IsDraft bool        `json:"isDraft"`
		Checks  []struct {
			Name       string `json:"name"`
			Conclusion string `json:"conclusion"`
			Status     string `json:"status"`
		} `json:"statusCheckRollup"`
	}
	if json.Unmarshal([]byte(out), &pr) != nil {
		return 0
	}
	w := e.stdout
	total := len(pr.Checks)
	if total == 0 {
		fmt.Fprintf(w, "\nPR #%s on %s — no CI checks reported yet.\n\n", pr.Number, branch)
		return 0
	}
	var success, failed, pending, other int
	var failing []string
	for _, c := range pr.Checks {
		conc := c.Conclusion
		if conc == "" {
			conc = c.Status
		}
		switch strings.ToUpper(conc) { // switch ignores case in PowerShell
		case "SUCCESS", "NEUTRAL":
			success++
		case "FAILURE", "TIMED_OUT", "CANCELLED", "STARTUP_FAILURE":
			failed++
		case "IN_PROGRESS", "QUEUED", "PENDING", "WAITING":
			pending++
		default:
			other++
		}
		// The failing names come from the conclusion alone, without duplicates.
		switch strings.ToUpper(c.Conclusion) {
		case "FAILURE", "TIMED_OUT", "CANCELLED", "STARTUP_FAILURE":
			if !slices.Contains(failing, c.Name) { // Select-Object -Unique compares with case
				failing = append(failing, c.Name)
			}
		}
	}
	state := "mixed"
	switch {
	case failed > 0:
		state = "red"
	case pending > 0:
		state = "pending"
	case success == total:
		state = "green"
	}
	draft := ""
	if pr.IsDraft {
		draft = " (draft)"
	}
	fmt.Fprintf(w, "\nPR #%s on %s — CI is %s%s\n", pr.Number, branch, state, draft)
	fmt.Fprintf(w, "  %d green / %d red / %d pending / %d other (of %d)\n", success, failed, pending, other, total)
	if failed > 0 {
		for _, n := range failing {
			fmt.Fprintf(w, "  ✗ %s\n", n)
		}
		fmt.Fprintf(w, "  → gh pr view %s --web   for the full report\n", pr.Number)
	}
	fmt.Fprintln(w)
	return 0
}
