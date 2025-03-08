import React from 'react';

export type BrightnessLevel = 'brightest' | 'brighter' | 'bright' | 'neutral' | 'dark' | 'darker' | 'darkest';

type BrightnessConfig = {
    smiley: string;
    color: string;
    description: string;
}

const brightnessConfigs: { [key in BrightnessLevel]: BrightnessConfig } = {
    "brightest": {
        smiley: "😊",
        color: "#FFD700", // Bright gold - keeping this one
        description: "Very happy"
    },
    "brighter": {
        smiley: "😃",
        color: "#FFA500", // Orange
        description: "Quite happy"
    },
    "bright": {
        smiley: "🙂",
        color: "#32CD32", // Lime green
        description: "Happy"
    },
    "neutral": {
        smiley: "😐",
        color: "#87CEEB", // Sky blue - keeping this one
        description: "Neutral"
    },
    "dark": {
        smiley: "🙁",
        color: "#8B4513", // Saddle brown
        description: "Sad"
    },
    "darker": {
        smiley: "😥",
        color: "#4B0082", // Indigo
        description: "Quite sad"
    },
    "darkest": {
        smiley: "😢",
        color: "#1a1a1a", // Very dark gray
        description: "Very sad"
    }
};

interface BrightnessSmileyProps {
    brightness: BrightnessLevel;
    size?: string;
}

export const BrightnessSmiley: React.FC<BrightnessSmileyProps> = ({
    brightness,
    size = '1.5em'
}) => {
    const config = brightnessConfigs[brightness];
    
    // Calculate filter values to approximate our target colors
    const getFilterValues = (color: string) => {
        switch (brightness) {
            case 'darkest':
                return 'brightness(0.4) saturate(0.6)';
            case 'darker':
                return 'brightness(0.6) saturate(0.7) hue-rotate(220deg)';
            case 'dark':
                return 'brightness(0.7) saturate(0.8) hue-rotate(180deg)';
            case 'neutral':
                return 'brightness(0.9) saturate(0.9) hue-rotate(180deg)';
            case 'bright':
                return 'brightness(1) saturate(1)';
            case 'brighter':
                return 'brightness(1.1) saturate(1.1)';
            case 'brightest':
                return 'brightness(1.2) saturate(1.2)';
        }
    };

    return (
        <div
            style={{
                fontSize: size,
                filter: getFilterValues(config.color),
                textShadow: '1px 1px 2px rgba(0,0,0,0.3)',
                cursor: 'help'
            }}
            title={config.description}
        >
            {config.smiley}
        </div>
    );
};