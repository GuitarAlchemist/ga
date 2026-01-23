# GA.Data.MongoDB

This project provides the MongoDB data access layer for Guitar Alchemist, following the modular architecture Layer 2 (Data/Infrastructure).

## Overview

This library provides:
- **Persistence**: Storage and retrieval of musical concepts, tabulature, and user data.
- **Indexing**: Optimized MongoDB indexes for musical search and vector-based retrieval.
- **Repository Pattern**: Generic and specialized repositories for domain entities.
- **GridFS Support**: Storage for large musical assets like PDFs or audio.

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 2 (Data)**: `GA.Data.MongoDB` (this project)
- **Dependencies**: `GA.Core`, `GA.Business.Core`
- **Consumers**: Higher-level services and applications.

## Services/Features

### Data Services
Located in `Services/`:
- **MongoRepository**: Base implementation for entity persistence.
- **MusicalConceptRepository**: Specialized storage for scales, modes, and chords.

### Models
Located in `Models/`:
- **MongoEntity**: Base class for all persisted documents.
- **MusicalDocument**: Schema-aligned documents for musical concepts.

## Usage

### Setting up MongoDB Services

```csharp
services.AddGuitarAlchemistMongo(configuration.GetSection("MongoDB"));
```

### Using a Repository

```csharp
public class MyService(IMongoRepository<Scale> scaleRepository)
{
    public async Task<Scale> GetScaleAsync(string id) => await scaleRepository.GetByIdAsync(id);
}
```

## Authors

Stephane Pareilleux
