// src/components/PrimeRadiant/GodotBridge.ts
// Typed bridge protocol for React ↔ Godot communication (Phase 1 of Godot integration plan).
// Supports WebSocket (desktop Godot) and postMessage (WASM embed) transports.

// ---------------------------------------------------------------------------
// Bridge Event Schema — all events flowing between React and Godot
// ---------------------------------------------------------------------------

export type BridgeEventType =
  | 'navigate-to-planet'
  | 'highlight-node'
  | 'update-belief-weather'
  | 'camera-sync'
  | 'graph-update'
  | 'health-status-change'
  | 'epistemic-tensor-update'
  | 'algedonic-signal'
  | 'screenshot-request'
  | 'screenshot-response'
  | 'demerzel-set-emotion'
  | 'demerzel-set-speaking'
  | 'demerzel-emotion-changed'
  | 'agent-connect'
  | 'agent-disconnect'
  | 'agent-status'
  | 'agent-invoke'
  | 'agent-result';

export interface BridgeEvent<T = unknown> {
  type: BridgeEventType;
  timestamp: string;
  source: 'react' | 'godot';
  payload: T;
}

// Typed payloads per event
export interface NavigateToPlanetPayload {
  planet: string;
  flyDurationMs?: number;
}

export interface HighlightNodePayload {
  nodeId: string;
  color: string;
  glow: boolean;
  pulse: boolean;
  durationMs?: number;
}

export interface BeliefWeatherPayload {
  nodeId: string;
  truthState: 'true' | 'false' | 'unknown' | 'contradictory';
  tensorConfig?: string;
  weather: 'clear' | 'night' | 'fog' | 'storm';
  intensity: number;
}

export interface CameraSyncPayload {
  position: { x: number; y: number; z: number };
  lookAt: { x: number; y: number; z: number };
  fov?: number;
}

export interface ScreenshotResponsePayload {
  imageBase64: string;
  width: number;
  height: number;
}

// ---------------------------------------------------------------------------
// A2A Agent payloads
// ---------------------------------------------------------------------------

/** Known A2A agent identifiers in the ecosystem */
export type A2AAgentId = 'demerzel' | 'ix' | 'tars' | 'ga' | 'seldon';

export interface A2AAgentSkill {
  id: string;
  name: string;
  tags: string[];
}

export interface AgentConnectPayload {
  agentId: A2AAgentId;
  name: string;
  description: string;
  version: string;
  url: string;
  port: number;
  skills: A2AAgentSkill[];
  capabilities: { streaming: boolean; pushNotifications: boolean; stateTransitionHistory: boolean };
}

export interface AgentDisconnectPayload {
  agentId: A2AAgentId;
  reason?: string;
}

export type A2AAgentStatus = 'online' | 'degraded' | 'offline' | 'unknown';

export interface AgentStatusPayload {
  agentId: A2AAgentId;
  status: A2AAgentStatus;
  latencyMs?: number;
  lastSeen: string;
  activeSkill?: string;
}

export interface AgentInvokePayload {
  agentId: A2AAgentId;
  skillId: string;
  input: string;
}

export interface AgentResultPayload {
  agentId: A2AAgentId;
  skillId: string;
  output: string;
  durationMs: number;
}

// ---------------------------------------------------------------------------
// Transport abstraction
// ---------------------------------------------------------------------------

type EventHandler = (event: BridgeEvent) => void;

interface BridgeTransport {
  send(event: BridgeEvent): void;
  onMessage(handler: EventHandler): () => void;
  close(): void;
  readonly connected: boolean;
}

// ---------------------------------------------------------------------------
// WebSocket transport (desktop Godot)
// ---------------------------------------------------------------------------

class WebSocketTransport implements BridgeTransport {
  private ws: WebSocket | null = null;
  private handlers = new Set<EventHandler>();
  private _connected = false;

  constructor(private url: string) {
    this.connect();
  }

  get connected(): boolean { return this._connected; }

  private connect(): void {
    try {
      this.ws = new WebSocket(this.url);
      this.ws.onopen = () => { this._connected = true; };
      this.ws.onclose = () => {
        this._connected = false;
        // Auto-reconnect after 3s
        setTimeout(() => this.connect(), 3000);
      };
      this.ws.onmessage = (msg) => {
        try {
          const event = JSON.parse(msg.data) as BridgeEvent;
          for (const handler of this.handlers) handler(event);
        } catch { /* ignore malformed messages */ }
      };
      this.ws.onerror = () => { this._connected = false; };
    } catch { /* Godot not running */ }
  }

  send(event: BridgeEvent): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(event));
    }
  }

  onMessage(handler: EventHandler): () => void {
    this.handlers.add(handler);
    return () => { this.handlers.delete(handler); };
  }

  close(): void {
    this.ws?.close();
    this.ws = null;
    this._connected = false;
  }
}

// ---------------------------------------------------------------------------
// PostMessage transport (WASM embed via iframe)
// ---------------------------------------------------------------------------

class PostMessageTransport implements BridgeTransport {
  private handlers = new Set<EventHandler>();
  private listener: ((e: MessageEvent) => void) | null = null;
  private _connected = false;

  constructor(private targetWindow: Window) {
    this.listener = (e: MessageEvent) => {
      if (e.data?.type && e.data?.source === 'godot') {
        this._connected = true;
        const event = e.data as BridgeEvent;
        for (const handler of this.handlers) handler(event);
      }
    };
    window.addEventListener('message', this.listener);
  }

  get connected(): boolean { return this._connected; }

  send(event: BridgeEvent): void {
    this.targetWindow.postMessage(event, '*');
  }

  onMessage(handler: EventHandler): () => void {
    this.handlers.add(handler);
    return () => { this.handlers.delete(handler); };
  }

  close(): void {
    if (this.listener) {
      window.removeEventListener('message', this.listener);
      this.listener = null;
    }
    this._connected = false;
  }
}

// ---------------------------------------------------------------------------
// GodotBridge — main API
// ---------------------------------------------------------------------------

export class GodotBridge {
  private transport: BridgeTransport;

  constructor(mode: 'websocket' | 'postmessage', target?: string | Window) {
    if (mode === 'websocket') {
      this.transport = new WebSocketTransport(target as string ?? 'ws://localhost:6505');
    } else {
      this.transport = new PostMessageTransport(target as Window ?? window);
    }
  }

  get connected(): boolean { return this.transport.connected; }

  // Send typed events to Godot
  navigateToPlanet(planet: string, flyDurationMs?: number): void {
    this.send('navigate-to-planet', { planet, flyDurationMs } as NavigateToPlanetPayload);
  }

  highlightNode(nodeId: string, color: string, glow = true, pulse = false): void {
    this.send('highlight-node', { nodeId, color, glow, pulse } as HighlightNodePayload);
  }

  updateBeliefWeather(nodeId: string, truthState: string, weather: string, intensity: number): void {
    this.send('update-belief-weather', { nodeId, truthState, weather, intensity } as BeliefWeatherPayload);
  }

  syncCamera(position: { x: number; y: number; z: number }, lookAt: { x: number; y: number; z: number }): void {
    this.send('camera-sync', { position, lookAt } as CameraSyncPayload);
  }

  requestScreenshot(): void {
    this.send('screenshot-request', {});
  }

  // A2A agent events
  notifyAgentConnect(payload: AgentConnectPayload): void {
    this.send('agent-connect', payload);
  }

  notifyAgentDisconnect(agentId: A2AAgentId, reason?: string): void {
    this.send('agent-disconnect', { agentId, reason } as AgentDisconnectPayload);
  }

  notifyAgentStatus(payload: AgentStatusPayload): void {
    this.send('agent-status', payload);
  }

  // Listen for events from Godot
  onEvent(handler: EventHandler): () => void {
    return this.transport.onMessage(handler);
  }

  close(): void {
    this.transport.close();
  }

  private send(type: BridgeEventType, payload: unknown): void {
    this.transport.send({
      type,
      timestamp: new Date().toISOString(),
      source: 'react',
      payload,
    });
  }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

export function createGodotBridge(mode?: 'websocket' | 'postmessage'): GodotBridge {
  return new GodotBridge(mode ?? 'websocket');
}
