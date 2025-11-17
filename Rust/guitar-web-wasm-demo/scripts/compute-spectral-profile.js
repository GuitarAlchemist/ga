// compute-spectral-profile.js
// Compute basic spectral metrics for a WAV file using ffmpeg + JS STFT.

const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

function convertWavToMono16k(wavPath, maxSeconds) {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ga-spec-'));
  const rawPath = path.join(tmpDir, 'audio.raw');
  const args = ['-y', '-i', wavPath, '-acodec', 'pcm_s16le', '-ar', '16000', '-ac', '1', '-f', 's16le', rawPath];
  const ff = spawnSync('ffmpeg', args, { stdio: 'inherit' });
  if (ff.status !== 0) throw new Error(`ffmpeg exited with code ${ff.status}`);
  let buf = fs.readFileSync(rawPath);
  if (maxSeconds && maxSeconds > 0) {
    const maxSamples = Math.floor(16000 * maxSeconds);
    if (buf.length / 2 > maxSamples) buf = buf.slice(0, maxSamples * 2);
  }
  const n = buf.length / 2;
  const out = new Float32Array(n);
  for (let i = 0; i < n; i++) out[i] = buf.readInt16LE(i * 2) / 32768;
  try { fs.unlinkSync(rawPath); fs.rmdirSync(tmpDir); } catch (_) {}
  return out;
}

function hann(N) {
  const w = new Float32Array(N);
  const f = 2 * Math.PI / (N - 1);
  for (let n = 0; n < N; n++) w[n] = 0.5 - 0.5 * Math.cos(f * n);
  return w;
}

function fftRadix2(re, im) {
  const n = re.length;
  let j = 0;
  for (let i = 0; i < n; i++) {
    if (i < j) {
      let tr = re[i]; re[i] = re[j]; re[j] = tr;
      let ti = im[i]; im[i] = im[j]; im[j] = ti;
    }
    let m = n >> 1;
    while (m >= 1 && j >= m) { j -= m; m >>= 1; }
    j += m;
  }
  for (let len = 2; len <= n; len <<= 1) {
    const ang = -2 * Math.PI / len;
    const wlen_r = Math.cos(ang), wlen_i = Math.sin(ang);
    for (let i = 0; i < n; i += len) {
      let wr = 1, wi = 0;
      for (let k = 0; k < len / 2; k++) {
        const u_r = re[i + k], u_i = im[i + k];
        const v_r = re[i + k + len / 2] * wr - im[i + k + len / 2] * wi;
        const v_i = re[i + k + len / 2] * wi + im[i + k + len / 2] * wr;
        re[i + k] = u_r + v_r;
        im[i + k] = u_i + v_i;
        re[i + k + len / 2] = u_r - v_r;
        im[i + k + len / 2] = u_i - v_i;
        const nwr = wr * wlen_r - wi * wlen_i;
        wi = wr * wlen_i + wi * wlen_r;
        wr = nwr;
      }
    }
  }
}

function computeStats(values) {
  const n = values.length;
  if (!n) return { mean: 0, std: 0 };
  let sum = 0; for (const v of values) sum += v;
  const mean = sum / n;
  let varSum = 0; for (const v of values) varSum += (v - mean) * (v - mean);
  return { mean, std: Math.sqrt(varSum / n) };
}

function computeEnvelopeTimes(times, env) {
  if (!times.length) return { t10: null, t90: null, tPeak: null, tHalfDecay: null };
  let peak = -Infinity, idxPeak = 0;
  for (let i = 0; i < env.length; i++) if (env[i] > peak) { peak = env[i]; idxPeak = i; }
  if (peak <= 0) return { t10: null, t90: null, tPeak: times[idxPeak], tHalfDecay: null };
  const t10v = 0.1 * peak, t90v = 0.9 * peak;
  let t10 = null, t90 = null, tHalf = null;
  for (let i = 0; i <= idxPeak; i++) {
    if (t10 === null && env[i] >= t10v) t10 = times[i];
    if (t90 === null && env[i] >= t90v) { t90 = times[i]; break; }
  }
  for (let i = idxPeak; i < env.length; i++) {
    if (env[i] <= 0.5 * peak) { tHalf = times[i]; break; }
  }
  return { t10, t90, tPeak: times[idxPeak], tHalfDecay: tHalf };
}

function computeSpectralProfile(wavPath, opts = {}) {
  const maxSeconds = opts.maxSeconds || 4.0;
  const frameSize = opts.frameSize || 1024;
  const hopSize = opts.hopSize || 512;
  const sampleRate = 16000;
  const samples = convertWavToMono16k(wavPath, maxSeconds);
  if (samples.length < frameSize * 2) throw new Error('Not enough samples for STFT');
  const window = hann(frameSize);
  const nyquist = sampleRate / 2;
  const binFreq = nyquist / (frameSize / 2);
  const frames = [];
  const times = [];
  for (let start = 0; start + frameSize <= samples.length; start += hopSize) {
    const re = new Float32Array(frameSize);
    const im = new Float32Array(frameSize);
    for (let n = 0; n < frameSize; n++) re[n] = samples[start + n] * window[n];
    fftRadix2(re, im);
    const mags = new Float32Array(frameSize / 2 + 1);
    for (let k = 0; k < mags.length; k++) {
      const rr = re[k], ii = im[k];
      mags[k] = Math.sqrt(rr * rr + ii * ii);
    }
    frames.push(mags);
    times.push(start / sampleRate);
  }
  const nFrames = frames.length;
  const centroids = new Float32Array(nFrames);
  const rolloffs = new Float32Array(nFrames);
  const flux = new Float32Array(nFrames);
  const bandLow = new Float32Array(nFrames);
  const bandMid = new Float32Array(nFrames);
  const bandHigh = new Float32Array(nFrames);
  const hnrArr = new Float32Array(nFrames);
  const f0Arr = new Float32Array(nFrames);
  const inhArr = new Float32Array(nFrames);
  const eps = 1e-12;
  const idx500 = Math.floor(500 / binFreq);
  const idx2000 = Math.floor(2000 / binFreq);
  let prevNorm = null;
  for (let t = 0; t < nFrames; t++) {
    const mags = frames[t];
    let sumMag = 0, sumFreqMag = 0, sumE = 0;
    for (let k = 0; k < mags.length; k++) {
      const m = mags[k];
      sumMag += m;
      const e = m * m;
      sumE += e;
      sumFreqMag += (k * binFreq) * m;
    }
    const totalMag = sumMag + eps;
    const totalE = sumE + eps;
    centroids[t] = sumFreqMag / totalMag;
    // rolloff
    const targetE = 0.85 * totalE;
    let accE = 0, rIdx = mags.length - 1;
    for (let k = 0; k < mags.length; k++) {
      accE += mags[k] * mags[k];
      if (accE >= targetE) { rIdx = k; break; }
    }
    rolloffs[t] = rIdx * binFreq;
    // band energies
    let eL = 0, eM = 0, eH = 0;
    for (let k = 0; k < mags.length; k++) {
      const e = mags[k] * mags[k];
      if (k <= idx500) eL += e; else if (k <= idx2000) eM += e; else eH += e;
    }
    bandLow[t] = eL / totalE;
    bandMid[t] = eM / totalE;
    bandHigh[t] = eH / totalE;
    // spectral flatness -> HNR-ish
    let logSum = 0;
    for (let k = 0; k < mags.length; k++) logSum += Math.log(mags[k] * mags[k] + eps);
    const geo = Math.exp(logSum / mags.length);
    const arith = totalE / mags.length;
    const flat = geo / (arith + eps);
    hnrArr[t] = 10 * Math.log10(1 / (flat + eps));
    // flux
    const norm = new Float32Array(mags.length);
    for (let k = 0; k < mags.length; k++) norm[k] = mags[k] / totalMag;
    if (prevNorm) {
      let s = 0;
      for (let k = 0; k < norm.length; k++) { const d = norm[k] - prevNorm[k]; s += d * d; }
      flux[t] = Math.sqrt(s);
    } else {
      flux[t] = 0;
    }
    prevNorm = norm;
    // F0 & inharmonicity (very approximate)
    let maxIdx = 1, maxVal = mags[1];
    for (let k = 2; k < mags.length; k++) if (mags[k] > maxVal) { maxVal = mags[k]; maxIdx = k; }
    const f0 = maxIdx * binFreq;
    f0Arr[t] = f0;
    // measure how close top peaks are to integer multiples of f0
    const peaks = [];
    for (let k = 1; k < mags.length; k++) peaks.push({ k, v: mags[k] * mags[k] });
    peaks.sort((a, b) => b.v - a.v);
    let errSum = 0, errCount = 0;
    for (let i = 0; i < Math.min(6, peaks.length); i++) {
      const f = peaks[i].k * binFreq;
      const r = f / (f0 + eps);
      const nInt = Math.max(1, Math.round(r));
      const frac = Math.abs(r - nInt);
      errSum += frac * frac;
      errCount++;
    }
    inhArr[t] = errCount ? Math.sqrt(errSum / errCount) : 0;
  }
  const centroidStats = computeStats(centroids);
  const rollStats = computeStats(rolloffs);
  const fluxStats = computeStats(flux);
  const lowStats = computeStats(bandLow);
  const midStats = computeStats(bandMid);
  const highStats = computeStats(bandHigh);
  const hnrStats = computeStats(hnrArr);
  const f0Stats = computeStats(f0Arr);
  const inhStats = computeStats(inhArr);
  const envLow = computeEnvelopeTimes(times, bandLow);
  const envMid = computeEnvelopeTimes(times, bandMid);
  const envHigh = computeEnvelopeTimes(times, bandHigh);
  return {
    wavPath,
    sampleRate,
    frameSize,
    hopSize,
    durationSec: samples.length / sampleRate,
    frames: nFrames,
    global: {
      centroid: centroidStats,
      rolloff: rollStats,
      flux: fluxStats,
      bandEnergy: { low: lowStats, mid: midStats, high: highStats },
      hnr: hnrStats,
      f0: f0Stats,
      inharmonicity: inhStats,
      envelopes: { low: envLow, mid: envMid, high: envHigh },
    },
  };
}

module.exports = { computeSpectralProfile };

