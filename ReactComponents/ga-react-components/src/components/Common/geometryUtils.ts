export const angleToCoordinates =
    (angle: number, radius: number, centerX: number, centerY: number) => {
    const radians = (angle - 90) * (Math.PI / 180);
    return {
        x: centerX + radius * Math.cos(radians),
        y: centerY + radius * Math.sin(radians)
    };
};
