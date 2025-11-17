let mediaRecorder = null;
let recordedChunks = [];

export async function initAudioEngine({ onLog } = {}) {
  const log = (msg) => {
    if (onLog) onLog(msg);
  };

  const AudioCtx = window.AudioContext || window.webkitAudioContext;
  if (!AudioCtx) {
    log('Web Audio API not supported in this browser.');
    throw new Error('Web Audio API not supported');
  }

  const audioContext = new AudioCtx();

  const workletUrl = new URL('./guitar-worklet.js', import.meta.url);
  await audioContext.audioWorklet.addModule(workletUrl);
  log('AudioWorklet module loaded.');

  const response = await fetch('/guitar_engine.wasm');
  const wasmModule = await response.arrayBuffer();
  log('WASM module fetched.');

  const node = new AudioWorkletNode(audioContext, 'guitar-processor', {
    numberOfOutputs: 1,
    outputChannelCount: [2],
    channelCount: 2,
  });
  node.port.postMessage({
    type: 'init',
    wasmModule,
    sampleRate: audioContext.sampleRate,
  });
  log('Init message sent to AudioWorklet.');

  // Connect to speakers
  node.connect(audioContext.destination);

  // Optional recording path using MediaRecorder
  try {
    if (typeof MediaRecorder !== 'undefined') {
      const dest = audioContext.createMediaStreamDestination();
      node.connect(dest);
      mediaRecorder = new MediaRecorder(dest.stream);
      recordedChunks = [];
      mediaRecorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          recordedChunks.push(event.data);
        }
      };
      log('Recording support enabled (MediaRecorder).');
    } else {
      log('MediaRecorder not supported; recording disabled.');
    }
  } catch (err) {
    log(`Recording setup failed: ${String(err)}`);
  }

  await audioContext.resume();
  log('AudioContext resumed.');

  return { audioContext, node };
}

export function startRecording() {
  if (!mediaRecorder) {
    throw new Error('Recording not available (MediaRecorder not initialized).');
  }
  if (mediaRecorder.state === 'recording') {
    return;
  }
  recordedChunks = [];
  mediaRecorder.start();
}

export async function stopRecording() {
  if (!mediaRecorder) {
    throw new Error('Recording not available (MediaRecorder not initialized).');
  }
  if (mediaRecorder.state === 'inactive') {
    return null;
  }

  return new Promise((resolve, reject) => {
    const handleStop = () => {
      mediaRecorder.removeEventListener('stop', handleStop);
      try {
        const blob = new Blob(recordedChunks, {
          type: mediaRecorder.mimeType || 'audio/webm',
        });
        resolve(blob);
      } catch (err) {
        reject(err);
      }
    };

    mediaRecorder.addEventListener('stop', handleStop);
    mediaRecorder.stop();
  });
}

export function triggerString(node, freq, velocity = 1.0) {
  if (!node) return;
  node.port.postMessage({ type: 'note_on', freq, velocity });
}

export function setEngineDecay(node, decay) {
  if (!node) return;
  node.port.postMessage({ type: 'set_decay', decay });
}

export function setEngineBrightness(node, brightness) {
  if (!node) return;
  node.port.postMessage({ type: 'set_brightness', brightness });
}

export function setEngineDispersion(node, dispersion) {
  if (!node) return;
  node.port.postMessage({ type: 'set_dispersion', dispersion });
}

export function setEngineAttackDecay(node, attackDecay) {
  if (!node) return;
  node.port.postMessage({ type: 'set_attack_decay', attackDecay });
}

export function setEngineReverbMix(node, mix) {
  if (!node) return;
  node.port.postMessage({ type: 'set_reverb_mix', mix });
}

export function setEngineGuitarType(node, guitarType) {
  if (!node) return;
  node.port.postMessage({ type: 'set_guitar_type', guitarType });
}


