// evalgate compares a chatbot evaluation run with a baseline run and fails on regressions.
//
// A case is good when the deterministic checks of convo passed and the LLM judge did not
// fail it (a judge error counts as no verdict, not as a failure). A regression is a case that
// was good in the baseline and is not good now; an improvement is the reverse. The summary
// is Markdown, written for a pull-request comment.
//
//	evalgate -base-convo b1.json,b2.json -base-judge base.jsonl \
//	         -convo c1.json,c2.json -judge cur.jsonl [-tolerance 0] [-summary out.md]
//
// Exit codes: 0 no regression beyond -tolerance, 1 regressions, 2 bad input.
package main

import (
	"bufio"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"sort"
	"strings"
)

type convoCase struct {
	ID    string `json:"id"`
	OK    bool   `json:"ok"`
	Turns []struct {
		Agent   string  `json:"agent"`
		Method  string  `json:"method"`
		Elapsed float64 `json:"elapsed"`
	} `json:"turns"`
}

type judgeRecord struct {
	Case    string `json:"case"`
	Verdict *struct {
		Verdict string `json:"verdict"`
		Reason  string `json:"reason"`
	} `json:"verdict"`
	Err     string  `json:"err"`
	CostUSD float64 `json:"costUsd"`
}

// outcome is one case of one run.
type outcome struct {
	DetOK   bool
	Judge   string // pass | fail | "" (no verdict)
	Reason  string
	Seconds float64 // slowest turn
}

func (o outcome) good() bool { return o.DetOK && o.Judge != "fail" }

type run struct {
	cases map[string]outcome
	cost  float64
}

func main() {
	baseConvo := flag.String("base-convo", "", "comma-separated convo JSON files of the baseline")
	baseJudge := flag.String("base-judge", "", "judge JSONL of the baseline")
	curConvo := flag.String("convo", "", "comma-separated convo JSON files of this run")
	curJudge := flag.String("judge", "", "judge JSONL of this run")
	tolerance := flag.Int("tolerance", 0, "regressions allowed before failing")
	summaryPath := flag.String("summary", "", "write the Markdown summary here (default stdout)")
	flag.Parse()

	if *baseConvo == "" || *curConvo == "" {
		fmt.Fprintln(os.Stderr, "usage: evalgate -base-convo ... -convo ... [-base-judge ...] [-judge ...]")
		os.Exit(2)
	}
	base, err := load(*baseConvo, *baseJudge)
	if err != nil {
		fmt.Fprintln(os.Stderr, "baseline:", err)
		os.Exit(2)
	}
	cur, err := load(*curConvo, *curJudge)
	if err != nil {
		fmt.Fprintln(os.Stderr, "current:", err)
		os.Exit(2)
	}

	var out io.Writer = os.Stdout
	if *summaryPath != "" {
		f, err := os.Create(*summaryPath)
		if err != nil {
			fmt.Fprintln(os.Stderr, err)
			os.Exit(2)
		}
		defer f.Close()
		out = f
	}

	regressions := report(out, base, cur, *tolerance)
	if regressions > *tolerance {
		os.Exit(1)
	}
}

func load(convoFiles, judgeFile string) (run, error) {
	r := run{cases: map[string]outcome{}}
	for _, path := range strings.Split(convoFiles, ",") {
		data, err := os.ReadFile(strings.TrimSpace(path))
		if err != nil {
			return r, err
		}
		var cases []convoCase
		if err := json.Unmarshal(data, &cases); err != nil {
			return r, fmt.Errorf("%s: %w", path, err)
		}
		for _, c := range cases {
			o := outcome{DetOK: c.OK}
			for _, t := range c.Turns {
				if t.Elapsed > o.Seconds {
					o.Seconds = t.Elapsed
				}
			}
			r.cases[c.ID] = o
		}
	}
	if judgeFile == "" {
		return r, nil
	}
	f, err := os.Open(judgeFile)
	if err != nil {
		return r, err
	}
	defer f.Close()
	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 1<<20), 8<<20)
	for sc.Scan() {
		var j judgeRecord
		if json.Unmarshal(sc.Bytes(), &j) != nil {
			continue
		}
		r.cost += j.CostUSD
		o, ok := r.cases[j.Case]
		if !ok || j.Verdict == nil {
			continue
		}
		o.Judge, o.Reason = j.Verdict.Verdict, j.Verdict.Reason
		r.cases[j.Case] = o
	}
	return r, sc.Err()
}

// report writes the summary and returns the number of regressions.
func report(w io.Writer, base, cur run, tolerance int) int {
	ids := make([]string, 0, len(cur.cases))
	for id := range cur.cases {
		ids = append(ids, id)
	}
	sort.Strings(ids)

	var regressions, improvements, stillBad []string
	for _, id := range ids {
		now := cur.cases[id]
		before, known := base.cases[id]
		switch {
		case known && before.good() && !now.good():
			regressions = append(regressions, id)
		case known && !before.good() && now.good():
			improvements = append(improvements, id)
		case !now.good():
			stillBad = append(stillBad, id)
		}
	}

	status := "no regression"
	if len(regressions) > tolerance {
		status = fmt.Sprintf("**%d regression(s)**", len(regressions))
	} else if len(regressions) > 0 {
		status = fmt.Sprintf("%d regression(s), within tolerance %d", len(regressions), tolerance)
	}

	fmt.Fprintf(w, "## Chatbot evaluation: %s\n\n", status)
	fmt.Fprintln(w, "| | baseline | this run |")
	fmt.Fprintln(w, "|---|---|---|")
	fmt.Fprintf(w, "| deterministic checks passed | %s | %s |\n", count(base, func(o outcome) bool { return o.DetOK }), count(cur, func(o outcome) bool { return o.DetOK }))
	fmt.Fprintf(w, "| judged fail | %d | %d |\n", tally(base, "fail"), tally(cur, "fail"))
	fmt.Fprintf(w, "| good (checks passed, judge did not fail) | %s | %s |\n", count(base, outcome.good), count(cur, outcome.good))
	fmt.Fprintf(w, "| slowest turn per case, median | %.1f s | %.1f s |\n", medianSeconds(base), medianSeconds(cur))
	fmt.Fprintf(w, "| judge cost | $%.4f | $%.4f |\n\n", base.cost, cur.cost)

	section(w, "Regressions", regressions, cur)
	section(w, "Improvements", improvements, cur)
	section(w, "Still failing", stillBad, cur)
	return len(regressions)
}

func section(w io.Writer, title string, ids []string, r run) {
	if len(ids) == 0 {
		return
	}
	fmt.Fprintf(w, "### %s (%d)\n\n", title, len(ids))
	for _, id := range ids {
		o := r.cases[id]
		judge := o.Judge
		if judge == "" {
			judge = "no verdict"
		}
		fmt.Fprintf(w, "- `%s`: checks %s, judge %s", id, passFail(o.DetOK), judge)
		if o.Reason != "" && o.Judge == "fail" {
			fmt.Fprintf(w, ": %s", trim(o.Reason, 240))
		}
		fmt.Fprintln(w)
	}
	fmt.Fprintln(w)
}

func count(r run, pred func(outcome) bool) string {
	n := 0
	for _, o := range r.cases {
		if pred(o) {
			n++
		}
	}
	return fmt.Sprintf("%d/%d", n, len(r.cases))
}

func tally(r run, verdict string) int {
	n := 0
	for _, o := range r.cases {
		if o.Judge == verdict {
			n++
		}
	}
	return n
}

func medianSeconds(r run) float64 {
	if len(r.cases) == 0 {
		return 0
	}
	v := make([]float64, 0, len(r.cases))
	for _, o := range r.cases {
		v = append(v, o.Seconds)
	}
	sort.Float64s(v)
	return v[len(v)/2]
}

func passFail(ok bool) string {
	if ok {
		return "passed"
	}
	return "failed"
}

func trim(s string, n int) string {
	s = strings.ReplaceAll(s, "\n", " ")
	if len(s) > n {
		return s[:n] + "…"
	}
	return s
}
