// run-critic.js

// Simple local critic harness.
// Reads iteration-report.json, computes a heuristic score from WAV stats,
// and (optionally) uses an ONNX model if onnxruntime-node + model are present.

const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

function parseArgs(argv) {
  const args = { report: null };
  for (let i = 2; i < argv.length; i++) {
    const v = argv[i];
    if (v === '--report' && i + 1 < argv.length) {
      args.report = argv[++i];
    }
  }
  return args;
}

function loadJson(p) {
  try {
    let text = fs.readFileSync(p, 'utf8');
    if (text.charCodeAt(0) === 0xFEFF) {
      text = text.slice(1);
    }
    return JSON.parse(text);
  } catch (e) {
    console.error(`Failed to read JSON at ${p}:`, e.message);
    process.exit(1);
  }
}

function clamp01(x) {
  if (x < 0) return 0;
  if (x > 1) return 1;
  return x;
}

function gaussianScore(x, target, sigma) {
  const d = x - target;
  return Math.exp(-(d * d) / (2 * sigma * sigma));
}

// Very small heuristic critic: uses duration / RMS / peak to produce a score in [0,1].
// This is just a placeholder until a real ONNX model is trained.
function heuristicScore(stats) {
  if (!stats || typeof stats.RMS !== 'number' || typeof stats.DurationSeconds !== 'number') {
    return 0.5;
  }

  const rms = stats.RMS; // already normalized ~[0,1]
  const dur = stats.DurationSeconds;
  const peak = typeof stats.Peak === 'number' ? stats.Peak : 0;

  // Target: moderately hot RMS, not too quiet, not brickwalled.
  const targetRms = 0.15;
  const rmsScore = gaussianScore(rms, targetRms, 0.08);

  // Target duration ~4 s (current pipeline); tolerate a couple seconds.
  const durScore = gaussianScore(dur, 4.0, 1.5);

  // Penalize clipping.
  const peakPenalty = peak >= 0.995 ? 0.7 : 1.0;

  let score = 0.2 + 0.8 * ((rmsScore * 0.6 + durScore * 0.4) * peakPenalty);
  return clamp01(score);
}

async function ensureOnnxModel(modelPath) {
  if (fs.existsSync(modelPath)) return;

  const url = process.env.GA_ONNX_MODEL_URL ||
    'https://huggingface.co/Xenova/wav2vec2-base-superb-ks/resolve/main/onnx/model.onnx';

  fs.mkdirSync(path.dirname(modelPath), { recursive: true });
  console.log(`Downloading ONNX model from ${url} ...`);

  if (typeof fetch !== 'function') {
    throw new Error('fetch is not available in this Node version; cannot download ONNX model.');
  }

  const res = await fetch(url);
  if (!res.ok) {
    throw new Error(`HTTP ${res.status} ${res.statusText}`);
  }

  const arrayBuffer = await res.arrayBuffer();
  await fs.promises.writeFile(modelPath, Buffer.from(arrayBuffer));
  console.log(`Saved ONNX model to ${modelPath}`);
}

async function convertWavToMono16k(inputWav) {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ga-critic-'));
  const rawPath = path.join(tmpDir, 'audio.raw');

  const ffArgs = [
    '-y',
    '-i',
    inputWav,
    '-acodec',
    'pcm_s16le',
    '-ar',
    '16000',
    '-ac',
    '1',
    '-f',
    's16le',
    rawPath,
  ];

  const ff = spawnSync('ffmpeg', ffArgs, { stdio: 'inherit' });
  if (ff.status !== 0) {
    throw new Error(`ffmpeg exited with code ${ff.status}`);
  }

  const buf = fs.readFileSync(rawPath);
  if (buf.length === 0) {
    throw new Error('No audio data after ffmpeg resample');
  }

  const sampleCount = buf.length / 2;
  const samples = new Float32Array(sampleCount);
  for (let i = 0; i < sampleCount; i++) {
    const s = buf.readInt16LE(i * 2);
    samples[i] = s / 32768;
  }

  try {
    fs.unlinkSync(rawPath);
    fs.rmdirSync(tmpDir);
  } catch (_) {
    // best-effort cleanup only
  }

  return samples;
}

async function runOnnxCritic(wavPath) {
  if (!wavPath) {
    return { score: null, reason: 'No WAV path in report; skipping ONNX critic.' };
  }

  let ort;
  try {
    ort = require('onnxruntime-node');
  } catch (e) {
    return { score: null, reason: `onnxruntime-node not available: ${e.message}` };
  }

  const criticDir = path.join(__dirname, '..', 'critic');
  const modelPath = path.join(criticDir, 'wav2vec2-base-superb-ks.onnx');

  try {
    await ensureOnnxModel(modelPath);
  } catch (e) {
    return { score: null, reason: `Failed to ensure ONNX model: ${e.message}` };
  }

  let samples;
  try {
    samples = await convertWavToMono16k(wavPath);
  } catch (e) {
    return { score: null, reason: `Failed to prepare audio for ONNX model: ${e.message}` };
  }

  if (!samples || samples.length === 0) {
    return { score: null, reason: 'No samples for ONNX critic.' };
  }

  let session;
  try {
    session = await ort.InferenceSession.create(modelPath);
  } catch (e) {
    return { score: null, reason: `Failed to create ONNX session: ${e.message}` };
  }

  const tensor = new ort.Tensor('float32', samples, [1, samples.length]);

  let output;
  try {
    const inputName = session.inputNames[0];
    const feeds = {};
    feeds[inputName] = tensor;
    output = await session.run(feeds);
  } catch (e) {
    return { score: null, reason: `ONNX inference failed: ${e.message}` };
  }

  const outputName = session.outputNames && session.outputNames[0]
    ? session.outputNames[0]
    : Object.keys(output)[0];
  const outTensor = output[outputName];
  if (!outTensor || !outTensor.data || outTensor.data.length === 0) {
    return { score: null, reason: 'ONNX model returned no data.' };
  }

  const logits = Array.from(outTensor.data);
  const maxLogit = Math.max(...logits);
  const exps = logits.map((v) => Math.exp(v - maxLogit));
  const sumExp = exps.reduce((a, b) => a + b, 0);
  const probs = exps.map((v) => v / sumExp);
  const maxProb = Math.max(...probs);
  const uniform = 1 / probs.length;
  const normScore = (maxProb - uniform) / (1 - uniform);
  const modelScore = clamp01(0.5 + 0.5 * normScore);

  const detail = `ONNX wav2vec2-base-superb-ks: classes=${probs.length}, maxProb=${maxProb.toFixed(3)}`;
  return { score: modelScore, reason: detail };
}

async function runTransformersCritic(wavPath) {
  if (!wavPath) {
    return { score: null, reason: 'No WAV path in report; skipping Transformers.js critic.' };
  }

  let pipelineFn;
  try {
    // Lazy-load Transformers.js so the script still works if it is not installed.
    ({ pipeline: pipelineFn } = await import('@huggingface/transformers'));
  } catch (e) {
    console.error('Transformers.js import failed:', e);
    return { score: null, reason: `Transformers.js import failed: ${e.message}` };
  }

  const classifier = await pipelineFn('audio-classification', 'Xenova/wav2vec2-base-superb-ks');

  let output;
  try {
    // Let Transformers.js handle all audio preprocessing.
    output = await classifier(wavPath, { top_k: 5 });
  } catch (e) {
    return { score: null, reason: `Transformers.js audio classification failed: ${e.message}` };
  }

  if (!Array.isArray(output) || output.length === 0) {
    return { score: null, reason: 'Transformers.js returned no predictions; using heuristic score.' };
  }

  const top = output[0];
  const unknown = output.find((r) => r.label === '_unknown_');
  const topScore = typeof top.score === 'number' ? top.score : 0;
  const unknownScore = unknown && typeof unknown.score === 'number' ? unknown.score : 0;

  // Map model confidence into [0,1], penalising "_unknown_" predictions.
  const modelScore = clamp01(0.5 + 0.5 * (topScore - unknownScore));
  const detail = `Transformers.js Xenova/wav2vec2-base-superb-ks: top=${top.label}:${topScore.toFixed(3)}, unknown=${unknownScore.toFixed(3)}`;

  return { score: modelScore, reason: detail };
}

async function main() {
  try {
    const args = parseArgs(process.argv);
    const defaultReport = path.join(__dirname, '..', 'playwright-downloads', 'iteration-report.json');
    const reportPath = args.report || defaultReport;

    if (!fs.existsSync(reportPath)) {
      console.error(`Iteration report not found at ${reportPath}`);
      process.exit(1);
    }

    const report = loadJson(reportPath);
    const wavPath = report.wav_path;
    const stats = report.wav_stats || null;

    const heuristic = heuristicScore(stats);

    let criticScore = heuristic;
    let reason = 'Using heuristic critic based on WAV stats.';

    const onnxResult = await runOnnxCritic(wavPath);
    if (onnxResult && typeof onnxResult.score === 'number') {
      criticScore = clamp01(0.3 * heuristic + 0.7 * onnxResult.score);
      reason = `Combined heuristic + ONNX score. Heuristic=${heuristic.toFixed(3)}. ${onnxResult.reason}`;
    } else if (onnxResult && onnxResult.reason) {
      reason = `${reason} (ONNX: ${onnxResult.reason})`;
    }

    if (!onnxResult || typeof onnxResult.score !== 'number') {
      const transformersResult = await runTransformersCritic(wavPath);
      if (transformersResult && typeof transformersResult.score === 'number') {
        criticScore = clamp01(0.3 * heuristic + 0.7 * transformersResult.score);
        reason = `Combined heuristic + Transformers.js score. Heuristic=${heuristic.toFixed(3)}. ${transformersResult.reason}`;
      } else if (transformersResult && transformersResult.reason) {
        reason = `${reason} (${transformersResult.reason})`;
      }
    }

    if (criticScore == null || Number.isNaN(criticScore)) {
      criticScore = 0.5;
    }

    console.log(`CRITIC_SCORE=${criticScore.toFixed(4)}`);
    console.log(`CRITIC_REASON=${reason}`);
    if (wavPath) {
      console.log(`CRITIC_WAV=${wavPath}`);
    }
  } catch (e) {
    console.error('Critic failed:', e);
    process.exit(1);
  }
}

main();

