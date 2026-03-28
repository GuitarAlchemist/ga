// src/components/PrimeRadiant/GodotMcpClient.ts
// JSON-RPC 2.0 WebSocket client for Godot MCP Pro.
// Connects to the MCP server on ports 6505-6509 and sends commands
// to the running Godot editor (create scenes, add nodes, etc.).

export interface JsonRpcRequest {
  jsonrpc: '2.0';
  id: number;
  method: string;
  params: Record<string, unknown>;
}

export interface JsonRpcResponse {
  jsonrpc: '2.0';
  id: number;
  result?: unknown;
  error?: { code: number; message: string };
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

type StatusListener = (status: ConnectionStatus) => void;
type ResponseListener = (id: number, response: JsonRpcResponse) => void;

const BASE_PORT = 6505;
const MAX_PORT = 6509;
const RECONNECT_DELAY = 3000;

// Connection strategies — try Vite proxy first (most reliable), then direct ports
function getConnectionUrls(): string[] {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const host = window.location.host;
  const urls: string[] = [
    // Strategy 1: Vite dev server proxy (same-origin, no CORS issues)
    `${protocol}//${host}/ws/godot-mcp`,
  ];
  // Strategy 2: Direct connection to MCP ports (works if no CORS block)
  for (let p = BASE_PORT; p <= MAX_PORT; p++) {
    urls.push(`ws://127.0.0.1:${p}`);
  }
  return urls;
}

class GodotMcpClient {
  private ws: WebSocket | null = null;
  private status: ConnectionStatus = 'disconnected';
  private statusListeners = new Set<StatusListener>();
  private pendingRequests = new Map<number, {
    resolve: (value: unknown) => void;
    reject: (reason: unknown) => void;
    timeout: ReturnType<typeof setTimeout>;
  }>();
  private responseListeners = new Set<ResponseListener>();
  private nextId = 1;
  private currentPort = BASE_PORT;
  private currentUrlIndex = 0;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private intentionalClose = false;

  get connectionStatus(): ConnectionStatus {
    return this.status;
  }

  private connectedUrl: string | null = null;

  get connectedPort(): number | null {
    return this.status === 'connected' ? this.currentPort : null;
  }

  get connectedEndpoint(): string | null {
    return this.connectedUrl;
  }

  connect(): void {
    if (this.ws?.readyState === WebSocket.OPEN || this.ws?.readyState === WebSocket.CONNECTING) return;
    this.intentionalClose = false;
    this.currentUrlIndex = 0;
    this.tryConnectUrl(0);
  }

  disconnect(): void {
    this.intentionalClose = true;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.ws) {
      this.ws.close(1000, 'Client disconnect');
      this.ws = null;
    }
    this.setStatus('disconnected');
  }

  onStatus(fn: StatusListener): () => void {
    this.statusListeners.add(fn);
    return () => { this.statusListeners.delete(fn); };
  }

  onResponse(fn: ResponseListener): () => void {
    this.responseListeners.add(fn);
    return () => { this.responseListeners.delete(fn); };
  }

  /** Send a JSON-RPC command and await the response */
  async call(method: string, params: Record<string, unknown> = {}): Promise<unknown> {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      throw new Error('Not connected to Godot MCP');
    }

    const id = this.nextId++;
    const request: JsonRpcRequest = { jsonrpc: '2.0', id, method, params };

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(new Error(`Timeout waiting for response to ${method}`));
      }, 30000);

      this.pendingRequests.set(id, { resolve, reject, timeout });
      this.ws!.send(JSON.stringify(request));
    });
  }

  /** Fire-and-forget (no response expected) */
  notify(method: string, params: Record<string, unknown> = {}): void {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return;
    // JSON-RPC notification: no "id" field
    this.ws.send(JSON.stringify({ jsonrpc: '2.0', method, params }));
  }

  // --- Convenience methods for common scene operations ---

  async createScene(scenePath: string, rootType: string = 'Node3D'): Promise<unknown> {
    return this.call('create_scene', { scene_path: scenePath, root_type: rootType });
  }

  async getSceneTree(scenePath?: string): Promise<unknown> {
    return this.call('get_scene_tree', scenePath ? { scene_path: scenePath } : {});
  }

  async addNode(name: string, type: string, parentPath: string = '.'): Promise<unknown> {
    return this.call('add_node', { name, type, parent_path: parentPath });
  }

  async deleteNode(nodePath: string): Promise<unknown> {
    return this.call('delete_node', { node_path: nodePath });
  }

  async getNodeProperties(nodePath: string): Promise<unknown> {
    return this.call('get_node_properties', { node_path: nodePath });
  }

  async updateProperty(nodePath: string, property: string, value: unknown): Promise<unknown> {
    return this.call('update_property', { node_path: nodePath, property, value });
  }

  async createScript(path: string, content: string, attachTo?: string): Promise<unknown> {
    const params: Record<string, unknown> = { path, content };
    if (attachTo) params.node_path = attachTo;
    return this.call('create_script', params);
  }

  async attachScript(nodePath: string, scriptPath: string): Promise<unknown> {
    return this.call('attach_script', { node_path: nodePath, script_path: scriptPath });
  }

  async saveScene(path?: string): Promise<unknown> {
    return this.call('save_scene', path ? { path } : {});
  }

  async openScene(scenePath: string): Promise<unknown> {
    return this.call('open_scene', { scene_path: scenePath });
  }

  async addMeshInstance(name: string, meshType: string, parentPath: string = '.'): Promise<unknown> {
    return this.call('add_mesh_instance', { name, mesh_type: meshType, parent_path: parentPath });
  }

  async setupLighting(type: string = 'outdoor'): Promise<unknown> {
    return this.call('setup_lighting', { type });
  }

  async setupCamera(name: string = 'Camera3D', parentPath: string = '.'): Promise<unknown> {
    return this.call('setup_camera_3d', { name, parent_path: parentPath });
  }

  async getProjectInfo(): Promise<unknown> {
    return this.call('get_project_info', {});
  }

  async listScripts(): Promise<unknown> {
    return this.call('list_scripts', {});
  }

  async getFilesystemTree(): Promise<unknown> {
    return this.call('get_filesystem_tree', {});
  }

  // --- Private connection management ---

  private tryConnectUrl(index: number): void {
    const urls = getConnectionUrls();
    if (index >= urls.length) {
      this.setStatus('error');
      this.scheduleReconnect();
      return;
    }

    this.currentUrlIndex = index;
    const url = urls[index];
    this.setStatus('connecting');

    // Extract port for display if it's a direct connection
    const portMatch = url.match(/:(\d+)$/);
    if (portMatch) this.currentPort = parseInt(portMatch[1], 10);

    try {
      this.ws = new WebSocket(url);
    } catch {
      this.tryConnectUrl(index + 1);
      return;
    }

    this.ws.onopen = () => {
      this.connectedUrl = url;
      this.setStatus('connected');
      // Send ping to verify Godot is responding
      this.notify('ping');
    };

    this.ws.onmessage = (ev) => {
      try {
        const msg = JSON.parse(ev.data) as JsonRpcResponse;
        if (msg.id != null && this.pendingRequests.has(msg.id)) {
          const pending = this.pendingRequests.get(msg.id)!;
          clearTimeout(pending.timeout);
          this.pendingRequests.delete(msg.id);

          if (msg.error) {
            pending.reject(new Error(msg.error.message));
          } else {
            pending.resolve(msg.result);
          }
        }
        // Notify listeners
        for (const fn of this.responseListeners) {
          fn(msg.id, msg);
        }
      } catch {
        // Non-JSON or malformed — ignore
      }
    };

    this.ws.onerror = () => {
      // Try next URL in the list
      this.tryConnectUrl(index + 1);
    };

    this.ws.onclose = () => {
      this.connectedUrl = null;
      if (!this.intentionalClose) {
        this.setStatus('disconnected');
        this.scheduleReconnect();
      }
    };

    // Connection timeout — if not open within 2s, try next URL
    setTimeout(() => {
      if (this.ws?.readyState === WebSocket.CONNECTING) {
        this.ws.close();
        this.tryConnectUrl(index + 1);
      }
    }, 2000);
  }

  private scheduleReconnect(): void {
    if (this.intentionalClose) return;
    if (this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (!this.intentionalClose) {
        this.currentUrlIndex = 0;
        this.tryConnectUrl(0);
      }
    }, RECONNECT_DELAY);
  }

  private setStatus(s: ConnectionStatus): void {
    if (this.status === s) return;
    this.status = s;
    for (const fn of this.statusListeners) fn(s);
  }
}

// Singleton instance
export const godotMcp = new GodotMcpClient();
