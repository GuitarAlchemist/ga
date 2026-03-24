// src/components/PrimeRadiant/InteractionHandler.ts
// Raycasting, selection, hover, zoom, and detail panel trigger

import * as THREE from 'three';
import type { SceneNode, SceneEdge, SelectionState, GovernanceNode } from './types';
import type { GraphIndex } from './DataLoader';
import {
  setNodeHighlight,
  setNodeDimmed,
} from './NodeRenderer';
import { setEdgeDimmed } from './EdgeRenderer';

// ---------------------------------------------------------------------------
// Interaction handler class
// ---------------------------------------------------------------------------
export class InteractionHandler {
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2(9999, 9999);
  private camera: THREE.Camera;
  private sceneNodes: Map<string, SceneNode>;
  private sceneEdges: Map<string, SceneEdge>;
  private graphIndex: GraphIndex;
  private selectionState: SelectionState;
  private onNodeSelect: ((node: GovernanceNode | null) => void) | undefined;
  private canvas: HTMLCanvasElement;

  constructor(
    camera: THREE.Camera,
    canvas: HTMLCanvasElement,
    sceneNodes: Map<string, SceneNode>,
    sceneEdges: Map<string, SceneEdge>,
    graphIndex: GraphIndex,
    onNodeSelect?: (node: GovernanceNode | null) => void,
  ) {
    this.camera = camera;
    this.canvas = canvas;
    this.sceneNodes = sceneNodes;
    this.sceneEdges = sceneEdges;
    this.graphIndex = graphIndex;
    this.onNodeSelect = onNodeSelect;
    this.selectionState = {
      selectedNodeId: null,
      hoveredNodeId: null,
      connectedNodeIds: new Set(),
      connectedEdgeIds: new Set(),
    };

    this.bindEvents();
  }

  // ─── Event binding ───
  private bindEvents(): void {
    this.canvas.addEventListener('mousemove', this.handleMouseMove);
    this.canvas.addEventListener('click', this.handleClick);
    this.canvas.addEventListener('dblclick', this.handleDoubleClick);
  }

  dispose(): void {
    this.canvas.removeEventListener('mousemove', this.handleMouseMove);
    this.canvas.removeEventListener('click', this.handleClick);
    this.canvas.removeEventListener('dblclick', this.handleDoubleClick);
  }

  // ─── Mouse position update ───
  private handleMouseMove = (event: MouseEvent): void => {
    const rect = this.canvas.getBoundingClientRect();
    this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  };

  // ─── Click → select node ───
  private handleClick = (_event: MouseEvent): void => {
    const hit = this.raycast();
    if (hit) {
      this.selectNode(hit.userData.nodeId as string);
    } else {
      this.clearSelection();
    }
  };

  // ─── Double-click → zoom to node ───
  private handleDoubleClick = (_event: MouseEvent): void => {
    const hit = this.raycast();
    if (hit) {
      this.zoomToNode(hit.userData.nodeId as string);
    }
  };

  // ─── Raycasting ───
  private raycast(): THREE.Object3D | null {
    this.raycaster.setFromCamera(this.mouse, this.camera);
    const meshes: THREE.Object3D[] = [];
    for (const sn of this.sceneNodes.values()) {
      meshes.push(sn.mesh);
    }
    const intersects = this.raycaster.intersectObjects(meshes, false);
    return intersects.length > 0 ? intersects[0].object : null;
  }

  // ─── Hover check (call each frame) ───
  checkHover(): string | null {
    const hit = this.raycast();
    const newHoveredId = hit ? (hit.userData.nodeId as string) : null;

    if (newHoveredId !== this.selectionState.hoveredNodeId) {
      // Unhover old
      if (this.selectionState.hoveredNodeId && this.selectionState.hoveredNodeId !== this.selectionState.selectedNodeId) {
        this.unhighlightNode(this.selectionState.hoveredNodeId);
      }
      // Hover new
      if (newHoveredId && newHoveredId !== this.selectionState.selectedNodeId) {
        this.highlightNode(newHoveredId);
      }
      this.selectionState.hoveredNodeId = newHoveredId;
      this.canvas.style.cursor = newHoveredId ? 'pointer' : 'grab';
    }

    return newHoveredId;
  }

  // ─── Selection ───
  selectNode(nodeId: string): void {
    // Clear previous
    this.restoreAllOpacity();

    this.selectionState.selectedNodeId = nodeId;
    const connected = this.graphIndex.connectedNodes.get(nodeId) ?? new Set();
    const connEdges = this.graphIndex.connectedEdges.get(nodeId) ?? new Set();
    this.selectionState.connectedNodeIds = connected;
    this.selectionState.connectedEdgeIds = connEdges;

    // Dim all non-connected nodes
    for (const sn of this.sceneNodes.values()) {
      if (sn.id === nodeId || connected.has(sn.id)) {
        setNodeHighlight(sn.mesh as THREE.Mesh, true);
        setNodeDimmed(sn.mesh as THREE.Mesh, false);
      } else {
        setNodeDimmed(sn.mesh as THREE.Mesh, true);
      }
    }

    // Dim all non-connected edges
    for (const se of this.sceneEdges.values()) {
      if (connEdges.has(se.id)) {
        setEdgeDimmed(se.line, false);
      } else {
        setEdgeDimmed(se.line, true);
      }
    }

    const node = this.graphIndex.nodeMap.get(nodeId) ?? null;
    this.onNodeSelect?.(node);
  }

  clearSelection(): void {
    this.selectionState.selectedNodeId = null;
    this.selectionState.connectedNodeIds = new Set();
    this.selectionState.connectedEdgeIds = new Set();
    this.restoreAllOpacity();
    this.onNodeSelect?.(null);
  }

  private highlightNode(nodeId: string): void {
    const sn = this.sceneNodes.get(nodeId);
    if (sn) setNodeHighlight(sn.mesh as THREE.Mesh, true);
  }

  private unhighlightNode(nodeId: string): void {
    const sn = this.sceneNodes.get(nodeId);
    if (sn) setNodeHighlight(sn.mesh as THREE.Mesh, false);
  }

  private restoreAllOpacity(): void {
    for (const sn of this.sceneNodes.values()) {
      setNodeHighlight(sn.mesh as THREE.Mesh, false);
      setNodeDimmed(sn.mesh as THREE.Mesh, false);
    }
    for (const se of this.sceneEdges.values()) {
      setEdgeDimmed(se.line, false);
    }
  }

  // ─── Zoom to node cluster ───
  private zoomToNode(nodeId: string): void {
    const sn = this.sceneNodes.get(nodeId);
    if (!sn) return;

    // Animate camera towards the node (simple linear lerp target)
    // The actual animation is handled in RadiantEngine via this target
    (this.camera as THREE.PerspectiveCamera).userData.zoomTarget = sn.position.clone();
  }

  // ─── Search → find and focus ───
  focusOnNode(nodeId: string): void {
    this.selectNode(nodeId);
    this.zoomToNode(nodeId);
  }

  // ─── Get current hover position for tooltip ───
  getMousePosition(): { x: number; y: number } {
    return { x: this.mouse.x, y: this.mouse.y };
  }

  getSelectionState(): SelectionState {
    return { ...this.selectionState };
  }
}
