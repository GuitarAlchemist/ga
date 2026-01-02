from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Any, Dict, List

import numpy as np
import soundfile as sf
from scipy.signal import stft


@dataclass
class LoudnessStats:
    rms_db: float
    peak_db: float


@dataclass
class SpectralStats:
    centroid_hz: float
    bandwidth_hz: float


@dataclass
class Transient:
    time_sec: float
    strength: float


@dataclass
class AudioAnalysis:
    sample_rate: int
    duration_sec: float
    loudness: LoudnessStats
    spectral: SpectralStats
    transients: List[Transient]


def _db(x: float) -> float:
    return 20.0 * np.log10(max(x, 1e-12))


def analyze_waveform(audio: np.ndarray, sr: int) -> AudioAnalysis:
    duration = len(audio) / float(sr)

    rms = float(np.sqrt(np.mean(audio ** 2)))
    peak = float(np.max(np.abs(audio)))
    loud = LoudnessStats(rms_db=_db(rms), peak_db=_db(peak))

    f, t, Zxx = stft(
        audio,
        fs=sr,
        nperseg=2048,
        noverlap=1536,
        padded=False,
        boundary=None,
    )
    mag = np.abs(Zxx) + 1e-12
    spec = mag.mean(axis=1)
    spec /= spec.sum()

    centroid = float((f * spec).sum())
    bw = float(np.sqrt(((f - centroid) ** 2 * spec).sum()))
    spectral = SpectralStats(centroid_hz=centroid, bandwidth_hz=bw)

    frame_energy = (mag ** 2).sum(axis=0)
    frame_energy /= frame_energy.max() + 1e-12
    diff = np.diff(frame_energy)
    transient_idx = np.where(diff > 0.15)[0]
    transients: List[Transient] = [
        Transient(time_sec=float(t[i]), strength=float(diff[i]))
        for i in transient_idx
    ]

    return AudioAnalysis(
        sample_rate=sr,
        duration_sec=duration,
        loudness=loud,
        spectral=spectral,
        transients=transients,
    )


def analyze_file(path: str) -> Dict[str, Any]:
    audio, sr = sf.read(path, always_2d=False)
    if audio.ndim > 1:
        audio = audio.mean(axis=1)
    audio = audio.astype(np.float32)
    analysis = analyze_waveform(audio, sr)
    return {
        "sample_rate": analysis.sample_rate,
        "duration_sec": analysis.duration_sec,
        "loudness": asdict(analysis.loudness),
        "spectral": asdict(analysis.spectral),
        "transients": [asdict(t) for t in analysis.transients],
    }
