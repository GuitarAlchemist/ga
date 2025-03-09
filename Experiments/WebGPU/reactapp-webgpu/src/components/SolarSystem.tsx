import React, { useRef, useEffect } from 'react';

interface Particle {
    x: number;
    y: number;
    color: [number, number, number, number];
}

interface Planet {
    radius: number;
    angle: number;
    speed: number;
    tail: { x: number; y: number }[];
    tailLength: number;
    color: [number, number, number, number];
}

const SolarSystem: React.FC = () => {
    const canvasRef = useRef<HTMLCanvasElement>(null);

    useEffect(() => {
        // Check for WebGPU support
        if (!navigator.gpu) {
            console.error("WebGPU not supported in this browser.");
            return;
        }

        async function init() {
            // Request an adapter and device
            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) {
                console.error("Failed to get GPU adapter.");
                return;
            }
            const device = await adapter.requestDevice();

            const canvas = canvasRef.current;
            if (!canvas) return;
            const context = canvas.getContext('webgpu');
            if (!context) return;
            const format = navigator.gpu.getPreferredCanvasFormat();

            const sampleCount = 4; // Can be 1, 2, 4, 8, or 16

            // Create multisampled texture
            const msaaTexture = device.createTexture({
                size: {
                    width: canvas.width,
                    height: canvas.height,
                },
                sampleCount,
                format,
                usage: GPUTextureUsage.RENDER_ATTACHMENT,
            });

            // Configure the context
            context.configure({
                device,
                format,
                alphaMode: 'opaque',
            });

            // Define a simple WGSL shader that takes in a position (vec2) and a color (vec4)
            const shaderCode = `
struct VertexOutput {
  @builtin(position) Position : vec4<f32>,
  @location(0) fragColor : vec4<f32>,
};

@vertex
fn vertex_main(
  @location(0) position: vec2<f32>,
  @location(1) color: vec4<f32>
) -> VertexOutput {
  var output: VertexOutput;
  output.Position = vec4<f32>(position, 0.0, 1.0);
  output.fragColor = color;
  return output;
}

@fragment
fn fragment_main(input: VertexOutput) -> @location(0) vec4<f32> {
  return input.fragColor;
}`;

            const shaderModule = device.createShaderModule({ code: shaderCode });

            // Create a render pipeline that draws points
            const pipeline = device.createRenderPipeline({
                layout: 'auto',
                vertex: {
                    module: shaderModule,
                    entryPoint: 'vertex_main',
                    buffers: [
                        {
                            arrayStride: 6 * 4,
                            attributes: [
                                { shaderLocation: 0, offset: 0, format: 'float32x2' },
                                { shaderLocation: 1, offset: 2 * 4, format: 'float32x4' },
                            ],
                        },
                    ],
                },
                fragment: {
                    module: shaderModule,
                    entryPoint: 'fragment_main',
                    targets: [{
                        format,
                        blend: {
                            color: {
                                srcFactor: 'src-alpha',
                                dstFactor: 'one-minus-src-alpha',
                            },
                            alpha: {
                                srcFactor: 'src-alpha',
                                dstFactor: 'one-minus-src-alpha',
                            },
                        },
                    }],
                },
                primitive: {
                    topology: 'point-list',
                },
                multisample: {
                    count: sampleCount,
                },
            });

            // Simulation data: a sun and a set of planets
            let particles: Particle[] = [];
            // Define astronomical colors with accurate representations
            const CELESTIAL_COLORS = {
                SUN: [1.0, 0.95, 0.4, 1.0],     // Bright yellow-white
                MERCURY: [0.7, 0.7, 0.7, 1.0],   // Grey
                VENUS: [0.9, 0.8, 0.5, 1.0],     // Pale yellow
                EARTH: [0.2, 0.5, 0.8, 1.0],     // Blue
                MARS: [0.8, 0.3, 0.1, 1.0],      // Red-orange
                JUPITER: [0.8, 0.6, 0.4, 1.0],   // Sandy brown
                SATURN: [0.9, 0.8, 0.5, 1.0],    // Pale gold
                URANUS: [0.5, 0.8, 0.8, 1.0],    // Cyan
                NEPTUNE: [0.2, 0.3, 0.9, 1.0]    // Deep blue
            };

            // Planet configuration with relative sizes and orbital periods
            const PLANET_CONFIGS = [
                {
                    name: 'Mercury',
                    color: CELESTIAL_COLORS.MERCURY,
                    baseRadius: 0.2,      // Closest to sun
                    speedMultiplier: 4.1, // Fastest orbit
                    tailLength: 30
                },
                {
                    name: 'Venus',
                    color: CELESTIAL_COLORS.VENUS,
                    baseRadius: 0.3,
                    speedMultiplier: 1.6,
                    tailLength: 40
                },
                {
                    name: 'Earth',
                    color: CELESTIAL_COLORS.EARTH,
                    baseRadius: 0.4,
                    speedMultiplier: 1.0, // Reference speed (1 Earth year)
                    tailLength: 50
                },
                {
                    name: 'Mars',
                    color: CELESTIAL_COLORS.MARS,
                    baseRadius: 0.5,
                    speedMultiplier: 0.53,
                    tailLength: 60
                },
                {
                    name: 'Jupiter',
                    color: CELESTIAL_COLORS.JUPITER,
                    baseRadius: 0.7,
                    speedMultiplier: 0.084,
                    tailLength: 70
                },
                {
                    name: 'Saturn',
                    color: CELESTIAL_COLORS.SATURN,
                    baseRadius: 0.85,
                    speedMultiplier: 0.034,
                    tailLength: 80
                },
                {
                    name: 'Uranus',
                    color: CELESTIAL_COLORS.URANUS,
                    baseRadius: 1.0,
                    speedMultiplier: 0.012,
                    tailLength: 90
                },
                {
                    name: 'Neptune',
                    color: CELESTIAL_COLORS.NEPTUNE,
                    baseRadius: 1.15,
                    speedMultiplier: 0.006,
                    tailLength: 100
                }
            ];

            // Initialize planets with the new configuration
            const planets: Planet[] = PLANET_CONFIGS.map(config => ({
                name: config.name,
                radius: config.baseRadius,
                angle: Math.random() * Math.PI * 2,
                speed: (0.0005 + Math.random() * 0.0002) * config.speedMultiplier,
                tail: [],
                tailLength: config.tailLength,
                color: config.color as [number, number, number, number]
            }));

            // Update label styles to include all planets
            const style = document.createElement('style');
            style.textContent = `
        .celestial-label {
          color: white;
          font-family: Arial, sans-serif;
          font-size: 12px;
          text-shadow: 0 0 4px rgba(0,0,0,0.8);
          pointer-events: none;
          opacity: 0.8;
          transform-style: preserve-3d;
          backface-visibility: hidden;
          padding: 2px 4px;
        }
        
        .sun-label {
          color: rgb(255, 243, 102);
          font-weight: bold;
        }
        
        .planet-label {
          font-size: 11px;
        }
        
        .mercury-label { color: rgb(179, 179, 179); }
        .venus-label { color: rgb(230, 204, 128); }
        .earth-label { color: rgb(51, 128, 204); }
        .mars-label { color: rgb(204, 77, 26); }
        .jupiter-label { color: rgb(204, 153, 102); }
        .saturn-label { color: rgb(230, 204, 128); }
        .uranus-label { color: rgb(128, 204, 204); }
        .neptune-label { color: rgb(51, 77, 230); }
      `;
            document.head.appendChild(style);

            // The sun orbits around the galactic center with a slow angular speed.
            let sunAngle = 0;
            const sunSpeed = 0.0002;
            const sunOrbitRadius = 0.5;
            const sunTail: { x: number; y: number }[] = [];
            const sunTailLength = 100;

            let lastTime = performance.now();

            function update() {
                const now = performance.now();
                const deltaTime = now - lastTime;
                lastTime = now;

                // Update sun position
                sunAngle += sunSpeed * deltaTime;
                const sunX = sunOrbitRadius * Math.cos(sunAngle);
                const sunY = sunOrbitRadius * Math.sin(sunAngle);
                sunTail.push({ x: sunX, y: sunY });
                if (sunTail.length > sunTailLength) {
                    sunTail.shift();
                }

                // Reset particles array and add the sun and its tail
                particles = [];
                const sunColor = CELESTIAL_COLORS.SUN;
                particles.push({ x: sunX, y: sunY, color: sunColor as [number, number, number, number] });
                sunTail.forEach((p, index) => {
                    const t = index / sunTail.length;
                    particles.push({
                        x: p.x,
                        y: p.y,
                        color: [
                            sunColor[0],
                            sunColor[1],
                            sunColor[2],
                            t * 0.5  // Reduced opacity for tail
                        ]
                    });
                });

                // Update planets
                planets.forEach(planet => {
                    planet.angle += planet.speed * deltaTime;
                    const planetX = sunX + planet.radius * Math.cos(planet.angle);
                    const planetY = sunY + planet.radius * Math.sin(planet.angle);
                    planet.tail.push({ x: planetX, y: planetY });
                    if (planet.tail.length > planet.tailLength) {
                        planet.tail.shift();
                    }
                    particles.push({ x: planetX, y: planetY, color: planet.color });
                    planet.tail.forEach((p, idx) => {
                        const t = idx / planet.tail.length;
                        particles.push({
                            x: p.x,
                            y: p.y,
                            color: [
                                planet.color[0],
                                planet.color[1],
                                planet.color[2],
                                t * 0.3  // Reduced opacity for trails
                            ]
                        });
                    });
                });

                // Create vertex buffer
                const vertexData = new Float32Array(particles.length * 6);
                particles.forEach((p, i) => {
                    vertexData[i * 6] = p.x;
                    vertexData[i * 6 + 1] = p.y;
                    vertexData[i * 6 + 2] = p.color[0];
                    vertexData[i * 6 + 3] = p.color[1];
                    vertexData[i * 6 + 4] = p.color[2];
                    vertexData[i * 6 + 5] = p.color[3];
                });
                const vertexBuffer = device.createBuffer({
                    size: vertexData.byteLength,
                    usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
                });
                device.queue.writeBuffer(vertexBuffer, 0, vertexData.buffer, vertexData.byteOffset, vertexData.byteLength);

                // Render
                const commandEncoder = device.createCommandEncoder();
                if (!context) {
                    throw new Error("Context is not initialized.");
                }
                const textureView = context.getCurrentTexture().createView();
                const renderPass = commandEncoder.beginRenderPass({
                    colorAttachments: [
                        {
                            view: msaaTexture.createView(),
                            resolveTarget: textureView,
                            clearValue: { r: 0, g: 0, b: 0, a: 1 },
                            loadOp: 'clear',
                            storeOp: 'store',
                        },
                    ],
                });
                renderPass.setPipeline(pipeline);
                renderPass.setVertexBuffer(0, vertexBuffer);
                renderPass.draw(particles.length, 1, 0, 0);
                renderPass.end();
                device.queue.submit([commandEncoder.finish()]);

                requestAnimationFrame(update);
            }
            requestAnimationFrame(update);

            return () => {
                msaaTexture.destroy();
            };
        }

        init();
    }, []);

    return (
        <div style={{
            width: '100%',
            height: '100vh',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center'
        }}>
            <canvas
                ref={canvasRef}
                width={800}    // Internal resolution
                height={600}   // Internal resolution
                style={{
                    width: '50%',     // 50% of screen width
                    height: '37.5%',  // Maintains 4:3 aspect ratio (50% * 3/4)
                    background: '#000'
                }}
            />
        </div>
    );
};

export default SolarSystem;