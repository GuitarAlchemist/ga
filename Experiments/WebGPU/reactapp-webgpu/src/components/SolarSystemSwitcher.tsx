import React, { useState } from 'react';
import SolarSystem1 from './SolarSystem1';
import SolarSystem from "./SolarSystem.tsx";

const SolarSystemSwitcher: React.FC = () => {
    const [isBasic, setIsBasic] = useState(true);

    return (
        <div style={{ width: '100%', height: '100%' }}>
            <div style={{ 
                marginBottom: '20px',
                display: 'grid',
                gridTemplateColumns: 'repeat(2, 20px 60px)',
                gap: '20px',
                width: '200px'
            }}>
                <input 
                    type="radio"
                    id="basic"
                    checked={isBasic}
                    onChange={() => setIsBasic(true)}
                    style={{ margin: 0 }}
                />
                <label htmlFor="basic" style={{ color: '#ffffff' }}>Basic</label>
                
                <input 
                    type="radio"
                    id="advanced"
                    checked={!isBasic}
                    onChange={() => setIsBasic(false)}
                    style={{ margin: 0 }}
                />
                <label htmlFor="advanced" style={{ color: '#ffffff' }}>Advanced</label>
            </div>

            {isBasic ? <SolarSystem /> : <SolarSystem1 />}
        </div>
    );
};

export default SolarSystemSwitcher;