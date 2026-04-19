import React from 'react';
import ReactDOM from 'react-dom/client';
import { Ocean } from '@ocean/Ocean';

// Early WebGPU probe: the Ocean component would crash during init without
// navigator.gpu. Show a friendly fallback instead of a blank page.
const hasWebGPU = typeof navigator !== 'undefined' && 'gpu' in navigator;

if (!hasWebGPU) {
  document.getElementById('webgpu-unavailable')?.classList.add('shown');
} else {
  const root = ReactDOM.createRoot(document.getElementById('root')!);
  root.render(
    <React.StrictMode>
      <Ocean />
    </React.StrictMode>,
  );
}
