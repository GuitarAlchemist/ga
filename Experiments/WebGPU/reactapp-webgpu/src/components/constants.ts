// Scale factors to make the visualization visible and centered
export const ASTRONOMICAL_UNIT = 150;
export const DISPLAY_SCALE = 1/20;  // Much smaller denominator to SPREAD THINGS OUT

// Physical constants
export const G = 6.67430e-11 * 1e-20; // Gravitational constant (scaled)
export const SUN_MASS = 1.989e30; // Mass of the Sun in kg

export const SOLAR_SYSTEM_CONFIG = {
    sunConfig: {
        mass: SUN_MASS,
        radius: 15,      // Smaller sun
        color: [1.0, 0.9, 0.0, 1.0],
        trailLength: 0
    },
    planetConfigs: [
        {
            name: "Mercury",
            mass: 3.285e23,
            radius: 8,    // Smaller Mercury
            orbitRadius: ASTRONOMICAL_UNIT * 0.387,
            color: [0.8, 0.8, 0.8, 1.0],
            trailLength: 50
        }
        // Removed other planets
    ]
};