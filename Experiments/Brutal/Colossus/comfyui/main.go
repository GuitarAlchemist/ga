// Command comfyui makes Colossus's ComfyUI assets through a local ComfyUI's API, one job at a time: the head (a portrait,
// cut out, then a TRELLIS.2 mesh) and the seamless textures. Go's standard library only.
//
//	go run . head <seed> [<seed> ...]
//	go run . textures -workflow <09-seamless.api.json> [name ...]
//
// -url sets ComfyUI's address; -print writes the graphs as JSON instead of queueing them.
package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"net/http"
	"os"
	"strconv"
	"time"
)

type job struct {
	name    string
	graph   any
	timeout time.Duration
}

func main() {
	if len(os.Args) < 2 {
		usage()
	}
	flags := flag.NewFlagSet(os.Args[1], flag.ExitOnError)
	url := flags.String("url", "http://127.0.0.1:8191", "ComfyUI's address")
	printOnly := flags.Bool("print", false, "write the graphs as JSON instead of queueing them")
	workflow := flags.String("workflow", "", "textures: learn-visuals' 09-seamless.api.json")
	flags.Parse(os.Args[2:])

	var jobs []job
	var poll time.Duration
	switch os.Args[1] {
	case "head":
		if flags.NArg() == 0 {
			usage()
		}
		for _, arg := range flags.Args() {
			seed, err := strconv.ParseInt(arg, 10, 64)
			if err != nil {
				fail(fmt.Errorf("seed %q: %w", arg, err))
			}
			jobs = append(jobs, job{arg, headGraph(seed), 15 * time.Minute})
		}
		poll = 3 * time.Second
	case "textures":
		if *workflow == "" {
			usage()
		}
		graphs, err := textureGraphs(*workflow, flags.Args())
		if err != nil {
			fail(err)
		}
		for _, g := range graphs {
			jobs = append(jobs, job{g.name, g.graph, 10 * time.Minute})
		}
		poll = 2 * time.Second
	default:
		usage()
	}

	if *printOnly {
		out := map[string]any{}
		for _, j := range jobs {
			out[j.name] = j.graph
		}
		enc := json.NewEncoder(os.Stdout)
		enc.SetIndent("", "  ")
		if err := enc.Encode(out); err != nil {
			fail(err)
		}
		return
	}
	c := &client{url: *url, http: &http.Client{Timeout: 30 * time.Second}, poll: poll}
	failed := false
	for _, j := range jobs {
		id, err := c.queue(j.graph)
		if err != nil {
			fail(fmt.Errorf("%s: %w", j.name, err))
		}
		r, err := c.wait(id, j.timeout)
		if err != nil {
			fail(fmt.Errorf("%s: %w", j.name, err))
		}
		messages, _ := json.Marshal(r.errors)
		if len(messages) > 600 {
			messages = messages[:600]
		}
		fmt.Printf("%s %s %.0f s %v %s\n", j.name, r.status, r.elapsed.Seconds(), r.images, messages)
		failed = failed || r.status != "success"
	}
	if failed {
		os.Exit(1)
	}
}

func usage() {
	fmt.Fprintln(os.Stderr, "usage: comfyui head [-url URL] [-print] <seed> [<seed> ...]\n"+
		"       comfyui textures -workflow <09-seamless.api.json> [-url URL] [-print] [name ...]")
	os.Exit(2)
}

func fail(err error) {
	fmt.Fprintln(os.Stderr, "FAIL:", err)
	os.Exit(1)
}
