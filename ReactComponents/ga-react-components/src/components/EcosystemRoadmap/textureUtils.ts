// src/components/EcosystemRoadmap/textureUtils.ts

import * as THREE from 'three';

const textureCache = new Map<string, THREE.CanvasTexture>();

export function createTextTexture(
  text: string,
  options: {
    fontSize?: number;
    color?: string;
    bgColor?: string;
    maxWidth?: number;
    subtitle?: string;
    subtitleColor?: string;
  } = {}
): THREE.CanvasTexture {
  const cacheKey = `${text}|${options.fontSize}|${options.color}|${options.subtitle}`;
  if (textureCache.has(cacheKey)) return textureCache.get(cacheKey)!;

  const {
    fontSize = 32,
    color = '#c9d1d9',
    bgColor = 'transparent',
    maxWidth = 512,
    subtitle,
    subtitleColor = '#8b949e',
  } = options;

  const dpr = Math.min(window.devicePixelRatio, 2);
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d')!;

  ctx.font = `bold ${fontSize}px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif`;
  const metrics = ctx.measureText(text);
  const textWidth = Math.min(metrics.width + 24, maxWidth);
  const height = subtitle ? fontSize * 2.5 : fontSize * 1.8;

  canvas.width = textWidth * dpr;
  canvas.height = height * dpr;
  ctx.scale(dpr, dpr);

  if (bgColor !== 'transparent') {
    ctx.fillStyle = bgColor;
    ctx.roundRect(0, 0, textWidth, height, 6);
    ctx.fill();
  }

  ctx.font = `bold ${fontSize}px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif`;
  ctx.fillStyle = color;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, textWidth / 2, subtitle ? height * 0.35 : height / 2, textWidth - 16);

  if (subtitle) {
    ctx.font = `${fontSize * 0.6}px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif`;
    ctx.fillStyle = subtitleColor;
    ctx.fillText(subtitle, textWidth / 2, height * 0.7, textWidth - 16);
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;
  textureCache.set(cacheKey, texture);
  return texture;
}

export function clearTextureCache(): void {
  textureCache.forEach(t => t.dispose());
  textureCache.clear();
}
