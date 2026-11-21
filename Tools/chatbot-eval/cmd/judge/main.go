// judge grades GA chatbot cases (answers AND agentic traces) with an LLM, through one of two
// backends, so their cost and latency can be compared on the same inputs:
//
//	judge -backend cli -model haiku  -out v-cli.jsonl  results.json [more.json ...]
//	judge -backend api -model claude-haiku-5-5 -out v-api.jsonl results.json ...
//
// cli: `claude -p --output-format stream-json` (Max plan, apiKeySource none); cost is the
// notional total_cost_usd the CLI reports, not a charge.
// api: Anthropic Messages API, streaming, GA_ANTHROPIC_API_KEY (+ GA_ANTHROPIC_WORKSPACE_ID); cost is
// computed from the returned usage and the published per-MTok prices.
package main

import (
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"os/exec"
	"regexp"
	"sort"
	"strings"
	"time"

	"github.com/anthropics/anthropic-sdk-go/v2"
	"github.com/anthropics/anthropic-sdk-go/v2/option"
)

const rubric = `You are a strict QA judge for Guitar Alchemist, a guitar and music-theory chatbot.
You get one test case: the user turns, and for each turn the chatbot's answer plus its agentic trace
(routing method, agent, confidence, server time, trace steps). Judge both the answer and the trace.

Score:
- correctness 0-2: 2 = musically correct and complete, 1 = minor error or omission, 0 = wrong or harmful.
- helpfulness 0-2: 2 = answers what was asked at the right level, 1 = partly, 0 = misses the question.
- context 0-2 (multi-turn only, else 2): 2 = uses earlier turns correctly, 0 = ignores or misreads them.
- routing "ok" | "questionable" | "wrong": was the agent/skill a sensible handler for the request?
- trace_issues: short strings for problems visible in the trace (wrong route, decline chain, slow
  opaque step, low confidence, fallback, missing trace). Empty list if none.
- verdict "pass" | "fail": fail if correctness is 0, or helpfulness is 0, or context is 0.

Write reason and trace_issues in English. Reply with ONE JSON object and nothing else:
{"correctness":n,"helpfulness":n,"context":n,"routing":"...","trace_issues":["..."],"verdict":"...","reason":"one sentence"}`

type turn struct {
	Prompt     string          `json:"prompt"`
	Agent      string          `json:"agent"`
	Method     string          `json:"method"`
	Answer     string          `json:"answer"`
	Confidence float64         `json:"confidence"`
	ServerMs   float64         `json:"serverMs"`
	Trace      json.RawMessage `json:"trace"`
}

type caseResult struct {
	ID    string   `json:"id"`
	Kind  string   `json:"kind"`
	OK    bool     `json:"ok"`
	Fails []string `json:"fails"`
	Turns []turn   `json:"turns"`
}

type verdict struct {
	Correctness int      `json:"correctness"`
	Helpfulness int      `json:"helpfulness"`
	Context     int      `json:"context"`
	Routing     string   `json:"routing"`
	TraceIssues []string `json:"trace_issues"`
	Verdict     string   `json:"verdict"`
	Reason      string   `json:"reason"`
}

type record struct {
	Case         string   `json:"case"`
	Backend      string   `json:"backend"`
	Model        string   `json:"model"`
	DetPass      bool     `json:"detPass"`
	Verdict      *verdict `json:"verdict,omitempty"`
	Raw          string   `json:"raw,omitempty"`
	Err          string   `json:"err,omitempty"`
	WallMs       int64    `json:"wallMs"`
	FirstTokMs   int64    `json:"firstTokenMs"`
	InTok        int64    `json:"inTok"`
	OutTok       int64    `json:"outTok"`
	CacheRead    int64    `json:"cacheRead"`
	CacheWrite   int64    `json:"cacheWrite"`
	CostUSD      float64  `json:"costUsd"`
	CostIsCharge bool     `json:"costIsCharge"`
	StopReason   string   `json:"stopReason,omitempty"`
}

// compactTrace keeps the trace small: step name, status, elapsed and a few attributes.
func compactTrace(raw json.RawMessage) string {
	if len(raw) == 0 || string(raw) == "null" {
		return "(no trace)"
	}
	var t struct {
		Steps []struct {
			Name       string         `json:"name"`
			Status     string         `json:"status"`
			ElapsedMs  float64        `json:"elapsedMs"`
			Attributes map[string]any `json:"attributes"`
		} `json:"steps"`
	}
	if json.Unmarshal(raw, &t) != nil {
		return "(unreadable trace)"
	}
	var b strings.Builder
	for _, s := range t.Steps {
		fmt.Fprintf(&b, "  - %s [%s] %.0fms", s.Name, s.Status, s.ElapsedMs)
		keys := make([]string, 0, len(s.Attributes))
		for k := range s.Attributes {
			keys = append(keys, k)
		}
		sort.Strings(keys)
		for _, k := range keys {
			v := fmt.Sprint(s.Attributes[k])
			if len(v) > 80 {
				v = v[:80] + "…"
			}
			fmt.Fprintf(&b, " %s=%s", k, v)
		}
		b.WriteString("\n")
	}
	return b.String()
}

func casePrompt(c caseResult) string {
	var b strings.Builder
	fmt.Fprintf(&b, "Case %s (kind %s, %d turn(s)).\n", c.ID, c.Kind, len(c.Turns))
	for i, t := range c.Turns {
		ans := t.Answer
		if len(ans) > 4000 {
			ans = ans[:4000] + "…(truncated)"
		}
		fmt.Fprintf(&b, "\n### Turn %d\nUser: %s\nRouted: agent=%s method=%s confidence=%.2f serverMs=%.0f\nTrace:\n%sAnswer:\n%s\n",
			i+1, t.Prompt, t.Agent, t.Method, t.Confidence, t.ServerMs, compactTrace(t.Trace), ans)
	}
	return b.String()
}

var jsonObj = regexp.MustCompile(`(?s)\{.*\}`)

func parseVerdict(text string) (*verdict, error) {
	m := jsonObj.FindString(text)
	if m == "" {
		return nil, fmt.Errorf("no JSON object in reply")
	}
	var v verdict
	if err := json.Unmarshal([]byte(m), &v); err != nil {
		return nil, err
	}
	return &v, nil
}

// --- backend cli: claude -p stream-json ---

func runCLI(ctx context.Context, model, prompt string, r *record) (string, error) {
	cmd := exec.CommandContext(ctx, "claude", "-p", "--output-format", "stream-json", "--verbose",
		"--include-partial-messages", "--model", model, "--tools", "", "--strict-mcp-config",
		"--no-session-persistence", "--system-prompt", rubric)
	// Never let an API key turn the Max-plan run into a billed one.
	var env []string
	for _, e := range os.Environ() {
		if !strings.HasPrefix(e, "ANTHROPIC_API_KEY=") && !strings.HasPrefix(e, "ANTHROPIC_AUTH_TOKEN=") {
			env = append(env, e)
		}
	}
	cmd.Env = env
	cmd.Stdin = strings.NewReader(prompt)
	out, err := cmd.StdoutPipe()
	if err != nil {
		return "", err
	}
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	start := time.Now()
	if err := cmd.Start(); err != nil {
		return "", err
	}
	var text, result string
	sc := bufio.NewScanner(out)
	sc.Buffer(make([]byte, 1<<20), 16<<20)
	for sc.Scan() {
		var ev map[string]any
		if json.Unmarshal(sc.Bytes(), &ev) != nil {
			continue // hook output and other non-JSON lines
		}
		switch ev["type"] {
		case "system":
			if init, _ := ev["subtype"].(string); init == "init" {
				if src, _ := ev["apiKeySource"].(string); src != "none" {
					_ = cmd.Process.Kill()
					return "", fmt.Errorf("refusing: apiKeySource=%q (expected none / Max plan)", src)
				}
			}
		case "stream_event":
			e, _ := ev["event"].(map[string]any)
			if e["type"] == "content_block_delta" {
				d, _ := e["delta"].(map[string]any)
				if t, ok := d["text"].(string); ok {
					if r.FirstTokMs == 0 {
						r.FirstTokMs = time.Since(start).Milliseconds()
					}
					text += t
				}
			}
		case "result":
			result, _ = ev["result"].(string)
			r.CostUSD, _ = ev["total_cost_usd"].(float64)
			if u, ok := ev["usage"].(map[string]any); ok {
				r.InTok = num(u["input_tokens"])
				r.OutTok = num(u["output_tokens"])
				r.CacheRead = num(u["cache_read_input_tokens"])
				r.CacheWrite = num(u["cache_creation_input_tokens"])
			}
		}
	}
	werr := cmd.Wait()
	r.WallMs = time.Since(start).Milliseconds()
	if result == "" {
		result = text
	}
	if result == "" && werr != nil {
		return "", fmt.Errorf("%v: %s", werr, strings.TrimSpace(stderr.String()))
	}
	return result, nil
}

func num(v any) int64 {
	f, _ := v.(float64)
	return int64(f)
}

// --- backend api: Anthropic Messages API, streaming ---

// USD per million tokens: input, output. Cache read 0.1x input, cache write (5 min) 1.25x input.
var prices = map[string][2]float64{
	"claude-haiku-5-5":  {0.10, 0.50},
	"claude-sonnet-5-5": {2, 10},
}

func runAPI(ctx context.Context, client anthropic.Client, model, prompt string, think bool, r *record) (string, error) {
	start := time.Now()
	params := anthropic.MessageNewParams{
		Model:     anthropic.Model(model),
		MaxTokens: 4096,
		System: []anthropic.TextBlockParam{{
			Text:         rubric,
			CacheControl: anthropic.NewCacheControlEphemeralParam(),
		}},
		Messages: []anthropic.MessageParam{anthropic.NewUserMessage(anthropic.NewTextBlock(prompt))},
	}
	if !think {
		params.Thinking = anthropic.ThinkingConfigParamUnion{OfDisabled: &anthropic.ThinkingConfigDisabledParam{}}
	}
	stream := client.Messages.NewStreaming(ctx, params)
	msg := anthropic.Message{}
	var text strings.Builder
	for stream.Next() {
		ev := stream.Current()
		_ = msg.Accumulate(ev)
		if d, ok := ev.AsAny().(anthropic.ContentBlockDeltaEvent); ok {
			if t, ok := d.Delta.AsAny().(anthropic.TextDelta); ok {
				if r.FirstTokMs == 0 {
					r.FirstTokMs = time.Since(start).Milliseconds()
				}
				text.WriteString(t.Text)
			}
		}
	}
	r.WallMs = time.Since(start).Milliseconds()
	if err := stream.Err(); err != nil {
		return "", err
	}
	r.StopReason = string(msg.StopReason)
	u := msg.Usage
	r.InTok, r.OutTok = u.InputTokens, u.OutputTokens
	r.CacheRead, r.CacheWrite = u.CacheReadInputTokens, u.CacheCreationInputTokens
	p, ok := prices[model]
	if !ok {
		return "", fmt.Errorf("no price for %s", model)
	}
	r.CostUSD = (float64(r.InTok)*p[0] + float64(r.CacheRead)*p[0]*0.1 + float64(r.CacheWrite)*p[0]*1.25 + float64(r.OutTok)*p[1]) / 1e6
	r.CostIsCharge = true
	return text.String(), nil
}

func main() {
	backend := flag.String("backend", "cli", "cli | api")
	model := flag.String("model", "", "cli: haiku|sonnet; api: claude-haiku-5-5|claude-sonnet-5-5")
	outPath := flag.String("out", "verdicts.jsonl", "output JSONL")
	limit := flag.Int("limit", 0, "max cases (0 = all)")
	think := flag.Bool("think", true, "api: keep adaptive thinking (Haiku 5.5 default); false disables it")
	flag.Parse()

	var cases []caseResult
	for _, p := range flag.Args() {
		raw, err := os.ReadFile(p)
		if err != nil {
			fmt.Fprintln(os.Stderr, err)
			os.Exit(2)
		}
		var cs []caseResult
		if err := json.Unmarshal(raw, &cs); err != nil {
			fmt.Fprintln(os.Stderr, p, err)
			os.Exit(2)
		}
		cases = append(cases, cs...)
	}
	if *limit > 0 && *limit < len(cases) {
		cases = cases[:*limit]
	}

	var client anthropic.Client
	if *backend == "api" {
		key := os.Getenv("GA_ANTHROPIC_API_KEY")
		if key == "" {
			fmt.Fprintln(os.Stderr, "GA_ANTHROPIC_API_KEY not set")
			os.Exit(2)
		}
		opts := []option.RequestOption{option.WithAPIKey(key)}
		// A key that is not scoped to a workspace must name one.
		if ws := os.Getenv("GA_ANTHROPIC_WORKSPACE_ID"); ws != "" {
			opts = append(opts, option.WithHeader("anthropic-workspace-id", ws))
		}
		client = anthropic.NewClient(opts...)
	}

	f, err := os.Create(*outPath)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}
	defer f.Close()
	enc := json.NewEncoder(f)

	var cost float64
	var wall, ftok []float64
	fails, agreeDet, judged := 0, 0, 0
	for _, c := range cases {
		r := record{Case: c.ID, Backend: *backend, Model: *model, DetPass: c.OK}
		ctx, cancel := context.WithTimeout(context.Background(), 3*time.Minute)
		var reply string
		if *backend == "cli" {
			reply, err = runCLI(ctx, *model, casePrompt(c), &r)
		} else {
			reply, err = runAPI(ctx, client, *model, casePrompt(c), *think, &r)
		}
		cancel()
		if err != nil {
			r.Err = err.Error()
		} else if v, perr := parseVerdict(reply); perr != nil {
			r.Err, r.Raw = perr.Error(), reply
		} else {
			r.Verdict = v
			judged++
			if v.Verdict == "fail" {
				fails++
			}
			if (v.Verdict == "pass") == c.OK {
				agreeDet++
			}
		}
		_ = enc.Encode(r)
		cost += r.CostUSD
		wall = append(wall, float64(r.WallMs))
		ftok = append(ftok, float64(r.FirstTokMs))
		status := "?"
		if r.Verdict != nil {
			status = r.Verdict.Verdict
		}
		fmt.Printf("%-30s %-5s det=%-5v %6dms ttft=%5dms in=%d cr=%d cw=%d out=%d $%.5f %s\n",
			c.ID, status, c.OK, r.WallMs, r.FirstTokMs, r.InTok, r.CacheRead, r.CacheWrite, r.OutTok, r.CostUSD, r.Err)
	}
	sort.Float64s(wall)
	sort.Float64s(ftok)
	n := len(cases)
	if n == 0 {
		return
	}
	fmt.Printf("\n%s/%s: %d cases, %d judged, %d judge-fail, agree with deterministic %d/%d\n",
		*backend, *model, n, judged, fails, agreeDet, judged)
	fmt.Printf("cost total $%.4f, per case $%.5f; wall p50 %.1fs p95 %.1fs; first token p50 %.1fs\n",
		cost, cost/float64(n), wall[n/2]/1000, wall[int(0.95*float64(n-1))]/1000, ftok[n/2]/1000)
}
