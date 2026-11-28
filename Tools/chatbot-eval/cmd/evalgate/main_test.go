package main

import (
	"bytes"
	"strings"
	"testing"
)

func runOf(cases map[string]outcome) run { return run{cases: cases} }

func TestReport_CountsRegressionsAndImprovements(t *testing.T) {
	base := runOf(map[string]outcome{
		"kept":      {DetOK: true, Judge: "pass"},
		"regressed": {DetOK: true, Judge: "pass"},
		"fixed":     {DetOK: true, Judge: "fail"},
		"judged":    {DetOK: true, Judge: "pass"},
	})
	cur := runOf(map[string]outcome{
		"kept":      {DetOK: true, Judge: "pass"},
		"regressed": {DetOK: false, Judge: "pass"},
		"fixed":     {DetOK: true, Judge: "pass"},
		"judged":    {DetOK: true, Judge: "fail", Reason: "wrong interval"},
		"new":       {DetOK: false},
	})

	var out bytes.Buffer
	n := report(&out, base, cur, 0)

	if n != 2 {
		t.Fatalf("regressions = %d, want 2 (deterministic and judge)", n)
	}
	s := out.String()
	for _, want := range []string{"**2 regression(s)**", "`regressed`", "`judged`", "wrong interval", "### Improvements (1)", "`fixed`", "### Still failing (1)", "`new`"} {
		if !strings.Contains(s, want) {
			t.Errorf("summary lacks %q:\n%s", want, s)
		}
	}
}

func TestReport_JudgeErrorIsNotARegression(t *testing.T) {
	base := runOf(map[string]outcome{"c": {DetOK: true, Judge: "pass"}})
	cur := runOf(map[string]outcome{"c": {DetOK: true, Judge: ""}})

	if n := report(&bytes.Buffer{}, base, cur, 0); n != 0 {
		t.Fatalf("regressions = %d, want 0: a judge error is no verdict, not a failure", n)
	}
}

func TestReport_GoodBaselineCaseMissingFromTheRunIsARegression(t *testing.T) {
	base := runOf(map[string]outcome{
		"dropped":     {DetOK: true, Judge: "pass"},
		"dropped-bad": {DetOK: false},
		"kept":        {DetOK: true, Judge: "pass"},
	})
	cur := runOf(map[string]outcome{"kept": {DetOK: true, Judge: "pass"}})

	var out bytes.Buffer
	if n := report(&out, base, cur, 0); n != 1 {
		t.Fatalf("regressions = %d, want 1: a good case that vanished must not pass the gate", n)
	}
	for _, want := range []string{"**1 regression(s)**", "### Missing from this run (2)", "`dropped`", "`dropped-bad`"} {
		if !strings.Contains(out.String(), want) {
			t.Errorf("summary lacks %q:\n%s", want, out.String())
		}
	}
}

func TestReport_ToleranceIsReported(t *testing.T) {
	base := runOf(map[string]outcome{"c": {DetOK: true}})
	cur := runOf(map[string]outcome{"c": {DetOK: false}})

	var out bytes.Buffer
	if n := report(&out, base, cur, 1); n != 1 {
		t.Fatalf("regressions = %d, want 1", n)
	}
	if !strings.Contains(out.String(), "within tolerance 1") {
		t.Errorf("summary should say the regression is within tolerance:\n%s", out.String())
	}
}
