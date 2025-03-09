import { Vector2 } from './Vector2';
import { CelestialBody, CelestialBodyConfig } from './CelestialBody';
import { ASTRONOMICAL_UNIT, G, SUN_MASS } from './constants';

export interface PlanetConfig extends CelestialBodyConfig {
    name: string;
    orbitRadius: number;
}

export class Planet extends CelestialBody {
    constructor(
        private readonly name: string,
        mass: number,
        radius: number,
        private readonly orbitRadius: number,
        color: number[],
        trailLength: number,
        initialAngle: number = 0
    ) {
        const position = new Vector2(
            Math.cos(initialAngle) * orbitRadius,
            Math.sin(initialAngle) * orbitRadius
        );
        
        super({ mass, radius, color, trailLength }, position);

        const orbitSpeed = Math.sqrt(G * SUN_MASS / orbitRadius);
        this.velocity = new Vector2(
            -Math.sin(initialAngle) * orbitSpeed,
            Math.cos(initialAngle) * orbitSpeed
        );

        // Log initial state with formatted string instead of object
        console.log(`${name} initial state:
    Position: (${position.x.toFixed(1)}, ${position.y.toFixed(1)}) units, (${(position.x / ASTRONOMICAL_UNIT).toFixed(3)}, ${(position.y / ASTRONOMICAL_UNIT).toFixed(3)}) AU
    Orbital Angle: ${(initialAngle * 180 / Math.PI).toFixed(1)}°
    Distance from Sun: ${position.length().toFixed(1)} units (${(position.length() / ASTRONOMICAL_UNIT).toFixed(3)} AU)
    Velocity: (${this.velocity.x.toFixed(3)}, ${this.velocity.y.toFixed(3)}) units/s
    Orbital Speed: ${orbitSpeed.toFixed(3)} units/s`);
    }

    public getName(): string {
        return this.name;
    }

    public getOrbitRadius(): number {
        return this.orbitRadius;
    }

    public getVelocity(): Vector2 {
        return this.velocity;
    }
}