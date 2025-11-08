import React, {useEffect, useRef, useState} from 'react';

// Type definitions matching the API models
interface Point {
    x: number;
    y: number;
}

interface Room {
    x: number;
    y: number;
    width: number;
    height: number;
    centerX: number;
    centerY: number;
}

interface Corridor {
    points: Point[];
    width: number;
}

interface DungeonGenerationParams {
    width: number;
    height: number;
    maxDepth: number;
    minRoomSize: number;
    maxRoomSize: number;
    corridorWidth: number;
    seed?: number;
}

interface DungeonLayout {
    width: number;
    height: number;
    rooms: Room[];
    corridors: Corridor[];
    seed?: number;
    params: DungeonGenerationParams;
}

interface ApiResponse<T> {
    success: boolean;
    data?: T;
    error?: string;
    message?: string;
}

const BSPRoomViewer: React.FC = () => {
    const [dungeon, setDungeon] = useState<DungeonLayout | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [seed, setSeed] = useState<number>(42);
    const canvasRef = useRef<HTMLCanvasElement>(null);

    const fetchDungeon = async (customSeed?: number) => {
        setLoading(true);
        setError(null);

        try {
            const useSeed = customSeed ?? seed;
            // Use relative URL - Vite proxy will forward to https://localhost:7001
            const response = await fetch(`/api/bsp-rooms/generate?seed=${useSeed}`);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const result: ApiResponse<DungeonLayout> = await response.json();

            if (result.success && result.data) {
                setDungeon(result.data);
                console.log('Fetched dungeon:', result.data);
            } else {
                throw new Error(result.error || result.message || 'Failed to fetch dungeon');
            }
        } catch (err) {
            const errorMessage = err instanceof Error ? err.message : 'Unknown error';
            setError(errorMessage);
            console.error('Error fetching dungeon:', err);
        } finally {
            setLoading(false);
        }
    };

    const generateNewDungeon = () => {
        const newSeed = Math.floor(Math.random() * 10000);
        setSeed(newSeed);
        fetchDungeon(newSeed);
    };

    useEffect(() => {
        fetchDungeon();
    }, []);

    useEffect(() => {
        if (dungeon && canvasRef.current) {
            drawDungeon();
        }
    }, [dungeon]);

    const drawDungeon = () => {
        const canvas = canvasRef.current;
        if (!canvas || !dungeon) return;

        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        // Set canvas size
        const scale = 8; // Scale factor for visualization
        canvas.width = dungeon.width * scale;
        canvas.height = dungeon.height * scale;

        // Clear canvas
        ctx.fillStyle = '#1a1a1a';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Draw corridors first (so they appear behind rooms)
        ctx.strokeStyle = '#4a4a4a';
        ctx.lineWidth = 2;
        dungeon.corridors.forEach(corridor => {
            if (corridor.points.length < 2) return;

            ctx.beginPath();
            ctx.moveTo(corridor.points[0].x * scale, corridor.points[0].y * scale);

            for (let i = 1; i < corridor.points.length; i++) {
                ctx.lineTo(corridor.points[i].x * scale, corridor.points[i].y * scale);
            }

            ctx.stroke();

            // Draw corridor as filled rectangles for better visibility
            ctx.fillStyle = '#3a3a3a';
            for (let i = 0; i < corridor.points.length - 1; i++) {
                const p1 = corridor.points[i];
                const p2 = corridor.points[i + 1];

                const minX = Math.min(p1.x, p2.x) * scale;
                const minY = Math.min(p1.y, p2.y) * scale;
                const width = Math.abs(p2.x - p1.x) * scale || corridor.width * scale;
                const height = Math.abs(p2.y - p1.y) * scale || corridor.width * scale;

                ctx.fillRect(minX, minY, width || 4, height || 4);
            }
        });

        // Draw rooms
        dungeon.rooms.forEach((room, index) => {
            // Room fill
            ctx.fillStyle = '#2d5a8a';
            ctx.fillRect(
                room.x * scale,
                room.y * scale,
                room.width * scale,
                room.height * scale
            );

            // Room border
            ctx.strokeStyle = '#4a90e2';
            ctx.lineWidth = 2;
            ctx.strokeRect(
                room.x * scale,
                room.y * scale,
                room.width * scale,
                room.height * scale
            );

            // Room number
            ctx.fillStyle = '#ffffff';
            ctx.font = '12px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(
                `${index + 1}`,
                room.centerX * scale,
                room.centerY * scale
            );
        });
    };

    return (
        <div style={{padding: '20px', backgroundColor: '#f5f5f5', minHeight: '100vh'}}>
            <div style={{maxWidth: '1200px', margin: '0 auto'}}>
                <h1 style={{color: '#333'}}>BSP Dungeon Room Generator</h1>

                <div style={{marginBottom: '20px', display: 'flex', gap: '10px', alignItems: 'center'}}>
                    <button
                        onClick={generateNewDungeon}
                        disabled={loading}
                        style={{
                            padding: '10px 20px',
                            fontSize: '16px',
                            backgroundColor: '#4a90e2',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: loading ? 'not-allowed' : 'pointer',
                            opacity: loading ? 0.6 : 1
                        }}
                    >
                        {loading ? 'Generating...' : 'Generate New Dungeon'}
                    </button>

                    <div style={{display: 'flex', gap: '10px', alignItems: 'center'}}>
                        <label style={{color: '#333'}}>Seed:</label>
                        <input
                            type="number"
                            value={seed}
                            onChange={(e) => setSeed(parseInt(e.target.value) || 0)}
                            style={{
                                padding: '8px',
                                fontSize: '14px',
                                border: '1px solid #ccc',
                                borderRadius: '4px',
                                width: '100px'
                            }}
                        />
                        <button
                            onClick={() => fetchDungeon()}
                            disabled={loading}
                            style={{
                                padding: '8px 16px',
                                fontSize: '14px',
                                backgroundColor: '#5cb85c',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: loading ? 'not-allowed' : 'pointer',
                                opacity: loading ? 0.6 : 1
                            }}
                        >
                            Use Seed
                        </button>
                    </div>
                </div>

                {error && (
                    <div style={{
                        padding: '15px',
                        backgroundColor: '#f8d7da',
                        color: '#721c24',
                        border: '1px solid #f5c6cb',
                        borderRadius: '4px',
                        marginBottom: '20px'
                    }}>
                        <strong>Error:</strong> {error}
                    </div>
                )}

                {dungeon && (
                    <div style={{marginBottom: '20px'}}>
                        <div style={{
                            padding: '15px',
                            backgroundColor: 'white',
                            border: '1px solid #ddd',
                            borderRadius: '4px',
                            marginBottom: '20px'
                        }}>
                            <h3 style={{marginTop: 0, color: '#333'}}>Dungeon Info</h3>
                            <p style={{margin: '5px 0', color: '#666'}}>
                                <strong>Size:</strong> {dungeon.width} × {dungeon.height}
                            </p>
                            <p style={{margin: '5px 0', color: '#666'}}>
                                <strong>Rooms:</strong> {dungeon.rooms.length}
                            </p>
                            <p style={{margin: '5px 0', color: '#666'}}>
                                <strong>Corridors:</strong> {dungeon.corridors.length}
                            </p>
                            <p style={{margin: '5px 0', color: '#666'}}>
                                <strong>Seed:</strong> {dungeon.seed ?? 'N/A'}
                            </p>
                        </div>

                        <div style={{
                            backgroundColor: 'white',
                            padding: '20px',
                            border: '1px solid #ddd',
                            borderRadius: '4px',
                            overflow: 'auto'
                        }}>
                            <canvas
                                ref={canvasRef}
                                style={{
                                    border: '2px solid #333',
                                    display: 'block',
                                    margin: '0 auto',
                                    imageRendering: 'pixelated'
                                }}
                            />
                        </div>
                    </div>
                )}

                {loading && !dungeon && (
                    <div style={{textAlign: 'center', padding: '40px', color: '#666'}}>
                        <p>Loading dungeon...</p>
                    </div>
                )}
            </div>
        </div>
    );
};

export default BSPRoomViewer;

