// src/components/PrimeRadiant/GalacticClock.tsx
// Galactic Standard Time clock — Foundation-era calendar overlay
// F.E. year = current year - 2024 + 1
// GST: 400 days/year, 10 months of 40 days

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Galactic Standard Time conversion
// ---------------------------------------------------------------------------
interface GalacticTime {
  feYear: number;
  month: number;
  day: number;
  hours: number;
  minutes: number;
  seconds: number;
}

function toGalacticTime(date: Date): GalacticTime {
  const feYear = date.getFullYear() - 2024 + 1;

  // Day of year (0-based), mapped proportionally to 0-399
  const startOfYear = new Date(date.getFullYear(), 0, 0);
  const diff = date.getTime() - startOfYear.getTime();
  const oneDay = 1000 * 60 * 60 * 24;
  const earthDayOfYear = Math.floor(diff / oneDay); // 1-366
  const daysInYear = ((date.getFullYear() % 4 === 0 && date.getFullYear() % 100 !== 0) || date.getFullYear() % 400 === 0) ? 366 : 365;

  // Map earth day to galactic day (1-400)
  const galacticDay = Math.floor((earthDayOfYear / daysInYear) * 400) + 1;
  const month = Math.ceil(galacticDay / 40);
  const day = ((galacticDay - 1) % 40) + 1;

  return {
    feYear,
    month,
    day,
    hours: date.getHours(),
    minutes: date.getMinutes(),
    seconds: date.getSeconds(),
  };
}

function formatGST(gt: GalacticTime): string {
  const h = String(gt.hours).padStart(2, '0');
  const m = String(gt.minutes).padStart(2, '0');
  const s = String(gt.seconds).padStart(2, '0');
  return `F.E. ${gt.feYear} \u00B7 Month ${gt.month} \u00B7 Day ${gt.day} \u00B7 ${h}:${m}:${s} GST`;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const GalacticClock: React.FC = () => {
  const [gst, setGst] = useState(() => formatGST(toGalacticTime(new Date())));

  useEffect(() => {
    const interval = setInterval(() => {
      setGst(formatGST(toGalacticTime(new Date())));
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div
      className="prime-radiant__gst-clock"
      title="Galactic Standard Time — Foundation Era calendar inspired by Asimov's Foundation series. F.E. Year = Earth year − 2023. 400 days/year, 10 months of 40 days each. GST tracks governance lifecycle in Foundation time."
    >
      <span style={{ marginRight: 4 }}>{'\u25CF'}</span>
      {gst}
    </div>
  );
};
