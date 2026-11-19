// convo grades the GA chatbot on free-form questions, multi-turn conversations
// and off-topic requests, with deterministic checks (no LLM judge).
//
//	convo <base_url> <cases.yaml> <out.json>
package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"regexp"
	"strings"
	"time"

	"gopkg.in/yaml.v3"
)

type testCase struct {
	ID       string   `yaml:"id"`
	Kind     string   `yaml:"kind"`
	Turns    []string `yaml:"turns"`
	Any      []string `yaml:"any"`
	Any2     []string `yaml:"any2"`
	All      []string `yaml:"all"`
	None     []string `yaml:"none"`
	NotAgent []string `yaml:"not_agent"`
	MinLen   int      `yaml:"min_len"`
}

type chatMessage struct {
	Role    string `json:"role"`
	Content string `json:"content"`
}

type chatResponse struct {
	Answer        string `json:"naturalLanguageAnswer"`
	AgentID       string `json:"agentId"`
	RoutingMethod string          `json:"routingMethod"`
	Confidence    float64         `json:"confidence"`
	ServerMs      float64         `json:"elapsedMs"`
	Trace         json.RawMessage `json:"trace"`
}

type turnResult struct {
	Prompt  string  `json:"prompt"`
	Agent   string  `json:"agent"`
	Method  string  `json:"method"`
	Elapsed float64 `json:"elapsed"`
	Answer  string  `json:"answer"`
	// Agentic trace as returned by GaApi (routing outcome, steps, timings), kept verbatim for traceana.
	Confidence float64         `json:"confidence"`
	ServerMs   float64         `json:"serverMs"`
	Trace      json.RawMessage `json:"trace,omitempty"`
}

type caseResult struct {
	ID    string       `json:"id"`
	Kind  string       `json:"kind"`
	OK    bool         `json:"ok"`
	Fails []string     `json:"fails,omitempty"`
	Turns []turnResult `json:"turns"`
}

// Text that must never reach a user: errors, parse failures, leaked prompt scaffolding.
var errorMarkers = []string{
	"I encountered an error", "Could not parse", "Could not identify",
	"currently unavailable", "Reproduce the catalog below verbatim", "Pure pedagogy",
	"Use when a learner asks", "returned no matches", "not yet implemented",
	"score 0.000", // tag-only voicing matches with no relevance
}

var client = &http.Client{Timeout: 180 * time.Second}

// has reports whether term occurs as a whole token. Note names need care: "G" must
// not match inside "G#" or "Gb", and "F#" must not match inside "F##".
func has(text, term string) bool {
	re := regexp.MustCompile(`(?i)(^|[^A-Za-z0-9#♯♭])` + regexp.QuoteMeta(term) + `($|[^A-Za-z0-9#♯♭])`)
	// Single-letter note names are matched case-sensitively so "a" (article) is not "A".
	if len(term) <= 2 && strings.ToUpper(term[:1]) == term[:1] {
		re = regexp.MustCompile(`(^|[^A-Za-z0-9#♯♭])` + regexp.QuoteMeta(term) + `($|[^A-Za-z0-9#♯♭])`)
	}
	return re.MatchString(text)
}

func ask(base, msg string, history []chatMessage) (chatResponse, int, float64, error) {
	body, _ := json.Marshal(map[string]any{"message": msg, "conversationHistory": history})
	start := time.Now()
	resp, err := client.Post(base+"/api/chatbot/chat", "application/json", bytes.NewReader(body))
	elapsed := time.Since(start).Seconds()
	if err != nil {
		return chatResponse{}, 0, elapsed, err
	}
	defer resp.Body.Close()
	var r chatResponse
	if resp.StatusCode == http.StatusOK {
		err = json.NewDecoder(resp.Body).Decode(&r)
	}
	return r, resp.StatusCode, elapsed, err
}

func grade(c testCase, last turnResult) []string {
	var fails []string
	a := last.Answer
	for _, m := range errorMarkers {
		if strings.Contains(strings.ToLower(a), strings.ToLower(m)) {
			fails = append(fails, "error marker "+fmt.Sprintf("%q", m))
		}
	}
	if len(a) < c.MinLen {
		fails = append(fails, fmt.Sprintf("answer %d chars < %d", len(a), c.MinLen))
	}
	for _, t := range c.All {
		if !has(a, t) {
			fails = append(fails, fmt.Sprintf("missing %q", t))
		}
	}
	for _, group := range [][]string{c.Any, c.Any2} {
		if len(group) == 0 {
			continue
		}
		hit := false
		for _, t := range group {
			hit = hit || has(a, t)
		}
		if !hit {
			fails = append(fails, fmt.Sprintf("none of %v", group))
		}
	}
	for _, t := range c.None {
		if has(a, t) {
			fails = append(fails, fmt.Sprintf("must not contain %q", t))
		}
	}
	for _, ag := range c.NotAgent {
		if last.Agent == ag {
			fails = append(fails, "answered by "+ag)
		}
	}
	return fails
}

func main() {
	if len(os.Args) < 4 {
		fmt.Fprintln(os.Stderr, "usage: convo <base_url> <cases.yaml> <out.json>")
		os.Exit(2)
	}
	base, casesPath, outPath := os.Args[1], os.Args[2], os.Args[3]
	raw, err := os.ReadFile(casesPath)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}
	var cases []testCase
	if err := yaml.Unmarshal(raw, &cases); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}

	var results []caseResult
	passed := map[string][2]int{}
	for _, c := range cases {
		cr := caseResult{ID: c.ID, Kind: c.Kind}
		var history []chatMessage
		failed := false
		for _, t := range c.Turns {
			r, code, el, err := ask(base, t, history)
			tr := turnResult{Prompt: t, Agent: r.AgentID, Method: r.RoutingMethod, Elapsed: el, Answer: r.Answer,
				Confidence: r.Confidence, ServerMs: r.ServerMs, Trace: r.Trace}
			cr.Turns = append(cr.Turns, tr)
			if err != nil || code != http.StatusOK {
				cr.Fails = append(cr.Fails, fmt.Sprintf("turn %q: HTTP %d %v", t, code, err))
				failed = true
				break
			}
			history = append(history, chatMessage{"user", t}, chatMessage{"assistant", r.Answer})
		}
		if !failed {
			cr.Fails = append(cr.Fails, grade(c, cr.Turns[len(cr.Turns)-1])...)
		}
		cr.OK = len(cr.Fails) == 0
		p := passed[c.Kind]
		p[1]++
		if cr.OK {
			p[0]++
		}
		passed[c.Kind] = p
		last := cr.Turns[len(cr.Turns)-1]
		status := "PASS"
		if !cr.OK {
			status = "FAIL"
		}
		fmt.Printf("%s %-32s %-26s %5.1fs %s\n", status, c.ID, last.Agent, last.Elapsed, strings.Join(cr.Fails, "; "))
		results = append(results, cr)
	}

	out, _ := json.MarshalIndent(results, "", " ")
	_ = os.WriteFile(outPath, out, 0o644)
	total, ok := 0, 0
	for kind, p := range passed {
		fmt.Printf("%-13s %d/%d\n", kind, p[0], p[1])
		total += p[1]
		ok += p[0]
	}
	fmt.Printf("TOTAL %d/%d passed\n", ok, total)
	if ok < total {
		os.Exit(1)
	}
}
