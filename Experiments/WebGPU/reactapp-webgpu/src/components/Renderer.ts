import {ParticleSystem} from "./ParticleSystem.ts";

export class Renderer {
    private device: GPUDevice | null = null;
    private context: GPUCanvasContext | null = null;
    private vertexBuffer: GPUBuffer | null = null;
    private pipeline: GPURenderPipeline | null = null;

    constructor(private canvas: HTMLCanvasElement) {
        this.initializeWebGPU(this.canvas);
    }

    private async initializeWebGPU(canvas: HTMLCanvasElement) {
        const adapter = await navigator.gpu.requestAdapter();
        if (!adapter) throw new Error('No GPU adapter found');
        
        this.device = await adapter.requestDevice();
        this.context = canvas.getContext('webgpu');
        
        if (!this.context) throw new Error('Failed to get WebGPU context');
        
        const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
        this.context.configure({
            device: this.device,
            format: canvasFormat,
            alphaMode: 'premultiplied',
            usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC
        });

        // Create the render pipeline
        const shader = this.device.createShaderModule({
            code: `
                @vertex
                fn vertexMain(@location(0) position: vec2f) -> @builtin(position) vec4f {
                    return vec4f(position, 0.0, 1.0);
                }

                @fragment
                fn fragmentMain() -> @location(0) vec4f {
                    return vec4f(1.0, 1.0, 1.0, 1.0);
                }
            `
        });

        this.pipeline = this.device.createRenderPipeline({
            layout: 'auto',
            vertex: {
                module: shader,
                entryPoint: 'vertexMain',
                buffers: [{
                    arrayStride: 6 * Float32Array.BYTES_PER_ELEMENT,
                    attributes: [
                        {
                            shaderLocation: 0,
                            offset: 0,
                            format: 'float32x2'
                        }
                    ]
                }]
            },
            fragment: {
                module: shader,
                entryPoint: 'fragmentMain',
                targets: [{
                    format: canvasFormat
                }]
            },
            primitive: {
                topology: 'point-list'
            }
        });
    }

    public render(particleSystem: ParticleSystem): void {
        if (!this.device || !this.context || !this.pipeline) return;  // Add pipeline check

        const vertexData = particleSystem.getVertexData();
        
        // Create or update vertex buffer
        if (!this.vertexBuffer || this.vertexBuffer.size < vertexData.byteLength) {
            this.vertexBuffer?.destroy();
            this.vertexBuffer = this.device.createBuffer({
                size: vertexData.byteLength,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
            });
        }

        this.device.queue.writeBuffer(this.vertexBuffer, 0, vertexData);

        const commandEncoder = this.device.createCommandEncoder();
        const renderPass = commandEncoder.beginRenderPass({
            colorAttachments: [{
                view: this.context.getCurrentTexture().createView(),
                clearValue: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
                loadOp: 'clear',
                storeOp: 'store',
            }]
        });

        renderPass.setPipeline(this.pipeline);  // Now TypeScript knows pipeline is non-null
        renderPass.setVertexBuffer(0, this.vertexBuffer);
        renderPass.draw(vertexData.length / 6);
        renderPass.end();

        this.device.queue.submit([commandEncoder.finish()]);
    }

    public dispose(): void {
        this.device?.destroy();
        this.device = null;
        this.context = null;
        this.vertexBuffer?.destroy();
        this.vertexBuffer = null;
        this.pipeline = null;
    }
}