package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"slices"
	"time"
)

// client queues graphs on ComfyUI's /prompt and polls /history until they finish.
type client struct {
	url  string
	http *http.Client
	poll time.Duration
}

type result struct {
	status  string
	errors  []json.RawMessage // the execution_error messages
	images  []string
	elapsed time.Duration
}

func (c *client) queue(graph any) (string, error) {
	body, err := json.Marshal(map[string]any{"prompt": graph})
	if err != nil {
		return "", err
	}
	resp, err := c.http.Post(c.url+"/prompt", "application/json", bytes.NewReader(body))
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		// ComfyUI explains a refused graph (node_errors) in the body.
		text, _ := io.ReadAll(io.LimitReader(resp.Body, 4000))
		return "", fmt.Errorf("/prompt: %s: %s", resp.Status, bytes.TrimSpace(text))
	}
	var out struct {
		PromptID string `json:"prompt_id"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&out); err != nil || out.PromptID == "" {
		return "", fmt.Errorf("/prompt: no prompt_id (%v)", err)
	}
	return out.PromptID, nil
}

func (c *client) wait(id string, timeout time.Duration) (result, error) {
	start := time.Now()
	for {
		time.Sleep(c.poll)
		var history map[string]struct {
			Status struct {
				StatusStr string            `json:"status_str"`
				Messages  []json.RawMessage `json:"messages"`
			} `json:"status"`
			Outputs map[string]struct {
				Images []struct {
					Filename string `json:"filename"`
				} `json:"images"`
			} `json:"outputs"`
		}
		if err := c.get("/history/"+id, &history); err != nil {
			return result{}, err
		}
		if h, done := history[id]; done {
			r := result{status: h.Status.StatusStr, elapsed: time.Since(start)}
			for _, m := range h.Status.Messages {
				var pair []json.RawMessage
				if json.Unmarshal(m, &pair) == nil && len(pair) > 0 && string(pair[0]) == `"execution_error"` {
					r.errors = append(r.errors, m)
				}
			}
			for _, o := range h.Outputs {
				for _, image := range o.Images {
					r.images = append(r.images, image.Filename)
				}
			}
			slices.Sort(r.images)
			return r, nil
		}
		if time.Since(start) > timeout {
			return result{}, fmt.Errorf("prompt %s: no result after %s", id, timeout)
		}
	}
}

func (c *client) get(path string, v any) error {
	resp, err := c.http.Get(c.url + path)
	if err != nil {
		return err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("%s: %s", path, resp.Status)
	}
	return json.NewDecoder(resp.Body).Decode(v)
}
