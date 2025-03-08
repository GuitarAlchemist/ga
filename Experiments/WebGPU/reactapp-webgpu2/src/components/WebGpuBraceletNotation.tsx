import { useEffect, useRef } from 'react';

interface WebGPUBraceletNotationProps {
    scale: number;  // This is PitchClassSetId value
    size?: number;
    colorTones?: { [key: number]: string };
}

export const WebGpuBraceletNotation: React.FC<WebGPUBraceletNotationProps> = ({ 
    scale, 
    size = 600,
    colorTones = {} 
}) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const textCanvasRef = useRef<HTMLCanvasElement>(null); // New canvas for text
    const animationFrameRef = useRef<number>();
    const startTimeRef = useRef<number>(Date.now());

    useEffect(() => {
        // Create and position text canvas overlay
        const textCanvas = document.createElement('canvas');
        textCanvas.style.position = 'absolute';
        textCanvas.style.left = canvasRef.current?.offsetLeft + 'px';
        textCanvas.style.top = canvasRef.current?.offsetTop + 'px';
        textCanvas.style.width = `${size}px`;
        textCanvas.style.height = `${size}px`;
        textCanvas.width = size;
        textCanvas.height = size;
        textCanvas.style.pointerEvents = 'none';
        canvasRef.current?.parentElement?.appendChild(textCanvas);
        textCanvasRef.current = textCanvas;

        // Function to draw text labels
        const drawColorToneLabels = () => {
            const ctx = textCanvas.getContext('2d');
            if (!ctx) return;

            ctx.clearRect(0, 0, size, size);
            ctx.font = `${size * 0.03}px Arial`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = '#1E90FF'; // Match the blue color of color tones

            const radius = size * 0.4;
            const center = size / 2;
            const labelRadius = radius + size * 0.08; // Position labels slightly outside the points

            for (let i = 0; i < 12; i++) {
                if (i in colorTones) {
                    const angle = (i * 30 - 90) * (Math.PI / 180);
                    const x = center + labelRadius * Math.cos(angle);
                    const y = center + labelRadius * Math.sin(angle);
                    
                    ctx.fillText(colorTones[i], x, y);
                }
            }
        };

        // Initial draw of labels
        drawColorToneLabels();

        // Cleanup
        return () => {
            textCanvas.remove();
        };
    }, [scale, size, colorTones]);

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
            const circleSegments = 32; // Renamed from segments

            // Circle vertices
            const circleVertices = new Float32Array((circleSegments + 1) * 2);
            for (let i = 0; i <= circleSegments; i++) {
                const angle = (i / circleSegments) * Math.PI * 2;
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
            const pointSize = size * 0.04; // Doubled the size
            const pointSegments = 32; // Number of segments in each point circle
            const pointVertices = new Float32Array(12 * pointSegments * 3 * 5); // 12 points, pointSegments * 3 vertices per triangle, 5 values per vertex

            for (let i = 0; i < 12; i++) {
                const angle = (i * 30 - 90) * (Math.PI / 180);
                const centerX = center + radius * Math.cos(angle);
                const centerY = center + radius * Math.sin(angle);
                const isActive = (scale & (1 << i)) !== 0;
                
                // Determine color based on whether it's a color tone
                let color: [number, number, number];
                if (!isActive) {
                    color = [0.7, 0.7, 0.7]; // Inactive notes stay gray
                } else if (i in colorTones) {
                    color = [0.2, 0.6, 1.0]; // Color tones in blue
                } else {
                    color = [0.0, 0.0, 0.0]; // Regular active notes in black
                }

                // Generate circle vertices
                for (let j = 0; j < pointSegments; j++) {
                    const baseIndex = (i * pointSegments * 3 + j * 3) * 5;
                    const startAngle = (j / pointSegments) * Math.PI * 2;
                    const endAngle = ((j + 1) / pointSegments) * Math.PI * 2;

                    // Center vertex
                    pointVertices[baseIndex] = centerX;
                    pointVertices[baseIndex + 1] = centerY;
                    pointVertices[baseIndex + 2] = color[0];
                    pointVertices[baseIndex + 3] = color[1];
                    pointVertices[baseIndex + 4] = color[2];

                    // First outer vertex
                    pointVertices[baseIndex + 5] = centerX + pointSize * Math.cos(startAngle);
                    pointVertices[baseIndex + 6] = centerY + pointSize * Math.sin(startAngle);
                    pointVertices[baseIndex + 7] = color[0];
                    pointVertices[baseIndex + 8] = color[1];
                    pointVertices[baseIndex + 9] = color[2];

                    // Second outer vertex
                    pointVertices[baseIndex + 10] = centerX + pointSize * Math.cos(endAngle);
                    pointVertices[baseIndex + 11] = centerY + pointSize * Math.sin(endAngle);
                    pointVertices[baseIndex + 12] = color[0];
                    pointVertices[baseIndex + 13] = color[1];
                    pointVertices[baseIndex + 14] = color[2];
                }
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

            const render = () => {
                const currentTime = Date.now();
                const elapsedTime = (currentTime - startTimeRef.current) / 1000; // Convert to seconds
                const basePointSize = size * 0.04;
                
                // Create point vertices with animation
                const pointVertices = new Float32Array(12 * pointSegments * 3 * 5);

                for (let i = 0; i < 12; i++) {
                    const angle = (i * 30 - 90) * (Math.PI / 180);
                    const centerX = center + radius * Math.cos(angle);
                    const centerY = center + radius * Math.sin(angle);
                    const isActive = (scale & (1 << i)) !== 0;
                    
                    // Determine color based on whether it's a color tone
                    let color: [number, number, number];
                    if (!isActive) {
                        color = [0.7, 0.7, 0.7]; // Inactive notes stay gray
                    } else if (i in colorTones) {
                        color = [0.2, 0.6, 1.0]; // Color tones in blue
                    } else {
                        color = [0.0, 0.0, 0.0]; // Regular active notes in black
                    }

                    // Animate size only for active circles
                    const animatedSize = isActive 
                        ? basePointSize * (1 + 0.2 * Math.sin(elapsedTime * 3))
                        : basePointSize;

                    for (let j = 0; j < pointSegments; j++) {
                        const baseIndex = (i * pointSegments * 3 + j * 3) * 5;
                        const startAngle = (j / pointSegments) * Math.PI * 2;
                        const endAngle = ((j + 1) / pointSegments) * Math.PI * 2;

                        // Center vertex
                        pointVertices[baseIndex] = centerX;
                        pointVertices[baseIndex + 1] = centerY;
                        pointVertices[baseIndex + 2] = color[0];
                        pointVertices[baseIndex + 3] = color[1];
                        pointVertices[baseIndex + 4] = color[2];

                        // First outer vertex
                        pointVertices[baseIndex + 5] = centerX + animatedSize * Math.cos(startAngle);
                        pointVertices[baseIndex + 6] = centerY + animatedSize * Math.sin(startAngle);
                        pointVertices[baseIndex + 7] = color[0];
                        pointVertices[baseIndex + 8] = color[1];
                        pointVertices[baseIndex + 9] = color[2];

                        // Second outer vertex
                        pointVertices[baseIndex + 10] = centerX + animatedSize * Math.cos(endAngle);
                        pointVertices[baseIndex + 11] = centerY + animatedSize * Math.sin(endAngle);
                        pointVertices[baseIndex + 12] = color[0];
                        pointVertices[baseIndex + 13] = color[1];
                        pointVertices[baseIndex + 14] = color[2];
                    }
                }

                device.queue.writeBuffer(pointsBuffer, 0, pointVertices);

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
                renderPass.draw(circleSegments + 1);

                // Then draw points
                renderPass.setPipeline(pointsPipeline);
                renderPass.setVertexBuffer(0, pointsBuffer);
                renderPass.draw(12 * pointSegments * 3, 1, 0, 0);

                renderPass.end();
                device.queue.submit([commandEncoder.finish()]);

                animationFrameRef.current = requestAnimationFrame(render);
            };

            render();
        };

        initWebGPU().catch(console.error);

        return () => {
            if (animationFrameRef.current) {
                cancelAnimationFrame(animationFrameRef.current);
            }
        };
    }, [scale, size]);

    return (
        <div style={{ position: 'relative', width: size, height: size }}>
            <canvas ref={canvasRef} />
        </div>
    );
};

export default WebGpuBraceletNotation;
