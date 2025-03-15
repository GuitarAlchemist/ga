struct Uniforms {
    modelViewProjectionMatrix: mat4x4<f32>,
    cameraPosition: vec3<f32>,
    time: f32,
    size: f32,
    bumpiness: f32,
}

@binding(0) @group(0) var<uniform> uniforms: Uniforms;

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) normal: vec3<f32>,
    @location(1) worldPos: vec3<f32>,
    @location(2) furLayer: f32,
    @location(3) random: f32,
}

@vertex
fn vertexMain(
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) furLayer: f32,
    @location(3) random: f32,
) -> VertexOutput {
    var output: VertexOutput;
    
    var pos = position;
    if (furLayer > 0.0) {
        var wind = sin(uniforms.time * 0.001 + random * 6.28) * 0.02;
        pos += normal * (wind * furLayer);
    }
    
    output.position = uniforms.modelViewProjectionMatrix * vec4<f32>(pos, 1.0);
    output.normal = normal;
    output.worldPos = pos;
    output.furLayer = furLayer;
    output.random = random;
    return output;
}

@fragment
fn fragmentMain(
    @location(0) normal: vec3<f32>,
    @location(1) worldPos: vec3<f32>,
    @location(2) furLayer: f32,
    @location(3) random: f32,
) -> @location(0) vec4<f32> {
    var lightDir = normalize(vec3<f32>(1.0, 1.0, 1.0));
    var viewDir = normalize(uniforms.cameraPosition - worldPos);
    var N = normalize(normal);
    
    var baseColor = vec3<f32>(0.0, 1.0, 0.0);
    var furColor = mix(baseColor, vec3<f32>(0.2, 1.0, 0.0), furLayer);
    
    var alpha = select(1.0, smoothstep(1.0, 0.0, furLayer), furLayer > 0.0);
    
    var diffuse = max(dot(N, lightDir), 0.0);
    var ambient = 0.2;
    
    var finalColor = furColor * (ambient + diffuse);
    finalColor *= (0.9 + random * 0.2);
    
    return vec4<f32>(finalColor, alpha);
}