import React from 'react';
import { useAtom } from 'jotai';
import {
  audioContextAtom,
  workletNodeAtom,
  isAudioReadyAtom,
  decayAtom,
  logAtom,
  stringsAtom,
} from './atoms/audioAtoms';
import {
  initAudioEngine,
  triggerString,
  setEngineDecay,
  startRecording,
  stopRecording,
  setEngineGuitarType,
} from './audio/audioEngine';

function App() {
  const [, setAudioContext] = useAtom(audioContextAtom);
  const [node, setNode] = useAtom(workletNodeAtom);
  const [isReady, setIsReady] = useAtom(isAudioReadyAtom);
  const [decay, setDecay] = useAtom(decayAtom);
  const [isRecording, setIsRecording] = React.useState(false);
  const [isTwelveString, setIsTwelveString] = React.useState(false);
  const [guitarType, setGuitarType] = React.useState(0);



  const [logs, setLogs] = useAtom(logAtom);
  const [strings] = useAtom(stringsAtom);

  const handleInit = async () => {
    if (isReady) return;
    try {
      const { audioContext, node } = await initAudioEngine({
        onLog: (msg) => setLogs((prev) => [...prev, msg]),
      });
      setAudioContext(audioContext);
      setNode(node);
      setIsReady(true);
    } catch (err) {
      setLogs((prev) => [...prev, `Error: ${String(err)}`]);
    }
  };

  const handleDecayChange = (event) => {
    const value = Number(event.target.value);
    setDecay(value);
    if (node) {
      setEngineDecay(node, value);
    }
  };

  const handleGuitarTypeChange = (type) => {
    setGuitarType(type);
    if (node) {
      setEngineGuitarType(node, type);
    }
  };

  const handleStrumCmaj7 = () => {
    if (!node) return;
    const notes = [
      { id: 'C3', freq: 130.81, twelveFreq: 261.63 },
      { id: 'E3', freq: 164.81, twelveFreq: 329.63 },
      { id: 'G3', freq: 196.0, twelveFreq: 392.0 },
      { id: 'B3', freq: 246.94, twelveFreq: 493.88 },
      { id: 'E4', freq: 329.63, twelveFreq: 659.26 },
    ];
    handleStrumChord(notes);
  };

  const handleStrumGmaj7 = () => {
    if (!node) return;
    const notes = [
      { id: 'G2', freq: 98.0, twelveFreq: 196.0 },
      { id: 'B2', freq: 123.47, twelveFreq: 246.94 },
      { id: 'D3', freq: 146.83, twelveFreq: 293.66 },
      { id: 'F#3', freq: 185.0, twelveFreq: 370.0 },
      { id: 'G3', freq: 196.0, twelveFreq: 392.0 },
    ];
    handleStrumChord(notes);
  };

  const handleStrumChord = (notes) => {
    const baseDelay = 10;
    notes.forEach((note, index) => {
      const microDelay = Math.min(index * 5, 10);
      setTimeout(() => {
        triggerWithExtras(note);
      }, microDelay);
    });
  };

  const triggerWithExtras = (note) => {
    triggerString(node, note.freq, 1.0);
    if (isTwelveString && note.twelveFreq) {
      triggerString(node, note.twelveFreq, 1.0);
    }
  };

  const handleStartRecording = () => {
    if (!node) return;
    try {
      startRecording();
      setIsRecording(true);
      setLogs((prev) => [...prev, 'Recording started']);
    } catch (err) {
      setLogs((prev) => [...prev, `Recording error: ${String(err)}`]);
    }
  };

  const handleStopRecording = async () => {
    try {
      const blob = await stopRecording();
      setIsRecording(false);
      if (blob) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'guitar-mix.webm';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        setLogs((prev) => [...prev, 'Recording saved (guitar-mix.webm).']);
      }
    } catch (err) {
      setLogs((prev) => [...prev, `Recording error: ${String(err)}`]);
    }
  };


  const handlePluck = (note) => {
    if (!node) return;
    triggerString(node, note.freq, 1.0);
    if (isTwelveString && note.twelveFreq) {
      triggerString(node, note.twelveFreq, 1.0);
    }
  };

  return (
    <div className="app">
      <div className="panel">
        <h1>Guitar Web WASM Demo</h1>
        <button onClick={handleInit} disabled={isReady}>
          {isReady ? 'Audio ready' : "Initialiser l'audio"}
        </button>

        <div className="strings">
          {strings.map((s) => (
            <button
              key={s.id}
              onClick={() => handlePluck(s)}
              disabled={!isReady}
            >
              {s.id}
            </button>
          ))}
        </div>
        <div className="control">
          <label>
            Decay
            <input
              type="range"
              min="0.990"
              max="0.9999"
              step="0.0001"
              value={decay}
              onChange={handleDecayChange}
              disabled={!isReady}
            />
            <span style={{ marginLeft: '0.5rem' }}>{decay.toFixed(4)}</span>
          </label>
        </div>

        <div className="control">
          <span>Guitar type:</span>
          <button
            data-guitar-type="0"
            onClick={() => handleGuitarTypeChange(0)}
            disabled={!isReady}
            style={{ marginLeft: '0.5rem' }}
          >
            Steel bright
          </button>
          <button
            data-guitar-type="1"
            onClick={() => handleGuitarTypeChange(1)}
            disabled={!isReady}
            style={{ marginLeft: '0.5rem' }}
          >
            Steel warm
          </button>
          <button
            data-guitar-type="2"
            onClick={() => handleGuitarTypeChange(2)}
            disabled={!isReady}
            style={{ marginLeft: '0.5rem' }}
          >
            Nylon
          </button>
          <button
            data-guitar-type="3"
            onClick={() => handleGuitarTypeChange(3)}
            disabled={!isReady}
            style={{ marginLeft: '0.5rem' }}
          >
            Jumbo steel
          </button>
        </div>


        <div className="control">
          <button
            onClick={handleStartRecording}
            disabled={!isReady || isRecording}
          >
            Start recording
          </button>
          <button
            onClick={handleStopRecording}
            disabled={!isReady || !isRecording}
            style={{ marginLeft: '0.5rem' }}
          >
            Stop & download
          </button>
        </div>

        <div className="control">
          <button onClick={handleStrumCmaj7} disabled={!isReady}>
            Strum Cmaj7
          </button>
          <button
            onClick={handleStrumGmaj7}
            disabled={!isReady}
            style={{ marginLeft: '0.5rem' }}
          >
            Strum Gmaj7
          </button>
        </div>

        <div className="control">
          <button
            onClick={() => setIsTwelveString((prev) => !prev)}
            disabled={!isReady}
          >
            {isTwelveString ? 'Switch to 6-string mode' : 'Switch to 12-string mode'}
          </button>
        </div>

        <div className="log">
          <h2>Status</h2>
          <ul>
            {logs.slice(-5).map((line, idx) => (
              <li key={idx}>{line}</li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

export default App;