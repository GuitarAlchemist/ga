import { Vector2 } from './Vector2';
import { CelestialBody, CelestialBodyConfig } from './CelestialBody';
import { Planet, PlanetConfig } from './Planet';
import { ParticleSystem } from './ParticleSystem';
import { ASTRONOMICAL_UNIT } from './constants';

export interface SolarSystemConfig {
    sunConfig: CelestialBodyConfig;
    planetConfigs: PlanetConfig[];
}

export class SolarSystem {
    private static readonly PHYSICS = {
        G: 6.67430e-11 * 1e-20,
        TIME_SCALE: 1/1000000,
        MAX_FORCE: 1e4,
        MIN_DISTANCE: ASTRONOMICAL_UNIT * 0.01
    };

    private static readonly DEBUG = false;
    private static readonly DEBUG_INTERVAL = 60; // Log debug info every 60 frames

    private readonly sun: CelestialBody;
    private readonly planets: Planet[];
    private readonly particleSystem: ParticleSystem;
    private frameCount = 0;

    constructor(config: SolarSystemConfig) {
        if (!SolarSystem.isValidConfig(config)) {
            throw new Error('Invalid solar system configuration');
        }
        
        this.particleSystem = new ParticleSystem();
        this.sun = new CelestialBody(config.sunConfig, new Vector2(0, 0));
        
        // Safely create planets array
        const totalPlanets = config.planetConfigs?.length || 0;
        this.planets = (config.planetConfigs || []).map((planetConfig, index) => 
            this.createPlanet(planetConfig, index, totalPlanets));

        // Add initial positions logging using formatPlanetPositions
        if (SolarSystem.DEBUG) {
            console.log('Solar System Initialization:');
            console.log(`Sun mass: ${config.sunConfig.mass.toExponential(3)}`);
            console.log('\nInitial planet positions:');
            console.log(this.formatPlanetPositions(this.planets));
        }
    }

    private static isValidConfig(config: SolarSystemConfig): boolean {
        return Boolean(config && config.sunConfig && Array.isArray(config.planetConfigs));
    }

    private createPlanet(config: PlanetConfig, index: number, total: number): Planet {
        const angle = (index * (2 * Math.PI / total));
        return new Planet(
            config.name,
            config.mass,
            config.radius,
            config.orbitRadius,
            config.color,
            config.trailLength,
            angle
        );
    }

    // Remove this static method since we're handling planet creation in the constructor
    // private static createPlanets = (configs: PlanetConfig[]): Planet[] =>
    //     configs.map((config, index) => 
    //         SolarSystem.createPlanet(config, index, configs.length));

    private  formatPlanetPositions = (planets: Planet[]): string => {
        const details = planets.map(planet => {
            const pos = planet.getPosition();
            const vel = planet.getVelocity();
            const dist = pos.length();
            const angle = (Math.atan2(pos.y, pos.x) * 180 / Math.PI + 360) % 360;
            const speed = vel.length();
            
            return `${planet.getName()}:\n` +
                   `  Position: (${pos.x.toFixed(1)}, ${pos.y.toFixed(1)}) units, ` +
                   `(${(pos.x / ASTRONOMICAL_UNIT).toFixed(3)}, ${(pos.y / ASTRONOMICAL_UNIT).toFixed(3)}) AU\n` +
                   `  Distance from Sun: ${dist.toFixed(1)} units (${(dist / ASTRONOMICAL_UNIT).toFixed(3)} AU)\n` +
                   `  Orbital Angle: ${angle.toFixed(1)}°\n` +
                   `  Velocity: (${vel.x.toFixed(3)}, ${vel.y.toFixed(3)}) units/s\n` +
                   `  Speed: ${speed.toFixed(3)} units/s\n` +
                   `  Mass: ${planet.getMass().toExponential(3)} kg\n` +
                   `  Orbit Radius: ${planet.getOrbitRadius().toFixed(1)} units ` +
                   `(${(planet.getOrbitRadius() / ASTRONOMICAL_UNIT).toFixed(3)} AU)\n`;
        });
        
        return details.join('\n');
    };

    public update(deltaTime: number): void {
        if (!deltaTime || deltaTime <= 0) return;

        const scaledDeltaTime = deltaTime * SolarSystem.PHYSICS.TIME_SCALE;
        const shouldLog = SolarSystem.DEBUG && this.frameCount % SolarSystem.DEBUG_INTERVAL === 0;
        
        this.frameCount++;
        
        this.planets.forEach((planet) => {
            // Calculate gravitational force first
            const pos = planet.getPosition();
            
            if (shouldLog) {
                console.log(`${planet.getName()}: ${JSON.stringify({
                    screenPos: `Screen(X=${pos.x.toFixed(1)}, Y=${pos.y.toFixed(1)})`,
                    screenDistance: `${pos.length().toFixed(1)} units`,
                    velocity: `${planet.getVelocity().length().toFixed(3)} units/s`
                })}`);
            }

            const toSun = this.sun.getPosition().subtract(pos);
            const distance = Math.max(toSun.length(), SolarSystem.PHYSICS.MIN_DISTANCE);
            
            let forceMagnitude = SolarSystem.PHYSICS.G * this.sun.getMass() * planet.getMass() / 
                (distance * distance);
                
            // Cap the force if needed
            forceMagnitude = Math.min(forceMagnitude, SolarSystem.PHYSICS.MAX_FORCE);
            
            // Apply gravitational force
            const force = toSun.normalize().scale(forceMagnitude);
            planet.applyForce(force);
            
            // Update the planet's position and trail
            planet.update(scaledDeltaTime);
        });
    }

    public getParticleSystem(): ParticleSystem {
        this.particleSystem.clear();
        this.particleSystem.add(this.sun.getParticles());
        this.planets.forEach(planet => 
            this.particleSystem.add(planet.getParticles()));
        return this.particleSystem;
    }
}
