import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Union, Optional

import numpy as np
import onnxruntime as ort
import soundfile as sf

DEFAULT_MODEL_REPO = "Xenova/clap-htsat-unfused"
DEFAULT_MODEL_DIR = Path(
    os.environ.get("CLAP_ONNX_DIR", "models/clap-htsat-unfused/onnx")
)


@dataclass
class ClapConfig:
    sample_rate: int = 48000
    clip_seconds: float = 10.0
    mono: bool = True


PathLike = Union[str, os.PathLike]


class ClapOnnxModel:
    """Wrapper simple autour d'un modèle CLAP/HTS-AT exporté en ONNX."""

    def __init__(
        self,
        model_path: Optional[PathLike] = None,
        config: Optional[ClapConfig] = None,
    ) -> None:
        self.config = config or ClapConfig()
        if model_path is None:
            model_path = DEFAULT_MODEL_DIR / "model.onnx"
        self.model_path = Path(model_path)

        if not self.model_path.exists():
            raise FileNotFoundError(
                f"ONNX model not found at {self.model_path}. "
                f"Run download_model.py or set CLAP_ONNX_DIR."
            )

        self.session = ort.InferenceSession(
            str(self.model_path),
            providers=["CPUExecutionProvider"],
        )

        self.input_name = self.session.get_inputs()[0].name
        self.output_name = self.session.get_outputs()[0].name

    def _load_audio(self, path: PathLike) -> np.ndarray:
        audio, sr = sf.read(path, always_2d=False)
        if audio.ndim > 1:
            audio = audio.mean(axis=1)
        if sr != self.config.sample_rate:
            from scipy.signal import resample

            target_len = int(len(audio) * self.config.sample_rate / sr)
            audio = resample(audio, target_len)
        return audio.astype(np.float32)

    def _ensure_length(self, audio: np.ndarray) -> np.ndarray:
        target_len = int(self.config.clip_seconds * self.config.sample_rate)
        if len(audio) == target_len:
            return audio
        if len(audio) > target_len:
            return audio[:target_len]
        pad = target_len - len(audio)
        return np.pad(audio, (0, pad), mode="constant")

    def embed_path(self, path: PathLike) -> Dict[str, Any]:
        audio = self._load_audio(path)
        return self.embed_waveform(audio)

    def embed_waveform(self, audio: np.ndarray) -> Dict[str, Any]:
        audio = self._ensure_length(audio)
        x = audio[None, None, :]
        outputs = self.session.run([self.output_name], {self.input_name: x})
        emb = outputs[0][0]
        return {
            "embedding": emb.tolist(),
            "dim": int(emb.shape[0]),
        }
