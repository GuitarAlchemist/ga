// src/components/PrimeRadiant/IconRail.tsx
// Vertical icon rail (desktop/tablet) / bottom tab bar (phone) for panel navigation.
// Data-driven from PanelRegistry — panels are grouped with LED status indicators.

import React, { useCallback, useMemo, useRef, useState } from 'react';
import { usePanelRegistry, ICON_CATALOG, PANEL_GROUPS } from './PanelRegistry';
import type { PanelId, BuiltInPanelId, PanelGroupId, PanelGroupDef } from './PanelRegistry';
import { RailPopover } from './RailPopover';

export type { PanelId, BuiltInPanelId };

export type PanelStatus = 'ok' | 'warn' | 'error' | 'critical' | null;

interface RailItem {
  id: PanelId;
  label: string;
  icon: React.ReactNode;
  group?: PanelGroupId;
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

// Severity order for aggregation (higher = worse)
const STATUS_SEVERITY: Record<NonNullable<PanelStatus>, number> = {
  ok: 0,
  warn: 1,
  error: 2,
  critical: 3,
};

/** State for the popover hover target. */
interface PopoverState {
  panelId: string;
  label: string;
  anchorTop: number;
}

/** Aggregate worst status from a list of panel statuses */
function aggregateGroupStatus(
  items: RailItem[],
  statuses: Partial<Record<string, PanelStatus>>,
): PanelStatus {
  let worst: PanelStatus = null;
  let worstSev = -1;
  for (const item of items) {
    const s = statuses[item.id];
    if (s && STATUS_SEVERITY[s] > worstSev) {
      worst = s;
      worstSev = STATUS_SEVERITY[s];
    }
  }
  return worst;
}

/** Group items by their group field, ordered by PANEL_GROUPS */
function groupItems(items: RailItem[]): { group: PanelGroupDef; items: RailItem[] }[] {
  const groupMap = new Map<PanelGroupId, RailItem[]>();
  const ungrouped: RailItem[] = [];

  for (const item of items) {
    if (item.group) {
      const list = groupMap.get(item.group) ?? [];
      list.push(item);
      groupMap.set(item.group, list);
    } else {
      ungrouped.push(item);
    }
  }

  const result: { group: PanelGroupDef; items: RailItem[] }[] = [];
  for (const gDef of PANEL_GROUPS) {
    const groupItems = groupMap.get(gDef.id);
    if (groupItems && groupItems.length > 0) {
      result.push({ group: gDef, items: groupItems });
    }
  }

  // Append any ungrouped items as a virtual group
  if (ungrouped.length > 0) {
    result.push({
      group: { id: 'ops' as PanelGroupId, label: 'Other', order: 99 },
      items: ungrouped,
    });
  }

  return result;
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
    const anchorTop = btnRect.top - railRect.top + btnRect.height / 2;
    setPopover({ panelId: item.id, label: item.label, anchorTop });
  }, []);

  const handleMouseLeave = useCallback(() => {
    setPopover(null);
  }, []);

  const items: RailItem[] = useMemo(() =>
    registrations.map((reg) => ({
      id: reg.definition.id as PanelId,
      label: reg.definition.label,
      icon: ICON_CATALOG[reg.definition.icon] ?? ICON_CATALOG['detail'],
      group: reg.definition.group,
    })),
    [registrations],
  );

  const groups = useMemo(() => groupItems(items), [items]);

  return (
    <div className="icon-rail" ref={railRef}>
      {groups.map((section, gi) => {
        const groupStatus = aggregateGroupStatus(section.items, panelStatuses);
        return (
          <React.Fragment key={section.group.id}>
            {/* Group divider with label + LED */}
            {gi > 0 && <div className="icon-rail__divider" />}
            <div className="icon-rail__group-header" title={section.group.label}>
              <span className="icon-rail__group-label">{section.group.label}</span>
              {groupStatus && (
                <span
                  className={`icon-rail__group-led ${groupStatus === 'critical' ? 'icon-rail__group-led--pulse' : ''}`}
                  style={{ backgroundColor: STATUS_COLORS[groupStatus] }}
                  title={`${section.group.label}: ${groupStatus}`}
                />
              )}
            </div>

            {/* Panel buttons */}
            {section.items.map((item) => {
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
          </React.Fragment>
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
