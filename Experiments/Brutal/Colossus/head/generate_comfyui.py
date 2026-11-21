# Generates Colossus's head through a local ComfyUI's API: Z-Image-Turbo paints a frontal portrait of an imaginary
# sculpted bust, BiRefNet cuts it out, and TRELLIS.2 (with DINOv3 features) turns it into a mesh, saved as GLB.
# The TRELLIS.2 part follows learn's ga-lab ga-3d-trellis2 workflow, shape only (no texture stage).
# Usage: python gen_head.py <seed> [<seed> ...]
import json, sys, time, urllib.request

URL = "http://127.0.0.1:8191"
PROMPT = ("front view studio photograph of a classical marble bust of an imaginary serene ageless androgynous face, "
          "eyes open, smooth bald head, calm closed mouth, head and neck only, perfectly symmetrical, facing the camera "
          "straight on, plain pure white background, soft even light, sharp focus")


def graph(seed):
    n = lambda cls, **inputs: {"class_type": cls, "inputs": inputs}
    return {
        "1": n("UNETLoader", unet_name="z_image_turbo_bf16.safetensors", weight_dtype="default"),
        "2": n("CLIPLoader", clip_name="qwen_3_4b.safetensors", type="lumina2", device="default"),
        "3": n("VAELoader", vae_name="z_image_ae.safetensors"),
        "4": n("ModelSamplingAuraFlow", model=["1", 0], shift=3.0),
        "5": n("CLIPTextEncode", text=PROMPT, clip=["2", 0]),
        "6": n("ConditioningZeroOut", conditioning=["5", 0]),
        "7": n("EmptySD3LatentImage", width=1024, height=1024, batch_size=1),
        "8": n("KSampler", seed=seed, steps=8, cfg=1.0, sampler_name="res_multistep", scheduler="simple", denoise=1.0,
               model=["4", 0], positive=["5", 0], negative=["6", 0], latent_image=["7", 0]),
        "9": n("VAEDecode", samples=["8", 0], vae=["3", 0]),
        "10": n("SaveImage", images=["9", 0], filename_prefix=f"head/portrait-{seed}"),
        "40": n("LoadBackgroundRemovalModel", bg_removal_name="birefnet.safetensors"),
        "41": n("RemoveBackground", bg_removal_model=["40", 0], image=["9", 0]),
        "42": n("ImageCropToMask", images=["9", 0], masks=["41", 0], width=1024, height=1024, pad_factor=1,
                grow_mask=0, background="#000000"),
        "60": n("CLIPVisionLoader", clip_name="dino_v3_vit_l.safetensors"),
        "61": n("UNETLoader", unet_name="trellis_2_int8_convrot.safetensors", weight_dtype="default"),
        "62": n("VAELoader", vae_name="trellis_2_shape_vae_bf16.safetensors"),
        "64": n("Trellis2Conditioning", clip_vision_model=["60", 0], image=["42", 0]),
        "65": n("EmptyTrellis2LatentStructure", batch_size=1),
        "66": n("CFGOverride", model=["61", 0], cfg=1, start_percent=0.667, end_percent=1),
        "67": n("RescaleCFG", model=["66", 0], multiplier=0.7),
        "68": n("ModelSamplingSD3", model=["67", 0], shift=5),
        "69": n("KSampler", seed=56, steps=12, cfg=7.5, sampler_name="euler", scheduler="normal", denoise=1,
                model=["68", 0], positive=["64", 0], negative=["64", 1], latent_image=["65", 0]),
        "70": n("VaeDecodeStructureTrellis2", samples=["69", 0], vae=["62", 0], resolution="32"),
        "71": n("Trellis2ShapeStage", positive=["64", 0], negative=["64", 1], voxel=["70", 0]),
        "72": n("CFGOverride", model=["61", 0], cfg=1, start_percent=0.769, end_percent=1),
        "73": n("RescaleCFG", model=["72", 0], multiplier=0.5),
        "74": n("KSampler", seed=42, steps=20, cfg=7.5, sampler_name="euler", scheduler="normal", denoise=1,
                model=["73", 0], positive=["71", 0], negative=["71", 1], latent_image=["71", 2]),
        "75": n("Trellis2UpsampleStage", positive=["71", 0], negative=["71", 1], shape_latent=["74", 0], vae=["62", 0],
                target_resolution=1024),
        "76": n("KSampler", seed=42, steps=12, cfg=7.5, sampler_name="euler", scheduler="simple", denoise=1,
                model=["73", 0], positive=["75", 0], negative=["75", 1], latent_image=["75", 2]),
        "77": n("VaeDecodeShapeTrellis", samples=["76", 0], vae=["62", 0]),
        "81": n("DecimateMesh", mesh=["77", 0], target_face_count=150000, placement_mode="midpoint"),
        "90": n("SaveGLB", mesh=["81", 0], filename_prefix=f"head/head-{seed}"),
    }


for seed in [int(s) for s in sys.argv[1:]]:
    req = urllib.request.Request(URL + "/prompt", data=json.dumps({"prompt": graph(seed)}).encode(),
                                 headers={"Content-Type": "application/json"})
    pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    t0 = time.time()
    while True:
        time.sleep(3)
        h = json.load(urllib.request.urlopen(f"{URL}/history/{pid}"))
        if pid in h:
            st = h[pid].get("status", {})
            msgs = [m for m in st.get("messages", []) if m[0] == "execution_error"]
            print(seed, st.get("status_str"), f"{time.time() - t0:.0f} s", json.dumps(msgs)[:600], flush=True)
            break
        if time.time() - t0 > 900:
            print(seed, "timeout", flush=True)
            sys.exit(1)
