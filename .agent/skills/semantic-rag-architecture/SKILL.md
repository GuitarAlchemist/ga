---
name: "Semantic Search & RAG Architecture"
description: "Standards for AI embedding generation, vector indexing strategies, and Retrieval-Augmented Generation (RAG) pipelines within Guitar Alchemist."
---

# Semantic Search & RAG Architecture

This skill governs the development of AI search capabilities, specifically within `GA.Business.ML` and the `Common/GA.Domain.Services/Fretboard/Voicings` search namespaces. It ensures that our vector search infrastructure remains consistent, deterministic, and high-performance.

## 1. Embedding Fundamentals

### 1.1 Model Standard
- **Provider**: ONNX Runtime.
- **Model**: `all-MiniLM-L6-v2` (Quantized).
- **Dimensions**: **384** (Fixed).
- **Constraint**: *Never* change the embedding model or dimension count without a coordinated full-system re-index. Doing so will break all existing vector search features.

### 1.2 Schema Governance
- Use the **OPTIC-K Schema Guardian** skill for specific weighting and dimension mapping rules. This skill focuses on the *architecture* around those embeddings.

## 2. Indexing Strategy

### 2.1 Deterministic Identity
Document IDs in the vector store must be **deterministic** and reproducible from the domain entity itself. Never use random UUIDs for domain entities.

- **Format**: `entity_type_discriminator`
- **Example (Voicing)**: `voicing_standard_0_x_3_2_0_1_0` (tuning + capo + fret diagram)

### 2.2 Document Payloads (Metadata)
- **Searchable Text**: Must be a natural language representation of the entity (e.g., "C Major 7 open chord high comfort").
- **Facets**: Store filtering fields (Difficulty, HandStretch, OmittedTones) as raw types (int, bool, string[]) in the payload to enable hybrid search (Vector + Filter).

## 3. RAG Pipeline & Orchestration

### 3.1 Retrieval Hierarchy
When assembling context for LLMs (e.g., Chatbot):
1.  **Direct Match**: Exact entity lookups (by name or text).
2.  **Semantic Match**: Vector neighbors (Top-K).
3.  **Theoretical Context**: Related keys/scales derived from the retrieved entities.

### 3.2 Context Window Management
- **Token Budget**: 4096 tokens (typical local model).
- **Priority**: System Prompt > User Query > Retrieved Voicings > Theoretical Rules.
- **Truncation**: Truncate the *least relevant* distinct search results first, preserving the highest-scored matches.

## 4. Testing & Validation

### 4.1 Zero-Shot Retrieval Check
Before modifying `MusicalEmbeddingGenerator` or `VoicingDocumentFactory`:
1.  Run a specific query: "Jimi Hendrix chord".
2.  verify that the "Dominant 7th Sharp 9th" (E7#9) appears in the Top 3 results.
3.  If this fails, the semantic mapping has drifted.

### 4.2 Integration Tests
- Ensure `GA.Data.SemanticKernel.Embeddings` tests pass.
- Verify memory mapping allows for large index loading without OOM exceptions.

## 5. How to Use This Skill
1.  **Modifying Search**: Consult this before touching `GpuVoicingSearchStrategy`.
2.  **New Embeddings**: Follow Section 2.1 for ID generation.
3.  **Debugging**: If search results feel "random", check Section 1.1 (Dimension mismatch) or 2.2 (Missing searchable text).
