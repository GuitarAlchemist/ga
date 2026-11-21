// judgecmp compares two judge runs (JSONL from judge) case by case: verdict agreement between
// the two judges, agreement of each with the deterministic grader, cost and latency totals,
// and the cases where they disagree or where a judge fails a deterministically passing case.
//
//	judgecmp a.jsonl b.jsonl
package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"os"
	"sort"
	"strings"
)

type rec struct {
	Case    string `json:"case"`
	Backend string `json:"backend"`
	Model   string `json:"model"`
	DetPass bool   `json:"detPass"`
	Verdict *struct {
		Correctness int      `json:"correctness"`
		Helpfulness int      `json:"helpfulness"`
		Context     int      `json:"context"`
		Routing     string   `json:"routing"`
		TraceIssues []string `json:"trace_issues"`
		Verdict     string   `json:"verdict"`
		Reason      string   `json:"reason"`
	} `json:"verdict"`
	Err        string  `json:"err"`
	WallMs     int64   `json:"wallMs"`
	FirstTokMs int64   `json:"firstTokenMs"`
	InTok      int64   `json:"inTok"`
	OutTok     int64   `json:"outTok"`
	CacheRead  int64   `json:"cacheRead"`
	CacheWrite int64   `json:"cacheWrite"`
	CostUSD    float64 `json:"costUsd"`
}

func load(path string) (map[string]rec, []string) {
	f, err := os.Open(path)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}
	defer f.Close()
	m := map[string]rec{}
	var order []string
	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 1<<20), 8<<20)
	for sc.Scan() {
		var r rec
		if json.Unmarshal(sc.Bytes(), &r) == nil {
			if _, seen := m[r.Case]; !seen {
				order = append(order, r.Case)
			}
			m[r.Case] = r
		}
	}
	return m, order
}

func summary(name string, m map[string]rec) {
	var cost float64
	var in, out, cr, cw int64
	var wall, ttft []float64
	judged, fails, agree, errs := 0, 0, 0, 0
	for _, r := range m {
		cost += r.CostUSD
		in, out, cr, cw = in+r.InTok, out+r.OutTok, cr+r.CacheRead, cw+r.CacheWrite
		wall = append(wall, float64(r.WallMs))
		ttft = append(ttft, float64(r.FirstTokMs))
		if r.Verdict == nil {
			errs++
			continue
		}
		judged++
		if r.Verdict.Verdict == "fail" {
			fails++
		}
		if (r.Verdict.Verdict == "pass") == r.DetPass {
			agree++
		}
	}
	sort.Float64s(wall)
	sort.Float64s(ttft)
	n := len(m)
	p := func(v []float64, q float64) float64 { return v[int(q*float64(len(v)-1))] / 1000 }
	fmt.Printf("%-32s cases=%d judged=%d errors=%d judge-fail=%d agree-with-det=%d/%d\n", name, n, judged, errs, fails, agree, judged)
	fmt.Printf("%-32s cost $%.4f total, $%.5f/case (x1000 cases: $%.2f) | tokens in=%d cacheRead=%d cacheWrite=%d out=%d\n",
		"", cost, cost/float64(n), 1000*cost/float64(n), in, cr, cw, out)
	fmt.Printf("%-32s wall p50 %.1fs p95 %.1fs | first token p50 %.1fs | sequential total %.0fs\n",
		"", p(wall, .5), p(wall, .95), p(ttft, .5), sum(wall)/1000)
}

func sum(v []float64) float64 {
	s := 0.0
	for _, x := range v {
		s += x
	}
	return s
}

func main() {
	if len(os.Args) != 3 {
		fmt.Fprintln(os.Stderr, "usage: judgecmp a.jsonl b.jsonl")
		os.Exit(2)
	}
	a, order := load(os.Args[1])
	b, _ := load(os.Args[2])
	summary(os.Args[1], a)
	summary(os.Args[2], b)

	both, agree := 0, 0
	fmt.Println("\nDisagreements and judge-detected failures:")
	for _, id := range order {
		ra, rb := a[id], b[id]
		if ra.Verdict == nil || rb.Verdict == nil {
			continue
		}
		both++
		va, vb := ra.Verdict.Verdict, rb.Verdict.Verdict
		if va == vb {
			agree++
		}
		if va != vb || va == "fail" || vb == "fail" {
			fmt.Printf("  %-30s det=%-5v A=%-4s B=%-4s\n    A: %s\n    B: %s\n", id, ra.DetPass, va, vb,
				trim(ra.Verdict.Reason), trim(rb.Verdict.Reason))
		}
	}
	fmt.Printf("\nJudge A vs judge B verdict agreement: %d/%d\n", agree, both)

	// Trace issues both judges raised, by frequency, as a rough signal of what to fix.
	counts := map[string]int{}
	for _, m := range []map[string]rec{a, b} {
		for _, r := range m {
			if r.Verdict == nil {
				continue
			}
			for _, t := range r.Verdict.TraceIssues {
				counts[bucket(t)]++
			}
		}
	}
	keys := make([]string, 0, len(counts))
	for k := range counts {
		keys = append(keys, k)
	}
	sort.Slice(keys, func(i, j int) bool { return counts[keys[i]] > counts[keys[j]] })
	fmt.Println("\nTrace issues raised (both judges, bucketed):")
	for _, k := range keys {
		fmt.Printf("  %3d  %s\n", counts[k], k)
	}
}

func trim(s string) string {
	if len(s) > 220 {
		return s[:220] + "…"
	}
	return s
}

func bucket(issue string) string {
	l := strings.ToLower(issue)
	switch {
	case strings.Contains(l, "slow") || strings.Contains(l, "latency") || strings.Contains(l, "opaque") || strings.Contains(l, "ms") && strings.Contains(l, "orchestration"):
		return "slow / opaque step"
	case strings.Contains(l, "confidence"):
		return "low or zero confidence"
	case strings.Contains(l, "declin") || strings.Contains(l, "fallthrough"):
		return "decline chain"
	case strings.Contains(l, "below") || strings.Contains(l, "threshold"):
		return "below routing threshold"
	case strings.Contains(l, "no trace") || strings.Contains(l, "trace absent") || strings.Contains(l, "missing trace"):
		return "missing trace"
	case strings.Contains(l, "rout") || strings.Contains(l, "agent") || strings.Contains(l, "skill"):
		return "routing choice"
	}
	return "other"
}
