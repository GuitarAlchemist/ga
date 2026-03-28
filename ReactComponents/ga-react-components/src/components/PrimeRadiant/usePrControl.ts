// src/components/PrimeRadiant/usePrControl.ts
// React hook that connects to the Prime Radiant control API (SSE).
// Receives commands from Claude (or any external agent) and executes them.
// Reports state back so Claude can validate.

import { useEffect, useRef, useCallback } from 'react';
import type { GisLayerManager, GisPin, GisPath } from './GisLayer';
import type { GovernanceNode } from './types';
import type { PanelId } from './PanelRegistry';
import { ixqlToGis, clearIxqlPins } from './IxqlGisBridge';

// ---------------------------------------------------------------------------
// Command types — what Claude can tell Prime Radiant to do
// ---------------------------------------------------------------------------

export interface PrCommand {
  id: string;
  action: string;
  params: Record<string, unknown>;
  timestamp: number;
}

export interface PrResult {
  commandId: string;
  success: boolean;
  data?: unknown;
  error?: string;
  timestamp: number;
}

// Supported actions:
// - panel:open {panelId}
// - panel:close
// - gis:add-pin {planet, pin: GisPin}
// - gis:add-pins {planet, pins: GisPin[]}
// - gis:add-path {planet, path: GisPath}
// - gis:clear {planet}
// - gis:cluster {planet, enabled, radius?}
// - gis:preset {planet, name}
// - navigate:node {nodeId}
// - navigate:planet {planetName}
// - state:report  — returns full state snapshot
// - ixql:exec {command}
// - ixql:gis-query {planet?, nodes: GovernanceNode[]}  — map IXQL results to GIS pins
// - ixql:gis-clear {planet?}  — clear IXQL-generated GIS pins

export interface PrControlHandlers {
  openPanel: (id: PanelId) => void;
  closePanel: () => void;
  selectNode: (nodeId: string) => void;
  getGisManager: (planet: string) => GisLayerManager | undefined;
  getState: () => Record<string, unknown>;
  executeIxql?: (command: string) => void;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export function usePrControl(handlers: PrControlHandlers): void {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  // Post result back to the control API
  const postResult = useCallback(async (result: PrResult) => {
    try {
      await fetch('/pr/result', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(result),
      });
    } catch {
      // Best effort
    }
  }, []);

  // Post state snapshot
  const postState = useCallback(async () => {
    try {
      const state = handlersRef.current.getState();
      await fetch('/pr/state', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(state),
      });
    } catch {
      // Best effort
    }
  }, []);

  // Execute a command
  const executeCommand = useCallback(async (cmd: PrCommand) => {
    const h = handlersRef.current;
    const { action, params } = cmd;
    const result: PrResult = {
      commandId: cmd.id,
      success: true,
      timestamp: Date.now(),
    };

    try {
      switch (action) {
        case 'panel:open':
          h.openPanel(params.panelId as PanelId);
          result.data = { panel: params.panelId };
          break;

        case 'panel:close':
          h.closePanel();
          break;

        case 'gis:add-pin': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet}`);
          mgr.addPin(params.pin as GisPin);
          result.data = { pinCount: mgr.pinCount };
          break;
        }

        case 'gis:add-pins': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet}`);
          mgr.addPins(params.pins as GisPin[]);
          result.data = { pinCount: mgr.pinCount };
          break;
        }

        case 'gis:add-path': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet}`);
          mgr.addPath(params.path as GisPath);
          result.data = { pathCount: mgr.pathCount };
          break;
        }

        case 'gis:clear': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet}`);
          mgr.clearAll();
          break;
        }

        case 'gis:cluster': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet}`);
          if (params.enabled) {
            mgr.enableClustering(params.radius as number ?? 10);
          } else {
            mgr.disableClustering();
          }
          break;
        }

        case 'navigate:node':
          h.selectNode(params.nodeId as string);
          break;

        case 'state:report':
          result.data = h.getState();
          break;

        case 'ixql:exec':
          if (h.executeIxql) {
            h.executeIxql(params.command as string);
          } else {
            throw new Error('IXQL executor not available');
          }
          break;

        case 'ixql:gis-query': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet ?? 'earth'}`);
          const nodes = params.nodes as GovernanceNode[];
          if (!Array.isArray(nodes)) throw new Error('params.nodes must be a GovernanceNode[]');
          ixqlToGis(nodes, mgr);
          result.data = { pinCount: mgr.pinCount };
          break;
        }

        case 'ixql:gis-clear': {
          const mgr = h.getGisManager(params.planet as string ?? 'earth');
          if (!mgr) throw new Error(`No GIS manager for ${params.planet ?? 'earth'}`);
          clearIxqlPins(mgr);
          result.data = { pinCount: mgr.pinCount };
          break;
        }

        default:
          throw new Error(`Unknown action: ${action}`);
      }
    } catch (err) {
      result.success = false;
      result.error = err instanceof Error ? err.message : String(err);
    }

    await postResult(result);
    // Update state after every command
    await postState();
  }, [postResult, postState]);

  // Connect to SSE stream
  useEffect(() => {
    let es: EventSource | null = null;
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

    function connect() {
      es = new EventSource('/pr/events');

      es.onmessage = (ev) => {
        try {
          const cmd = JSON.parse(ev.data) as PrCommand;
          executeCommand(cmd);
        } catch {
          // Malformed event
        }
      };

      es.onerror = () => {
        es?.close();
        // Reconnect after 2s
        reconnectTimer = setTimeout(connect, 2000);
      };
    }

    connect();
    // Post initial state
    postState();

    return () => {
      es?.close();
      if (reconnectTimer) clearTimeout(reconnectTimer);
    };
  }, [executeCommand, postState]);
}
