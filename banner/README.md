# Ocean banner

Standalone static build of the Ocean WebGPU demo. Deployed to GitHub Pages at
[https://guitaralchemist.github.io/ga/](https://guitaralchemist.github.io/ga/) as a visual banner
for the [GuitarAlchemist](https://github.com/GuitarAlchemist) org profile.

## What it is

- A tiny Vite + React app that mounts only the `Ocean` component from
  `ReactComponents/ga-react-components/src/components/Ocean/` via the
  `@ocean` path alias — no copy-paste, no drift.
- Assets (warship GLB, moon, Milky Way, displacement maps) live in
  `public/` so Vite copies them verbatim into `dist/`.
- Third-party asset credits in [`public/ATTRIBUTION.md`](public/ATTRIBUTION.md).

## Local dev

```bash
cd banner
npm install
npm run dev         # http://localhost:5177
```

## Production build

```bash
npm run build       # → dist/, base '/ga/'
npm run preview     # serves dist/ for a smoke test
```

Override the base path for other hosts:

```bash
VITE_BANNER_BASE=/ npm run build
```

## Deployment

Pushes to `main` that touch `banner/**` or the shared Ocean component
trigger `.github/workflows/deploy-banner.yml`, which builds and
publishes `dist/` to GitHub Pages.

**One-time setup in repo settings**: go to Settings → Pages, set "Source"
to "GitHub Actions". After the first successful deploy, the workflow
output includes the live URL.

## Size note

`public/models/warship.glb` is ~63 MB (the Sketchfab Imperial Warship
with CC-BY attribution). It's committed directly rather than via Git
LFS to keep setup minimal — if repo size becomes a problem, migrate
`*.glb` to LFS.
