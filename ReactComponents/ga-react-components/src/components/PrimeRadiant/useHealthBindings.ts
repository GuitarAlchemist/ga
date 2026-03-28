// src/components/PrimeRadiant/useHealthBindings.ts
// React hook that subscribes to HealthBindingEngine and returns
// panel health statuses for the IconRail.

import { useState, useEffect } from 'react';
import { healthBindingEngine, type HealthState } from './HealthBindingEngine';
import type { PanelStatus } from './IconRail';

export function useHealthBindings(): Partial<Record<string, PanelStatus>> {
  const [healthRecord, setHealthRecord] = useState<Partial<Record<string, PanelStatus>>>({});

  useEffect(() => {
    // Sync initial state
    setHealthRecord(healthBindingEngine.getPanelHealthRecord());

    // Subscribe to changes
    const unsub = healthBindingEngine.subscribe((_state: HealthState) => {
      setHealthRecord(healthBindingEngine.getPanelHealthRecord());
    });

    return unsub;
  }, []);

  return healthRecord;
}
