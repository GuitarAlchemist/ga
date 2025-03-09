export class Color {
    constructor(
        public r: number,
        public g: number,
        public b: number,
        public a: number = 1.0
    ) {}

    public static fromArray(array: number[]): Color {
        return new Color(
            array[0] || 0,
            array[1] || 0,
            array[2] || 0,
            array[3] || 1.0
        );
    }

    public clone(): Color {
        return new Color(this.r, this.g, this.b, this.a);
    }

    public withAlpha(alpha: number): Color {
        return new Color(this.r, this.g, this.b, alpha);
    }
}