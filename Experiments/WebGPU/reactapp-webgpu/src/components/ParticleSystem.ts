import { Particle } from './Particle';
import { DISPLAY_SCALE } from './constants';

export class ParticleSystem {
    private particles: Particle[] = [];

    public clear(): void {
        this.particles = [];
    }

    public add(newParticles: Particle[] | Particle): void {
        if (Array.isArray(newParticles)) {
            this.particles.push(...newParticles);
        } else {
            this.particles.push(newParticles);
        }
    }

    public getParticles(): Particle[] {
        return this.particles;
    }

    public getVertexData(): Float32Array {
        const vertexData = new Float32Array(this.particles.length * 6);
        
        this.particles.forEach((particle, index) => {
            const baseIndex = index * 6;
            const position = particle.getPosition();
            const color = particle.getColor();
            
            // Scale and center the positions
            vertexData[baseIndex] = position.x * DISPLAY_SCALE;
            vertexData[baseIndex + 1] = position.y * DISPLAY_SCALE;
            
            // Color (RGB)
            vertexData[baseIndex + 2] = color.r;
            vertexData[baseIndex + 3] = color.g;
            vertexData[baseIndex + 4] = color.b;
            
            // Alpha
            vertexData[baseIndex + 5] = color.a;
        });

        return vertexData;
    }
}
