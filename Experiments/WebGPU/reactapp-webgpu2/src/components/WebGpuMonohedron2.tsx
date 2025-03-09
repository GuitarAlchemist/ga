import React, { useEffect, useRef } from 'react';
import { mat4 } from 'gl-matrix';

interface WebGPUMonohedron2Props {
    width?: number;
    height?: number;
}

export const WebGPUMonohedron2: React.FC<WebGPUMonohedron2Props> = ({
                                                                      width = 300,
                                                                      height = 300
                                                                  }) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);

    useEffect(() => {
        const initWebGPU = async () => {
            if (!canvasRef.current) return;

            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) throw new Error('No GPU adapter found');

            const device = await adapter.requestDevice();
            const context = canvasRef.current.getContext('webgpu');
            if (!context) throw new Error('WebGPU context not found');

            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            context.configure({
                device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Generate monohedron vertices
            const vertices: number[] = [];
            const numPoints = 100;
            const radius = 0.7; // Reduced size to fit better in view

            for (let i = 0; i < numPoints; i++) {
                const t = (i / (numPoints - 1)) * 2 * Math.PI;
                const x = radius * Math.cos(t);
                const y = radius * Math.sin(t);
                const z = radius * Math.sin(2 * t) / 2;

                vertices.push(x, y, z);  // position
                vertices.push(
                    Math.sin(t) * 0.5 + 0.5,  // r
                    Math.cos(t) * 0.5 + 0.5,  // g
                    1.0,                      // b
                    1.0                       // a
                );
            }

            const vertexBuffer = device.createBuffer({
                size: vertices.length * Float32Array.BYTES_PER_ELEMENT,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
                mappedAtCreation: true,
            });
            new Float32Array(vertexBuffer.getMappedRange()).set(vertices);
            vertexBuffer.unmap();

            const uniformBufferSize = 64; // 4x4 matrix
            const uniformBuffer = device.createBuffer({
                size: uniformBufferSize,
                usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            });

            const shader = device.createShaderModule({
                code: `
                    struct Uniforms {
                        transform: mat4x4<f32>,
                    }
                    @binding(0) @group(0) var<uniform> uniforms: Uniforms;

                    struct VertexOutput {
                        @builtin(position) position: vec4<f32>,
                        @location(0) color: vec4<f32>,
                    }

                    @vertex
                    fn vs_main(
                        @location(0) position: vec3<f32>,
                        @location(1) color: vec4<f32>
                    ) -> VertexOutput {
                        var output: VertexOutput;
                        output.position = uniforms.transform * vec4<f32>(position, 1.0);
                        output.color = color;
                        return output;
                    }

                    @fragment
                    fn fs_main(@location(0) color: vec4<f32>) -> @location(0) vec4<f32> {
                        return color;
                    }
                `
            });

            const sampleCount = 4;

            // Create multisampled texture
            const msaaTexture = device.createTexture({
                size: [canvasRef.current.width, canvasRef.current.height],
                sampleCount,
                format: canvasFormat,
                usage: GPUTextureUsage.RENDER_ATTACHMENT
            });

            // Create multisampled depth texture
            const msaaDepthTexture = device.createTexture({
                size: [canvasRef.current.width, canvasRef.current.height],
                sampleCount,
                format: 'depth24plus',
                usage: GPUTextureUsage.RENDER_ATTACHMENT
            });

            const pipeline = device.createRenderPipeline({
                layout: 'auto',
                vertex: {
                    module: shader,
                    entryPoint: 'vs_main',
                    buffers: [{
                        arrayStride: 7 * 4, // 3 for position, 4 for color
                        attributes: [
                            { shaderLocation: 0, offset: 0, format: 'float32x3' },  // position
                            { shaderLocation: 1, offset: 3 * 4, format: 'float32x4' }  // color
                        ]
                    }]
                },
                fragment: {
                    module: shader,
                    entryPoint: 'fs_main',
                    targets: [{ format: canvasFormat }]
                },
                primitive: {
                    topology: 'line-strip',
                    cullMode: 'none'
                },
                depthStencil: {
                    depthWriteEnabled: true,
                    depthCompare: 'less',
                    format: 'depth24plus'
                },
                multisample: {
                    count: sampleCount
                }
            });

            const bindGroup = device.createBindGroup({
                layout: pipeline.getBindGroupLayout(0),
                entries: [{
                    binding: 0,
                    resource: { buffer: uniformBuffer }
                }]
            });

            let rotation = 0;

            function render() {
                rotation += 0.01;

                const aspect = width / height;
                const projectionMatrix = mat4.create();
                mat4.perspective(projectionMatrix, Math.PI / 4, aspect, 0.1, 100.0);

                const viewMatrix = mat4.create();
                mat4.lookAt(
                    viewMatrix,
                    new Float32Array([0, 0, 3]), // camera position
                    new Float32Array([0, 0, 0]), // look at point
                    new Float32Array([0, 1, 0])  // up vector
                );
                const modelMatrix = mat4.create();
                mat4.fromRotation(modelMatrix, rotation, new Float32Array([0, 1, 0]));
                
                const mvpMatrix = mat4.create();
                mat4.multiply(mvpMatrix, projectionMatrix, viewMatrix);
                mat4.multiply(mvpMatrix, mvpMatrix, modelMatrix);

                device.queue.writeBuffer(uniformBuffer, 0, mvpMatrix as Float32Array);

                const commandEncoder = device.createCommandEncoder();
                const renderPass = commandEncoder.beginRenderPass({
                    colorAttachments: [{
                        view: msaaTexture.createView(),
                        resolveTarget: context!.getCurrentTexture().createView(),
                        clearValue: { r: 0.1, g: 0.1, b: 0.1, a: 1.0 },
                        loadOp: 'clear',
                        storeOp: 'store'
                    }],
                    depthStencilAttachment: {
                        view: msaaDepthTexture.createView(),
                        depthClearValue: 1.0,
                        depthLoadOp: 'clear',
                        depthStoreOp: 'store'
                    }
                });

                renderPass.setPipeline(pipeline);
                renderPass.setBindGroup(0, bindGroup);
                renderPass.setVertexBuffer(0, vertexBuffer);
                renderPass.draw(numPoints, 1, 0, 0);
                renderPass.end();

                device.queue.submit([commandEncoder.finish()]);
                requestAnimationFrame(render);
            }

            render();
        };

        initWebGPU().catch(console.error);
    }, [width, height]);

    return (
        <canvas
            ref={canvasRef}
            width={width}
            height={height}
            style={{
                width: `${width}px`,
                height: `${height}px`,
                backgroundColor: '#1a1a1a',
                borderRadius: '8px'
            }}
        />
    );
};