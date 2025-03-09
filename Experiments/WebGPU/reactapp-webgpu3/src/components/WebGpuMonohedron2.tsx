import React, { useEffect, useRef } from 'react';
import { mat4, vec3 } from 'gl-matrix';

interface WebGPUMonohedronProps {
    width?: number;
    height?: number;
}

// Helper function to generate monohedron geometry
function generateGeometry() {
    const vertices: number[] = [];
    const indices: number[] = [];
    const gridSize = 180; // Number of segments
    const scale = 0.25;   // Scale factor

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
    height = 400
}) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const deviceRef = useRef<GPUDevice | null>(null);
    const contextRef = useRef<GPUCanvasContext | null>(null);

    useEffect(() => {
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

            // Create new device and context
            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) throw new Error('No GPU adapter found');
            
            const device = await adapter.requestDevice();
            deviceRef.current = device;

            const context = canvasRef.current.getContext('webgpu');
            if (!context) throw new Error('WebGPU context not found');
            contextRef.current = context;

            // Configure the context
            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            context.configure({
                device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Generate vertices and indices first
            const { vertices, indices } = generateGeometry();

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

            // Create depth texture
            const depthTexture = device.createTexture({
                size: [width, height],
                format: 'depth24plus',
                usage: GPUTextureUsage.RENDER_ATTACHMENT,
                label: 'Depth Texture'
            });

            // Create uniform buffer
            const uniformBufferSize = 16 * 4 + 16; // mat4x4 (64 bytes) + vec3 padded to 16 bytes
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

            const shaderSource = `
                struct Uniforms {
                    transform: mat4x4<f32>,
                    viewPosition: vec3<f32>,
                }

                @binding(0) @group(0) var<uniform> uniforms: Uniforms;

                struct VertexInput {
                    @location(0) position: vec3<f32>,
                    @location(1) color: vec4<f32>,
                    @location(2) normal: vec3<f32>,
                }

                struct VertexOutput {
                    @builtin(position) position: vec4<f32>,
                    @location(0) color: vec4<f32>,
                    @location(1) normal: vec3<f32>,
                    @location(2) worldPos: vec3<f32>,
                }

                @vertex
                fn vs_main(input: VertexInput) -> VertexOutput {
                    var output: VertexOutput;
                    output.position = uniforms.transform * vec4<f32>(input.position, 1.0);
                    output.color = input.color;
                    output.normal = (uniforms.transform * vec4<f32>(input.normal, 0.0)).xyz;
                    output.worldPos = input.position;
                    return output;
                }

                @fragment
                fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
                    // Basic lighting calculation
                    let N = normalize(input.normal);
                    let L = normalize(vec3<f32>(1.0, 1.0, 1.0)); // Light direction
                    let diffuse = max(dot(N, L), 0.2); // Add ambient light
                    
                    return vec4<f32>(input.color.rgb * diffuse, input.color.a);
                }
            `;

            // Create shader module
            const shader = device.createShaderModule({
                code: shaderSource
            });

            // Create pipeline
            const pipeline = device.createRenderPipeline({
                layout: pipelineLayout,
                vertex: {
                    module: shader,
                    entryPoint: 'vs_main',
                    buffers: [{
                        arrayStride: 10 * 4, // 3 for position, 4 for color, 3 for normal
                        attributes: [
                            { shaderLocation: 0, offset: 0, format: 'float32x3' },  // position
                            { shaderLocation: 1, offset: 3 * 4, format: 'float32x4' },  // color
                            { shaderLocation: 2, offset: 7 * 4, format: 'float32x3' }   // normal
                        ]
                    }]
                },
                fragment: {
                    module: shader,
                    entryPoint: 'fs_main',
                    targets: [{
                        format: canvasFormat
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
                }
            });

            let rotation = 0;

            function render() {
                if (!device || !context) return;

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

                // Update uniform buffer with transform matrix and camera position
                const uniformData = new Float32Array(20); // 16 for matrix + 4 for camera position (with padding)
                uniformData.set(mvpMatrix, 0);
                uniformData.set([cameraPos[0], cameraPos[1], cameraPos[2]], 16);
                device.queue.writeBuffer(uniformBuffer, 0, uniformData);

                const commandEncoder = device.createCommandEncoder();
                const textureView = context.getCurrentTexture().createView();

                const renderPass = commandEncoder.beginRenderPass({
                    colorAttachments: [{
                        view: textureView,
                        clearValue: { r: 0.1, g: 0.1, b: 0.1, a: 1.0 },
                        loadOp: 'clear',
                        storeOp: 'store'
                    }],
                    depthStencilAttachment: {
                        view: depthTexture.createView(),
                        depthClearValue: 1.0,
                        depthLoadOp: 'clear',
                        depthStoreOp: 'store',
                    }
                });

                renderPass.setPipeline(pipeline);
                renderPass.setBindGroup(0, bindGroup);
                renderPass.setVertexBuffer(0, vertexBuffer);
                renderPass.setIndexBuffer(indexBuffer, 'uint16');
                renderPass.drawIndexed(indices.length);
                renderPass.end();

                device.queue.submit([commandEncoder.finish()]);
                requestAnimationFrame(render);
            }

            requestAnimationFrame(render);
        };

        initWebGPU();

        // Cleanup
        return () => {
            if (contextRef.current) {
                contextRef.current.unconfigure();
            }
            if (deviceRef.current) {
                deviceRef.current.destroy();
            }
        };
    }, [width, height]); // Reinitialize when dimensions change

    return <canvas ref={canvasRef} width={width} height={height} />;
};

export default WebGPUMonohedron2;
