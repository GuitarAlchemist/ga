import { useEffect, useRef } from 'react';

interface WebGPUBraceletNotationProps {
    scale: number;  // This is PitchClassSetId value
    size?: number;
}

export const WebGPUCanvas2: React.FC<WebGPUBraceletNotationProps> = ({ scale, size = 600 }) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);

    useEffect(() => {
        const initWebGPU = async () => {
            if (!canvasRef.current) return;

            // Canvas setup
            canvasRef.current.style.width = `${size}px`;
            canvasRef.current.style.height = `${size}px`;
            const devicePixelRatio = window.devicePixelRatio || 1;
            canvasRef.current.width = size * devicePixelRatio;
            canvasRef.current.height = size * devicePixelRatio;

            if (!navigator.gpu) throw new Error('WebGPU not supported');
            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) throw new Error('No adapter found');
            const device = await adapter.requestDevice();
            const context = canvasRef.current.getContext('webgpu');
            if (!context) throw new Error('WebGPU context not found');
            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            
            context.configure({
                device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Create vertices
            const margin = size * 0.1;
            const effectiveSize = size - 2 * margin;
            const radius = effectiveSize * 0.4;
            const center = size / 2;
            const segments = 32;
            
            // Circle vertices
            const circleVertices = new Float32Array((segments + 1) * 2);
            for (let i = 0; i <= segments; i++) {
                const angle = (i / segments) * Math.PI * 2;
                circleVertices[i * 2] = center + radius * Math.cos(angle);
                circleVertices[i * 2 + 1] = center + radius * Math.sin(angle);
            }

            // Convert scale number to binary array
            // PitchClassSetId uses bits 0-11 where bit 0 is C, bit 1 is C#, etc.
            const scaleArray = new Array(12).fill(0);
            for (let i = 0; i < 12; i++) {
                scaleArray[i] = (scale & (1 << i)) ? 1 : 0;
            }
            
            // Create vertices for points (as quads)
            const pointSize = size * 0.02; // Adjust this value to change point size
            const pointVertices = new Float32Array(12 * 6 * 5); // 12 points, 6 vertices per quad (2 triangles), 5 values per vertex (x,y,r,g,b)
            
            for (let i = 0; i < 12; i++) {
                const angle = (i * 30 - 90) * (Math.PI / 180);
                const x = center + radius * Math.cos(angle);
                const y = center + radius * Math.sin(angle);
                const isActive = scaleArray[i];
                
                // Colors for active/inactive notes
                const color = isActive ? [0.0, 0.0, 0.0] : [0.7, 0.7, 0.7];
                
                // Create quad (two triangles) for each point
                const baseIndex = i * 30; // 6 vertices * 5 values per vertex
                
                // First triangle
                pointVertices[baseIndex + 0] = x - pointSize; // x1
                pointVertices[baseIndex + 1] = y - pointSize; // y1
                pointVertices[baseIndex + 2] = color[0];      // r
                pointVertices[baseIndex + 3] = color[1];      // g
                pointVertices[baseIndex + 4] = color[2];      // b

                pointVertices[baseIndex + 5] = x + pointSize; // x2
                pointVertices[baseIndex + 6] = y - pointSize; // y2
                pointVertices[baseIndex + 7] = color[0];
                pointVertices[baseIndex + 8] = color[1];
                pointVertices[baseIndex + 9] = color[2];

                pointVertices[baseIndex + 10] = x + pointSize; // x3
                pointVertices[baseIndex + 11] = y + pointSize; // y3
                pointVertices[baseIndex + 12] = color[0];
                pointVertices[baseIndex + 13] = color[1];
                pointVertices[baseIndex + 14] = color[2];

                // Second triangle
                pointVertices[baseIndex + 15] = x - pointSize; // x4
                pointVertices[baseIndex + 16] = y - pointSize; // y4
                pointVertices[baseIndex + 17] = color[0];
                pointVertices[baseIndex + 18] = color[1];
                pointVertices[baseIndex + 19] = color[2];

                pointVertices[baseIndex + 20] = x + pointSize; // x5
                pointVertices[baseIndex + 21] = y + pointSize; // y5
                pointVertices[baseIndex + 22] = color[0];
                pointVertices[baseIndex + 23] = color[1];
                pointVertices[baseIndex + 24] = color[2];

                pointVertices[baseIndex + 25] = x - pointSize; // x6
                pointVertices[baseIndex + 26] = y + pointSize; // y6
                pointVertices[baseIndex + 27] = color[0];
                pointVertices[baseIndex + 28] = color[1];
                pointVertices[baseIndex + 29] = color[2];
            }

            const circleBuffer = device.createBuffer({
                size: circleVertices.byteLength,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            });
            device.queue.writeBuffer(circleBuffer, 0, circleVertices);

            const pointsBuffer = device.createBuffer({
                size: pointVertices.byteLength,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            });
            device.queue.writeBuffer(pointsBuffer, 0, pointVertices);

            const circleShader = device.createShaderModule({
                code: `
                    @vertex
                    fn vertexMain(@location(0) position: vec2f) -> @builtin(position) vec4f {
                        return vec4f(
                            (position.x / ${size}) * 2.0 - 1.0,
                            -((position.y / ${size}) * 2.0 - 1.0),
                            0.0,
                            1.0
                        );
                    }

                    @fragment
                    fn fragmentMain() -> @location(0) vec4f {
                        return vec4f(0.0, 0.0, 0.0, 1.0);
                    }
                `
            });

            const pointShader = device.createShaderModule({
                code: `
                    struct VertexOutput {
                        @builtin(position) position: vec4f,
                        @location(0) color: vec3f,
                    };

                    @vertex
                    fn vertexMain(
                        @location(0) position: vec2f,
                        @location(1) color: vec3f,
                    ) -> VertexOutput {
                        var output: VertexOutput;
                        output.position = vec4f(
                            (position.x / ${size}) * 2.0 - 1.0,
                            -((position.y / ${size}) * 2.0 - 1.0),
                            0.0,
                            1.0
                        );
                        output.color = color;
                        return output;
                    }

                    @fragment
                    fn fragmentMain(@location(0) color: vec3f) -> @location(0) vec4f {
                        return vec4f(color, 1.0);
                    }
                `
            });

            const circlePipeline = device.createRenderPipeline({
                layout: 'auto',
                vertex: {
                    module: circleShader,
                    entryPoint: 'vertexMain',
                    buffers: [{
                        arrayStride: 8,
                        attributes: [{
                            shaderLocation: 0,
                            offset: 0,
                            format: 'float32x2',
                        }],
                    }],
                },
                fragment: {
                    module: circleShader,
                    entryPoint: 'fragmentMain',
                    targets: [{ format: canvasFormat }],
                },
                primitive: {
                    topology: 'line-strip',
                },
            });

            const pointsPipeline = device.createRenderPipeline({
                layout: 'auto',
                vertex: {
                    module: pointShader,
                    entryPoint: 'vertexMain',
                    buffers: [{
                        arrayStride: 20, // 5 floats per vertex
                        attributes: [
                            {
                                shaderLocation: 0,
                                offset: 0,
                                format: 'float32x2', // position
                            },
                            {
                                shaderLocation: 1,
                                offset: 8,
                                format: 'float32x3', // color
                            }
                        ],
                    }],
                },
                fragment: {
                    module: pointShader,
                    entryPoint: 'fragmentMain',
                    targets: [{ format: canvasFormat }],
                },
                primitive: {
                    topology: 'triangle-list',
                },
            });

            // Render
            const commandEncoder = device.createCommandEncoder();
            const renderPass = commandEncoder.beginRenderPass({
                colorAttachments: [{
                    view: context.getCurrentTexture().createView(),
                    clearValue: { r: 1.0, g: 1.0, b: 1.0, a: 1.0 },
                    loadOp: 'clear',
                    storeOp: 'store',
                }],
            });

            // Draw circle first
            renderPass.setPipeline(circlePipeline);
            renderPass.setVertexBuffer(0, circleBuffer);
            renderPass.draw(segments + 1);

            // Then draw points with larger size
            renderPass.setPipeline(pointsPipeline);
            renderPass.setVertexBuffer(0, pointsBuffer);
            renderPass.draw(72, 1, 0, 0); // 12 points * 6 vertices per point

            renderPass.end();
            device.queue.submit([commandEncoder.finish()]);
        };

        initWebGPU().catch(console.error);
    }, [scale, size]);

    return <canvas ref={canvasRef} style={{ border: '1px solid #ccc', background: 'white' }} />;
};

export default WebGPUCanvas2;