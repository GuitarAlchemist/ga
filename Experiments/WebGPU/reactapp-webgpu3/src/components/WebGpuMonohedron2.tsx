// @ts-ignore
import React, { useRef, useEffect, useState } from 'react';
import { mat4, vec3 } from 'gl-matrix';

interface WebGPUMonohedronProps {
    width?: number;
    height?: number;
    size: number;
    bumpiness?: number;
    fullScreen?: boolean;
}

// Helper function to generate monohedron geometry
function generateGeometry(size: number = 0.25) {
    const vertices: number[] = [];
    const indices: number[] = [];
    const gridSize = 180; // Number of segments
    const scale = size;   // Use size parameter instead of hardcoded value

    // Generate vertices for monohedron
    for (let i = 0; i <= gridSize; i++) {
        for (let j = 0; j <= gridSize; j++) {
            const u = (i / gridSize) * 2 * Math.PI;
            const v = (j / gridSize) * 2 * Math.PI;
            
            // Monohedron parametric equations
            const x = scale * (Math.cos(u) * (3 + Math.cos(v)));
            const y = scale * (Math.sin(u) * (3 + Math.cos(v)));
            const z = scale * (Math.sin(v) + Math.sin(2 * u));

            // Calculate tangent vectors for normal computation
            const du = [
                scale * (-Math.sin(u) * (3 + Math.cos(v))),
                scale * (Math.cos(u) * (3 + Math.cos(v))),
                scale * (2 * Math.cos(2 * u))
            ];

            const dv = [
                scale * (-Math.cos(u) * Math.sin(v)),
                scale * (-Math.sin(u) * Math.sin(v)),
                scale * Math.cos(v)
            ];

            // Cross product for normal
            const normal = [
                du[1] * dv[2] - du[2] * dv[1],
                du[2] * dv[0] - du[0] * dv[2],
                du[0] * dv[1] - du[1] * dv[0]
            ];

            // Normalize normal vector
            const len = Math.sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            normal[0] /= len;
            normal[1] /= len;
            normal[2] /= len;

            // Generate color
            const color = [
                0.7 + 0.3 * Math.cos(u * Math.PI),  // Use u for red component
                0.7 + 0.3 * Math.sin(v * Math.PI),  // Use v for green component
                0.8,
                1.0
            ];

            // Add vertex data
            vertices.push(
                x, y, z,           // position
                ...color,          // color
                ...normal          // normal
            );

            // Generate indices
            if (i < gridSize && j < gridSize) {
                const current = i * (gridSize + 1) + j;
                const next = current + 1;
                const below = current + (gridSize + 1);
                const belowNext = below + 1;

                indices.push(current, below, next);
                indices.push(next, below, belowNext);
            }
        }
    }

    return {
        vertices: new Float32Array(vertices),
        indices: new Uint16Array(indices)
    };
}

export const WebGPUMonohedron2: React.FC<WebGPUMonohedronProps> = ({
    width = 400,
    height = 400,
    size = 0.25,
    bumpiness = 0.05,
    fullScreen = false
}) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const deviceRef = useRef<GPUDevice | null>(null);
    const contextRef = useRef<GPUCanvasContext | null>(null);
    const sizeRef = useRef<number>(size);
    const bumpinessRef = useRef<number>(bumpiness);
    const [dimensions, setDimensions] = useState({ width, height });

    // Add these refs to store textures
    const msaaTextureRef = useRef<GPUTexture | null>(null);
    const depthTextureRef = useRef<GPUTexture | null>(null);

    // Define sampleCount as a constant at component level
    const SAMPLE_COUNT = 4;

    // Update the updateTextures function to use SAMPLE_COUNT
    const updateTextures = (device: GPUDevice, width: number, height: number) => {
        // Cleanup old textures
        msaaTextureRef.current?.destroy();
        depthTextureRef.current?.destroy();

        // Create new MSAA texture
        msaaTextureRef.current = device.createTexture({
            size: { width, height },
            sampleCount: SAMPLE_COUNT,
            format: navigator.gpu.getPreferredCanvasFormat(),
            usage: GPUTextureUsage.RENDER_ATTACHMENT
        });

        // Create new depth texture
        depthTextureRef.current = device.createTexture({
            size: { width, height },
            sampleCount: SAMPLE_COUNT,
            format: 'depth24plus',
            usage: GPUTextureUsage.RENDER_ATTACHMENT
        });
    };

    // Update sizeRef when size prop changes
    useEffect(() => {
        sizeRef.current = size;
    }, [size]);

    // Handle fullscreen and resize
    useEffect(() => {
        if (fullScreen) {
            setDimensions({
                width: window.innerWidth,
                height: window.innerHeight
            });
        } else {
            setDimensions({ width, height });
        }

        const handleResize = () => {
            if (fullScreen) {
                setDimensions({
                    width: window.innerWidth,
                    height: window.innerHeight
                });
            }
        };

        window.addEventListener('resize', handleResize);
        return () => window.removeEventListener('resize', handleResize);
    }, [fullScreen, width, height]);

    // Update WebGPU context when dimensions change
    useEffect(() => {
        if (contextRef.current && deviceRef.current) {
            const device = deviceRef.current;
            
            // Reconfigure the context
            contextRef.current.configure({
                device,
                format: navigator.gpu.getPreferredCanvasFormat(),
                alphaMode: 'premultiplied',
            });

            // Update textures with new dimensions
            updateTextures(device, dimensions.width, dimensions.height);
        }
    }, [dimensions]);

    useEffect(() => {
        let animationFrameId: number;
        let isContextValid = true;

        const initWebGPU = async () => {
            if (!canvasRef.current) return;

            // Clear previous device and context
            if (deviceRef.current) {
                deviceRef.current.destroy();
                deviceRef.current = null;
            }
            if (contextRef.current) {
                contextRef.current.unconfigure();
                contextRef.current = null;
            }

            // Cleanup old textures
            msaaTextureRef.current?.destroy();
            depthTextureRef.current?.destroy();

            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) throw new Error('No GPU adapter found');
            
            const device = await adapter.requestDevice();
            deviceRef.current = device;

            const context = canvasRef.current.getContext('webgpu');
            if (!context) throw new Error('WebGPU context not found');
            contextRef.current = context;

            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            
            // Configure the context
            context.configure({
                device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Create new textures immediately after context configuration
            updateTextures(device, dimensions.width, dimensions.height);

            // Generate vertices and indices with size parameter
            const { vertices, indices } = generateGeometry(size);

            // Create and populate the vertex buffer
            const vertexBuffer = device.createBuffer({
                size: vertices.length * Float32Array.BYTES_PER_ELEMENT,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
                mappedAtCreation: true,
            });
            new Float32Array(vertexBuffer.getMappedRange()).set(vertices);
            vertexBuffer.unmap();

            // Create and populate the index buffer
            const indexBuffer = device.createBuffer({
                size: indices.length * Uint16Array.BYTES_PER_ELEMENT,
                usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
                mappedAtCreation: true,
            });
            new Uint16Array(indexBuffer.getMappedRange()).set(indices);
            indexBuffer.unmap();

            // Remove these unused texture creations
            // const msaaTexture = device.createTexture({...});
            // const depthTexture = device.createTexture({...});

            // Create uniform buffer
            const uniformBufferSize = 96; // = (4x4 matrix = 64 bytes) + (vec3 = 16 bytes) + (2 floats = 16 bytes with padding)
            const uniformBuffer = device.createBuffer({
                size: uniformBufferSize,
                usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
                label: 'Uniform Buffer'
            });

            // Create bind group layout
            const bindGroupLayout = device.createBindGroupLayout({
                entries: [
                    {
                        binding: 0,
                        visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                        buffer: { type: 'uniform' }
                    }
                ]
            });

            // Create pipeline layout
            const pipelineLayout = device.createPipelineLayout({
                bindGroupLayouts: [bindGroupLayout]
            });

            // Create bind group
            const bindGroup = device.createBindGroup({
                layout: bindGroupLayout,
                entries: [
                    {
                        binding: 0,
                        resource: { buffer: uniformBuffer }
                    }
                ]
            });

            const shader = device.createShaderModule({
                code: `
                    struct Uniforms {
                        transform: mat4x4<f32>,
                        viewPosition: vec3<f32>,
                        size: f32,
                        bumpiness: f32,
                    };
                    
                    @binding(0) @group(0) var<uniform> uniforms: Uniforms;

                    struct VertexInput {
                        @location(0) position: vec3<f32>,
                        @location(1) color: vec4<f32>,
                        @location(2) normal: vec3<f32>
                    }

                    struct VertexOutput {
                        @builtin(position) position: vec4<f32>,
                        @location(0) color: vec4<f32>,
                        @location(1) normal: vec3<f32>,
                        @location(2) worldPos: vec3<f32>,
                        @location(3) uv: vec2<f32>
                    }

                    @vertex
                    fn vs_main(input: VertexInput) -> VertexOutput {
                        var output: VertexOutput;
                        output.position = uniforms.transform * vec4<f32>(input.position, 1.0);
                        output.worldPos = input.position;
                        output.normal = input.normal;
                        output.color = input.color;
                        output.uv = input.position.xy * 2.0; // Generate UVs from position
                        return output;
                    }

                    // Add noise function for bump mapping
                    fn hash(p: vec2<f32>) -> f32 {
                        let p2 = vec2<f32>(dot(p, vec2<f32>(127.1, 311.7)),
                                           dot(p, vec2<f32>(269.5, 183.3)));
                        return fract(sin(p2.x + p2.y) * 43758.5453123);
                    }

                    fn noise2d(p: vec2<f32>) -> f32 {
                        let i = floor(p);
                        let f = fract(p);
                        let u = f * f * (3.0 - 2.0 * f);

                        let a = hash(i);
                        let b = hash(i + vec2<f32>(1.0, 0.0));
                        let c = hash(i + vec2<f32>(0.0, 1.0));
                        let d = hash(i + vec2<f32>(1.0, 1.0));

                        return mix(mix(a, b, u.x),
                                  mix(c, d, u.x), u.y);
                    }

                    @fragment
                    fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
                        // Whiter base colors
                        let pureWhite = vec3<f32>(1.0, 1.0, 1.0);
                        let softWhite = vec3<f32>(0.98, 0.98, 0.97);    // Whiter base
                        let veinColor = vec3<f32>(0.45, 0.45, 0.48);    // Keep dark veins
                        let darkVein = vec3<f32>(0.25, 0.25, 0.28);     // Keep darkest veins

                        // Enhanced bump mapping
                        let bumpScale = uniforms.bumpiness; // Use bumpiness from uniforms
                        let p = input.uv * 8.0; // Scale UV for more frequent bumps
                        
                        // Multi-layered noise for more natural bumps
                        var bump = noise2d(p) * 0.5;
                        bump += noise2d(p * 2.0) * 0.25;
                        bump += noise2d(p * 4.0) * 0.125;
                        
                        // Modify normal with bump mapping
                        var N = normalize(input.normal);
                        let tangent = normalize(cross(N, vec3<f32>(0.0, 1.0, 0.0)));
                        let bitangent = normalize(cross(N, tangent));
                        
                        // Apply bump mapping to normal
                        N = normalize(N + (tangent * dpdx(bump) + bitangent * dpdy(bump)) * bumpScale);

                        // Generate marble pattern with more variation
                        var noise = sin(input.uv.x * 4.0 + input.uv.y * 2.0 + bump * 2.0) * 0.5 + 0.5;
                        noise = noise + sin(input.uv.x * 8.0 + input.uv.y * 4.0) * 0.25;
                        
                        // Enhanced turbulence
                        let turbulence = sin(input.uv.x * 16.0 + input.uv.y * 8.0) * 0.125 +
                                       sin(input.uv.x * 32.0 + input.uv.y * 16.0) * 0.0625;
                        
                        let marblePattern = (noise + turbulence) * 0.8;

                        // Lighting setup
                        let V = normalize(uniforms.viewPosition - input.worldPos);
                        let L = normalize(vec3<f32>(1.0, 1.0, 1.0));
                        let H = normalize(L + V);

                        // Enhanced material parameters
                        let ambientStrength = 0.25;        // Slightly increased ambient
                        let diffuseStrength = 0.7;         // Keep diffuse
                        let specularStrength = 2.0;        // Much higher specular
                        let shininess = 256.0;             // Much higher shininess
                        let fresnel = 0.15;                // Increased fresnel

                        // Color mixing with sharper transitions for veins
                        var finalColor = mix(pureWhite, softWhite, marblePattern * 0.3); // Reduced mixing for whiter base
                        finalColor = mix(finalColor, veinColor, smoothstep(0.6, 0.7, marblePattern) * 0.8);
                        finalColor = mix(finalColor, darkVein, smoothstep(0.75, 0.8, marblePattern) * 0.7);

                        // Enhanced lighting calculations
                        let ambient = ambientStrength;
                        let diffuse = max(dot(N, L), 0.0) * diffuseStrength;
                        
                        // Enhanced specular with stronger fresnel effect
                        let NdotH = max(dot(N, H), 0.0);
                        let fresnelTerm = fresnel + (1.0 - fresnel) * pow(1.0 - max(dot(V, H), 0.0), 5.0);
                        let specular = pow(NdotH, shininess) * specularStrength * fresnelTerm;
                        
                        // Subtle subsurface scattering
                        let sss = pow(max(dot(N, -L), 0.0), 2.0) * 0.15;

                        // Combine lighting with enhanced specular
                        finalColor = finalColor * (ambient + diffuse + sss) + vec3<f32>(1.0) * specular;
                        
                        // Enhanced tone mapping for brighter whites
                        finalColor = finalColor / (finalColor + vec3<f32>(0.5)); // Adjusted for brighter result
                        
                        return vec4<f32>(finalColor, 1.0);
                    }
                `
            });

            // Create pipeline
            const pipeline = device.createRenderPipeline({
                layout: pipelineLayout,
                vertex: {
                    module: shader,
                    entryPoint: 'vs_main',
                    buffers: [{
                        arrayStride: 10 * 4, // position(3) + color(4) + normal(3)
                        attributes: [
                            { shaderLocation: 0, offset: 0, format: 'float32x3' },
                            { shaderLocation: 1, offset: 3 * 4, format: 'float32x3' },
                            { shaderLocation: 2, offset: 6 * 4, format: 'float32x4' }
                        ]
                    }]
                },
                fragment: {
                    module: shader,
                    entryPoint: 'fs_main',
                    targets: [{
                        format: canvasFormat,
                        blend: {
                            color: {
                                srcFactor: 'src-alpha',
                                dstFactor: 'one-minus-src-alpha',
                                operation: 'add'
                            },
                            alpha: {
                                srcFactor: 'one',
                                dstFactor: 'one-minus-src-alpha',
                                operation: 'add'
                            }
                        }
                    }]
                },
                primitive: {
                    topology: 'triangle-list',
                    cullMode: 'back',
                    frontFace: 'ccw'
                },
                depthStencil: {
                    depthWriteEnabled: true,
                    depthCompare: 'less',
                    format: 'depth24plus',
                },
                multisample: {
                    count: SAMPLE_COUNT,
                    alphaToCoverageEnabled: true, // Enable alpha to coverage for better transparency handling
                }
            });

            let rotation = 0;

            const render = () => {
                if (!isContextValid || !deviceRef.current || !contextRef.current || 
                    !msaaTextureRef.current || !depthTextureRef.current) return;

                try {
                    rotation += 0.01;

                    const aspect = width / height;
                    const projectionMatrix = mat4.create();
                    mat4.perspective(projectionMatrix, Math.PI / 4, aspect, 0.1, 100.0);

                    const viewMatrix = mat4.create();
                    const cameraPos = vec3.fromValues(1.2, 0.8, 0.8);
                    const target = vec3.fromValues(0, 0, 0);
                    const up = vec3.fromValues(0, 0, 1);

                    mat4.lookAt(
                        viewMatrix,
                        cameraPos,
                        target,
                        up
                    );

                    const modelMatrix = mat4.create();
                    mat4.fromRotation(modelMatrix, rotation, [0, 0, 1]);

                    const mvpMatrix = mat4.create();
                    mat4.multiply(mvpMatrix, projectionMatrix, viewMatrix);
                    mat4.multiply(mvpMatrix, mvpMatrix, modelMatrix);

                    // Update uniform buffer with transform matrix, camera position, and time
                    const uniformData = new Float32Array(24); // 96 bytes / 4 bytes per float = 24 floats
                    uniformData.set(mvpMatrix, 0);
                    uniformData.set([cameraPos[0], cameraPos[1], cameraPos[2]], 16);
                    uniformData[20] = sizeRef.current;
                    uniformData[21] = bumpinessRef.current;
                    device.queue.writeBuffer(uniformBuffer, 0, uniformData);

                    const commandEncoder = device.createCommandEncoder();
                    const textureView = context.getCurrentTexture().createView();

                    const renderPassDescriptor: GPURenderPassDescriptor = {
                        colorAttachments: [{
                            view: msaaTextureRef.current!.createView(),
                            resolveTarget: textureView,
                            clearValue: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
                            loadOp: 'clear',
                            storeOp: 'store',
                        }],
                        depthStencilAttachment: {
                            view: depthTextureRef.current!.createView(),
                            depthClearValue: 1.0,
                            depthLoadOp: 'clear',
                            depthStoreOp: 'store',
                        }
                    };

                    const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);

                    passEncoder.setPipeline(pipeline);
                    passEncoder.setBindGroup(0, bindGroup);
                    passEncoder.setVertexBuffer(0, vertexBuffer);
                    passEncoder.setIndexBuffer(indexBuffer, 'uint16');
                    passEncoder.drawIndexed(indices.length);
                    passEncoder.end();

                    device.queue.submit([commandEncoder.finish()]);
                    animationFrameId = requestAnimationFrame(render);
                } catch (error) {
                    console.error('Render error:', error);
                    isContextValid = false;
                }
            };

            render();
        };

        initWebGPU().catch(console.error);

        return () => {
            isContextValid = false;
            cancelAnimationFrame(animationFrameId);
            
            if (deviceRef.current) {
                deviceRef.current.destroy();
                deviceRef.current = null;
            }
            if (contextRef.current) {
                contextRef.current.unconfigure();
                contextRef.current = null;
            }
            msaaTextureRef.current?.destroy();
            depthTextureRef.current?.destroy();
        };
    }, [dimensions.width, dimensions.height, size, bumpiness]); // Add size and bumpiness to dependency array

    return (
        <div style={{
            position: 'relative',
            width: dimensions.width,
            height: dimensions.height
        }}>
            <canvas
                ref={canvasRef}
                width={dimensions.width}
                height={dimensions.height}
                style={{
                    width: `${dimensions.width}px`,
                    height: `${dimensions.height}px`,
                    backgroundColor: '#000',
                    borderRadius: fullScreen ? '0' : '8px'
                }}
            />
        </div>
    );
};

export default WebGPUMonohedron2;
