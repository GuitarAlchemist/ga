import React, { useRef, useEffect, useState } from 'react';
import { SolarSystem as SolarSystemSimulation } from "./SolarSystem";
import { Renderer } from "./Renderer";
import { SOLAR_SYSTEM_CONFIG } from "./constants";

const TIME_SCALE = 0.5;

const SolarSystem1: React.FC = () => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const [canvas, setCanvas] = useState<HTMLCanvasElement | null>(null);
    const [renderer, setRenderer] = useState<Renderer | null>(null);
    const [simulation, setSimulation] = useState<SolarSystemSimulation | null>(null);

    // First effect: Set up canvas and resize handler
    useEffect(() => {
        if (!canvasRef.current) return;

        const currentCanvas = canvasRef.current;
        const resizeCanvas = () => {
            const container = currentCanvas.parentElement;
            if (container) {
                currentCanvas.width = container.clientWidth;
                currentCanvas.height = container.clientHeight;
            }
        };

        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);
        setCanvas(currentCanvas);

        return () => {
            window.removeEventListener('resize', resizeCanvas);
            setCanvas(null);
        };
    }, []);

    // Second effect: Initialize renderer and simulation
    useEffect(() => {
        if (!canvas) return;

        const newRenderer = new Renderer(canvas);
        const newSimulation = new SolarSystemSimulation(SOLAR_SYSTEM_CONFIG);

        setRenderer(newRenderer);
        setSimulation(newSimulation);

        return () => {
            newRenderer.dispose();
            setRenderer(null);
            setSimulation(null);
        };
    }, [canvas]);

    // Third effect: Animation loop
    useEffect(() => {
        if (!renderer || !simulation) return;

        let lastTime = 0;
        let animationFrame: number;

        const animate = (time: number) => {
            if (lastTime === 0) {
                lastTime = time;
                animationFrame = requestAnimationFrame(animate);
                return;
            }

            const deltaTime = (time - lastTime) / 1000;
            lastTime = time;

            const maxDeltaTime = 0.1;
            const clampedDeltaTime = Math.min(deltaTime, maxDeltaTime);

            simulation.update(clampedDeltaTime * TIME_SCALE);
            renderer.render(simulation.getParticleSystem());
            animationFrame = requestAnimationFrame(animate);
        };

        animationFrame = requestAnimationFrame(animate);

        return () => {
            cancelAnimationFrame(animationFrame);
        };
    }, [renderer, simulation]);

    return (
        <canvas
            ref={canvasRef}
            style={{
                width: '100%',
                height: '100%',
                background: '#000'
            }}
        />
    );
};

export default SolarSystem1;
