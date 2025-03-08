import { useEffect, useRef } from 'react';

export const WebGPUCanvas = () => {
    const canvasRef = useRef<HTMLCanvasElement>(null);

    useEffect(() => {
        const initWebGPU = async () => {
            if (!canvasRef.current) return;

            if (!navigator.gpu) {
                throw new Error('WebGPU not supported');
            }

            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) {
                throw new Error('No adapter found');
            }

            const device = await adapter.requestDevice();
            const context = canvasRef.current.getContext('webgpu');
            if (!context) {
                throw new Error('WebGPU context not found');
            }

            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            context.configure({
                device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Clear the canvas with a blue color
            const commandEncoder = device.createCommandEncoder();
            const textureView = context.getCurrentTexture().createView();

            const renderPassDescriptor: GPURenderPassDescriptor = {
                colorAttachments: [{
                    view: textureView,
                    clearValue: { r: 0.0, g: 0.0, b: 1.0, a: 1.0 },
                    loadOp: 'clear',
                    storeOp: 'store',
                }],
            };

            const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);
            passEncoder.end();

            device.queue.submit([commandEncoder.finish()]);
        };

        initWebGPU().catch(console.error);
    }, []);

    return <canvas ref={canvasRef} width={800} height={600} />;
};