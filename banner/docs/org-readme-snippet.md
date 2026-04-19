# Org profile README snippet

Paste into `GuitarAlchemist/.github/profile/README.md` (create the
`.github` repo if it doesn't exist yet — GitHub uses that file as the
org-level profile).

```markdown
<p align="center">
  <a href="https://guitaralchemist.github.io/ga/">
    <img src="https://guitaralchemist.github.io/ga/banner.png" alt="Guitar Alchemist — WebGPU ocean with a spinning warship" />
  </a>
</p>

# Guitar Alchemist

Music-theory toolkit, fretboard explorer, and music-ML playground.
F# DSL, C#/.NET services, React + WebGPU front-end.

- **Live demos** — [demos.guitaralchemist.com](https://demos.guitaralchemist.com)
- **Ocean banner** — [guitaralchemist.github.io/ga](https://guitaralchemist.github.io/ga/) (click ↑ the banner above; WebGPU required)
- **Source** — [github.com/GuitarAlchemist/ga](https://github.com/GuitarAlchemist/ga)
```

## Why an external PNG rather than a committed one

The banner gets regenerated on every `banner/**` push via
`.github/workflows/deploy-banner.yml` — Playwright grabs a fresh PNG
from the deployed page, so the thumbnail never drifts from the live
scene. A committed PNG in the `.github` repo would go stale the
moment the ship, presets, or lighting change.

If GitHub's image proxy caches too aggressively, append a versioned
query (`banner.png?v=2`) to bust it.
