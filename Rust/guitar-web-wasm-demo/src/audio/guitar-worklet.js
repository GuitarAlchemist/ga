class GuitarProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this.ready = false;
    this.frames = 128;
    this.enginePtr = 0;
    this.bufferPtr = 0;
    this.buffer = null;
    this.exports = null;

    this.port.onmessage = async (event) => {
      const msg = event.data;
      if (msg.type === 'init') {
        try {
          const { wasmModule, sampleRate: sr } = msg;
          const result = await WebAssembly.instantiate(wasmModule, {});
          this.exports = result.instance.exports;
          const srToUse = typeof sr === 'number' ? sr : sampleRate;
          this.enginePtr = this.exports.engine_init(srToUse);
          // Engine renders mono; we duplicate to L/R in process()
          this.bufferPtr = this.exports.alloc_buffer(this.frames);
          this.buffer = new Float32Array(
            this.exports.memory.buffer,
            this.bufferPtr,
            this.frames,
          );
          this.ready = true;
          this.port.postMessage({
            type: 'status',
            message: 'WASM engine initialized.',
          });
        } catch (err) {
          this.port.postMessage({ type: 'error', message: String(err) });
        }
      } else if (msg.type === 'note_on' && this.ready) {
        const velocity = typeof msg.velocity === 'number' ? msg.velocity : 1.0;
        this.exports.engine_note_on(this.enginePtr, msg.freq, velocity);
      } else if (msg.type === 'set_decay' && this.ready) {
        this.exports.engine_set_decay(this.enginePtr, msg.decay);
      } else if (msg.type === 'set_brightness' && this.ready) {
        this.exports.engine_set_brightness(this.enginePtr, msg.brightness);
      } else if (msg.type === 'set_dispersion' && this.ready) {
        this.exports.engine_set_dispersion(this.enginePtr, msg.dispersion);
      } else if (msg.type === 'set_attack_decay' && this.ready) {
        this.exports.engine_set_attack_decay(this.enginePtr, msg.attackDecay);
      } else if (msg.type === 'set_reverb_mix' && this.ready) {
        this.exports.engine_set_reverb_mix(this.enginePtr, msg.mix);
      } else if (msg.type === 'set_guitar_type' && this.ready) {
        const t = (msg.guitarType | 0);
        if (this.exports.engine_set_guitar_type) {
          this.exports.engine_set_guitar_type(this.enginePtr, t);
        }
      }
    };
  }

  process(inputs, outputs) {
    if (!this.ready || !this.exports) {
      return true;
    }

    const output = outputs[0];
    if (!output || output.length === 0) return true;
    const left = output[0];
    const right = output[1] || output[0];
    const frames = left.length;

    this.exports.engine_render(this.enginePtr, this.bufferPtr, frames);

    for (let i = 0; i < frames; i += 1) {
      const v = this.buffer[i];
      left[i] = v;
      right[i] = v;
    }

    return true;
  }
}

registerProcessor('guitar-processor', GuitarProcessor);

