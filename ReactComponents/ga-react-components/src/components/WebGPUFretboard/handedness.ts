/**
 * Left-handed guitar support
 */
import { Container } from 'pixi.js';

/**
 * Apply left-handed mirroring to a container
 * @param root - Container to mirror
 * @param width - Viewport width
 * @param enabled - Whether left-handed mode is enabled
 */
export function applyLeftHanded(
  root: Container,
  width: number,
  enabled: boolean
): void {
  root.scale.x = enabled ? -1 : 1;
  root.position.x = enabled ? width : 0;
}

/**
 * Reverse an array for left-handed mode
 */
export function reverseIfLeftHanded<T>(arr: T[], leftHanded: boolean): T[] {
  return leftHanded ? [...arr].reverse() : arr;
}

