import { useEffect, useState } from 'react';

export function VersionDisplay() {
    const [startTime] = useState(new Date());
    const [currentTime, setCurrentTime] = useState(new Date());

    useEffect(() => {
        const timer = setInterval(() => {
            setCurrentTime(new Date());
        }, 1000);
        return () => clearInterval(timer);
    }, []);

    return (
        <div style={{
            position: 'fixed',
            bottom: '10px',
            right: '10px',
            background: 'rgba(0,0,0,0.8)',
            color: 'white',
            padding: '10px',
            borderRadius: '5px',
            fontFamily: 'monospace',
            fontSize: '12px'
        }}>
            <div>Build ID: {(window as any).BUILD_ID}</div>
            <div>Started: {startTime.toISOString()}</div>
            <div>Uptime: {Math.floor((currentTime.getTime() - startTime.getTime()) / 1000)}s</div>
        </div>
    );
}