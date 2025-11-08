import React from 'react';
import { InstrumentIconProps } from '../types/instrument';

/**
 * Component to display instrument SVG icons
 * 
 * @example
 * ```tsx
 * <InstrumentIcon icon={instrument.icon} size={32} />
 * ```
 */
export const InstrumentIcon: React.FC<InstrumentIconProps> = ({
  icon,
  size = 24,
  color = 'currentColor',
  className = '',
}) => {
  if (!icon) {
    // Return a default music note icon if no icon is provided
    return (
      <svg
        width={size}
        height={size}
        viewBox="0 0 24 24"
        xmlns="http://www.w3.org/2000/svg"
        className={className}
        style={{ color }}
      >
        <path
          d="M12 3L9 6v12l3 3 3-3V6z"
          fill="currentColor"
        />
        <line
          x1="9"
          y1="11"
          x2="15"
          y2="11"
          stroke="currentColor"
          strokeWidth="0.5"
        />
        <line
          x1="9"
          y1="15"
          x2="15"
          y2="15"
          stroke="currentColor"
          strokeWidth="0.5"
        />
      </svg>
    );
  }

  // Parse the SVG string and inject size and color
  const svgWithProps = icon
    .replace(/width="[^"]*"/, `width="${size}"`)
    .replace(/height="[^"]*"/, `height="${size}"`)
    .replace(/class="[^"]*"/, `class="${className}"`)
    .replace(/style="[^"]*"/, `style="color: ${color}"`);

  return (
    <div
      className={className}
      style={{ 
        display: 'inline-flex', 
        alignItems: 'center', 
        justifyContent: 'center',
        width: size,
        height: size,
        color 
      }}
      dangerouslySetInnerHTML={{ __html: svgWithProps }}
    />
  );
};

export default InstrumentIcon;

