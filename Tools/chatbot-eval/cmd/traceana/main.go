// traceana analyses the agentic traces that convo records for every turn, not just the answers.
//
//	traceana <results.json> [more.json ...]
//
// Per turn it flags: slow (server > 10 s), opaque (one step holds > 80 % of the time),
// below-threshold routing (fell to the LLM agent router), low confidence (< 0.6),
// declines / fallbacks / non-completed steps, and missing traces. Then it aggregates
// latency and failures by routing method and by agent.
package main

import (
	"encoding/json"
	"fmt"
	"os"
	"sort"
	"strings"
)

type step struct {
	Name       string         `json:"name"`
	Status     string         `json:"status"`
	ElapsedMs  float64        `json:"elapsedMs"`
	Attributes map[string]any `json:"attributes"`
}

type trace struct {
	Steps []step `json:"steps"`
}

type turn struct {
	Prompt     string          `json:"prompt"`
	Agent      string          `json:"agent"`
	Method     string          `json:"method"`
	Elapsed    float64         `json:"elapsed"`
	Confidence float64         `json:"confidence"`
	ServerMs   float64         `json:"serverMs"`
	Trace      json.RawMessage `json:"trace"`
}

type caseResult struct {
	ID    string   `json:"id"`
	OK    bool     `json:"ok"`
	Fails []string `json:"fails"`
	Turns []turn   `json:"turns"`
}

type bucket struct {
	ms         []float64
	turns, bad int
}

func (b *bucket) add(ms float64, ok bool) {
	b.ms = append(b.ms, ms)
	b.turns++
	if !ok {
		b.bad++
	}
}

func pct(v []float64, p float64) float64 {
	if len(v) == 0 {
		return 0
	}
	s := append([]float64(nil), v...)
	sort.Float64s(s)
	return s[int(p*float64(len(s)-1))]
}

func main() {
	byMethod, byAgent := map[string]*bucket{}, map[string]*bucket{}
	flagCount := map[string]int{}
	turns := 0
	for _, path := range os.Args[1:] {
		raw, err := os.ReadFile(path)
		if err != nil {
			fmt.Fprintln(os.Stderr, err)
			os.Exit(2)
		}
		var cases []caseResult
		if err := json.Unmarshal(raw, &cases); err != nil {
			fmt.Fprintln(os.Stderr, path, err)
			os.Exit(2)
		}
		fmt.Printf("== %s\n", path)
		for _, c := range cases {
			for i, t := range c.Turns {
				turns++
				last := i == len(c.Turns)-1
				ok := !last || c.OK
				ms := t.ServerMs
				if ms == 0 {
					ms = t.Elapsed * 1000
				}
				var flags []string
				var tr trace
				if len(t.Trace) == 0 || string(t.Trace) == "null" {
					flags = append(flags, "no-trace")
				} else if err := json.Unmarshal(t.Trace, &tr); err != nil {
					flags = append(flags, "bad-trace")
				}
				if ms > 10000 {
					flags = append(flags, fmt.Sprintf("slow %.1fs", ms/1000))
				}
				if t.Confidence > 0 && t.Confidence < 0.6 {
					flags = append(flags, fmt.Sprintf("low-conf %.2f", t.Confidence))
				}
				var biggest step
				for _, s := range tr.Steps {
					if s.ElapsedMs > biggest.ElapsedMs {
						biggest = s
					}
					if o, _ := s.Attributes["routing.outcome"].(string); strings.Contains(o, "below_threshold") {
						flags = append(flags, "below-threshold")
					}
					low := strings.ToLower(s.Name)
					switch {
					case strings.Contains(low, "decline"):
						flags = append(flags, "declined:"+s.Name)
					case strings.Contains(low, "fallback") && !strings.Contains(low, "skipped"):
						flags = append(flags, "fallback:"+s.Name)
					case s.Status != "" && s.Status != "completed":
						flags = append(flags, s.Status+":"+s.Name)
					}
				}
				if ms > 3000 && biggest.ElapsedMs > 0.8*ms {
					flags = append(flags, fmt.Sprintf("opaque %s=%.0f%%", biggest.Name, 100*biggest.ElapsedMs/ms))
				}
				if last && !c.OK {
					flags = append(flags, "case-failed")
				}
				for _, f := range flags {
					flagCount[strings.SplitN(strings.SplitN(f, " ", 2)[0], ":", 2)[0]]++
				}
				m := t.Method
				if m == "" {
					m = "?"
				}
				if byMethod[m] == nil {
					byMethod[m] = &bucket{}
				}
				if byAgent[t.Agent] == nil {
					byAgent[t.Agent] = &bucket{}
				}
				byMethod[m].add(ms, ok)
				byAgent[t.Agent].add(ms, ok)
				if len(flags) > 0 {
					p := t.Prompt
					if len(p) > 60 {
						p = p[:60] + "…"
					}
					fmt.Printf("  %-28s t%d %-22s %-30s %6.1fs  %s\n    %q\n", c.ID, i+1, t.Agent, m, ms/1000, strings.Join(flags, ", "), p)
				}
			}
		}
	}
	report := func(title string, m map[string]*bucket) {
		fmt.Printf("\n%s\n  %-34s %5s %6s %7s %7s %7s\n", title, "", "turns", "fail", "p50 s", "p95 s", "max s")
		keys := make([]string, 0, len(m))
		for k := range m {
			keys = append(keys, k)
		}
		sort.Slice(keys, func(i, j int) bool { return m[keys[i]].turns > m[keys[j]].turns })
		for _, k := range keys {
			b := m[k]
			fmt.Printf("  %-34s %5d %6d %7.1f %7.1f %7.1f\n", k, b.turns, b.bad, pct(b.ms, .5)/1000, pct(b.ms, .95)/1000, pct(b.ms, 1)/1000)
		}
	}
	report("By routing method", byMethod)
	report("By agent", byAgent)
	fmt.Printf("\nFlags over %d turns:", turns)
	keys := make([]string, 0, len(flagCount))
	for k := range flagCount {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	for _, k := range keys {
		fmt.Printf(" %s=%d", k, flagCount[k])
	}
	fmt.Println()
}
