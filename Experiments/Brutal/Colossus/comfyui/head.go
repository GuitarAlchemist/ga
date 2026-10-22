package main

// The head: Z-Image-Turbo paints a frontal portrait of an imaginary sculpted bust, BiRefNet cuts it out, and TRELLIS.2
// (with DINOv3 features) turns it into a mesh, saved as GLB. The TRELLIS.2 part follows learn's ga-lab ga-3d-trellis2
// workflow, shape only (no texture stage).

const headPrompt = "front view studio photograph of a classical marble bust of an imaginary serene ageless androgynous " +
	"face, eyes open, smooth bald head, calm closed mouth, head and neck only, perfectly symmetrical, facing the camera " +
	"straight on, plain pure white background, soft even light, sharp focus"

type node struct {
	ClassType string         `json:"class_type"`
	Inputs    map[string]any `json:"inputs"`
}

type in = map[string]any

// out links to output slot of node id.
func out(id string, slot int) []any { return []any{id, slot} }

func headGraph(seed int64) map[string]node {
	n := func(class string, inputs in) node { return node{class, inputs} }
	return map[string]node{
		"1": n("UNETLoader", in{"unet_name": "z_image_turbo_bf16.safetensors", "weight_dtype": "default"}),
		"2": n("CLIPLoader", in{"clip_name": "qwen_3_4b.safetensors", "type": "lumina2", "device": "default"}),
		"3": n("VAELoader", in{"vae_name": "z_image_ae.safetensors"}),
		"4": n("ModelSamplingAuraFlow", in{"model": out("1", 0), "shift": 3.0}),
		"5": n("CLIPTextEncode", in{"text": headPrompt, "clip": out("2", 0)}),
		"6": n("ConditioningZeroOut", in{"conditioning": out("5", 0)}),
		"7": n("EmptySD3LatentImage", in{"width": 1024, "height": 1024, "batch_size": 1}),
		"8": n("KSampler", in{"seed": seed, "steps": 8, "cfg": 1.0, "sampler_name": "res_multistep", "scheduler": "simple",
			"denoise": 1.0, "model": out("4", 0), "positive": out("5", 0), "negative": out("6", 0), "latent_image": out("7", 0)}),
		"9":  n("VAEDecode", in{"samples": out("8", 0), "vae": out("3", 0)}),
		"10": n("SaveImage", in{"images": out("9", 0), "filename_prefix": "head/portrait-" + itoa(seed)}),
		"40": n("LoadBackgroundRemovalModel", in{"bg_removal_name": "birefnet.safetensors"}),
		"41": n("RemoveBackground", in{"bg_removal_model": out("40", 0), "image": out("9", 0)}),
		"42": n("ImageCropToMask", in{"images": out("9", 0), "masks": out("41", 0), "width": 1024, "height": 1024,
			"pad_factor": 1, "grow_mask": 0, "background": "#000000"}),
		"60": n("CLIPVisionLoader", in{"clip_name": "dino_v3_vit_l.safetensors"}),
		"61": n("UNETLoader", in{"unet_name": "trellis_2_int8_convrot.safetensors", "weight_dtype": "default"}),
		"62": n("VAELoader", in{"vae_name": "trellis_2_shape_vae_bf16.safetensors"}),
		"64": n("Trellis2Conditioning", in{"clip_vision_model": out("60", 0), "image": out("42", 0)}),
		"65": n("EmptyTrellis2LatentStructure", in{"batch_size": 1}),
		"66": n("CFGOverride", in{"model": out("61", 0), "cfg": 1, "start_percent": 0.667, "end_percent": 1}),
		"67": n("RescaleCFG", in{"model": out("66", 0), "multiplier": 0.7}),
		"68": n("ModelSamplingSD3", in{"model": out("67", 0), "shift": 5}),
		"69": n("KSampler", in{"seed": 56, "steps": 12, "cfg": 7.5, "sampler_name": "euler", "scheduler": "normal",
			"denoise": 1, "model": out("68", 0), "positive": out("64", 0), "negative": out("64", 1), "latent_image": out("65", 0)}),
		"70": n("VaeDecodeStructureTrellis2", in{"samples": out("69", 0), "vae": out("62", 0), "resolution": "32"}),
		"71": n("Trellis2ShapeStage", in{"positive": out("64", 0), "negative": out("64", 1), "voxel": out("70", 0)}),
		"72": n("CFGOverride", in{"model": out("61", 0), "cfg": 1, "start_percent": 0.769, "end_percent": 1}),
		"73": n("RescaleCFG", in{"model": out("72", 0), "multiplier": 0.5}),
		"74": n("KSampler", in{"seed": 42, "steps": 20, "cfg": 7.5, "sampler_name": "euler", "scheduler": "normal",
			"denoise": 1, "model": out("73", 0), "positive": out("71", 0), "negative": out("71", 1), "latent_image": out("71", 2)}),
		"75": n("Trellis2UpsampleStage", in{"positive": out("71", 0), "negative": out("71", 1), "shape_latent": out("74", 0),
			"vae": out("62", 0), "target_resolution": 1024}),
		"76": n("KSampler", in{"seed": 42, "steps": 12, "cfg": 7.5, "sampler_name": "euler", "scheduler": "simple",
			"denoise": 1, "model": out("73", 0), "positive": out("75", 0), "negative": out("75", 1), "latent_image": out("75", 2)}),
		"77": n("VaeDecodeShapeTrellis", in{"samples": out("76", 0), "vae": out("62", 0)}),
		"81": n("DecimateMesh", in{"mesh": out("77", 0), "target_face_count": 150000, "placement_mode": "midpoint"}),
		"90": n("SaveGLB", in{"mesh": out("81", 0), "filename_prefix": "head/head-" + itoa(seed)}),
	}
}
