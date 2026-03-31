// src/components/PrimeRadiant/IconRail.tsx
// VS Code Activity Bar pattern — 5 group icons with panel picker flyout.
// Desktop: vertical rail with group buttons + slide-out picker.
// Mobile: bottom tab bar with 5 groups + overflow drawer filtered by group.

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { usePanelRegistry, ICON_CATALOG, PANEL_GROUPS } from './PanelRegistry';
import type { PanelId, BuiltInPanelId, PanelGroupId, PanelGroupDef } from './PanelRegistry';

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

/** Hook: returns true when viewport is at most 640px wide. */
function useIsMobile(): boolean {
  const [isMobile, setIsMobile] = useState(() =>
    typeof window !== 'undefined' && window.matchMedia('(max-width: 640px)').matches,
  );

  useEffect(() => {
    const mql = window.matchMedia('(max-width: 640px)');
    const onChange = (e: MediaQueryListEvent) => setIsMobile(e.matches);
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, []);

  return isMobile;
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

/** Representative icon key per group */
const GROUP_ICON_KEY: Record<PanelGroupId, string> = {
  governance: 'activity',
  agents: 'agent',
  knowledge: 'university',
  viz: 'godot',
  ops: 'cicd',
};

/** Short label per group (displayed under the icon) */
const GROUP_SHORT_LABEL: Record<PanelGroupId, string> = {
  governance: 'GOV',
  agents: 'AGENTS',
  knowledge: 'KNOW',
  viz: 'VIZ',
  ops: 'OPS',
};

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
    const gItems = groupMap.get(gDef.id);
    if (gItems && gItems.length > 0) {
      result.push({ group: gDef, items: gItems });
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
  const [expandedGroup, setExpandedGroup] = useState<PanelGroupId | null>(null);
  const [overflowGroup, setOverflowGroup] = useState<PanelGroupId | null>(null);
  const isMobile = useIsMobile();

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

  /** Does this group contain the active panel? */
  const groupContainsActive = useCallback(
    (groupId: PanelGroupId): boolean =>
      groups.some((g) => g.group.id === groupId && g.items.some((it) => it.id === activePanel)),
    [groups, activePanel],
  );

  /** Desktop: toggle picker for a group */
  const handleGroupClick = useCallback((groupId: PanelGroupId) => {
    setExpandedGroup((prev) => (prev === groupId ? null : groupId));
  }, []);

  /** Desktop: select a panel from the picker */
  const handlePickerSelect = useCallback((panelId: PanelId) => {
    onPanelToggle(panelId);
    setExpandedGroup(null);
  }, [onPanelToggle]);

  /** Mobile: tap group → open overflow drawer filtered to that group */
  const handleMobileGroupTap = useCallback((groupId: PanelGroupId) => {
    setOverflowGroup((prev) => (prev === groupId ? null : groupId));
  }, []);

  /** Mobile: select panel from overflow */
  const handleOverflowSelect = useCallback((panelId: PanelId) => {
    setOverflowGroup(null);
    onPanelToggle(panelId);
  }, [onPanelToggle]);

  // Close expanded group / overflow when switching layout
  useEffect(() => {
    if (isMobile) {
      setExpandedGroup(null);
    } else {
      setOverflowGroup(null);
    }
  }, [isMobile]);

  // Close picker when clicking outside (desktop)
  useEffect(() => {
    if (!expandedGroup) return;
    const handleClick = (e: MouseEvent) => {
      const rail = railRef.current;
      if (rail && !rail.contains(e.target as Node)) {
        setExpandedGroup(null);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [expandedGroup]);

  // Find items for the expanded group
  const pickerItems = useMemo(() => {
    if (!expandedGroup) return [];
    const section = groups.find((g) => g.group.id === expandedGroup);
    return section?.items ?? [];
  }, [expandedGroup, groups]);

  const pickerLabel = useMemo(() => {
    if (!expandedGroup) return '';
    const section = groups.find((g) => g.group.id === expandedGroup);
    return section?.group.label ?? '';
  }, [expandedGroup, groups]);

  // ─── Desktop ───
  if (!isMobile) {
    return (
      <div className="icon-rail" ref={railRef}>
        {groups.map((section) => {
          const gId = section.group.id;
          const groupStatus = aggregateGroupStatus(section.items, panelStatuses);
          const isActive = groupContainsActive(gId);
          const isExpanded = expandedGroup === gId;
          const iconNode = ICON_CATALOG[GROUP_ICON_KEY[gId]] ?? ICON_CATALOG['detail'];

          return (
            <button
              key={gId}
              className={
                'icon-rail__group-btn' +
                (isActive ? ' icon-rail__group-btn--active' : '') +
                (isExpanded ? ' icon-rail__group-btn--expanded' : '')
              }
              onClick={() => handleGroupClick(gId)}
              aria-label={`${section.group.label} panels`}
              title={section.group.label}
            >
              <span className="icon-rail__group-btn-icon">{iconNode}</span>
              <span className="icon-rail__group-btn-label">{GROUP_SHORT_LABEL[gId]}</span>
              {groupStatus && (
                <span
                  className={`icon-rail__status-dot ${groupStatus === 'critical' ? 'icon-rail__status-dot--pulse' : ''}`}
                  style={{ backgroundColor: STATUS_COLORS[groupStatus] }}
                />
              )}
            </button>
          );
        })}

        {/* Panel picker flyout */}
        {expandedGroup && (
          <div className="icon-rail__picker">
            <div className="icon-rail__picker-header">{pickerLabel}</div>
            {pickerItems.map((item) => {
              const status = panelStatuses[item.id] ?? null;
              const isItemActive = activePanel === item.id;
              return (
                <button
                  key={item.id}
                  className={
                    'icon-rail__picker-item' +
                    (isItemActive ? ' icon-rail__picker-item--active' : '')
                  }
                  onClick={() => handlePickerSelect(item.id)}
                  aria-label={`Open ${item.label} panel`}
                >
                  <span className="icon-rail__picker-item-icon">
                    {item.icon}
                    {status && (
                      <span
                        className={`icon-rail__status-dot ${status === 'critical' ? 'icon-rail__status-dot--pulse' : ''}`}
                        style={{ backgroundColor: STATUS_COLORS[status] }}
                      />
                    )}
                  </span>
                  <span className="icon-rail__picker-item-label">{item.label}</span>
                </button>
              );
            })}
          </div>
        )}
      </div>
    );
  }

  // ─── Mobile ───
  // Find overflow items for the tapped group
  const overflowSection = overflowGroup
    ? groups.find((g) => g.group.id === overflowGroup)
    : null;

  return (
    <>
      <div className="icon-rail" ref={railRef}>
        {groups.map((section) => {
          const gId = section.group.id;
          const groupStatus = aggregateGroupStatus(section.items, panelStatuses);
          const isActive = groupContainsActive(gId);
          const isExpanded = overflowGroup === gId;
          const iconNode = ICON_CATALOG[GROUP_ICON_KEY[gId]] ?? ICON_CATALOG['detail'];

          return (
            <button
              key={gId}
              className={
                'icon-rail__btn icon-rail__group-btn-mobile' +
                (isActive ? ' icon-rail__btn--active' : '') +
                (isExpanded ? ' icon-rail__group-btn-mobile--expanded' : '')
              }
              onClick={() => handleMobileGroupTap(gId)}
              aria-label={`${section.group.label} panels`}
            >
              {iconNode}
              <span className="icon-rail__group-btn-label-mobile">{GROUP_SHORT_LABEL[gId]}</span>
              {groupStatus && (
                <span
                  className={`icon-rail__status-dot ${groupStatus === 'critical' ? 'icon-rail__status-dot--pulse' : ''}`}
                  style={{ backgroundColor: STATUS_COLORS[groupStatus] }}
                />
              )}
            </button>
          );
        })}
      </div>

      {/* Overflow drawer filtered to tapped group */}
      {overflowSection &&
        createPortal(
          <>
            <div
              className="icon-rail__overflow-backdrop"
              onClick={() => setOverflowGroup(null)}
            />
            <div className="icon-rail__overflow-drawer">
              <div className="icon-rail__overflow-drawer-header">
                <span className="icon-rail__overflow-drawer-title">
                  {overflowSection.group.label}
                </span>
                <button
                  className="icon-rail__overflow-drawer-close"
                  onClick={() => setOverflowGroup(null)}
                  aria-label="Close panel drawer"
                >
                  &times;
                </button>
              </div>
              <div className="icon-rail__overflow-grid" style={{ padding: '8px 16px' }}>
                {overflowSection.items.map((item) => {
                  const status = panelStatuses[item.id] ?? null;
                  return (
                    <button
                      key={item.id}
                      className={`icon-rail__overflow-item ${activePanel === item.id ? 'icon-rail__overflow-item--active' : ''}`}
                      onClick={() => handleOverflowSelect(item.id)}
                      aria-label={`Open ${item.label} panel`}
                    >
                      <span className="icon-rail__overflow-item-icon">
                        {item.icon}
                        {status && (
                          <span
                            className={`icon-rail__status-dot ${status === 'critical' ? 'icon-rail__status-dot--pulse' : ''}`}
                            style={{ backgroundColor: STATUS_COLORS[status] }}
                          />
                        )}
                      </span>
                      <span className="icon-rail__overflow-item-label">{item.label}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          </>,
          document.body,
        )}
    </>
  );
};
