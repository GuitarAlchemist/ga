import argparse
import os
import sys
import time

import numpy as np
import librosa

HAVE_DDSP = False
try:
    import ddsp
    import ddsp.training
    import gin
    import tensorflow.compat.v2 as tf  # noqa: F401
    HAVE_DDSP = True
except ImportError:
    print("WARNING: DDSP library not available, using simple spectral coloration fallback.")


def load_audio_mono(path, sample_rate=16000):
    """Load mono audio and resample to the DDSP model rate."""
    audio, sr = librosa.load(path, sr=sample_rate, mono=True)
    if audio.ndim == 1:
        audio = audio[np.newaxis, :]
    return audio.astype(np.float32), sample_rate


def find_gin_and_ckpt(ckpt_dir):
    """Locate operative_config (.gin) and checkpoint prefix in a directory."""
    gin_file = None
    ckpt_prefix = None
    for name in os.listdir(ckpt_dir):
        if name.endswith(".gin") and not name.startswith("."):
            gin_file = os.path.join(ckpt_dir, name)
            break
    for name in os.listdir(ckpt_dir):
        if name.startswith("ckpt-") and name.endswith(".index"):
            ckpt_prefix = os.path.join(ckpt_dir, name[:-6])
            break
    if gin_file is None or ckpt_prefix is None:
        raise RuntimeError(
            f"Could not find .gin or ckpt-* files in checkpoint dir: {ckpt_dir}"
        )
    return gin_file, ckpt_prefix


def write_wav_int16(path, audio, sample_rate):
    """Write mono float32 audio in [-1, 1] to a 16-bit PCM WAV."""
    import wave
    import struct

    os.makedirs(os.path.dirname(path), exist_ok=True)
    # Normalize to avoid clipping.
    max_abs = float(np.max(np.abs(audio))) if audio.size else 0.0
    if max_abs > 1.0:
        audio = audio / max_abs

    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)  # 16-bit
        wf.setframerate(sample_rate)
        for x in audio:
            v = int(np.clip(x, -1.0, 1.0) * 32767)
            wf.writeframes(struct.pack("<h", v))


def simple_spectral_color(input_wav, output_wav, max_seconds=4.0):
    """Fallback: simple spectral coloration using librosa STFT."""
    y, sr = librosa.load(input_wav, sr=None, mono=True)
    if max_seconds is not None:
        y = y[: int(max_seconds * sr)]
    if y.size == 0:
        write_wav_int16(output_wav, y, sr)
        return

    n_fft = 2048
    hop = 512
    S = librosa.stft(y, n_fft=n_fft, hop_length=hop)
    freqs = librosa.fft_frequencies(sr=sr, n_fft=n_fft)

    # Broad body resonance around ~220 Hz and gentle high-frequency roll-off.
    body = np.exp(-0.5 * ((freqs - 220.0) / 180.0) ** 2)
    air = 0.4 + 0.6 * np.exp(-freqs / 6000.0)
    tilt = body * air

    S_colored = S * tilt[:, np.newaxis]
    y_out = librosa.istft(S_colored, hop_length=hop, length=len(y))

    peak = float(np.max(np.abs(y_out))) if y_out.size else 0.0
    if peak > 0:
        y_out = y_out / peak

    write_wav_int16(output_wav, y_out.astype(np.float32), sr)


def resynthesize_with_ddsp(input_wav, output_wav, ckpt_dir, max_seconds=4.0):
    """Run DDSP timbre transfer-style resynthesis on input_wav, or fallback if DDSP is unavailable."""
    if not HAVE_DDSP:
        print("DDSP library not available, applying simple spectral coloration.")
        simple_spectral_color(input_wav, output_wav, max_seconds=max_seconds)
        return

    gin_file, ckpt = find_gin_and_ckpt(ckpt_dir)

    # Parse model config.
    with gin.unlock_config():
        gin.parse_config_file(gin_file, skip_unknown=True)

    audio, sr = load_audio_mono(input_wav, sample_rate=16000)

    # Get training lengths from gin and adapt to the new audio length.
    time_steps_train = gin.query_parameter("F0LoudnessPreprocessor.time_steps")
    n_samples_train = gin.query_parameter("Harmonic.n_samples")
    hop_size = int(n_samples_train / time_steps_train)

    total_steps = int(audio.shape[1] / hop_size)
    if max_seconds is not None:
        max_steps = int(max_seconds * sr / hop_size)
        total_steps = min(total_steps, max_steps)
    time_steps = max(1, total_steps)
    n_samples = time_steps * hop_size

    audio = audio[:, :n_samples]

    # Compute audio features (f0, loudness).
    af = ddsp.training.metrics.compute_audio_features(audio)
    # Ensure loudness is float32. In newer DDSP versions this may be a Tensor, not a NumPy array.
    try:
        import tensorflow.compat.v2 as tf  # type: ignore
        af["loudness_db"] = tf.cast(af["loudness_db"], tf.float32)
    except Exception:
        af["loudness_db"] = np.asarray(af["loudness_db"]).astype(np.float32)

    # Update gin lengths to match this particular clip.
    gin_params = [
        f"Harmonic.n_samples = {n_samples}",
        f"FilteredNoise.n_samples = {n_samples}",
        f"F0LoudnessPreprocessor.time_steps = {time_steps}",
        "oscillator_bank.use_angular_cumsum = True",
    ]
    with gin.unlock_config():
        gin.parse_config(gin_params)

    # Trim feature tensors.
    for key in ["f0_hz", "f0_confidence", "loudness_db"]:
        af[key] = af[key][:time_steps]
    af["audio"] = af["audio"][:, :n_samples]

    # Build and restore model.
    model = ddsp.training.models.Autoencoder()
    model.restore(ckpt)

    # Forward pass and render audio.
    outputs = model(af, training=False)
    audio_gen = model.get_audio_from_outputs(outputs).numpy()[0]

    # Normalize to [-1, 1] and write.
    peak = float(np.max(np.abs(audio_gen))) if audio_gen.size else 0.0
    if peak > 0:
        audio_gen = audio_gen / peak

    write_wav_int16(output_wav, audio_gen, sr)


def main():
    parser = argparse.ArgumentParser(description="Apply DDSP coloration to a WAV file.")
    parser.add_argument(
        "--input",
        default=os.path.join("..", "playwright-downloads", "guitar-mix.wav"),
        help="Input WAV path (default: playwright-downloads/guitar-mix.wav)",
    )
    parser.add_argument(
        "--output",
        default=os.path.join("..", "playwright-downloads", "guitar-mix-ddsp.wav"),
        help="Output WAV path (default: playwright-downloads/guitar-mix-ddsp.wav)",
    )
    parser.add_argument(
        "--ckpt_dir",
        required=False,
        default=None,
        help=(
            "Directory containing DDSP checkpoint (.gin + ckpt-* files). "
            "Required only when using the real DDSP autoencoder backend."
        ),
    )
    parser.add_argument(
        "--max_seconds",
        type=float,
        default=4.0,
        help="Maximum duration (in seconds) of input to process.",
    )
    args = parser.parse_args()

    in_path = os.path.abspath(args.input)
    out_path = os.path.abspath(args.output)
    ckpt_dir = os.path.abspath(args.ckpt_dir) if args.ckpt_dir else None

    if not os.path.exists(in_path):
        print(f"ERROR: Input WAV not found: {in_path}")
        sys.exit(1)

    use_ddsp = False

    # Decide whether to use the real DDSP autoencoder or the simple spectral fallback.
    if HAVE_DDSP and ckpt_dir and os.path.isdir(ckpt_dir):
        use_ddsp = True
    else:
        if HAVE_DDSP:
            if ckpt_dir is None:
                print(
                    "WARNING: DDSP library is available but no checkpoint directory was provided; "
                    "using simple spectral coloration fallback instead."
                )
            elif not os.path.isdir(ckpt_dir):
                print(
                    f"WARNING: DDSP checkpoint directory not found or invalid ('{ckpt_dir}'); "
                    "using simple spectral coloration fallback instead."
                )
        else:
            if ckpt_dir is not None and not os.path.isdir(ckpt_dir):
                print(f"WARNING: Checkpoint directory not found (ignored in fallback mode): {ckpt_dir}")
        ckpt_dir = None

    t0 = time.time()
    try:
        if use_ddsp:
            resynthesize_with_ddsp(in_path, out_path, ckpt_dir, max_seconds=args.max_seconds)
        else:
            simple_spectral_color(in_path, out_path, max_seconds=args.max_seconds)
    except Exception as e:  # noqa: BLE001
        print("DDSP coloration failed:", e)
        sys.exit(1)

    dt = time.time() - t0
    if use_ddsp:
        mode = "DDSP autoencoder"
    elif HAVE_DDSP:
        mode = "spectral fallback (DDSP library available but no valid checkpoint)"
    else:
        mode = "spectral fallback (no ddsp library)"
    print(f"{mode} coloration done in {dt:.1f} s")
    print("DDSP_INPUT=" + in_path)
    print("DDSP_OUTPUT=" + out_path)


if __name__ == "__main__":
    main()

