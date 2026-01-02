// Automated pipeline: use Playwright to open the demo, record a chord,
// download the WebM recording, convert it to WAV with ffmpeg, and
// launch Sonic Visualiser on Windows.

const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');
const { chromium } = require('playwright');

async function runPlaywright(baseUrl, downloadDir, guitarType) {
  fs.mkdirSync(downloadDir, { recursive: true });

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext({ acceptDownloads: true });
  const page = await context.newPage();

  console.log(`Opening ${baseUrl} ...`);
  await page.goto(baseUrl, { waitUntil: 'networkidle' });

  // Init audio
  await page.getByRole('button', { name: "Initialiser l'audio" }).click();
  await page.waitForTimeout(500);

  // Optionally select a specific guitar profile via UI buttons
  if (typeof guitarType === 'number' && guitarType >= 0 && guitarType <= 3) {
    console.log(`Setting guitar type = ${guitarType}`);
    await page.waitForTimeout(200);
    await page.locator(`[data-guitar-type="${guitarType}"]`).click();
    await page.waitForTimeout(300);
  }

  // Start recording
  console.log('Starting recording...');
  await page.getByRole('button', { name: 'Start recording' }).click();

  // Pluck each open string before the chord for a broader snapshot
  const openStrings = ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'];
  await page.waitForTimeout(250);
  for (const label of openStrings) {
    console.log(`Pluck ${label}`);
    await page.getByRole('button', { name: label }).click();
    await page.waitForTimeout(220);
  }

  // Switch to 12-string mode for both the plucks and chord
  await page.getByRole('button', { name: 'Switch to 12-string mode' }).click();

  // Play a Cmaj7 strum
  await page.waitForTimeout(350);
  await page.getByRole('button', { name: 'Strum Cmaj7' }).click();

  // Play a second chord for variation
  await page.waitForTimeout(1200);
  await page.getByRole('button', { name: 'Strum Gmaj7' }).click();

  // Let the sound ring long enough so sustain is captured
  await page.waitForTimeout(9000);

  console.log('Stopping recording & waiting for download...');
  const [download] = await Promise.all([
    page.waitForEvent('download', { timeout: 60000 }),
    page.getByRole('button', { name: 'Stop & download' }).click(),
  ]);

  // Choose deterministic base name based on guitar profile (if provided)
  let baseName = 'guitar-mix';
  if (typeof guitarType === 'number' && guitarType >= 0 && guitarType <= 3) {
    baseName = `guitar-mix-profile${guitarType}`;
  }

  const webmPath = path.join(downloadDir, `${baseName}.webm`);
  await download.saveAs(webmPath);

  console.log('Downloaded recording:', webmPath);
  await browser.close();

  return webmPath;
}

function runFfmpeg(inputWebm, outputWav) {
  return new Promise((resolve, reject) => {
    console.log('Converting to WAV with ffmpeg ...');
    const args = ['-y', '-i', inputWebm, '-vn', '-acodec', 'pcm_s16le', '-ar', '48000', '-ac', '1', outputWav];
    const ff = spawn('ffmpeg', args, { stdio: 'inherit' });
    ff.on('close', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`ffmpeg exited with code ${code}`));
    });
  });
}

function runSpectrogram(inputWav, outputPng) {
  return new Promise((resolve, reject) => {
    console.log('Rendering spectrogram PNG with ffmpeg ...');
    const args = [
      '-y',
      '-i', inputWav,
      '-lavfi', 'showspectrumpic=s=1280x720:mode=combined:legend=0',
      outputPng,
    ];
    const ff = spawn('ffmpeg', args, { stdio: 'inherit' });
    ff.on('close', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`ffmpeg (spectrogram) exited with code ${code}`));
    });
  });
}

function launchSonicVisualiser(wavPath) {
  const exe = process.env.SONIC_VISUALISER_EXE || 'C\\\\Program Files\\\\Sonic Visualiser\\\\sonic-visualiser.exe';
  if (!fs.existsSync(exe)) {
    // Optional: we now rely mainly on the PNG, so don't treat this as an error.
    console.warn(`Sonic Visualiser executable not found at "${exe}".`);
    console.warn('You can still open the WAV manually if needed:');
    console.warn(wavPath);
    return;
  }
  console.log('Launching Sonic Visualiser ...');
  const child = spawn(exe, [wavPath], { detached: true, stdio: 'ignore' });
  child.unref();
}

async function main() {
  const baseUrl = process.env.GA_DEMO_URL || 'http://localhost:5173/';
  const downloadDir = path.resolve(__dirname, '..', 'playwright-downloads');

  let guitarType = null;
  const envVal = process.env.GA_GUITAR_TYPE;
  if (envVal !== undefined && envVal !== '') {
    const parsed = parseInt(envVal, 10);
    if (!Number.isNaN(parsed)) {
      guitarType = parsed;
    }
  }
  const argIndex = process.argv.indexOf('--guitar-type');
  if (argIndex >= 0 && argIndex < process.argv.length - 1) {
    const parsed = parseInt(process.argv[argIndex + 1], 10);
    if (!Number.isNaN(parsed)) {
      guitarType = parsed;
    }
  }

  try {
    const webmPath = await runPlaywright(baseUrl, downloadDir, guitarType);
    const wavPath = webmPath.replace(/\.webm$/i, '.wav');
    await runFfmpeg(webmPath, wavPath);

    const spectroPath = wavPath.replace(/\.wav$/i, '-spectrogram.png');
    await runSpectrogram(wavPath, spectroPath);

    // Optional: still launch Sonic Visualiser if configured, but the main
    // outputs for agents are WAV_PATH and SPECTRO_PATH.
    launchSonicVisualiser(wavPath);

    console.log('WAV_PATH=' + wavPath);
    console.log('SPECTRO_PATH=' + spectroPath);
    console.log('Done. WAV and spectrogram ready.');
  } catch (err) {
    console.error('Automation failed:', err);
    process.exit(1);
  }
}

main();

