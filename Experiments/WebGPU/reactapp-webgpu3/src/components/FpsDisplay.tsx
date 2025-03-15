import { useState, useEffect } from 'react';

export const FpsDisplay = () => {
    const [fps, setFps] = useState(0);
    const [frames, setFrames] = useState(0);

    useEffect(() => {
        let lastTime = performance.now();
        let frameCount = 0;

        const updateFPS = () => {
            const currentTime = performance.now();
            frameCount++;

            // Update FPS every second
            if (currentTime - lastTime >= 1000) {
                setFps(Math.round(frameCount * 1000 / (currentTime - lastTime)));
                setFrames(prevFrames => prevFrames + frameCount);
                frameCount = 0;
                lastTime = currentTime;
            }

            requestAnimationFrame(updateFPS);
        };

        const animationId = requestAnimationFrame(updateFPS);

        return () => cancelAnimationFrame(animationId);
    }, []);

    return (
        <div style={{
            position: 'fixed',
            top: '10px',
            right: '10px',
            backgroundColor: 'rgba(0, 0, 0, 0.7)',
            color: '#fff',
            padding: '8px 12px',
            borderRadius: '5px',
            fontSize: '14px',
            fontFamily: 'monospace',
            zIndex: 1000,
            userSelect: 'none'
        }}>
            FPS: {fps} | Frames: {frames}
        </div>
    );
};