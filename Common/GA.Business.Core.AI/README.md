# GA.Business.Core.AI

This project contains AI and Machine Learning services for Guitar Alchemist, following the modular architecture Layer 4 (AI/ML).

## Overview

This library provides:
- **Embedding Services**: Multiple implementations for generating text embeddings
- **Semantic Search**: Vector-based search capabilities
- **GPU Acceleration**: ILGPU-powered high-performance computing
- **Vector Operations**: Optimized vector similarity and search algorithms

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 4 (AI/ML)**: `GA.Business.Core.AI` (this project)
- **Dependencies**: Layers 1-3 (Core, Domain, Analysis)
- **Consumers**: Layer 5 (Orchestration) and applications

## Services

### Embedding Services

Located in `Services/Embeddings/`:

- **OnnxEmbeddingService**: Local ONNX model-based embeddings
- **OllamaEmbeddingService**: Ollama API integration
- **OpenAiEmbeddingService**: OpenAI API integration  
- **AzureOpenAiEmbeddingService**: Azure OpenAI integration
- **HuggingFaceEmbeddingService**: Hugging Face API integration
- **GPUAcceleratedEmbeddingService**: ILGPU-accelerated embeddings
- **BatchOllamaEmbeddingService**: Batch processing for Ollama

### Semantic Search Services

Located in `Services/SemanticSearch/`:

- **SemanticSearchService**: Core semantic search functionality
- **SemanticFretboardService**: Guitar fretboard-specific semantic operations

## Usage

### Basic Setup

```csharp
using GA.Business.Core.AI.Extensions;

// Add all AI services
services.AddGuitarAlchemistAI();

// Or add just embedding services with configuration
services.AddEmbeddingServices(options =>
{
    options.ServiceType = EmbeddingServiceType.OpenAi;
    options.ApiKey = "your-api-key";
    options.ModelName = "text-embedding-ada-002";
});
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
