package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"math"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"
)

// system is the outside world a hook reads: the clock, the environment, the processes it runs
// and the HTTP endpoints it probes. Files are touched directly; tests give each hook its own
// temporary repository.
type system struct {
	now    func() time.Time
	getenv func(string) string
	// getwd is the working directory (the scripts' `(Get-Location).Path`).
	getwd func() (string, error)
	// output runs a program, never through a shell, and returns its stdout, trimmed, and whether
	// it exited 0 (the scripts' `& git … 2>$null`). dir "" is the working directory.
	output func(dir, name string, args ...string) (string, bool)
	// fetch GETs a URL within the timeout and returns the status and body; an error for a non-2xx
	// status, as Invoke-WebRequest threw one.
	fetch func(url string, timeout time.Duration) (int, []byte, error)
	// executable is this program's own path.
	executable func() (string, error)
	// toplevel is the repository holding the working directory (`git rev-parse --show-toplevel`).
	toplevel func() (string, bool)
}

type httpStatus struct{ code int }

func (h *httpStatus) Error() string { return fmt.Sprintf("HTTP %d", h.code) }

func realSystem() *system {
	return &system{
		now:    time.Now,
		getenv: os.Getenv,
		getwd:  os.Getwd,
		output: func(dir, name string, args ...string) (string, bool) {
			cmd := exec.Command(name, args...)
			cmd.Dir = dir
			out, err := cmd.Output()
			return strings.TrimSpace(string(out)), err == nil
		},
		fetch: func(url string, timeout time.Duration) (int, []byte, error) {
			c := &http.Client{Timeout: timeout}
			resp, err := c.Get(url)
			if err != nil {
				return 0, nil, err
			}
			defer resp.Body.Close()
			if resp.StatusCode/100 != 2 {
				return resp.StatusCode, nil, &httpStatus{resp.StatusCode}
			}
			body, err := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
			return resp.StatusCode, body, err
		},
		executable: os.Executable,
		toplevel: func() (string, bool) {
			dir, err := os.Getwd()
			if err != nil {
				return "", false
			}
			return findTopLevel(dir)
		},
	}
}

// findTopLevel is the nearest ancestor of dir (itself included) holding a `.git` directory or
// worktree file, with forward slashes as git prints it. It replaces a `git rev-parse` process,
// which cost 1 to 2.5 s on a loaded Windows machine, on every prompt and every edit.
func findTopLevel(dir string) (string, bool) {
	for {
		if _, err := os.Stat(filepath.Join(dir, ".git")); err == nil {
			return filepath.ToSlash(dir), true
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			return "", false
		}
		dir = parent
	}
}

// repoRoot is the repository holding the hook's working directory, "" outside one (every
// digest hook then exits 0, as the scripts did).
func (e *env) repoRoot() string {
	root, ok := e.sys.toplevel()
	if !ok {
		return ""
	}
	return root
}

// projectDir is $env:CLAUDE_PROJECT_DIR, else the working directory (the annotation hooks'
// `$repo`).
func (e *env) projectDir() string {
	if d := e.sys.getenv("CLAUDE_PROJECT_DIR"); d != "" {
		return d
	}
	d, _ := e.sys.getwd()
	return d
}

// git is a git query's stdout in root, "" when it prints nothing (the scripts kept whatever
// `& git … 2>$null` printed).
func (e *env) git(root string, args ...string) string {
	out, _ := e.sys.output("", "git", append([]string{"-C", root}, args...)...)
	return out
}

// payload reads stdin as a JSON object; nil when it is empty or not an object.
func (e *env) payload() (map[string]any, []byte) {
	raw, _ := io.ReadAll(e.stdin)
	var m map[string]any
	if json.Unmarshal(bytes.TrimSpace(bytes.TrimPrefix(raw, []byte(bom))), &m) != nil {
		return nil, raw
	}
	return m, raw
}

// field is a nested string member, "" when absent or not a string.
func field(m map[string]any, path ...string) string {
	var v any = m
	for _, k := range path {
		obj, ok := v.(map[string]any)
		if !ok {
			return ""
		}
		v = obj[k]
	}
	s, _ := v.(string)
	return s
}

// age is the time since path was modified; ok false when it does not exist.
func (e *env) age(path string) (time.Duration, bool) {
	st, err := os.Stat(path)
	if err != nil {
		return 0, false
	}
	return e.sys.now().Sub(st.ModTime()), true
}

// psInt is PowerShell's [int] cast of a double: rounded to the nearest, halves to even.
func psInt(f float64) int64 {
	return int64(math.RoundToEven(f))
}

// writeContext prints what the scripts printed with
// `@{ additionalContext = $ctx } | ConvertTo-Json -Compress`.
func writeContext(w io.Writer, ctx string) {
	enc := json.NewEncoder(w)
	enc.SetEscapeHTML(false)                                    // ConvertTo-Json writes < > & as they are
	_ = enc.Encode(map[string]string{"additionalContext": ctx}) // stdout, as fmt.Print
}

// writeText writes a file as `Set-Content -Encoding UTF8` did in PowerShell 7: UTF-8 without a
// byte-order mark, a line break appended.
func writeText(path, text string) error {
	return os.WriteFile(path, []byte(text+"\n"), 0o644)
}

// bom is the byte-order mark Windows PowerShell's Set-Content wrote at the head of a UTF-8 file.
var bom = string(rune(0xFEFF))

// counter reads a whole-number file (`^\d+$` once trimmed), 0 otherwise.
func counter(path string) int {
	raw, err := os.ReadFile(path)
	if err != nil {
		return 0
	}
	s := strings.TrimSpace(strings.TrimPrefix(string(raw), bom))
	if s == "" || strings.Trim(s, "0123456789") != "" {
		return 0
	}
	n := 0
	for _, c := range s {
		n = n*10 + int(c-'0')
		if n > 1<<30 {
			return 0
		}
	}
	return n
}

// envCount is an environment variable matching `^\d+$`, def otherwise (the scripts' override
// pattern for thresholds).
func envCount(e *env, name string, def int) int {
	v := e.sys.getenv(name)
	if v == "" || strings.Trim(v, "0123456789") != "" || len(v) > 9 {
		return def
	}
	n := 0
	for _, c := range v {
		n = n*10 + int(c-'0')
	}
	return n
}

// lines splits text as Get-Content did: on CRLF, LF or CR, a leading byte-order mark dropped,
// no empty last line for a trailing line break.
func lines(text string) []string {
	text = strings.TrimPrefix(text, bom)
	if text == "" {
		return nil
	}
	text = strings.ReplaceAll(text, "\r\n", "\n")
	text = strings.ReplaceAll(text, "\r", "\n")
	text = strings.TrimSuffix(text, "\n")
	return strings.Split(text, "\n")
}
