import type { CanonicalBodyId, TextureAssetSet } from './assetTypes';

const solarSystemScopeUrl = 'https://www.solarsystemscope.com/textures/';

export const SOLAR_SYSTEM_MANIFEST: Record<CanonicalBodyId, TextureAssetSet> = {
  sun: {
    id: 'sun',
    displayName: 'Sun',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_sun.jpg', '8k': '/textures/planets/8k_sun.jpg' },
    },
  },
  mercury: {
    id: 'mercury',
    displayName: 'Mercury',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_mercury.jpg', '8k': '/textures/planets/8k_mercury.jpg' },
      displacement: { '2k': '/textures/planets/2k_mercury_displacement.jpg' },
    },
  },
  venus: {
    id: 'venus',
    displayName: 'Venus',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_venus_surface.jpg', '8k': '/textures/planets/8k_venus_surface.jpg' },
      displacement: { '2k': '/textures/planets/2k_venus_displacement.jpg' },
      atmosphere: { '2k': '/textures/planets/2k_venus_atmosphere.jpg' },
    },
  },
  earth: {
    id: 'earth',
    displayName: 'Earth',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_earth_daymap.jpg', '8k': '/textures/planets/8k_earth_daymap.jpg' },
      night: { '2k': '/textures/planets/2k_earth_nightmap.jpg', '8k': '/textures/planets/8k_earth_nightmap.jpg' },
      clouds: { '2k': '/textures/planets/2k_earth_clouds.jpg', '8k': '/textures/planets/8k_earth_clouds.jpg' },
      specular: { '2k': '/textures/planets/2k_earth_specular.jpg' },
      displacement: { '2k': '/textures/planets/2k_earth_displacement.jpg' },
      overlay: {},
    },
    notes: 'Canonical Earth body supports layered rendering. 8K available for albedo, night, clouds.',
  },
  moon: {
    id: 'moon',
    displayName: 'Moon',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_moon.jpg', '8k': '/textures/planets/8k_moon.jpg' },
      displacement: { '2k': '/textures/planets/2k_moon_displacement.jpg' },
    },
  },
  mars: {
    id: 'mars',
    displayName: 'Mars',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_mars.jpg', '8k': '/textures/planets/8k_mars.jpg' },
      displacement: { '2k': '/textures/planets/2k_mars_displacement.jpg' },
    },
  },
  jupiter: {
    id: 'jupiter',
    displayName: 'Jupiter',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_jupiter.jpg', '8k': '/textures/planets/8k_jupiter.jpg' },
    },
  },
  saturn: {
    id: 'saturn',
    displayName: 'Saturn',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_saturn.jpg', '8k': '/textures/planets/8k_saturn.jpg' },
      alpha: { '2k': '/textures/planets/2k_saturn_ring_alpha.png', '8k': '/textures/planets/8k_saturn_ring_alpha.png' },
    },
  },
  uranus: {
    id: 'uranus',
    displayName: 'Uranus',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_uranus.jpg' },
    },
    notes: 'No 8K available from Solar System Scope.',
  },
  neptune: {
    id: 'neptune',
    displayName: 'Neptune',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      albedo: { '2k': '/textures/planets/2k_neptune.jpg' },
    },
    notes: 'No 8K available from Solar System Scope.',
  },
  'milky-way': {
    id: 'milky-way',
    displayName: 'Milky Way Panorama',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      stars: {
        '2k': '/textures/milky-way-2k.jpg',
        '8k': '/textures/milky-way-8k.jpg',
      },
    },
    notes: 'Canonical photographic sky panorama used behind the star dome.',
  },
  stars: {
    id: 'stars',
    displayName: 'Star Dome',
    source: 'Solar System Scope',
    sourceUrl: solarSystemScopeUrl,
    license: 'CC BY 4.0',
    textures: {
      stars: { '2k': '/textures/planets/2k_stars.jpg', '8k': '/textures/planets/8k_stars.jpg' },
    },
  },
};

export const CANONICAL_SOLAR_SYSTEM_BODIES = Object.values(SOLAR_SYSTEM_MANIFEST);
