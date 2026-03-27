# Live Weather Cloud Rendering System

**Date:** 2026-03-27
**Status:** Approved
**Scope:** Shared cloud texture service + 4 consumers (Earth sphere, Prime Radiant bg, widget, Demerzel atmosphere)

## Problem

The Prime Radiant visualization, Solar System, and Demerzel avatar lack connection to the real world. Adding live satellite cloud data creates an ambient, grounding layer that ties the digital governance system to Earth.

## Solution

A frontend-only cloud texture service that fetches global cloud cover tiles from OpenWeatherMap, stitches them into a single equirectangular texture, and shares it across multiple visual consumers via React context.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Data source | OpenWeatherMap `clouds_new` tiles | Global cloud coverage (not just radar), free tier 1M calls/month |
| Fallback | RainViewer (no key) + static texture | RainViewer for radar overlay; static PNG if all APIs fail |
| Architecture | Frontend-only, React context | No backend proxy needed for a local dev tool; avoids CORS via OWM's open tile endpoint |
| Texture format | 2048x1024 equirectangular canvas | Standard format for sphere projection; 64 tiles at zoom 3 |
| Refresh rate | Every 15 minutes | Balances freshness with API quota (185K calls/month of 1M limit) |
| Build order | Provider → Earth → Prime Radiant → Widget → Demerzel | Each consumer is independent; Earth is visual proof, ordered by impact |

## Architecture

```
OpenWeatherMap API (clouds_new tiles, zoom 3)
        | fetch 64 tiles (8x8 grid, 256px each)
        v
Offscreen Canvas (2048x1024 equirectangular)
        | alpha-process: clear sky = transparent, clouds = white
        v
CloudTextureProvider (React context)
        | THREE.CanvasTexture, shared instance
        v
  +--------+-----------+-------------+
  |        |           |             |
Earth    Prime      Weather     Demerzel
Sphere   Radiant    Widget      Atmosphere
clouds   background panel       ambient fx
```

## Components

### 1. CloudTextureProvider

**Location:** `src/components/PrimeRadiant/CloudTextureProvider.tsx`

React context provider that manages the cloud texture lifecycle.

**Exports:**
- `CloudTextureProvider` — wrap around consumers
- `useCloudTexture()` — hook returning `{ texture: THREE.CanvasTexture | null, canvas: HTMLCanvasElement | null, lastUpdated: Date | null, loading: boolean, error: string | null }`

**Behavior:**
- On mount: fetch 64 tiles from OWM `clouds_new` at zoom level 3
- Stitch tiles onto offscreen 2048x1024 canvas (8 cols x 8 rows, each tile 256x256)
- Process alpha: OWM cloud tiles have colored backgrounds — convert to white clouds on transparent background for overlay use
- Create `THREE.CanvasTexture` from canvas
- Set up 15-minute refresh interval
- On refresh: re-fetch tiles, update canvas, set `texture.needsUpdate = true`
- On error: log warning, keep last good texture, retry on next interval
- Expose raw `canvas` for 2D consumers (weather widget)

**API key:** Read from `import.meta.env.VITE_OWM_API_KEY`. If missing, skip fetching and use static fallback texture.

**Tile URL:**
```
https://tile.openweathermap.org/map/clouds_new/3/{x}/{y}.png?appid={key}
```

At zoom 3: x = 0-7, y = 0-7 (64 tiles total).

### 2. Earth Sphere Clouds (Solar System)

**Location:** Modify `src/components/PrimeRadiant/SolarSystem.ts`

Add a transparent cloud shell mesh as a child of the Earth sphere.

**Implementation:**
- New `SphereGeometry` at radius `earthRadius * 1.004` (0.4% above surface)
- `MeshPhongMaterial` with `map: cloudTexture`, `transparent: true`, `opacity: 0.6`
- Slow independent rotation: `cloudMesh.rotation.y += 0.0001` per frame (simulates atmospheric drift)
- Only created when `cloudTexture` is available (graceful degradation)

### 3. Prime Radiant Cloud Background

**Location:** Modify `src/components/PrimeRadiant/ForceRadiant.tsx`

Add a large background sphere behind the governance graph with the cloud texture.

**Implementation:**
- Large `SphereGeometry` (radius = camera far plane * 0.9) with `side: THREE.BackSide`
- `MeshBasicMaterial` with `map: cloudTexture`, `transparent: true`, `opacity: 0.08`
- Very subtle — just enough to see cloud patterns drifting behind nodes
- Slow rotation for ambient motion
- Disable depth write so it never occludes graph elements

### 4. Weather Widget Panel

**Location:** New `src/components/PrimeRadiant/WeatherPanel.tsx`

New icon rail panel showing a flat cloud map with basic info.

**Implementation:**
- Add `'weather'` to `PanelId` type in `IconRail.tsx`
- Render the cloud canvas as a 2D image (using `canvas.toDataURL()` or direct canvas element)
- Overlay on a blue marble Earth map (equirectangular projection)
- Show `lastUpdated` timestamp
- Show basic cloud coverage percentage (computed from canvas pixel analysis)
- Cloud icon in the icon rail (sun + cloud SVG)

### 5. Demerzel Atmosphere (Phase 2)

**Location:** Modify `src/components/PrimeRadiant/DemerzelFace.ts`

Sample the cloud texture at a configured location to derive ambient effects.

**Implementation (deferred to Phase 2):**
- Config: `VITE_WEATHER_LAT` / `VITE_WEATHER_LON` env vars (default: user's approximate location)
- Convert lat/lon to texture UV coordinates
- Sample pixel brightness at that point
- Map brightness to ambient parameters:
  - High brightness (heavy clouds) → darker ambient light, potential particle effects
  - Low brightness (clear sky) → warm golden glow
- Smooth transitions between states (lerp over 30s)

## Data Flow

```
1. App mounts → CloudTextureProvider initializes
2. Provider fetches 64 tiles concurrently (Promise.all with concurrency limit of 8)
3. Tiles load as Images → drawImage() onto offscreen canvas
4. Alpha processing pass → clear sky becomes transparent
5. THREE.CanvasTexture created → consumers re-render
6. Every 15 min → repeat steps 2-5, set texture.needsUpdate = true
7. Consumers read texture from context, apply to their materials
```

## Environment Variables

```env
# Required for live clouds (free: https://openweathermap.org/api)
VITE_OWM_API_KEY=your_key_here

# Optional: location for Demerzel atmosphere (Phase 2)
VITE_WEATHER_LAT=40.7128
VITE_WEATHER_LON=-74.0060
```

## Error Handling

| Scenario | Response |
|----------|----------|
| No API key configured | Skip fetching, use static fallback texture, log info |
| API call fails | Keep last good texture, retry on next 15-min interval |
| Partial tile failure | Stitch available tiles, leave gaps transparent |
| All tiles fail | Fall back to static cloud texture bundled in assets |
| Canvas creation fails | Consumers get null texture, render without clouds |
| Rate limit hit | Back off to 30-min refresh, log warning |

## Static Fallback

Bundle a static equirectangular cloud texture (`clouds-fallback.png`, 2048x1024) in the assets directory. Used when:
- No API key is configured
- API is unreachable
- First render before tiles load

Source: NASA Visible Earth cloud composite (public domain).

## Governance Integration

Cloud state maps to algedonic signals:

| Event | Signal | Severity |
|-------|--------|----------|
| Cloud texture loaded successfully | pleasure/info | First successful fetch |
| API key missing | pain/warning | Degraded visual experience |
| All tile fetches failing for >1h | pain/warning | Possible API outage |

Belief state: `belief-cloud-data-available` (T/F/U) — tracks whether live cloud data is flowing.

## Testing

1. **Unit:** Tile URL generation for zoom 3 grid (x=0-7, y=0-7)
2. **Unit:** Canvas stitching — correct tile placement at grid positions
3. **Unit:** Alpha processing — verify transparent background
4. **Integration:** Full fetch → stitch → texture creation cycle with mock API
5. **Visual:** Earth sphere with cloud overlay renders correctly
6. **Fallback:** Remove API key, verify static texture loads
7. **Refresh:** Mock timer, verify texture updates after 15 min

## Future Enhancements

- **RainViewer radar overlay** — precipitation layer on top of cloud cover
- **Matrix-Game-3.0** — AI-generated cloud flythrough from seed image + weather data
- **Historical playback** — animate cloud movement over past 24h
- **Weather alerts** — severe weather triggers algedonic emergency signals
- **Location-aware** — auto-detect user location for local weather in widget
