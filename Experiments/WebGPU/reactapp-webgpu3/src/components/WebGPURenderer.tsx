import { useEffect, useRef } from 'react';
import shaderSource from './shaders.wgsl';

// Define the type for our custom event
interface ShaderUpdateEvent extends CustomEvent {
    detail: {
        path: string;
        code: string;
    };
}

export function WebGPURenderer() {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const pipelineRef = useRef<GPURenderPipeline | null>(null);
    const contextRef = useRef<GPUCanvasContext | null>(null);
    
    const createPipeline = async (device: GPUDevice, format: GPUTextureFormat, shaderCode: string) => {
        const shaderModule = device.createShaderModule({
            code: shaderCode
        });

        return device.createRenderPipeline({
            layout: 'auto',
            vertex: {
                module: shaderModule,
                entryPoint: 'vertexMain',
                buffers: [] // Add your vertex buffer layouts here
            },
            fragment: {
                module: shaderModule,
                entryPoint: 'fragmentMain',
                targets: [{
                    format: format
                }]
            },
            primitive: {
                topology: 'triangle-list'
            }
        });
    };

    useEffect(() => {
        let device: GPUDevice;
        let animationFrameId: number;
        let cleanup: (() => void) | undefined;

        const init = async () => {
            if (!canvasRef.current) return;

            // Initialize WebGPU
            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) throw new Error('No adapter found');
            
            device = await adapter.requestDevice();
            const context = canvasRef.current.getContext('webgpu');
            if (!context) throw new Error('No WebGPU context');

            contextRef.current = context;
            
            const format = navigator.gpu.getPreferredCanvasFormat();
            context.configure({
                device,
                format,
                alphaMode: 'premultiplied',
            });

            // Create initial pipeline
            pipelineRef.current = await createPipeline(device, format, shaderSource);

            // Set up shader hot reload handler
            const handleShaderUpdate = (event: Event) => {
                const customEvent = event as ShaderUpdateEvent;
                if (customEvent.detail.path.endsWith('shaders.wgsl')) {
                    console.log('Reloading shader...');
                    createPipeline(device, format, customEvent.detail.code)
                        .then(newPipeline => {
                            pipelineRef.current = newPipeline;
                        })
                        .catch(error => {
                            console.error('Error reloading shader:', error);
                        });
                }
            };

            window.addEventListener('shader-update', handleShaderUpdate);

            const render = () => {
                if (!contextRef.current || !pipelineRef.current) return;

                const commandEncoder = device.createCommandEncoder();
                const textureView = contextRef.current.getCurrentTexture().createView();

                const renderPassDescriptor: GPURenderPassDescriptor = {
                    colorAttachments: [{
                        view: textureView,
                        clearValue: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
                        loadOp: 'clear',
                        storeOp: 'store',
                    }]
                };

                const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);
                passEncoder.setPipeline(pipelineRef.current);
                // Add your draw calls here
                passEncoder.end();

                device.queue.submit([commandEncoder.finish()]);
                animationFrameId = requestAnimationFrame(render);
            };

            render();

            cleanup = () => {
                window.removeEventListener('shader-update', handleShaderUpdate);
                if (animationFrameId) {
                    cancelAnimationFrame(animationFrameId);
                }
            };
        };

        init().catch(console.error);

        return () => {
            cleanup?.();
        };
    }, []);

    return (
        <canvas
            ref={canvasRef}
            width={800}
            height={600}
            style={{
                width: '800px',
                height: '600px',
                backgroundColor: '#1a1a1a',
                borderRadius: '8px'
            }}
        />
    );
}