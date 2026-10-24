package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"slices"
	"strconv"
)

// The seamless textures, with Z-Image-Turbo. They start from learn-visuals' 09-seamless workflow: generate, offset by
// half, repaint the cross over the old edges.

type texture struct {
	name   string
	seed   int
	prompt string
}

var textures = []texture{
	{"hull", 1207, "flat orthographic texture photograph of dark gunmetal armour plating, riveted rectangular panels, " +
		"fine scratches, worn edges, oil stains, faint rust in the seams, even diffuse lighting, no shadows, " +
		"seamless metal texture"},
	{"facade", 2026, "flat orthographic texture photograph of a windowless dark brutalist facade, weathered concrete " +
		"panels and corroded steel ribs in a regular grid, rain streaks, grime, even diffuse lighting, " +
		"no shadows, seamless architectural texture"},
	{"concrete", 1965, "flat orthographic texture photograph of a board-formed raw concrete wall, horizontal timber " +
		"plank imprints, a grid of small round tie holes, light grey cement with faint warm stains and " +
		"pale efflorescence, even diffuse lighting, no shadows, seamless architectural texture"},
	{"cladding", 1972, "flat orthographic texture photograph of brushed metal facade cladding, large rectangular " +
		"panels with thin recessed joints, fine vertical brushing, faint patina, even diffuse lighting, " +
		"no shadows, seamless metal texture"},
	// For the Lego blocks: their stone and their roof tiles.
	{"marble", 480, "flat orthographic texture photograph of a honed white marble slab, warm ivory stone with soft grey " +
		"veins and a faint golden patina, fine pores, gently worn surface, even diffuse lighting, no shadows, " +
		"seamless stone texture"},
	{"terracotta", 432, "flat orthographic texture photograph of weathered terracotta clay, warm orange-red fired clay " +
		"with darker burnt patches, fine cracks, pale dust and a few lichen spots, even diffuse lighting, " +
		"no shadows, seamless clay texture"},
}

// The workflow's nodes that the textures leave out.
var unusedNodes = []string{"10", "18", "32", "33", "34", "35", "36", "37", "38", "39"}

type named struct {
	name  string
	graph map[string]any
}

// textureGraphs builds the graphs of the textures named, or of all of them.
func textureGraphs(workflow string, names []string) ([]named, error) {
	data, err := os.ReadFile(workflow)
	if err != nil {
		return nil, err
	}
	for _, name := range names {
		if !slices.ContainsFunc(textures, func(t texture) bool { return t.name == name }) {
			return nil, fmt.Errorf("no texture %q", name)
		}
	}
	var graphs []named
	for _, t := range textures {
		if len(names) > 0 && !slices.Contains(names, t.name) {
			continue
		}
		g, err := textureGraph(data, t)
		if err != nil {
			return nil, fmt.Errorf("%s: %w", workflow, err)
		}
		graphs = append(graphs, named{t.name, g})
	}
	return graphs, nil
}

func textureGraph(workflow []byte, t texture) (map[string]any, error) {
	// Numbers stay as written: a seed past 2^53 would not survive a float.
	dec := json.NewDecoder(bytes.NewReader(workflow))
	dec.UseNumber()
	var g map[string]any
	if err := dec.Decode(&g); err != nil {
		return nil, err
	}
	for _, id := range unusedNodes {
		if _, ok := g[id]; !ok {
			return nil, fmt.Errorf("node %s is missing", id)
		}
		delete(g, id)
	}
	for _, set := range []struct {
		id, input string
		value     any
	}{
		{"5", "text", t.prompt},
		{"8", "seed", t.seed},
		{"28", "seed", t.seed},
		{"31", "filename_prefix", "colossus/" + t.name},
	} {
		n, _ := g[set.id].(map[string]any)
		inputs, ok := n["inputs"].(map[string]any)
		if !ok {
			return nil, fmt.Errorf("node %s has no inputs", set.id)
		}
		inputs[set.input] = set.value
	}
	return g, nil
}

func itoa(n int64) string { return strconv.FormatInt(n, 10) }
