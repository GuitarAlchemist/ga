// src/components/PrimeRadiant/NearbyStars.ts
// Curated foreground star layer using real nearby/bright star coordinates.

import * as THREE from 'three';

interface StarCatalogEntry {
  name: string;
  raHours: number;
  decDeg: number;
  distanceLy: number;
  magnitude: number;
  spectral: string;
}

export interface NearbyStarsHandle {
  group: THREE.Group;
  update(time: number, cameraPosition: THREE.Vector3): void;
  dispose(): void;
}

const STAR_CATALOG: StarCatalogEntry[] = [
  { name: 'Alpha Centauri', raHours: 14.660, decDeg: -60.833, distanceLy: 4.37, magnitude: -0.27, spectral: 'G2V' },
  { name: "Barnard's Star", raHours: 17.963, decDeg: 4.693, distanceLy: 5.96, magnitude: 9.54, spectral: 'M4V' },
  { name: 'Wolf 359', raHours: 10.941, decDeg: 7.015, distanceLy: 7.86, magnitude: 13.5, spectral: 'M6V' },
  { name: 'Lalande 21185', raHours: 11.055, decDeg: 35.970, distanceLy: 8.31, magnitude: 7.52, spectral: 'M2V' },
  { name: 'Sirius', raHours: 6.752, decDeg: -16.716, distanceLy: 8.60, magnitude: -1.46, spectral: 'A1V' },
  { name: 'Luyten 726-8', raHours: 1.653, decDeg: -17.949, distanceLy: 8.73, magnitude: 12.5, spectral: 'M5V' },
  { name: 'Ross 154', raHours: 18.826, decDeg: -23.837, distanceLy: 9.69, magnitude: 10.44, spectral: 'M3V' },
  { name: 'Ross 248', raHours: 23.697, decDeg: 44.176, distanceLy: 10.30, magnitude: 12.29, spectral: 'M5V' },
  { name: 'Epsilon Eridani', raHours: 3.549, decDeg: -9.458, distanceLy: 10.47, magnitude: 3.73, spectral: 'K2V' },
  { name: 'Procyon', raHours: 7.655, decDeg: 5.225, distanceLy: 11.46, magnitude: 0.34, spectral: 'F5IV' },
  { name: '61 Cygni', raHours: 21.115, decDeg: 38.749, distanceLy: 11.40, magnitude: 5.20, spectral: 'K5V' },
  { name: 'Groombridge 34', raHours: 0.307, decDeg: 44.023, distanceLy: 11.62, magnitude: 8.10, spectral: 'M1V' },
  { name: 'Tau Ceti', raHours: 1.734, decDeg: -15.938, distanceLy: 11.91, magnitude: 3.50, spectral: 'G8V' },
  { name: 'Epsilon Indi', raHours: 22.057, decDeg: -56.786, distanceLy: 11.87, magnitude: 4.69, spectral: 'K5V' },
  { name: 'Lacaille 9352', raHours: 23.096, decDeg: -35.854, distanceLy: 10.74, magnitude: 7.34, spectral: 'M0V' },
  { name: 'Altair', raHours: 19.846, decDeg: 8.868, distanceLy: 16.73, magnitude: 0.77, spectral: 'A7V' },
  { name: 'Fomalhaut', raHours: 22.961, decDeg: -29.622, distanceLy: 25.13, magnitude: 1.16, spectral: 'A3V' },
  { name: 'Vega', raHours: 18.616, decDeg: 38.784, distanceLy: 25.04, magnitude: 0.03, spectral: 'A0V' },
  { name: 'Capella', raHours: 5.279, decDeg: 45.998, distanceLy: 42.92, magnitude: 0.08, spectral: 'G8III' },
  { name: 'Arcturus', raHours: 14.261, decDeg: 19.182, distanceLy: 36.66, magnitude: -0.05, spectral: 'K1III' },
  { name: 'Aldebaran', raHours: 4.599, decDeg: 16.509, distanceLy: 65.23, magnitude: 0.85, spectral: 'K5III' },
  { name: 'Pollux', raHours: 7.755, decDeg: 28.026, distanceLy: 33.78, magnitude: 1.14, spectral: 'K0III' },
  { name: 'Regulus', raHours: 10.139, decDeg: 11.967, distanceLy: 79.30, magnitude: 1.35, spectral: 'B8IV' },
  { name: 'Spica', raHours: 13.420, decDeg: -11.161, distanceLy: 250.0, magnitude: 1.04, spectral: 'B1III' },
  { name: 'Antares', raHours: 16.490, decDeg: -26.432, distanceLy: 550.0, magnitude: 1.06, spectral: 'M1I' },
  { name: 'Polaris', raHours: 2.530, decDeg: 89.264, distanceLy: 447.0, magnitude: 1.98, spectral: 'F7I' },
  { name: 'Betelgeuse', raHours: 5.919, decDeg: 7.407, distanceLy: 548.0, magnitude: 0.50, spectral: 'M2I' },
  { name: 'Rigel', raHours: 5.243, decDeg: -8.202, distanceLy: 860.0, magnitude: 0.13, spectral: 'B8I' },
  { name: 'Deneb', raHours: 20.691, decDeg: 45.280, distanceLy: 2600.0, magnitude: 1.25, spectral: 'A2I' },
];

const spriteTexture = (() => {
  let texture: THREE.CanvasTexture | null = null;
  return () => {
    if (texture) return texture;
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      const gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 30);
      gradient.addColorStop(0, 'rgba(255,255,255,1)');
      gradient.addColorStop(0.25, 'rgba(255,255,255,0.8)');
      gradient.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, 64, 64);
    }
    texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    return texture;
  };
})();

function colorForSpectralClass(spectral: string): THREE.Color {
  const cls = spectral.charAt(0).toUpperCase();
  if (cls === 'O' || cls === 'B') return new THREE.Color(0x9bbcff);
  if (cls === 'A') return new THREE.Color(0xdbe8ff);
  if (cls === 'F') return new THREE.Color(0xf7f4dc);
  if (cls === 'G') return new THREE.Color(0xfff2b0);
  if (cls === 'K') return new THREE.Color(0xffc879);
  if (cls === 'M') return new THREE.Color(0xff8a5c);
  return new THREE.Color(0xffffff);
}

function positionFromRaDec(raHours: number, decDeg: number, radius: number): THREE.Vector3 {
  const ra = (raHours / 24) * Math.PI * 2;
  const dec = THREE.MathUtils.degToRad(decDeg);
  const cosDec = Math.cos(dec);
  return new THREE.Vector3(
    Math.cos(ra) * cosDec * radius,
    Math.sin(dec) * radius,
    Math.sin(ra) * cosDec * radius,
  );
}

export function createNearbyStars(radius = 1550): NearbyStarsHandle {
  const group = new THREE.Group();
  group.name = 'nearby-stars';
  const texture = spriteTexture();
  const sprites: THREE.Sprite[] = [];

  for (const star of STAR_CATALOG) {
    const normalizedDistance = THREE.MathUtils.clamp(star.distanceLy / 90, 0, 1);
    const layerRadius = radius + normalizedDistance * 820;
    const material = new THREE.SpriteMaterial({
      map: texture,
      color: colorForSpectralClass(star.spectral),
      transparent: true,
      opacity: THREE.MathUtils.clamp(1.15 - star.magnitude * 0.08, 0.22, 0.95),
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const sprite = new THREE.Sprite(material);
    sprite.name = `nearby-star-${star.name}`;
    sprite.position.copy(positionFromRaDec(star.raHours, star.decDeg, layerRadius));
    const scale = THREE.MathUtils.clamp(13 - star.magnitude * 1.25, 2.4, 18);
    sprite.scale.setScalar(scale);
    sprite.userData = {
      baseOpacity: material.opacity,
      baseScale: scale,
      seed: star.raHours * 12.9898 + star.decDeg * 78.233,
      star,
    };
    group.add(sprite);
    sprites.push(sprite);
  }

  return {
    group,
    update(time, cameraPosition) {
      group.position.lerp(cameraPosition, 0.012);
      for (const sprite of sprites) {
        const mat = sprite.material as THREE.SpriteMaterial;
        const baseOpacity = sprite.userData.baseOpacity as number;
        const baseScale = sprite.userData.baseScale as number;
        const seed = sprite.userData.seed as number;
        const twinkle = 0.88 + Math.sin(time * 1.7 + seed) * 0.08 + Math.sin(time * 4.1 + seed * 0.37) * 0.04;
        mat.opacity = THREE.MathUtils.clamp(baseOpacity * twinkle, 0.12, 1);
        sprite.scale.setScalar(baseScale * (0.95 + twinkle * 0.05));
      }
    },
    dispose() {
      for (const sprite of sprites) {
        (sprite.material as THREE.SpriteMaterial).dispose();
      }
    },
  };
}
