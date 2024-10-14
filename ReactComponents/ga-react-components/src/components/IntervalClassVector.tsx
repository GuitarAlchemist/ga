import React from 'react';

interface IntervalClassVectorProps {
    id: number;
}

const IntervalClassVector: React.FC<IntervalClassVectorProps> = ({ id }) => {
    // Function to convert ID to vector
    const getVector = (value: number): number[] => {
        const vector: number[] = [];
        let dividend = value;
        for (let i = 0; i < 6; i++) {
            vector.unshift(dividend % 12);
            dividend = Math.floor(dividend / 12);
        }
        return vector;
    };

    const vector = getVector(id);

    return (
        <div className="interval-class-vector">
            <h3>
                Interval Class Vector
            </h3>
            <div className="vector-display">
                <span className="bracket">&lt;</span>
                {vector.map((value, index) => (
                    <span key={index} className="vector-value">
            {value}
                        {index < vector.length - 1 && <span className="comma">,</span>}
          </span>
                ))}
                <span className="bracket">&gt;</span>
            </div>
            <div className="id-display">
                ID: {id}
            </div>
        </div>
    );
};

export default IntervalClassVector;