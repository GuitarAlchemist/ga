import React from 'react';

interface WebGpuBraceletNotationProps {
    scale: number;
    size: number;
    colorTones?: { [key: number]: string };
    rootPosition: number;
    hoveredPosition: number | null;
    onPositionHover: (position: number | null) => void;
}

export const WebGpuBraceletNotation: React.FC<WebGpuBraceletNotationProps> = ({
    scale,
    size,
    colorTones = {},
    rootPosition,
    hoveredPosition,
    onPositionHover
}) => {
    const center = size / 2;
    const radius = size * 0.35;  // Reduced from 0.4
    const dotRadius = size * 0.04;  // Reduced from 0.05
    const fontSize = dotRadius * 1.2;
    const labelOffset = dotRadius * 2.2;  // Slightly increased from 2

    // Function to create star path
    const createStar = (cx: number, cy: number, r: number) => {
        const points = 5;
        const innerRadius = r * 0.5;
        let path = '';
        
        for (let i = 0; i < points * 2; i++) {
            const currentRadius = i % 2 === 0 ? r : innerRadius;
            const angle = (i * Math.PI) / points;
            const x = cx + currentRadius * Math.sin(angle);
            const y = cy - currentRadius * Math.cos(angle);
            path += (i === 0 ? 'M' : 'L') + `${x},${y}`;
        }
        path += 'Z';
        return path;
    };

    return (
        <svg 
            width={size} 
            height={size} 
            viewBox={`0 0 ${size} ${size}`}
            style={{ cursor: 'pointer' }}
        >
            {/* Background circle */}
            <circle
                cx={center}
                cy={center}
                r={radius}
                fill="none"
                stroke="#ddd"
                strokeWidth="1"
            />

            {/* Dots and Labels */}
            {Array.from({ length: 12 }).map((_, i) => {
                const angle = (i * 30 - 90) * (Math.PI / 180);
                const x = center + radius * Math.cos(angle);
                const y = center + radius * Math.sin(angle);
                
                // Calculate label position
                const labelX = center + (radius + labelOffset) * Math.cos(angle);
                const labelY = center + (radius + labelOffset) * Math.sin(angle);
                
                const isActive = (scale & (1 << i)) !== 0;
                const isHovered = hoveredPosition === i;
                const isRoot = i === rootPosition;
                const colorTone = i in colorTones ? colorTones[i] : null;
                
                return (
                    <g key={i}>
                        {isRoot ? (
                            <>
                                <circle
                                    cx={x}
                                    cy={y}
                                    r={isHovered ? dotRadius * 1.3 : dotRadius}
                                    fill="#000000"
                                    stroke={isHovered ? "#339af0" : "none"}
                                    strokeWidth={2}
                                    style={{ transition: 'all 0.2s ease' }}
                                />
                                <path
                                    d={createStar(x, y, dotRadius * (isHovered ? 1.0 : 0.8))}
                                    fill="white"
                                    style={{ transition: 'all 0.2s ease', cursor: 'pointer' }}
                                    onMouseEnter={() => onPositionHover(i)}
                                    onMouseLeave={() => onPositionHover(null)}
                                />
                            </>
                        ) : (
                            <circle
                                cx={x}
                                cy={y}
                                r={isActive && isHovered ? dotRadius * 1.3 : dotRadius}
                                fill={isActive ? '#000000' : '#ddd'}
                                stroke={isActive && isHovered ? "#339af0" : "none"}
                                strokeWidth={2}
                                style={{ transition: 'all 0.2s ease', cursor: isActive ? 'pointer' : 'default' }}
                                onMouseEnter={() => isActive && onPositionHover(i)}
                                onMouseLeave={() => isActive && onPositionHover(null)}
                            />
                        )}
                        {colorTone && isActive && (
                            <text
                                x={labelX}
                                y={labelY}
                                textAnchor="middle"
                                dominantBaseline="central"
                                fill="black"
                                fontSize={fontSize}
                                fontFamily="Arial"
                            >
                                {colorTone}
                            </text>
                        )}
                    </g>
                );
            })}
        </svg>
    );
};