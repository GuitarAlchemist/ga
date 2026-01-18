# GA.Business.ML

This project contains AI and Machine Learning services for Guitar Alchemist, following the modular architecture Layer 4 (AI/ML).

## Overview

This library provides:
- **Embedding Services**: Multiple implementations for generating text and musical embeddings.
- **Spectral RAG**: Harmonic-aware retrieval augmented generation using Wavelet Transforms.
- **Tab Analysis**: Advanced Viterbi-based tab solving and naturalness evaluation.
- **GPU Acceleration**: ILGPU-powered high-performance computing for musical analysis.

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 4 (AI/ML)**: `GA.Business.ML` (this project)
- **Dependencies**: Layers 1-3 (Core, Domain, Analysis)
- **Consumers**: Layer 5 (Orchestration) and applications

## Services

### Embedding Services

Located in `Embeddings/`:

- **MusicalEmbeddingGenerator**: OPTIC-K based harmonic embeddings.
- **OnnxEmbeddingService**: Local ONNX model-based text embeddings.
- **OllamaEmbeddingService**: Ollama API integration.
- **OpenAiEmbeddingService**: OpenAI API integration.

### Spectral RAG

Located in `Retrieval/`:

- **WaveletTransformService**: DWT for harmonic signal analysis.
- **ProgressionSignalService**: Converts chord sequences to spectral signals.
- **SpectralRetrievalService**: Contextual retrieval based on harmonic proximity.

### Tab & Musical Intelligence

Located in `Tabs/` and `Musical/`:

- **AdvancedTabSolver**: Optimal fingering using physical cost heuristics.
- **NaturalnessClassifier**: ML-based evaluation of tab quality.
- **ProductionOrchestrator**: Unified entry point for the Guitar Alchemist Chatbot.

## Usage

### Basic Setup

```csharp
using GA.Business.ML.Extensions;

// Add all AI/ML services
services.AddGuitarAlchemistAI();
```

### Using Embedding Services

```csharp
// Inject the service
public class MyService
{
    private readonly IEmbeddingService _embeddingService;
    
    public MyService(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }
    
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        return await _embeddingService.GenerateEmbeddingAsync(text);
    }
}
```

## Migration Notes

This project consolidates AI services that were previously scattered across:
- `GA.Data.MongoDB/Services/Embeddings/`
- `GA.Data.SemanticKernel.Embeddings/`
- `GA.Business.Intelligence/SemanticIndexing/`

All services have been moved here to follow proper architectural layering.
