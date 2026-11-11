# GeoTiles: OGC 3D Tiles in a BRUTAL window

A demo on top of the [BRUTAL evaluation](../README.md): it loads the 3D tiles around a place and draws them with
Vulkan through BRUTAL's bindings, in the renderer of `HallSlice` (M2). The tiles are either Google's
[Photorealistic 3D Tiles](https://developers.google.com/maps/documentation/tile/3d-tiles) (Map Tiles API), or a
synthetic tileset when there is no API key.

![The synthetic tileset in the BRUTAL window](../evidence/geotiles-synthetic-20261111.png)

## Run

```powershell
# No key, no network: a made-up square kilometre, through the same path as Google's tiles.
dotnet run --project Experiments/Brutal/GeoTiles/GeoTiles -- --synthetic

# Google's tiles around a place (default: Parliament Hill, Ottawa). The key stays in the environment.
$env:GOOGLE_MAPS_API_KEY = '<your key>'
dotnet run --project Experiments/Brutal/GeoTiles/GeoTiles -- --lat 45.4236 --lon -75.7009 --height 40 --radius 400

dotnet test Experiments/Brutal/GeoTiles/GeoTiles.Tests
```

| Option | Default | Meaning |
|---|---|---|
| `--lat`, `--lon` | 45.4236, -75.7009 | the place looked at, in degrees |
| `--height` | 40 | its height above the WGS84 ellipsoid, in metres (not above sea level) |
| `--radius` | 400 | only tiles within this many metres of the place are fetched |
| `--sse` | 16 | tolerated screen-space error, in pixels; lower fetches finer tiles |
| `--max-tiles` | 120 | a cap on the meshes fetched, so a mistake costs a bounded number of requests |
| `--seconds` | 20 | the window closes by itself after this long |
| `--synthetic` | | use the synthetic tileset even when a key is set |
| `--no-window` | | load, print the counts and exit |

In the window: the camera orbits; the arrow keys turn and tilt it, W and S move it in and out, Esc closes.

## How it works

| Step | Where | What |
|---|---|---|
| Fetch | `TileSource.cs` | `root.json` with the key; every later request carries the key and the `session` that the tilesets name in their URIs. The key is sent to `tile.googleapis.com` over HTTPS only, and never appears in an error message. |
| Tree | `Tileset.cs` | `tileset.json`: bounding volumes (box, sphere, region) reduced to spheres, geometric error, `REPLACE` or `ADD`, transforms, nested tilesets. |
| Select | `Selection.cs` | The screen-space error of each tile from the eye decides whether its children replace it. Tiles away from the place are skipped. |
| Decode | `Glb.cs` | GLB, the node hierarchy, `KHR_draco_mesh_compression` through `Openize.Drako`, the base colour image as bytes. glTF's y-up is turned to the tileset's z-up. |
| Locate | `Geo.cs`, `Scene.cs` | Positions stay in ECEF **doubles** until they are expressed in an east-north-up frame at the place; only then do they become floats. In ECEF a float steps by half a metre; in the local frame it keeps millimetres (a test holds both). |
| Draw | `GeoTiles/Program.cs` | `HallSlice`'s renderer: one vertex buffer, flat normals, a colour per vertex. |

`GeoTiles.Core` has no BRUTAL reference, so all of this but the last row is tested without a GPU.

## What is verified, and what is not

- **Verified, 2026-11-11, Windows:** 18 tests, among them the whole path on the synthetic tileset (nested tileset,
  Draco, PNG texture, ECEF to local). Four mutants of the core each fail a test: no y-up to z-up, no ECEF to local,
  always refine, session dropped. The window drew the synthetic tileset at 60 fps with vsync (4,848 triangles).
- **Not verified: Google's tiles.** No API key was available, so no request was ever sent to Google. The request
  shape, the session handling and the decoding follow the public documentation and the 3D Tiles and glTF
  specifications; the first real run may meet something they do not say (a texture format, an extension, a quota
  answer).

## Limits of this slice

- **One selection, at start.** The tiles are chosen once for the first camera position; moving the camera does not
  fetch finer tiles. There is no frustum culling and no streaming.
- **Colours, not textures.** Each vertex takes the colour of the tile's image at its UV. Google's meshes are dense
  enough for that to read as a picture from afar; up close it is blurred. JPEG and PNG decode; KTX2 and WebP do not,
  and such a tile is drawn grey.
- **Attribution is in the title bar and on the console**, not in the picture, and Google's logo is not shown.

## Google's terms

These tiles are not open data. This demo is built for them as follows, and anything beyond private prototyping
needs more:

- **No tile is written to disk**, cached between runs or committed: they live in memory for one run. Do not add a
  disk cache, and do not commit captures of Google's imagery to this repository. The capture above is of the
  synthetic tileset.
- **Attribution must be visible with the imagery.** The tiles name their data providers (`asset.copyright`); the
  demo collects them and shows them with "Google" in the title bar. Showing them inside the viewport with Google's
  logo is required before any picture or build of this leaves the machine, and is not done yet.
- Requests are billed past the free quota: a run makes one root request and a few dozen to a few hundred tile
  requests, bounded by `--max-tiles`.

For data that can be stored and redistributed, use open sources instead: SRTM or municipal LiDAR elevation with
OpenStreetMap buildings. Turned into a 3D Tiles tileset, they load through this same path.
