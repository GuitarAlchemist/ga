import { Vector2 } from './Vector2';
import { Color } from './Color';
import { Particle } from './Particle';
import { Trail } from './Trail';

export interface CelestialBodyConfig {
    mass: number;
    radius: number;
    color: number[];
    trailLength: number;
}

export class CelestialBody {
    protected position: Vector2;
    protected velocity: Vector2;
    protected particle: Particle;
    protected trail: Trail;
    protected mass: number;
    protected radius: number;

    constructor(
        config: CelestialBodyConfig,
        position: Vector2 = new Vector2()
    ) {
        this.mass = config.mass;
        this.radius = config.radius;
        this.position = position;
        this.velocity = new Vector2();
        this.particle = new Particle(position, Color.fromArray(config.color));
        this.trail = new Trail(config.trailLength);
    }

    public update(deltaTime: number): void {
        this.position = this.position.add(this.velocity.scale(deltaTime));
        this.particle = new Particle(this.position, this.particle.getColor());
        this.trail.update(this.position);
    }

    public applyForce(force: Vector2): void {
        const acceleration = force.scale(1 / this.mass);
        this.velocity = this.velocity.add(acceleration);
    }

    public getPosition(): Vector2 {
        return this.position;
    }

    public getMass(): number {
        return this.mass;  // Return the actual mass instead of 0
    }

    public getParticles(): Particle[] {
        return [this.particle, ...this.trail.getParticles()];
    }
}