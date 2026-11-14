package main

import (
	"fmt"
	"regexp"
)

// dslChanged (PostToolUse on Edit|Write) prints "DSL file changed" when the edited file's path
// names GA.Business.DSL (was an inline `pwsh -Command` hook; -match ignores case). Never blocks.
func dslChanged(e *env) int {
	p, _ := e.payload()
	if dslPath.MatchString(field(p, "tool_input", "file_path")) {
		fmt.Fprintln(e.stdout, "DSL file changed")
	}
	return 0
}

var dslPath = regexp.MustCompile(`(?i)GA\.Business\.DSL`)
