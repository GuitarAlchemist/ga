# Generates Colossus's seamless textures with Z-Image-Turbo through a local ComfyUI's API: all of them, or the ones
# named on the command line (python generate_comfyui.py concrete cladding).
# Based on learn-visuals' 09-seamless workflow: generate, offset by half, repaint the cross over the old edges.
import json, sys, time, urllib.request

URL = "http://127.0.0.1:8191"
BASE = r"C:\tmp\learn-visuals\code\comfyui\workflows\09-seamless.api.json"
JOBS = [
    ("hull", 1207, "flat orthographic texture photograph of dark gunmetal armour plating, riveted rectangular panels, "
                   "fine scratches, worn edges, oil stains, faint rust in the seams, even diffuse lighting, no shadows, "
                   "seamless metal texture"),
    ("facade", 2026, "flat orthographic texture photograph of a windowless dark brutalist facade, weathered concrete "
                     "panels and corroded steel ribs in a regular grid, rain streaks, grime, even diffuse lighting, "
                     "no shadows, seamless architectural texture"),
    ("concrete", 1965, "flat orthographic texture photograph of a board-formed raw concrete wall, horizontal timber "
                       "plank imprints, a grid of small round tie holes, light grey cement with faint warm stains and "
                       "pale efflorescence, even diffuse lighting, no shadows, seamless architectural texture"),
    ("cladding", 1972, "flat orthographic texture photograph of brushed metal facade cladding, large rectangular "
                       "panels with thin recessed joints, fine vertical brushing, faint patina, even diffuse lighting, "
                       "no shadows, seamless metal texture"),
]
wanted = sys.argv[1:] or [name for name, _, _ in JOBS]

base = json.load(open(BASE, encoding="utf-8"))
for k in ["10", "18", "32", "33", "34", "35", "36", "37", "38", "39"]:
    base.pop(k)

for name, seed, prompt in JOBS:
    if name not in wanted:
        continue
    wf = json.loads(json.dumps(base))
    wf["5"]["inputs"]["text"] = prompt
    wf["8"]["inputs"]["seed"] = seed
    wf["28"]["inputs"]["seed"] = seed
    wf["31"]["inputs"]["filename_prefix"] = f"colossus/{name}"
    req = urllib.request.Request(URL + "/prompt", data=json.dumps({"prompt": wf}).encode(), headers={"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    t0 = time.time()
    while True:
        time.sleep(2)
        h = json.load(urllib.request.urlopen(f"{URL}/history/{pid}"))
        if pid in h:
            st = h[pid].get("status", {})
            outs = [i["filename"] for o in h[pid]["outputs"].values() for i in o.get("images", [])]
            print(name, st.get("status_str"), f"{time.time() - t0:.0f} s", outs, flush=True)
            break
        if time.time() - t0 > 600:
            print(name, "timeout", flush=True)
            sys.exit(1)
