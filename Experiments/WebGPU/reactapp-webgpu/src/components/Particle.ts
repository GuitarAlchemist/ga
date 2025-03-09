import { Vector2 } from './Vector2';
import { Color } from './Color';

export class Particle {
    constructor(
        private position: Vector2,
        private color: Color
    ) {}

    public getPosition(): Vector2 {
        return this.position;
    }

    public getColor(): Color {
        return this.color;
    }
}