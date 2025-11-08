import React, { CSSProperties } from 'react';

interface UsageProgressProps {
  value: number;
  good: number;
  warn: number;
}

const UsageProgress: React.FC<UsageProgressProps> = ({ value, good, warn }) => {
  const status = value <= good ? 'good' : value <= warn ? 'warn' : 'danger';
  const clamped = Math.max(0, Math.min(100, value));
  const barClass = 'metrics-dashboard__usage-bar-fill metrics-dashboard__usage-bar-fill--' + status;
  const widthStyle: CSSProperties = { width: clamped + '%' };

  return (
    <div className="metrics-dashboard__usage">
      <span>{value.toFixed(1)}%</span>
      <div className="metrics-dashboard__usage-bar">
        <div className={barClass} style={widthStyle} />
      </div>
    </div>
  );
};

export default UsageProgress;
