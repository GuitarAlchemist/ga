/**
 * First-person controls: WASD/Space/Shift for movement, mouse for look.
 *
 * Uses drei's `PointerLockControls` for the look component (handles
 * vendor-prefixed pointer-lock and Euler clamping). Movement is a
 * frame-rate-independent velocity integrator in `useFrame` — the
 * legacy implementation reimplemented this inline 4 times.
 *
 * `onPositionChange` fires every frame with the camera world position
 * so the region detector can find the current room and the minimap can
 * track the player.
 */

import { useEffect, useRef } from 'react';
import { useFrame, useThree } from '@react-three/fiber';
import { PointerLockControls as DreiPointerLockControls } from '@react-three/drei';
import * as THREE from 'three';

interface Props {
  moveSpeed: number;
  lookSpeed: number;
  initialPosition?: [number, number, number];
  onPositionChange?: (position: THREE.Vector3) => void;
}

interface KeyState {
  forward: boolean;
  backward: boolean;
  left: boolean;
  right: boolean;
  up: boolean;
  down: boolean;
}

const KEY_MAP: Record<string, keyof KeyState> = {
  KeyW: 'forward',
  KeyS: 'backward',
  KeyA: 'left',
  KeyD: 'right',
  ArrowUp: 'forward',
  ArrowDown: 'backward',
  ArrowLeft: 'left',
  ArrowRight: 'right',
  Space: 'up',
  ShiftLeft: 'down',
  ShiftRight: 'down',
};

export function Player({
  moveSpeed,
  lookSpeed,
  initialPosition = [0, 4, 20],
  onPositionChange,
}: Props) {
  const { camera } = useThree();
  const keys = useRef<KeyState>({
    forward: false,
    backward: false,
    left: false,
    right: false,
    up: false,
    down: false,
  });
  const velocity = useRef(new THREE.Vector3());
  const forward = useRef(new THREE.Vector3());
  const right = useRef(new THREE.Vector3());

  // Seed initial camera pose once.
  useEffect(() => {
    camera.position.set(...initialPosition);
  }, [camera, initialPosition]);

  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      const k = KEY_MAP[e.code];
      if (k) {
        keys.current[k] = true;
        // Prevent space-scrolls-page.
        if (e.code === 'Space') e.preventDefault();
      }
    };
    const up = (e: KeyboardEvent) => {
      const k = KEY_MAP[e.code];
      if (k) keys.current[k] = false;
    };
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    return () => {
      window.removeEventListener('keydown', down);
      window.removeEventListener('keyup', up);
    };
  }, []);

  useFrame((_, delta) => {
    const dt = Math.min(delta, 0.1); // clamp to avoid teleport on tab-resume
    const k = keys.current;

    camera.getWorldDirection(forward.current);
    forward.current.y = 0;
    forward.current.normalize();
    right.current.copy(forward.current).cross(camera.up).normalize();

    velocity.current.set(0, 0, 0);
    if (k.forward)  velocity.current.add(forward.current);
    if (k.backward) velocity.current.sub(forward.current);
    if (k.right)    velocity.current.add(right.current);
    if (k.left)     velocity.current.sub(right.current);
    if (k.up)       velocity.current.y += 1;
    if (k.down)     velocity.current.y -= 1;

    if (velocity.current.lengthSq() > 0) {
      velocity.current.normalize().multiplyScalar(moveSpeed * dt);
      camera.position.add(velocity.current);
    }

    onPositionChange?.(camera.position);
  });

  return <DreiPointerLockControls pointerSpeed={lookSpeed * 500} />;
}
