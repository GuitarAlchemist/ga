export class Vector2 {
    constructor(
        public x: number = 0,
        public y: number = 0
    ) {}

    public clone(): Vector2 {
        return new Vector2(this.x, this.y);
    }

    public add(other: Vector2): Vector2 {
        return new Vector2(this.x + other.x, this.y + other.y);
    }

    public subtract(other: Vector2): Vector2 {
        return new Vector2(this.x - other.x, this.y - other.y);
    }

    public scale(scalar: number): Vector2 {
        return new Vector2(this.x * scalar, this.y * scalar);
    }

    public length(): number {
        return Math.sqrt(this.x * this.x + this.y * this.y);
    }

    public normalize(): Vector2 {
        const len = this.length();
        if (len === 0) return new Vector2();
        return this.scale(1 / len);
    }
}