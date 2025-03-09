import React, { useRef, useEffect, useState } from 'react';
import { mat4 } from 'gl-matrix';

interface WebGPUMonohedronProps {
    width: number;
    height: number;
    fullScreen?: boolean;
    size?: number;
    bumpiness?: number;
}

const WebGPUMonohedron2: React.FC<WebGPUMonohedronProps> = ({
    width = 400,
    height = 400,
    fullScreen = false,
    size = 0.15,  // Changed from 0.2 to 0.15 (default size)
    bumpiness = 0.02
}) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const deviceRef = useRef<GPUDevice | null>(null);
    const contextRef = useRef<GPUCanvasContext | null>(null);
    const sizeRef = useRef<number>(size);
    const bumpinessRef = useRef<number>(bumpiness);
    const [dimensions, setDimensions] = useState({ width, height });
    const msaaTextureRef = useRef<GPUTexture | null>(null);
    const depthTextureRef = useRef<GPUTexture | null>(null);
    const uniformBufferRef = useRef<GPUBuffer | null>(null);
    const renderPipelineRef = useRef<GPURenderPipeline | null>(null);
    const vertexBufferRef = useRef<GPUBuffer | null>(null);
    const indexBufferRef = useRef<GPUBuffer | null>(null);
    const bindGroupRef = useRef<GPUBindGroup | null>(null);
    const autoRotationRef = useRef(0);
    const AUTO_ROTATION_SPEED = 0.001; // Increased from 0.0005 to 0.001

    // Camera control state
    const [isDragging, setIsDragging] = useState(false);
    const [cameraAngles, setCameraAngles] = useState({ 
        phi: Math.PI / 4,
        theta: Math.PI / 6
    });
    const lastMousePosRef = useRef({ x: 0, y: 0 });

    const SAMPLE_COUNT = 4;

    // Initialize WebGPU
    useEffect(() => {
        const initWebGPU = async () => {
            if (!canvasRef.current) return;

            // Request adapter and device
            const adapter = await navigator.gpu?.requestAdapter();
            if (!adapter) {
                throw new Error('No appropriate GPUAdapter found.');
            }

            const device = await adapter.requestDevice();
            deviceRef.current = device;

            // Setup context
            const context = canvasRef.current.getContext('webgpu');
            if (!context) {
                throw new Error('Failed to get WebGPU context');
            }
            contextRef.current = context;

            const format = navigator.gpu.getPreferredCanvasFormat();

            context.configure({
                device,
                format,
                alphaMode: 'premultiplied',
            });

            // Create buffers and pipeline
            await createResources(device, format);
        };

        initWebGPU().catch(console.error);

        return () => {
            // Cleanup WebGPU resources
            msaaTextureRef.current?.destroy();
            depthTextureRef.current?.destroy();
            uniformBufferRef.current?.destroy();
            vertexBufferRef.current?.destroy();
            indexBufferRef.current?.destroy();
        };
    }, []);

    const createResources = async (device: GPUDevice, format: GPUTextureFormat) => {
        // Create vertex and index buffers
        const geometry = generateGeometry();
        
        vertexBufferRef.current = device.createBuffer({
            size: geometry.vertices.byteLength,
            usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            mappedAtCreation: true,
        });
        new Float32Array(vertexBufferRef.current.getMappedRange()).set(geometry.vertices);
        vertexBufferRef.current.unmap();

        indexBufferRef.current = device.createBuffer({
            size: geometry.indices.byteLength,
            usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
            mappedAtCreation: true,
        });
        new Uint16Array(indexBufferRef.current.getMappedRange()).set(geometry.indices);
        indexBufferRef.current.unmap();

        // Create uniform buffer
        const uniformBufferSize = 
            16 * 4 +  // mat4x4 (modelViewProjectionMatrix) - 64 bytes
            4 * 4 +   // vec3 (cameraPosition) + padding - 16 bytes
            4 +       // float (size) - 4 bytes
            4 +       // float (bumpiness) - 4 bytes
            8;        // additional padding to ensure 16-byte alignment - 8 bytes
            // Total: 96 bytes
        uniformBufferRef.current = device.createBuffer({
            size: uniformBufferSize,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
        });

        // Create bind group layout and pipeline layout
        const bindGroupLayout = device.createBindGroupLayout({
            entries: [{
                binding: 0,
                visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                buffer: { type: 'uniform' }
            }]
        });

        bindGroupRef.current = device.createBindGroup({
            layout: bindGroupLayout,
            entries: [{
                binding: 0,
                resource: { buffer: uniformBufferRef.current }
            }]
        });

        const pipelineLayout = device.createPipelineLayout({
            bindGroupLayouts: [bindGroupLayout]
        });

        // Create shader module
        const shaderModule = device.createShaderModule({
            code: `
                struct Uniforms {
                    modelViewProjectionMatrix: mat4x4<f32>,
                    cameraPosition: vec3<f32>,
                    size: f32,
                    bumpiness: f32,
                }

                @binding(0) @group(0) var<uniform> uniforms: Uniforms;

                struct VertexInput {
                    @location(0) position: vec3<f32>,
                    @location(1) normal: vec3<f32>,
                }

                struct VertexOutput {
                    @builtin(position) position: vec4<f32>,
                    @location(0) worldPos: vec3<f32>,
                    @location(1) normal: vec3<f32>,
                }

                // Simplified noise function for better performance
                fn simpleNoise(pos: vec3<f32>) -> f32 {
                    let freq = 8.0;
                    return sin(pos.x * freq) * 
                           sin(pos.y * freq) * 
                           sin(pos.z * freq) * 0.5;
                }

                @vertex
                fn vertexMain(input: VertexInput) -> VertexOutput {
                    var output: VertexOutput;
                    
                    // Scale bumpiness inversely with size to maintain consistent appearance
                    let scaledBumpiness = uniforms.bumpiness * uniforms.size;
                    let noise = simpleNoise(input.position);
                    let displacement = input.normal * noise * scaledBumpiness;
                    let finalPosition = input.position + displacement;
                    
                    output.position = uniforms.modelViewProjectionMatrix * vec4<f32>(finalPosition, 1.0);
                    output.worldPos = finalPosition;
                    output.normal = normalize(input.normal + displacement * 0.2);
                    return output;
                }

                @fragment
                fn fragmentMain(input: VertexOutput) -> @location(0) vec4<f32> {
                    // Define lights with increased intensity and better positions for metallic reflection
                    let light1Pos = vec3<f32>(5.0, 5.0, 5.0);
                    let light2Pos = vec3<f32>(-5.0, -2.0, 3.0);
                    let light3Pos = vec3<f32>(0.0, 3.0, -5.0);
                    let light4Pos = vec3<f32>(-3.0, -4.0, -4.0);
                    
                    // Brighter lights with color variation for more dynamic reflections
                    let light1Color = vec3<f32>(1.0, 0.95, 0.8) * 2.5;   // Warm bright
                    let light2Color = vec3<f32>(0.8, 0.9, 1.0) * 2.5;    // Cool bright
                    let light3Color = vec3<f32>(0.7, 0.8, 1.0) * 2.5;    // Blue tint
                    let light4Color = vec3<f32>(1.0, 0.98, 0.9) * 2.5;   // Slight warm

                    let normal = normalize(input.normal);
                    let viewDir = normalize(uniforms.cameraPosition - input.worldPos);

                    // Enhanced metallic properties
                    let metalColor = vec3<f32>(0.92, 0.92, 0.92);    // Brighter base color
                    let roughness = 0.05;                            // Decreased roughness for sharper reflections
                    let metallic = 0.95;                            // Increased metallic factor
                    let specularPower = 32.0;                       // Higher specular power for tighter highlights
                    
                    // Initialize final color
                    var finalColor = vec3<f32>(0.0);
                    
                    // Calculate lighting for each light source
                    let lights = array<vec3<f32>, 4>(light1Pos, light2Pos, light3Pos, light4Pos);
                    let lightColors = array<vec3<f32>, 4>(light1Color, light2Color, light3Color, light4Color);
                    
                    for (var i = 0; i < 4; i++) {
                        let lightDir = normalize(lights[i] - input.worldPos);
                        let halfwayDir = normalize(lightDir + viewDir);
                        
                        // Enhanced Fresnel effect
                        let F0 = mix(vec3<f32>(0.04), metalColor, vec3<f32>(metallic));
                        let cosTheta = max(dot(normal, viewDir), 0.0);
                        let F = F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
                        
                        // Specular reflection
                        let specular = pow(max(dot(normal, halfwayDir), 0.0), specularPower);
                        
                        // Diffuse lighting with proper vector types
                        let diffuse = max(dot(normal, lightDir), 0.0);
                        let diffuseColor = vec3<f32>(diffuse);
                        
                        // Combine components with metallic weighting
                        let lightContrib = lightColors[i] * (
                            diffuseColor * (vec3<f32>(1.0) - vec3<f32>(metallic)) +  // Fixed diffuse calculation
                            specular * F * (1.0 - roughness) * 2.0                    // Enhanced specular
                        );
                        
                        finalColor += lightContrib;
                    }
                    
                    // Environment reflection approximation
                    let envReflection = reflect(-viewDir, normal);
                    let envContrib = pow(max(1.0 - dot(normal, viewDir), 0.0), 3.0) * metalColor * metallic;
                    finalColor += envContrib * 0.5;
                    
                    // Tone mapping and final adjustments
                    finalColor = finalColor / (finalColor + vec3<f32>(1.0));  // Simple tone mapping
                    finalColor = pow(finalColor, vec3<f32>(1.0 / 2.2));      // Gamma correction
                    
                    return vec4<f32>(finalColor, 1.0);
                }
            `
        });

        // Create render pipeline
        renderPipelineRef.current = device.createRenderPipeline({
            layout: pipelineLayout,
            vertex: {
                module: shaderModule,
                entryPoint: 'vertexMain',
                buffers: [{
                    arrayStride: 24, // 6 floats * 4 bytes (3 for position, 3 for normal)
                    attributes: [
                        {
                            // position
                            shaderLocation: 0,
                            offset: 0,
                            format: 'float32x3'
                        },
                        {
                            // normal
                            shaderLocation: 1,
                            offset: 12,
                            format: 'float32x3'
                        }
                    ]
                }]
            },
            fragment: {
                module: shaderModule,
                entryPoint: 'fragmentMain',
                targets: [{
                    format: format
                }]
            },
            primitive: {
                topology: 'triangle-list',
                cullMode: 'back'
            },
            depthStencil: {
                depthWriteEnabled: true,
                depthCompare: 'less',
                format: 'depth24plus'
            },
            multisample: {
                count: SAMPLE_COUNT
            }
        });

        // Create initial textures
        updateTextures(device, dimensions.width, dimensions.height);
    };

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

    // Update bumpinessRef when bumpiness prop changes
    useEffect(() => {
        bumpinessRef.current = bumpiness;
    }, [bumpiness]);

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

    const handleMouseDown = (e: React.MouseEvent) => {
        setIsDragging(true);
        lastMousePosRef.current = { x: e.clientX, y: e.clientY };
    };

    const handleMouseMove = (e: React.MouseEvent) => {
        if (!isDragging) return;

        const deltaX = e.clientX - lastMousePosRef.current.x;
        const deltaY = e.clientY - lastMousePosRef.current.y;

        setCameraAngles(prev => ({
            phi: prev.phi - deltaX * 0.003,
            theta: Math.max(-Math.PI/2 + 0.1, Math.min(Math.PI/2 - 0.1, prev.theta - deltaY * 0.003))
        }));

        lastMousePosRef.current = { x: e.clientX, y: e.clientY };
    };

    const handleMouseUp = () => {
        setIsDragging(false);
        // Store the current phi angle in the auto-rotation
        autoRotationRef.current = cameraAngles.phi;
    };

    let lastTime = 0;

    const render = (timestamp: number) => {
        if (!deviceRef.current || !contextRef.current || !uniformBufferRef.current || 
            !renderPipelineRef.current || !vertexBufferRef.current || !indexBufferRef.current || 
            !bindGroupRef.current || !msaaTextureRef.current || !depthTextureRef.current) return;

        const device = deviceRef.current;
        const context = contextRef.current;

        if (timestamp - lastTime > 16) { // Cap at ~60fps
            lastTime = timestamp;

            // Update auto-rotation only when not dragging
            if (!isDragging) {
                autoRotationRef.current += AUTO_ROTATION_SPEED;
            }

            // Calculate view matrix based on camera angles
            const cameraRadius = 2.0 + (1.0 / sizeRef.current);
            const adjustedPhi = cameraAngles.phi + autoRotationRef.current; // Add auto-rotation to phi
            const cameraX = cameraRadius * Math.cos(cameraAngles.theta) * Math.cos(adjustedPhi);
            const cameraY = cameraRadius * Math.sin(cameraAngles.theta);
            const cameraZ = cameraRadius * Math.cos(cameraAngles.theta) * Math.sin(adjustedPhi);

            const viewMatrix = mat4.lookAt(
                mat4.create(),
                [cameraX, cameraY, cameraZ],
                [0, 0, 0],
                [0, 1, 0]
            );

            const projectionMatrix = mat4.perspective(
                mat4.create(),
                Math.PI / 4,
                dimensions.width / dimensions.height,
                0.1,
                100.0
            );

            const modelMatrix = mat4.create();
            const mvpMatrix = mat4.create();
            mat4.multiply(mvpMatrix, projectionMatrix, viewMatrix);
            mat4.multiply(mvpMatrix, mvpMatrix, modelMatrix);

            // Update uniform buffer
            const uniformData = new Float32Array(24);
            uniformData.set(Array.from(mvpMatrix), 0);
            uniformData.set([cameraX, cameraY, cameraZ, 0.0], 16);
            uniformData.set([sizeRef.current, bumpinessRef.current, 0.0, 0.0], 20);
            device.queue.writeBuffer(uniformBufferRef.current, 0, uniformData);
        }

        const commandEncoder = device.createCommandEncoder();
        const textureView = context.getCurrentTexture().createView();

        const renderPassDescriptor: GPURenderPassDescriptor = {
            colorAttachments: [{
                view: msaaTextureRef.current.createView(),
                resolveTarget: textureView,
                clearValue: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
                loadOp: 'clear',
                storeOp: 'store'
            }],
            depthStencilAttachment: {
                view: depthTextureRef.current.createView(),
                depthClearValue: 1.0,
                depthLoadOp: 'clear',
                depthStoreOp: 'store',
            }
        };

        const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);
        passEncoder.setPipeline(renderPipelineRef.current);
        passEncoder.setBindGroup(0, bindGroupRef.current);
        passEncoder.setVertexBuffer(0, vertexBufferRef.current);
        passEncoder.setIndexBuffer(indexBufferRef.current, 'uint16');
        const gridSize = 180;
        const numTriangles = gridSize * gridSize * 2;
        const numIndices = numTriangles * 3;
        passEncoder.drawIndexed(numIndices);
        passEncoder.end();

        device.queue.submit([commandEncoder.finish()]);
    };

    useEffect(() => {
        let animationFrameId: number;
        const animate = (timestamp: number) => {
            render(timestamp);
            animationFrameId = requestAnimationFrame(animate);
        };
        animationFrameId = requestAnimationFrame(animate);
        return () => {
            cancelAnimationFrame(animationFrameId);
        };
    }, [dimensions]);

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
                    borderRadius: fullScreen ? '0' : '8px',
                    cursor: isDragging ? 'grabbing' : 'grab'
                }}
                onMouseDown={handleMouseDown}
                onMouseMove={handleMouseMove}
                onMouseUp={handleMouseUp}
                onMouseLeave={handleMouseUp}
            />
        </div>
    );
};

function generateGeometry(size: number = 0.15) { // Changed default size
    const vertices: number[] = [];
    const indices: number[] = [];
    const gridSize = 180; // Number of segments

    // Generate vertices for monohedron
    for (let i = 0; i <= gridSize; i++) {
        for (let j = 0; j <= gridSize; j++) {
            const u = (i / gridSize) * 2 * Math.PI;
            const v = (j / gridSize) * 2 * Math.PI;
            
            // Monohedron parametric equations with size parameter
            const x = size * (Math.cos(u) * (3 + Math.cos(v)));
            const y = size * (Math.sin(u) * (3 + Math.cos(v)));
            const z = size * (Math.sin(v) + Math.sin(2 * u));

            // Calculate normal
            const du = [
                -size * Math.sin(u) * (3 + Math.cos(v)),
                size * Math.cos(u) * (3 + Math.cos(v)),
                size * Math.cos(2 * u)
            ];
            const dv = [
                -size * Math.cos(u) * Math.sin(v),
                -size * Math.sin(u) * Math.sin(v),
                size * Math.cos(v)
            ];
            
            // Cross product for normal
            const normal = [
                du[1] * dv[2] - du[2] * dv[1],
                du[2] * dv[0] - du[0] * dv[2],
                du[0] * dv[1] - du[1] * dv[0]
            ];
            
            // Normalize
            const len = Math.sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            normal[0] /= len;
            normal[1] /= len;
            normal[2] /= len;

            // Add vertex data
            vertices.push(
                x, y, z,           // position
                normal[0], normal[1], normal[2]  // normal
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

export default WebGPUMonohedron2;
