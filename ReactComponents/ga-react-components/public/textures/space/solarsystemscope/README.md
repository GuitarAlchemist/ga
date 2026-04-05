# Solar System Scope Texture Namespace

This directory is reserved for the canonical Solar System Scope texture pack once the existing `/textures/planets` assets are fully normalized into the manifest-driven layout.

Current state:

- Runtime currently resolves canonical planet textures via the TypeScript manifest in `src/assets/space/solarSystemManifest.ts`.
- Existing files still live under `/public/textures/planets/`.

Migration goal:

- Move or regenerate curated canonical textures into this namespace without breaking runtime imports.

