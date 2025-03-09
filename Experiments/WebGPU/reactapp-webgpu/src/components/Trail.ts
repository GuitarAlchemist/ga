import { Vector2 } from './Vector2';
import { Color } from './Color';
import { Particle } from './Particle';

export class Trail {
    private positions: Vector2[] = [];
    private baseColor: Color = new Color(1, 1, 1, 1);

    constructor(private maxLength: number) {}

    public update(newPosition: Vector2): void {
        this.positions.unshift(newPosition.clone());
        if (this.positions.length > this.maxLength) {
            this.positions.pop();
        }
    }

    public getParticles(): Particle[] {
        return this.positions.map((pos, index) => {
            const alpha = 1 - (index / this.maxLength);
            return new Particle(pos, this.baseColor.withAlpha(alpha));
        });
    }
}