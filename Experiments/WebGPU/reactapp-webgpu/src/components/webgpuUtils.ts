export async function initializeWebGPU(canvas: HTMLCanvasElement) {
    const adapter = await navigator.gpu?.requestAdapter();
    if (!adapter) {
        throw new Error('No GPU adapter found');
    }

    const device = await adapter.requestDevice();
    const context = canvas.getContext('webgpu');
    if (!context) {
        throw new Error('WebGPU not supported');
    }

    const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
    context.configure({
        device,
        format: canvasFormat,
        alphaMode: 'premultiplied',
    });

    const depthTexture = device.createTexture({
        size: {
            width: canvas.width,
            height: canvas.height,
        },
        format: 'depth24plus',
        usage: GPUTextureUsage.RENDER_ATTACHMENT,
    });

    return {
        device,
        context,
        canvasFormat,
        depthTexture
    };
}

export function createBuffer(
    device: GPUDevice,
    data: Float32Array | Uint16Array,
    usage: GPUBufferUsageFlags
) {
    const buffer = device.createBuffer({
        size: data.byteLength,
        usage: usage,
        mappedAtCreation: true,
    });
    new (data.constructor as typeof Float32Array)(buffer.getMappedRange()).set(data);
    buffer.unmap();
    return buffer;
}

export function createSphereGeometry(radius: number, segments: number) {
    const vertices: number[] = [];
    const indices: number[] = [];

    for (let lat = 0; lat <= segments; lat++) {
        const theta = lat * Math.PI / segments;
        const sinTheta = Math.sin(theta);
        const cosTheta = Math.cos(theta);

        for (let lon = 0; lon <= segments; lon++) {
            const phi = lon * 2 * Math.PI / segments;
            const sinPhi = Math.sin(phi);
            const cosPhi = Math.cos(phi);

            const x = cosPhi * sinTheta;
            const y = cosTheta;
            const z = sinPhi * sinTheta;

            // Position
            vertices.push(radius * x);
            vertices.push(radius * y);
            vertices.push(radius * z);

            // Normal
            vertices.push(x);
            vertices.push(y);
            vertices.push(z);

            // UV
            vertices.push(lon / segments);
            vertices.push(lat / segments);
        }
    }

    for (let lat = 0; lat < segments; lat++) {
        for (let lon = 0; lon < segments; lon++) {
            const first = (lat * (segments + 1)) + lon;
            const second = first + segments + 1;

            indices.push(first);
            indices.push(second);
            indices.push(first + 1);

            indices.push(second);
            indices.push(second + 1);
            indices.push(first + 1);
        }
    }

    return {
        vertices: new Float32Array(vertices),
        indices: new Uint16Array(indices)
    };
}