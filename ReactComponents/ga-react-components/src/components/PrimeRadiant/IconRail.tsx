// src/components/PrimeRadiant/IconRail.tsx
// Vertical icon rail (desktop/tablet) / bottom tab bar (phone) for panel navigation
// Now data-driven from the PanelRegistry.

import React, { useCallback, useRef, useState } from 'react';
import { usePanelRegistry, ICON_CATALOG } from './PanelRegistry';
import type { PanelId, BuiltInPanelId } from './PanelRegistry';
import { RailPopover } from './RailPopover';

export type { PanelId, BuiltInPanelId };

export type PanelStatus = 'ok' | 'warn' | 'error' | 'critical' | null;

interface RailItem {
  id: PanelId;
  label: string;
  icon: React.ReactNode;
}

interface IconRailProps {
  activePanel: PanelId | null;
  onPanelToggle: (panelId: PanelId) => void;
  panelStatuses?: Partial<Record<string, PanelStatus>>;
}

const STATUS_COLORS: Record<NonNullable<PanelStatus>, string> = {
  ok: '#33CC66',
  warn: '#FFB300',
  error: '#FF4444',
  critical: '#FF0000',
};

/** State for the popover hover target. */
interface PopoverState {
  panelId: string;
  label: string;
  anchorTop: number;
}

export const IconRail: React.FC<IconRailProps> = ({ activePanel, onPanelToggle, panelStatuses = {} }) => {
  const registrations = usePanelRegistry();
  const railRef = useRef<HTMLDivElement>(null);
  const [popover, setPopover] = useState<PopoverState | null>(null);

  const handleMouseEnter = useCallback((e: React.MouseEvent<HTMLButtonElement>, item: RailItem) => {
    const btn = e.currentTarget;
    const rail = railRef.current;
    if (!rail) return;
    const railRect = rail.getBoundingClientRect();
    const btnRect = btn.getBoundingClientRect();
    // Position the popover vertically aligned with the button
    const anchorTop = btnRect.top - railRect.top + btnRect.height / 2;
    setPopover({ panelId: item.id, label: item.label, anchorTop });
  }, []);

  const handleMouseLeave = useCallback(() => {
    setPopover(null);
  }, []);

  const items: RailItem[] = registrations.map((reg) => ({
    id: reg.definition.id as PanelId,
    label: reg.definition.label,
    icon: ICON_CATALOG[reg.definition.icon] ?? ICON_CATALOG['detail'],
  }));

  return (
    <div className="icon-rail" ref={railRef}>
      {items.map((item) => {
        const status = panelStatuses[item.id] ?? null;
        return (
          <button
            key={item.id}
            className={`icon-rail__btn ${activePanel === item.id ? 'icon-rail__btn--active' : ''}`}
            onClick={() => onPanelToggle(item.id)}
            onMouseEnter={(e) => handleMouseEnter(e, item)}
            onMouseLeave={handleMouseLeave}
            aria-label={`Toggle ${item.label} panel`}
          >
            {item.icon}
            <span className="icon-rail__tooltip">{item.label}</span>
            {status && (
              <span
                className={`icon-rail__status-dot ${status === 'critical' ? 'icon-rail__status-dot--pulse' : ''}`}
                style={{ backgroundColor: STATUS_COLORS[status] }}
              />
            )}
          </button>
        );
      })}
      <RailPopover
        panelType={popover?.panelId ?? ''}
        label={popover?.label ?? ''}
        anchorTop={popover?.anchorTop ?? 0}
        visible={popover !== null}
      />
    </div>
  );
};
