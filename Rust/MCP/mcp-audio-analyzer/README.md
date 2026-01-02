# MCP Audio Analyzer (GA / Augment Code)

Serveur MCP qui analyse des fichiers audio (WAV, FLAC, MP3…) :
- stats de loudness (RMS / peak en dB),
- stats spectrales (centroid, bandwidth),
- détection de transitoires (attaques),
- embedding CLAP (ONNX) optionnel.

## 1. Python

```bash
cd python
python -m venv .venv
# Windows :
#   .venv\Scripts\activate
pip install -r requirements.txt
python download_model.py   # télécharge le modèle CLAP ONNX (si Internet)
```

## 2. Node / MCP

```bash
cd node
npm install
npm run build
```

## 3. Intégration MCP (Augment Code)

Dans Augment, configure un serveur MCP en pointant vers `mcp.json`
à la racine de ce projet.
```
