// run-spectral-critic.js
// Compare spectral profile of synth vs reference and emit a spectral score + hints.

const fs = require('fs');
const path = require('path');
const { computeSpectralProfile } = require('./compute-spectral-profile');

function loadJson(p) {
  let text = fs.readFileSync(p, 'utf8');
  if (text.charCodeAt(0) === 0xFEFF) text = text.slice(1);
  return JSON.parse(text);
}

function diff(a, b) { return { synth: a, reference: b, delta: a - b, ratio: b !== 0 ? a / b : null }; }

function summarizeComparison(synth, ref) {
  const gS = synth.global, gR = ref.global;
  const out = {};
  out.centroid = diff(gS.centroid.mean, gR.centroid.mean);
  out.rolloff = diff(gS.rolloff.mean, gR.rolloff.mean);
  out.flux = diff(gS.flux.mean, gR.flux.mean);
  out.bandLow = diff(gS.bandEnergy.low.mean, gR.bandEnergy.low.mean);
  out.bandMid = diff(gS.bandEnergy.mid.mean, gR.bandEnergy.mid.mean);
  out.bandHigh = diff(gS.bandEnergy.high.mean, gR.bandEnergy.high.mean);
  out.hnr = diff(gS.hnr.mean, gR.hnr.mean);
  out.inharm = diff(gS.inharmonicity.mean, gR.inharmonicity.mean);
  return out;
}

function scoreFromComparison(c) {
  // Start from 1.0 and subtract weighted normalized errors.
  let score = 1.0;
  const norm = (d, scale) => Math.min(Math.abs(d) / scale, 1.5);
  score -= 0.20 * norm(c.centroid.delta, 400); // Hz
  score -= 0.15 * norm(c.rolloff.delta, 800);
  score -= 0.10 * norm(c.flux.delta, 0.2);
  score -= 0.25 * norm(c.bandHigh.delta, 0.15);
  score -= 0.10 * norm(c.hnr.delta, 5);
  score -= 0.10 * norm(c.inharm.delta, 0.15);
  if (score < 0) score = 0;
  if (score > 1) score = 1;
  return score;
}

function suggestAdjustments(c) {
  const suggestions = [];
  if (c.bandHigh.delta > 0.05 || c.centroid.delta > 200) {
    suggestions.push('Spectrum is brighter / more high-heavy than reference: lower brightness (global brightness parameter), reduce dispersion a bit, or slightly lower body high-frequency emphasis.');
  } else if (c.bandHigh.delta < -0.05 || c.centroid.delta < -200) {
    suggestions.push('Spectrum is darker than reference: increase brightness (global brightness), slightly increase dispersion or air noise, or raise high-frequency body resonance gains.');
  }
  if (c.flux.delta > 0.05) {
    suggestions.push('Spectral flux is higher (more jittery): reduce randomization in pick noise, increase decay slightly, or smooth high-frequency components.');
  }
  if (c.hnr.delta < -2) {
    suggestions.push('Harmonic-to-noise ratio lower than reference: reduce broadband noise level, increase body resonance contribution, or reduce attack noise duration.');
  }
  if (c.inharm.delta > 0.05) {
    suggestions.push('Inharmonicity higher than reference: reduce dispersion for high strings or adjust string length / tuning model so partials are closer to integer multiples.');
  }
  if (!suggestions.length) suggestions.push('Spectral match is already close; fine-tune by ear or small tweaks to brightness/decay/dispersion.');
  return suggestions;
}

function main() {
  const reportPath = path.join(__dirname, '..', 'playwright-downloads', 'iteration-report.json');
  const refPath = path.join(__dirname, '..', 'reference', 'by-the-lake.wav');
  if (!fs.existsSync(reportPath)) {
    console.error(`Synth iteration report not found at ${reportPath}`);
    process.exit(1);
  }
  if (!fs.existsSync(refPath)) {
    console.error(`Reference WAV not found at ${refPath}`);
    process.exit(1);
  }
  const report = loadJson(reportPath);
  const synthWav = report.wav_path;
  if (!synthWav || !fs.existsSync(synthWav)) {
    console.error(`Synth WAV path in report is missing or invalid: ${synthWav}`);
    process.exit(1);
  }
  console.log('== Computing spectral profiles ==');
  console.log('Synth WAV:', synthWav);
  console.log('Ref WAV  :', refPath);
  const synthProfile = computeSpectralProfile(synthWav, { maxSeconds: 4 });
  const refProfile = computeSpectralProfile(refPath, { maxSeconds: 4 });
  const cmp = summarizeComparison(synthProfile, refProfile);
  const spectralScore = scoreFromComparison(cmp);
  const suggestions = suggestAdjustments(cmp);

  console.log('SPECTRAL_SCORE=' + spectralScore.toFixed(4));
  console.log('SPECTRAL_CENTROID_DELTA=' + cmp.centroid.delta.toFixed(2));
  console.log('SPECTRAL_ROLLOFF_DELTA=' + cmp.rolloff.delta.toFixed(2));
  console.log('SPECTRAL_FLUX_DELTA=' + cmp.flux.delta.toFixed(4));
  console.log('BAND_LOW_DELTA=' + cmp.bandLow.delta.toFixed(4));
  console.log('BAND_MID_DELTA=' + cmp.bandMid.delta.toFixed(4));
  console.log('BAND_HIGH_DELTA=' + cmp.bandHigh.delta.toFixed(4));
  console.log('HNR_DELTA=' + cmp.hnr.delta.toFixed(3));
  console.log('INHARMONICITY_DELTA=' + cmp.inharm.delta.toFixed(4));
  console.log('SPECTRAL_HINTS_BEGIN');
  for (const s of suggestions) console.log('- ' + s);
  console.log('SPECTRAL_HINTS_END');
}

if (require.main === module) {
  try { main(); } catch (e) { console.error('Spectral critic failed:', e); process.exit(1); }
}

