# Project Documentation Standard

This document defines the standard for documenting projects within the Guitar Alchemist solution, based on the `GA.Business.ML` template.

## 1. README.md

Each project must have a `README.md` at its root following this structure:

- **# Project Name**: Clear title.
- **Project Description**: Short paragraph explaining what the project does and its place in the architecture (Layer).
- **## Overview**: Bullet points of key library features.
- **## Architecture**: Diagram or list of dependencies and consumers.
- **## Services/Features**: Description of main namespaces and their contents.
- **## Usage**: Code snippets for basic setup and common use cases.
- **## Migration Notes**: (Optional) Information about moves or consolidations.

## 2. Documentation Folder

Each project must have a `Documentation/` folder at its root with the following subfolders (as applicable):

- **Architecture/**: Detailed design documents, roadmaps, and technical plans.
- **Schema/**: Definitions for data models, embeddings, or domain-specific schemas.
- **Research/**: Background information, spikes, and mathematical foundations.
- **Walkthroughs/**: Step-by-step guides for specific phases or complex features.
- **Papers/**: Relevant academic papers or external research (usually PDFs).
- **Guides/**: General how-to guides for developers.

## 3. Style Guidelines

- Use Markdown for all text documentation.
- Include code snippets for all technical explanations.
- Reference other documentation files using relative paths.
- Keep documentation close to the code it describes.
- Ensure all diagrams are either Mermaid-based (embedded in Markdown) or linked images.
