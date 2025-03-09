export const BASIC_SHADER = `
    struct Uniforms {
        modelMatrix : mat4x4f,
        vpMatrix : mat4x4f,
    }
    
    @binding(0) @group(0) var<uniform> uniforms : Uniforms;

    struct VertexOutput {
        @builtin(position) position : vec4f,
        @location(0) normal : vec3f,
        @location(1) worldPos : vec3f,
        @location(2) uv : vec2f,
    }

    @vertex
    fn vertexMain(
        @location(0) position: vec3f,
        @location(1) normal: vec3f,
        @location(2) uv: vec2f
    ) -> VertexOutput {
        var output: VertexOutput;
        let worldPos = (uniforms.modelMatrix * vec4f(position, 1.0)).xyz;
        output.position = uniforms.vpMatrix * vec4f(worldPos, 1.0);
        output.normal = (uniforms.modelMatrix * vec4f(normal, 0.0)).xyz;
        output.worldPos = worldPos;
        output.uv = uv;
        return output;
    }

    @fragment
    fn fragmentMain(
        @location(0) normal: vec3f,
        @location(1) worldPos: vec3f,
        @location(2) uv: vec2f
    ) -> @location(0) vec4f {
        let N = normalize(normal);
        let L = normalize(vec3f(1.0, 1.0, 1.0));
        
        let ambient = 0.2;
        let diffuse = max(dot(N, L), 0.0);
        
        let V = normalize(-worldPos);
        let H = normalize(L + V);
        let specular = pow(max(dot(N, H), 0.0), 32.0) * 0.4;
        
        let baseColor = vec3f(0.7, 0.7, 0.7);
        let finalColor = baseColor * (ambient + diffuse) + vec3f(specular);
        return vec4f(finalColor, 1.0);
    }
`;