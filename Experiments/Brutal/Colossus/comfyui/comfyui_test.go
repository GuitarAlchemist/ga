package main

import (
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

// fakeComfy answers /prompt with p1, then /history/p1 with nothing until the given poll, then with the finished prompt.
func fakeComfy(t *testing.T, finishedAt int32, posted *[]byte) *client {
	t.Helper()
	var polls atomic.Int32
	mux := http.NewServeMux()
	mux.HandleFunc("POST /prompt", func(w http.ResponseWriter, r *http.Request) {
		*posted, _ = io.ReadAll(r.Body)
		io.WriteString(w, `{"prompt_id": "p1", "number": 0, "node_errors": {}}`)
	})
	mux.HandleFunc("GET /history/p1", func(w http.ResponseWriter, r *http.Request) {
		if polls.Add(1) < finishedAt {
			io.WriteString(w, `{}`)
			return
		}
		io.WriteString(w, `{"p1": {"status": {"status_str": "error", "messages": [
			["execution_start", {"prompt_id": "p1"}],
			["execution_error", {"node_id": "77", "exception_message": "out of memory"}]]},
			"outputs": {"31": {"images": [{"filename": "colossus/hull_00002_.png"}]},
			            "10": {"images": [{"filename": "colossus/hull_00001_.png"}]}}}}`)
	})
	server := httptest.NewServer(mux)
	t.Cleanup(server.Close)
	return &client{url: server.URL, http: server.Client(), poll: time.Millisecond}
}

func TestQueueAndWait(t *testing.T) {
	var posted []byte
	c := fakeComfy(t, 3, &posted)
	id, err := c.queue(headGraph(7))
	if err != nil || id != "p1" {
		t.Fatalf("queue: %q, %v", id, err)
	}
	var body struct {
		Prompt map[string]node `json:"prompt"`
	}
	if err := json.Unmarshal(posted, &body); err != nil {
		t.Fatal(err)
	}
	if got := body.Prompt["90"].Inputs["filename_prefix"]; got != "head/head-7" {
		t.Fatalf("posted SaveGLB prefix %v", got)
	}
	r, err := c.wait(id, time.Minute)
	if err != nil {
		t.Fatal(err)
	}
	if r.status != "error" || len(r.errors) != 1 || !strings.Contains(string(r.errors[0]), "out of memory") {
		t.Fatalf("status %q, errors %s", r.status, r.errors)
	}
	if strings.Join(r.images, " ") != "colossus/hull_00001_.png colossus/hull_00002_.png" {
		t.Fatalf("images %v", r.images)
	}
}

func TestWaitGivesUp(t *testing.T) {
	var posted []byte
	c := fakeComfy(t, 1<<30, &posted)
	if _, err := c.wait("p1", 20*time.Millisecond); err == nil || !strings.Contains(err.Error(), "no result after") {
		t.Fatalf("got %v", err)
	}
}

func TestQueueShowsWhyComfyRefused(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusBadRequest)
		io.WriteString(w, `{"error": {"type": "prompt_outputs_failed_validation"}, "node_errors": {"61": "unet_name not in list"}}`)
	}))
	defer server.Close()
	c := &client{url: server.URL, http: server.Client()}
	if _, err := c.queue(headGraph(7)); err == nil || !strings.Contains(err.Error(), "unet_name not in list") {
		t.Fatalf("got %v", err)
	}
}

// Every link of the head's graph points at a node of the graph.
func TestHeadLinksResolve(t *testing.T) {
	g := headGraph(7)
	for id, n := range g {
		for name, v := range n.Inputs {
			if link, ok := v.([]any); ok {
				if _, found := g[link[0].(string)]; !found {
					t.Errorf("node %s input %s links to missing node %v", id, name, link[0])
				}
			}
		}
	}
}

func TestTextureGraph(t *testing.T) {
	workflow := `{"5": {"inputs": {"text": ""}}, "8": {"inputs": {"seed": 0}}, "28": {"inputs": {"seed": 0}},
		"31": {"inputs": {"filename_prefix": ""}}, "40": {"inputs": {"seed": 18446744073709551615}}`
	for _, id := range unusedNodes {
		workflow += `, "` + id + `": {}`
	}
	workflow += "}"
	g, err := textureGraph([]byte(workflow), textures[0])
	if err != nil {
		t.Fatal(err)
	}
	out, _ := json.Marshal(g)
	for _, want := range []string{`"seed":1207`, `"colossus/hull"`, `armour plating`, `18446744073709551615`} {
		if !strings.Contains(string(out), want) {
			t.Errorf("%s lacks %s", out, want)
		}
	}
	for _, id := range unusedNodes {
		if _, ok := g[id]; ok {
			t.Errorf("node %s kept", id)
		}
	}
	if _, err := textureGraph([]byte(`{"5": {}}`), textures[0]); err == nil {
		t.Error("a workflow without the unused nodes passed")
	}
}
