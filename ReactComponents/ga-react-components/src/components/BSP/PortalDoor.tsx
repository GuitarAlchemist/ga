/**
 * Portal Door Component
 * 
 * Creates a 3D portal/door that links between different visualizations.
 * Can be placed in BSP DOOM Explorer, Sunburst3D, or Immersive Musical World.
 */

import * as THREE from 'three';

export interface PortalConfig {
  position: THREE.Vector3;
  rotation?: THREE.Euler;
  scale?: number;
  color?: number;
  targetUrl: string;
  label: string;
  glowIntensity?: number;
}

export class PortalDoor {
  public mesh: THREE.Group;
  public targetUrl: string;
  public label: string;
  private particleSystem: THREE.Points | null = null;
  private animationTime = 0;

  constructor(config: PortalConfig) {
    this.mesh = new THREE.Group();
    this.targetUrl = config.targetUrl;
    this.label = config.label;

    const scale = config.scale || 1;
    const color = config.color || 0x00ffff;
    const glowIntensity = config.glowIntensity || 0.5;

    // Portal frame (archway)
    const frameGeometry = new THREE.TorusGeometry(3 * scale, 0.3 * scale, 16, 32, Math.PI);
    const frameMaterial = new THREE.MeshStandardMaterial({
      color: 0x333333,
      metalness: 0.8,
      roughness: 0.2,
      emissive: color,
      emissiveIntensity: 0.3,
    });
    const frame = new THREE.Mesh(frameGeometry, frameMaterial);
    frame.rotation.x = Math.PI / 2;
    this.mesh.add(frame);

    // Portal pillars
    const pillarGeometry = new THREE.CylinderGeometry(0.3 * scale, 0.4 * scale, 6 * scale, 8);
    const pillarMaterial = new THREE.MeshStandardMaterial({
      color: 0x222222,
      metalness: 0.9,
      roughness: 0.1,
      emissive: color,
      emissiveIntensity: 0.2,
    });

    const leftPillar = new THREE.Mesh(pillarGeometry, pillarMaterial);
    leftPillar.position.set(-3 * scale, 0, 0);
    this.mesh.add(leftPillar);

    const rightPillar = new THREE.Mesh(pillarGeometry, pillarMaterial);
    rightPillar.position.set(3 * scale, 0, 0);
    this.mesh.add(rightPillar);

    // Portal surface (glowing plane)
    const portalGeometry = new THREE.CircleGeometry(2.8 * scale, 32);
    const portalMaterial = new THREE.ShaderMaterial({
      uniforms: {
        time: { value: 0 },
        color1: { value: new THREE.Color(color) },
        color2: { value: new THREE.Color(color).multiplyScalar(0.5) },
        glowIntensity: { value: glowIntensity },
      },
      vertexShader: `
        varying vec2 vUv;
        varying vec3 vPosition;
        void main() {
          vUv = uv;
          vPosition = position;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform float time;
        uniform vec3 color1;
        uniform vec3 color2;
        uniform float glowIntensity;
        varying vec2 vUv;
        varying vec3 vPosition;
        
        void main() {
          vec2 center = vec2(0.5, 0.5);
          float dist = distance(vUv, center);
          
          // Swirling effect
          float angle = atan(vUv.y - 0.5, vUv.x - 0.5);
          float spiral = sin(angle * 5.0 + time * 2.0 - dist * 10.0) * 0.5 + 0.5;
          
          // Radial gradient
          float radial = 1.0 - smoothstep(0.0, 0.5, dist);
          
          // Combine effects
          vec3 color = mix(color2, color1, spiral * radial);
          float alpha = radial * glowIntensity;
          
          gl_FragColor = vec4(color, alpha);
        }
      `,
      transparent: true,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
    });

    const portalSurface = new THREE.Mesh(portalGeometry, portalMaterial);
    this.mesh.add(portalSurface);

    // Outer glow ring
    const glowGeometry = new THREE.RingGeometry(2.8 * scale, 3.2 * scale, 32);
    const glowMaterial = new THREE.MeshBasicMaterial({
      color: color,
      transparent: true,
      opacity: 0.3,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
    });
    const glowRing = new THREE.Mesh(glowGeometry, glowMaterial);
    this.mesh.add(glowRing);

    // Particle system around portal
    this.createParticleSystem(color, scale);

    // Position and rotation
    this.mesh.position.copy(config.position);
    if (config.rotation) {
      this.mesh.rotation.copy(config.rotation);
    }

    // Add label
    this.createLabel(config.label, color, scale);

    // Make it interactive
    this.mesh.userData = {
      isPortal: true,
      targetUrl: this.targetUrl,
      label: this.label,
    };
  }

  private createParticleSystem(color: number, scale: number): void {
    const particleCount = 100;
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(particleCount * 3);
    const velocities = new Float32Array(particleCount * 3);

    for (let i = 0; i < particleCount * 3; i += 3) {
      const angle = Math.random() * Math.PI * 2;
      const radius = Math.random() * 3 * scale;
      positions[i] = Math.cos(angle) * radius;
      positions[i + 1] = (Math.random() - 0.5) * 6 * scale;
      positions[i + 2] = Math.sin(angle) * radius;

      velocities[i] = (Math.random() - 0.5) * 0.02;
      velocities[i + 1] = (Math.random() - 0.5) * 0.02;
      velocities[i + 2] = (Math.random() - 0.5) * 0.02;
    }

    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('velocity', new THREE.BufferAttribute(velocities, 3));

    const material = new THREE.PointsMaterial({
      color: color,
      size: 0.1 * scale,
      transparent: true,
      opacity: 0.6,
      blending: THREE.AdditiveBlending,
    });

    this.particleSystem = new THREE.Points(geometry, material);
    this.mesh.add(this.particleSystem);
  }

  private createLabel(text: string, color: number, scale: number): void {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return;

    canvas.width = 512;
    canvas.height = 128;

    context.fillStyle = '#000000';
    context.fillRect(0, 0, canvas.width, canvas.height);

    context.font = 'bold 48px monospace';
    context.textAlign = 'center';
    context.textBaseline = 'middle';

    // Glow effect
    context.shadowColor = `#${color.toString(16).padStart(6, '0')}`;
    context.shadowBlur = 20;
    context.fillStyle = `#${color.toString(16).padStart(6, '0')}`;
    context.fillText(text, canvas.width / 2, canvas.height / 2);

    const texture = new THREE.CanvasTexture(canvas);
    const spriteMaterial = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      opacity: 0.9,
    });

    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(8 * scale, 2 * scale, 1);
    sprite.position.set(0, 4 * scale, 0);
    this.mesh.add(sprite);
  }

  public update(deltaTime: number): void {
    this.animationTime += deltaTime;

    // Animate portal surface
    const portalSurface = this.mesh.children.find(
      (child) => child instanceof THREE.Mesh && child.material instanceof THREE.ShaderMaterial
    ) as THREE.Mesh | undefined;

    if (portalSurface && portalSurface.material instanceof THREE.ShaderMaterial) {
      portalSurface.material.uniforms.time.value = this.animationTime;
    }

    // Animate particles
    if (this.particleSystem) {
      const positions = this.particleSystem.geometry.attributes.position.array as Float32Array;
      const velocities = this.particleSystem.geometry.attributes.velocity?.array as Float32Array;

      if (velocities) {
        for (let i = 0; i < positions.length; i += 3) {
          positions[i] += velocities[i];
          positions[i + 1] += velocities[i + 1];
          positions[i + 2] += velocities[i + 2];

          // Reset particles that drift too far
          const dist = Math.sqrt(
            positions[i] * positions[i] +
            positions[i + 1] * positions[i + 1] +
            positions[i + 2] * positions[i + 2]
          );

          if (dist > 5) {
            const angle = Math.random() * Math.PI * 2;
            const radius = Math.random() * 3;
            positions[i] = Math.cos(angle) * radius;
            positions[i + 1] = (Math.random() - 0.5) * 6;
            positions[i + 2] = Math.sin(angle) * radius;
          }
        }

        this.particleSystem.geometry.attributes.position.needsUpdate = true;
      }

      // Rotate particle system
      this.particleSystem.rotation.z += deltaTime * 0.5;
    }

    // Pulse glow ring
    const glowRing = this.mesh.children.find(
      (child) => child instanceof THREE.Mesh && child.geometry instanceof THREE.RingGeometry
    ) as THREE.Mesh | undefined;

    if (glowRing && glowRing.material instanceof THREE.MeshBasicMaterial) {
      glowRing.material.opacity = 0.3 + Math.sin(this.animationTime * 2) * 0.2;
    }
  }

  public dispose(): void {
    this.mesh.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        child.geometry.dispose();
        if (Array.isArray(child.material)) {
          child.material.forEach((mat) => mat.dispose());
        } else {
          child.material.dispose();
        }
      }
      if (child instanceof THREE.Sprite) {
        child.material.dispose();
      }
    });

    if (this.particleSystem) {
      this.particleSystem.geometry.dispose();
      if (this.particleSystem.material instanceof THREE.Material) {
        this.particleSystem.material.dispose();
      }
    }
  }
}

export default PortalDoor;

