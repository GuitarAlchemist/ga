# Solar System Scope Attribution

These textures are sourced from Solar System Scope:

- Source: https://www.solarsystemscope.com/textures/
- License: Creative Commons Attribution 4.0 International (CC BY 4.0)
- Retrieved for GA asset-pipeline integration work on 2026-04-05
- 8K texture upgrade downloaded 2026-04-05

Use, adaptation, and commercial distribution are permitted under CC BY 4.0 with attribution.

## Texture Inventory

| Body | 2K | 8K | Maps |
|------|:--:|:--:|------|
| Sun | yes | yes | albedo |
| Mercury | yes | yes | albedo, displacement |
| Venus | yes | yes | albedo, displacement, atmosphere |
| Earth | yes | yes | albedo, night, clouds, specular, displacement |
| Moon | yes | yes | albedo, displacement |
| Mars | yes | yes | albedo, displacement |
| Jupiter | yes | yes | albedo |
| Saturn | yes | yes | albedo, ring alpha |
| Uranus | yes | -- | albedo (no 8K available) |
| Neptune | yes | -- | albedo (no 8K available) |
| Stars | yes | yes | star dome |

Total: ~89MB textures (8.5MB 2K + 77MB 8K + 3.5MB milky way)

Runtime note:

- `loadCanonicalTexture()` prefers 8K, falls back to 2K automatically via `resolveTexturePath`
- Low-end devices still get 2K through the LOD system in ForceRadiant

