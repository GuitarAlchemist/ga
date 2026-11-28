"""Records the colossus's lines ahead with Chatterbox Multilingual (Resemble AI, MIT), offline, as voice/fr/N.wav and
voice/en/N.wav: 16-bit mono PCM at 24 kHz. See README.md beside it for the voices and their licences.

usage (in a Python environment with chatterbox-tts, its weights already cached, HF_HUB_OFFLINE=1):
    python render.py jobs.json
jobs.json: [{"text", "lang": "fr"|"en", "voice": reference WAV, "out", "exaggeration"?, "cfg_weight"?, "seed"?}]
"""
import json, sys
import torch, soundfile as sf
from chatterbox.mtl_tts import ChatterboxMultilingualTTS

jobs = json.load(open(sys.argv[1], encoding='utf-8'))
model = ChatterboxMultilingualTTS.from_pretrained(device='cuda')
for job in jobs:
    torch.manual_seed(job.get('seed', 0))
    wav = model.generate(job['text'], language_id=job['lang'], audio_prompt_path=job['voice'],
                         exaggeration=job.get('exaggeration', 0.85), cfg_weight=job.get('cfg_weight', 0.3))
    sf.write(job['out'], wav.squeeze(0).cpu().numpy(), model.sr, subtype='PCM_16')
    print('%s: %.1fs' % (job['out'], wav.shape[-1] / model.sr), flush=True)
