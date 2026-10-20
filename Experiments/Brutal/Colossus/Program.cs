// A BRUTAL showcase for the evaluation (GuitarAlchemist/gaia#257). An original scene: a lone figure stands at the end
// of a walkway in a city of dark towers, and a swarm of machines, each one a Jolt rigid body, circles above, then
// gathers into a colossal head that looks at the figure and speaks. The towers are modelled in Blender and loaded with
// BRUTAL's glTF reader; everything else is an instance of one cube. Vulkan draws it all in one draw call per tower
// variant plus one; a Dear ImGui panel with an ImPlot graph shows the timings and steers the swarm.
// The scene renders in HDR with 4x MSAA and reversed depth; a second pass adds the sky, a bloom built from the image's own
// mips, ACES tone mapping, a vignette and grain, then the panel.
// Keys: Space assembles or disperses, C changes the camera, a drag turns it, the wheel zooms, Esc quits.
// Arguments: --swarm N (bodies, default 10000), --seconds T (time limit, 0 = none), --immediate (no vsync),
// --no-rtx (no ray queries), --rtx-off (ray queries unticked at start), --images N (swapchain images, default 2),
// --capture T1,T2,... (save the window's image at those seconds, as
// colossus-<T>s.png in the working directory).

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Brutal;
using Brutal.GlfwApi;
using Brutal.GltfApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.ImPlotApi;
using Brutal.JoltApi;
using Brutal.Numerics;
using Brutal.Pointers;
using Brutal.ShaderCApi;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using SN = System.Numerics;

return Colossus.Run(args);

[StructLayout(LayoutKind.Sequential)]
// One mesh vertex; Material picks a building's surface (0 concrete, 1 glass, 2 metal, 3 lamp) and is 0 on the cube.
internal struct MeshVertex(SN.Vector3 pos, SN.Vector3 normal, float material = 0f)
{
    public SN.Vector3 Pos = pos;
    public SN.Vector3 Normal = normal;
    public float Material = material;
}

// One drawn instance: its model matrix, its albedo and, in Color.W, how much it glows; Extra.X is its kind (0 plain,
// 1 machine, 2 box tower, 3 lamp, 4 modelled tower) and Extra.Y a seed.
[StructLayout(LayoutKind.Sequential)]
internal struct Drawn(SN.Matrix4x4 model, SN.Vector4 color, SN.Vector4 extra = default)
{
    public SN.Matrix4x4 Model = model;
    public SN.Vector4 Color = color;
    public SN.Vector4 Extra = extra;
}

// Both passes' constants: the scene's view-projection or the sky's inverse, the eye and the time, the sun's direction and
// the exposure, then the bloom.
[StructLayout(LayoutKind.Sequential)]
internal struct Push
{
    public SN.Matrix4x4 Matrix;
    public SN.Vector4 Eye;
    public SN.Vector4 Sun;
    public SN.Vector4 Params;
}

internal static unsafe class Colossus
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int FramesInFlight = 2;
    private const int MaxInstances = 40_000;
    private const int History = 300;
    private const int ApiVersion13 = (1 << 22) | (3 << 12);
    private const VkFormat HdrFormat = VkFormat.R16G16B16A16SFloat;
    private const VkFormat DepthFormat = VkFormat.D32SFloat;
    private const VkSampleCountFlags Msaa = VkSampleCountFlags._4Bit;
    private const int BloomLevels = 7;
    private const float Near = 0.1f;

    // The face: its centre, its half width and half height, in metres. It looks down the walkway (+z).
    private static readonly SN.Vector3 FaceCenter = new(0f, 24f, -2f);
    private const float FaceA = 13f;
    private const float FaceB = 18f;
    private const float FigureZ = 40f;

    private static readonly SN.Vector3 Metal = new(0.05f, 0.045f, 0.04f);
    private static readonly SN.Vector3 Amber = new(1.0f, 0.55f, 0.18f);

    // A low sun behind the face, right of it as seen from the walkway.
    private static readonly SN.Vector3 SunDir = SN.Vector3.Normalize(new SN.Vector3(0.42f, 0.2f, -0.88f));

    // The sky over the machine city, shared by both passes: a dark zenith, a burning horizon, an abyss that glows below,
    // bands of smog, and the low sun with its halo.
    private const string SkyGlsl = """

        float hash12(vec2 p)
        {
            vec3 p3 = fract(vec3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return fract((p3.x + p3.y) * p3.z);
        }
        float noise(vec2 p)
        {
            vec2 i = floor(p);
            vec2 f = fract(p);
            f = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), f.x), f.y);
        }
        vec3 sky(vec3 d, vec3 sun, float t)
        {
            float h = d.y;
            vec3 c = h >= 0.0
                ? mix(vec3(0.50, 0.22, 0.07), vec3(0.010, 0.008, 0.014), pow(clamp(h, 0.0, 1.0), 0.42))
                : mix(vec3(0.50, 0.22, 0.07), vec3(0.12, 0.05, 0.018), pow(clamp(-h, 0.0, 1.0), 0.35));
            vec2 q = d.xz / (abs(h) + 0.18);
            float smog = noise(q * 1.7 + vec2(t * 0.01, 0.0)) * 0.6 + noise(q * 4.3 - vec2(0.0, t * 0.015)) * 0.4;
            float band = smoothstep(0.35, 0.8, smog) * exp(-abs(h) * 5.0);
            float s = max(dot(d, sun), 0.0);
            c = mix(c, c * 0.45, band * 0.7) + vec3(1.0, 0.45, 0.15) * band * pow(s, 4.0) * 0.6;
            c += vec3(1.0, 0.52, 0.2) * (pow(s, 5.0) * 0.55 + pow(s, 48.0) * 1.6);
            c += vec3(1.0, 0.86, 0.66) * smoothstep(0.99965, 0.9999, s) * 40.0;
            return c;
        }

        """;

    private const string VertexShader = """
        #version 450
        layout(location = 0) in vec3 inPos;
        layout(location = 1) in vec3 inNormal;
        layout(location = 2) in vec4 iModel0;
        layout(location = 3) in vec4 iModel1;
        layout(location = 4) in vec4 iModel2;
        layout(location = 5) in vec4 iModel3;
        layout(location = 6) in vec4 iColor;
        layout(location = 7) in vec4 iExtra;
        layout(location = 8) in float inMaterial;
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; } pc;
        layout(location = 0) out vec3 vWorld;
        layout(location = 1) out vec3 vNormal;
        layout(location = 2) out vec4 vColor;
        layout(location = 3) out vec3 vUnit;
        layout(location = 4) flat out vec3 vScale;
        layout(location = 5) flat out vec3 vUnitNormal;
        layout(location = 6) flat out vec4 vExtra;
        layout(location = 7) flat out vec3 vTangent;
        layout(location = 8) flat out vec3 vBitangent;
        layout(location = 9) flat out float vMaterial;
        void main()
        {
            mat4 model = mat4(iModel0, iModel1, iModel2, iModel3);
            vec4 world = model * vec4(inPos, 1.0);
            gl_Position = pc.viewProj * world;
            vWorld = world.xyz;
            vNormal = normalize(transpose(inverse(mat3(model))) * inNormal);
            vColor = iColor;
            vUnit = inPos;
            vScale = vec3(length(iModel0.xyz), length(iModel1.xyz), length(iModel2.xyz));
            vUnitNormal = inNormal;
            vExtra = iExtra;
            vMaterial = inMaterial;
            // The face's texture axes, in the same order as the fragment shader's box mapping.
            vTangent = normalize(mat3(model) * (abs(inNormal.x) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0)));
            vBitangent = normalize(mat3(model) * (abs(inNormal.y) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0)));
        }
        """;

    // The scene in HDR: the low sun, a warm bounce from the abyss, a cool fill from the city, a backlit rim, the sun's
    // highlight and the sky's reflection on metal. Machines and tower sides are box-mapped with the surface textures
    // (albedo, then normal, roughness and height): machines get a pulsing eye, towers panels of which a few are lit.
    // Then the haze, coloured by the sky. Alpha marks geometry, so that the second pass puts the sky only where nothing
    // was drawn.
    private const string FragmentShader = "#version 450\n" + SkyGlsl + """
        layout(location = 0) in vec3 vWorld;
        layout(location = 1) in vec3 vNormal;
        layout(location = 2) in vec4 vColor;
        layout(location = 3) in vec3 vUnit;
        layout(location = 4) flat in vec3 vScale;
        layout(location = 5) flat in vec3 vUnitNormal;
        layout(location = 6) flat in vec4 vExtra;
        layout(location = 7) flat in vec3 vTangent;
        layout(location = 8) flat in vec3 vBitangent;
        layout(location = 9) flat in float vMaterial;
        layout(set = 0, binding = 0) uniform sampler2DArray surfaces;
        #ifdef RTX
        layout(set = 0, binding = 1) uniform accelerationStructureEXT scene;
        #endif
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; } pc;
        layout(location = 0) out vec4 outColor;
        const vec3 SunColor = vec3(1.0, 0.56, 0.26);
        vec3 bumped(vec4 map, vec3 n, float strength)
        {
            vec2 d = (map.rg * 2.0 - 1.0) * strength;
            return normalize(n + vTangent * d.x + vBitangent * d.y);
        }
        void main()
        {
            vec3 n = normalize(vNormal);
            vec3 toEye = pc.eye.xyz - vWorld;
            float dist = length(toEye);
            vec3 v = toEye / dist;
            vec3 sun = pc.sun.xyz;
            float t = pc.eye.w;
            float kind = vExtra.x;
            vec3 albedo = vColor.rgb;
            vec3 emit = albedo * vColor.a;
            float shine = 0.0;
            float rough = 0.8;
            // Modelled towers map in their own metres, so that window cells keep to the floors at any scale.
            vec3 p = kind > 3.5 ? vUnit : vUnit * vScale;
            vec2 st = vec2(dot(p, abs(vUnitNormal.x) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0)),
                dot(p, abs(vUnitNormal.y) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0)));
            if (kind > 0.5 && kind < 1.5)
            {
                vec2 uv = st / 1.2 + vExtra.y * vec2(7.31, 3.17);
                vec4 map = texture(surfaces, vec3(uv, 1.0));
                albedo = pow(texture(surfaces, vec3(uv, 0.0)).rgb, vec3(2.2)) * 0.45;
                n = bumped(map, n, 2.0);
                rough = map.b;
                shine = 1.0;
                float eye = (1.0 - smoothstep(0.18, 0.32, length(vUnit.xy))) * step(0.49, vUnit.z);
                emit += vec3(1.0, 0.3, 0.06) * eye * (2.5 + 1.5 * sin(t * 6.0 + vExtra.y * 40.0));
            }
            else if (kind > 1.5 && kind < 2.5 && abs(vUnitNormal.y) < 0.5)
            {
                // One texture tile is 8 by 4 panels of 1.6 by 3 m; a few panels are lit, their glass streaked by the
                // texture. Where a panel shrinks under two pixels, the lights fade to their average: no shimmer far away.
                vec2 cell = vec2(st.x / 1.6, st.y / 3.0);
                vec2 f = fract(cell);
                vec2 uv = vec2(cell.x / 8.0, -cell.y / 4.0);
                vec4 surface = texture(surfaces, vec3(uv, 2.0));
                vec4 map = texture(surfaces, vec3(uv, 3.0));
                float window = step(0.1, f.x) * step(f.x, 0.9) * step(0.08, f.y) * step(f.y, 0.92);
                float r = hash12(floor(cell) + vec2(vExtra.y * 97.0, dot(vUnitNormal, vec3(1.0, 2.0, 3.0)) * 13.0));
                float lit = step(r, 0.08);
                vec3 lamp = mix(vec3(1.0, 0.42, 0.12), vec3(1.0, 0.7, 0.4), fract(r * 9.1)) * 2.2;
                float fade = clamp(1.0 - (max(fwidth(cell.x), fwidth(cell.y)) - 0.25) * 4.0, 0.0, 1.0);
                emit += mix(vec3(1.0, 0.56, 0.26) * 0.1, window * lit * lamp * (0.45 + 0.8 * surface.r), fade);
                albedo = pow(surface.rgb, vec3(2.2)) * 0.25;
                n = bumped(map, n, 3.0);
                rough = map.b;
                shine = 0.6;
            }
            else if (kind > 3.5)
            {
                // A modelled tower: concrete with the facade texture, glass with lit cells of 1.6 by 3.2 m (one floor),
                // metal with the hull texture, and amber lamps that breathe.
                float mat = vMaterial;
                if (mat < 0.5)
                {
                    vec2 uv = vec2(st.x / 12.8, -st.y / 12.8) + vExtra.y * vec2(3.7, 1.3);
                    vec4 map = texture(surfaces, vec3(uv, 3.0));
                    albedo = pow(texture(surfaces, vec3(uv, 2.0)).rgb, vec3(2.2)) * 0.32;
                    n = bumped(map, n, 3.0);
                    rough = map.b;
                    shine = 0.35;
                }
                else if (mat < 1.5)
                {
                    vec2 cell = vec2(st.x / 1.6, st.y / 3.2);
                    vec2 f = fract(cell);
                    float pane = step(0.06, f.x) * step(f.x, 0.94);
                    float r = hash12(floor(cell) + vec2(vExtra.y * 97.0, dot(vUnitNormal, vec3(1.0, 2.0, 3.0)) * 13.0));
                    float lit = step(r, 0.1);
                    vec3 lamp = mix(vec3(1.0, 0.42, 0.12), vec3(1.0, 0.72, 0.42), fract(r * 9.1)) * 2.4;
                    float fade = clamp(1.0 - (max(fwidth(cell.x), fwidth(cell.y)) - 0.25) * 4.0, 0.0, 1.0);
                    emit += mix(vec3(1.0, 0.56, 0.26) * 0.12, pane * lit * lamp, fade);
                    albedo = vec3(0.02, 0.022, 0.025);
                    rough = 0.12;
                    shine = 1.0;
                }
                else if (mat < 2.5)
                {
                    vec2 uv = st / 2.4 + vExtra.y * vec2(5.1, 2.9);
                    vec4 map = texture(surfaces, vec3(uv, 1.0));
                    albedo = pow(texture(surfaces, vec3(uv, 0.0)).rgb, vec3(2.2)) * 0.35;
                    n = bumped(map, n, 2.0);
                    rough = map.b;
                    shine = 1.0;
                }
                else
                {
                    emit = vec3(1.0, 0.45, 0.12) * (4.0 + 2.0 * sin(t * 2.0 + vExtra.y * 31.0));
                }
            }

            // RTX: one ray toward the sun for its shadow; on glass and metal, one along the mirror direction, whose hit
            // replaces the sky in the reflection: a dark facade or a lamp, hazed by its distance.
            float shadow = 1.0;
            vec3 traced = vec3(-1.0);
            #ifdef RTX
            if (pc.params.y > 0.5)
            {
                vec3 origin = vWorld + normalize(vNormal) * 0.04;
                rayQueryEXT q;
                // The swarm shades the city, the walkway and the figure, but not itself: its machines would hide each other's
                // sunlight and the head would read as a silhouette.
                uint casters = kind > 0.5 && kind < 1.5 ? 0x01u : 0x05u;
                rayQueryInitializeEXT(q, scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT, casters, origin, 0.02, sun, 800.0);
                while (rayQueryProceedEXT(q)) {}
                if (rayQueryGetIntersectionTypeEXT(q, true) != gl_RayQueryCommittedIntersectionNoneEXT)
                {
                    shadow = 0.0;
                }

                if (shine > 0.5)
                {
                    vec3 r = reflect(-v, n);
                    rayQueryEXT m;
                    rayQueryInitializeEXT(m, scene, gl_RayFlagsOpaqueEXT, 0xFF, origin, 0.05, r, 900.0);
                    while (rayQueryProceedEXT(m)) {}
                    if (rayQueryGetIntersectionTypeEXT(m, true) != gl_RayQueryCommittedIntersectionNoneEXT)
                    {
                        float d = rayQueryGetIntersectionTEXT(m, true);
                        bool lamp = rayQueryGetIntersectionInstanceCustomIndexEXT(m, true) == 1;
                        vec3 hit = lamp ? vec3(1.0, 0.45, 0.12) * 4.0 : vec3(0.025, 0.02, 0.016) + SunColor * 0.04;
                        traced = mix(hit, sky(r, sun, t) * 0.55, 1.0 - exp(-d * 0.0028));
                    }
                }
            }
            #endif
            float ndl = max(dot(n, sun), 0.0) * shadow;
            vec3 ambient = mix(vec3(0.07, 0.035, 0.015), vec3(0.035, 0.035, 0.045), n.y * 0.5 + 0.5);
            vec3 fill = vec3(0.16, 0.17, 0.22) * max(dot(n, normalize(vec3(-0.3, 0.45, 1.0))), 0.0);
            float back = pow(1.0 - max(dot(n, v), 0.0), 4.0) * pow(max(dot(-v, sun), 0.0), 3.0);
            vec3 h = normalize(sun + v);
            vec3 spec = SunColor * pow(max(dot(n, h), 0.0), mix(180.0, 12.0, rough)) * mix(3.0, 0.3, rough) * shine * ndl;
            float fresnel = 0.3 + 0.7 * pow(1.0 - max(dot(n, v), 0.0), 5.0);
            vec3 env = shine > 0.0 ? (traced.x >= 0.0 ? traced : sky(reflect(-v, n), sun, t)) * fresnel * (1.0 - rough) * 0.5 * shine : vec3(0.0);
            vec3 c = albedo * (ambient + fill + SunColor * 1.8 * ndl) + spec + env + SunColor * back * 1.4 * (0.6 + 0.4 * shadow) + emit;
            float fog = 1.0 - exp(-dist * 0.0028);
            fog = max(fog, clamp(-vWorld.y / 140.0, 0.0, 1.0) * (1.0 - exp(-dist * 0.012)));
            c = mix(c, sky(-v, sun, t) * 0.55, fog);
            outColor = vec4(c, 1.0);
        }
        """;

    private const string CompositeVertexShader = """
        #version 450
        layout(location = 0) out vec2 vUv;
        void main()
        {
            vec2 p = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
            vUv = p;
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    // The second pass: the sky where nothing was drawn, a bloom from the HDR image's own mips (four taps a level), ACES,
    // cool shadows and warm highlights, a vignette, gamma for the UNORM swapchain, and grain against banding. Dear ImGui then draws in the same space.
    private const string CompositeFragmentShader = "#version 450\n" + SkyGlsl + """
        layout(set = 0, binding = 0) uniform sampler2D hdr;
        layout(push_constant) uniform Push { mat4 invViewProj; vec4 eye; vec4 sun; vec4 params; } pc;
        layout(location = 0) in vec2 vUv;
        layout(location = 0) out vec4 outColor;
        vec3 aces(vec3 x)
        {
            return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
        }
        void main()
        {
            vec4 base = textureLod(hdr, vUv, 0.0);
            vec4 near = pc.invViewProj * vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
            vec3 dir = normalize(near.xyz / near.w - pc.eye.xyz);
            vec3 c = base.rgb + sky(dir, pc.sun.xyz, pc.eye.w) * (1.0 - base.a);
            vec2 texel = 1.0 / vec2(textureSize(hdr, 0));
            vec3 bloom = vec3(0.0);
            for (int k = 1; k < 7; k++)
            {
                float lod = float(k);
                vec2 o = texel * exp2(lod) * 0.75;
                vec3 s = textureLod(hdr, vUv + o, lod).rgb + textureLod(hdr, vUv - o, lod).rgb
                    + textureLod(hdr, vUv + vec2(o.x, -o.y), lod).rgb + textureLod(hdr, vUv + vec2(-o.x, o.y), lod).rgb;
                bloom += s * 0.25 * (0.5 + lod * 0.12);
            }

            c += bloom / 5.52 * pc.params.x;
            c = aces(c * pc.sun.w);
            float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));
            c = mix(c * vec3(0.86, 0.96, 1.12), c * vec3(1.06, 1.0, 0.92), smoothstep(0.05, 0.6, luma));
            vec2 p = vUv - 0.5;
            c *= 1.0 - dot(p, p) * 0.55;
            c = pow(c, vec3(1.0 / 2.2));
            c += (hash12(gl_FragCoord.xy + fract(pc.eye.w) * 640.0) - 0.5) / 160.0;
            outColor = vec4(c, 1.0);
        }
        """;

    // The swarm, in arrays indexed alike.
    private static uint[] _ids = [];
    private static BodyState[] _states = [];
    private static SN.Vector3[] _faceP = [], _faceN = [], _dirs = [];
    private static float[] _eye = [], _mouth = [], _jaw = [], _phase = [], _radius = [], _height = [], _speed = [];
    private static float _memberRadius;

    // The head the swarm forms, when head/head_points.bin is there: points on a generated head (see the README), 9 floats
    // each (position, normal, eye, mouth and jaw weights), in an order where any first n cover it evenly.
    private static readonly float[] Head = LoadHead(Path.Combine(AppContext.BaseDirectory, "head", "head_points.bin"));
    private const float HeadScale = 42f;
    private static readonly SN.Vector3 HeadCenter = new(0f, 22f, -6f);

    public static int Run(string[] args)
    {
        var limit = Arg(args, "--seconds", 0);
        var swarm = (int)Arg(args, "--swarm", 10000);
        var immediate = args.Contains("--immediate");
        var captureAt = Array.IndexOf(args, "--capture");
        var captures = captureAt >= 0 && captureAt + 1 < args.Length ? args[captureAt + 1].Split(',').Select(double.Parse).Order().ToArray() : [];
        var nextCapture = 0;
        var clock = Stopwatch.StartNew();

        if (!Glfw.Init())
        {
            Console.Error.WriteLine("SWARM FAIL: Glfw.Init returned false");
            return 1;
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
        Glfw.WindowHint(GlfwWindowHint.Resizable, 0);
        var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = "BRUTAL showcase: the swarm", Size = new int2(Width, Height) });

        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var extensionCount);
        var extensions = new HashSet<string>();
        for (var i = 0; i < extensionCount; i++)
        {
            extensions.Add(Marshal.PtrToStringUTF8((nint)glfwExtensions[i])!);
        }

        var instance = Vulkan.CreateInstance(new Instance.CreateInfo
        {
            AppInfo = new VkApplicationInfo { ApiVersion = ApiVersion13 },
            EnabledExtensions = extensions,
            EnabledLayers = [],
        });
        if (Glfw.CreateWindowSurface(instance.VkHandle, window, out var rawSurface) != 0)
        {
            Console.Error.WriteLine("SWARM FAIL: no window surface");
            return 1;
        }

        var surface = new VkSurfaceKHR((nint)rawSurface);
        PhysicalDevice? physical = null;
        var family = -1;
        foreach (var pd in instance.PhysicalDevices.OrderBy(p => p.GetProperties().DeviceType == VkPhysicalDeviceType.DiscreteGpu ? 0 : 1))
        {
            var count = pd.GetQueueFamilyProperties_Count();
            var families = new VkQueueFamilyProperties[count];
            pd.GetQueueFamilyProperties_Write(families);
            for (var i = 0; i < count && family < 0; i++)
            {
                if (families[i].QueueFlags.HasFlag(VkQueueFlags.GraphicsBit) && pd.GetSurfaceSupportKHR(i, surface))
                {
                    family = i;
                }
            }

            if (family >= 0)
            {
                physical = pd;
                break;
            }
        }

        if (physical is null)
        {
            Console.Error.WriteLine("SWARM FAIL: no GPU can draw and present to this window");
            return 1;
        }

        // RTX, when the GPU has ray queries and --no-rtx is not given: the scene's shader traces the sun's shadows and the
        // reflections on glass and metal against acceleration structures of the whole scene.
        var hasRayQuery = new VkPhysicalDeviceRayQueryFeaturesKHR();
        var hasAccel = new VkPhysicalDeviceAccelerationStructureFeaturesKHR { Next = &hasRayQuery };
        var has12 = new VkPhysicalDeviceVulkan12Features { Next = &hasAccel };
        var features2 = new VkPhysicalDeviceFeatures2 { Next = &has12 };
        physical.GetFeatures2(ref features2);
        var rtx = !args.Contains("--no-rtx") && hasRayQuery.RayQuery && hasAccel.AccelerationStructure && has12.BufferDeviceAddress;
        var rayQueryOn = new VkPhysicalDeviceRayQueryFeaturesKHR { RayQuery = true };
        var accelOn = new VkPhysicalDeviceAccelerationStructureFeaturesKHR { Next = &rayQueryOn, AccelerationStructure = true };
        var vulkan12On = new VkPhysicalDeviceVulkan12Features { Next = &accelOn, BufferDeviceAddress = true };

        var priority = 1.0f;
        var queueInfo = new VkDeviceQueueCreateInfo { QueueFamilyIndex = family, QueueCount = 1, QueuePriorities = &priority };
        var device = physical.CreateDevice(new DeviceEx.CreateInfo
        {
            Extensions = rtx ? ["VK_KHR_swapchain", "VK_KHR_acceleration_structure", "VK_KHR_ray_query", "VK_KHR_deferred_host_operations"] : ["VK_KHR_swapchain"],
            QueueCreateInfos = new ReadOnlySpan<VkDeviceQueueCreateInfo>(&queueInfo, 1),
            Next = rtx ? &vulkan12On : null,
        });
        var queue = device.GetQueue(family, 0);

        var formatCount = physical.GetSurfaceFormatsKHR_Count(surface);
        var formats = new VkSurfaceFormatKHR[formatCount];
        physical.GetSurfaceFormatsKHR_Write(surface, formats);
        var surfaceFormat = formats[0];
        foreach (var f in formats)
        {
            if (f.Format == VkFormat.B8G8R8A8UNorm && f.ColorSpace == VkColorSpaceKHR.SRGBNonlinearKHR)
            {
                surfaceFormat = f;
            }
        }

        var modeCount = physical.GetSurfacePresentModesKHR_Count(surface);
        var modes = new VkPresentModeKHR[modeCount];
        physical.GetSurfacePresentModesKHR_Write(surface, modes);
        var presentMode = immediate && modes.Contains(VkPresentModeKHR.ImmediateKHR) ? VkPresentModeKHR.ImmediateKHR : VkPresentModeKHR.FifoKHR;

        var caps = physical.GetSurfaceCapabilitiesKHR(surface);
        var extent = caps.CurrentExtent.Width == -1 ? new VkExtent2D { Width = Width, Height = Height } : caps.CurrentExtent;
        var imageCount = Math.Max(caps.MinImageCount, (int)Arg(args, "--images", 2));
        if (caps.MaxImageCount != 0)
        {
            imageCount = Math.Min(imageCount, caps.MaxImageCount);
        }

        var swapchain = device.CreateSwapchainKHR(new VkSwapchainCreateInfoKHR
        {
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransferSrcBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            PreTransform = caps.CurrentTransform,
            CompositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
            PresentMode = presentMode,
            Clipped = true,
        }, null);
        var images = new VkImage[device.GetSwapchainImagesKHR_Count(swapchain)];
        device.GetSwapchainImagesKHR_Write(swapchain, images);

        const VkFormat depthFormat = VkFormat.D32SFloat;
        var depthImage = device.CreateImage(new VkImageCreateInfo
        {
            ImageType = VkImageType._2D,
            Format = depthFormat,
            Extent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = VkSampleCountFlags._1Bit,
            Tiling = VkImageTiling.Optimal,
            Usage = VkImageUsageFlags.DepthStencilAttachmentBit,
            SharingMode = VkSharingMode.Exclusive,
            InitialLayout = VkImageLayout.Undefined,
        }, null);
        var depthMemory = device.AllocateMemory(physical, depthImage, VkMemoryPropertyFlags.DeviceLocalBit, null);
        device.BindImageMemory(depthImage, depthMemory, ByteSize.Zero);
        var depthView = device.CreateImageView(View(depthImage, depthFormat, VkImageAspectFlags.DepthBit), null);

        var attachments = stackalloc VkAttachmentDescription[]
        {
            new()
            {
                Format = surfaceFormat.Format,
                Samples = VkSampleCountFlags._1Bit,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.Store,
                StencilLoadOp = VkAttachmentLoadOp.DontCare,
                StencilStoreOp = VkAttachmentStoreOp.DontCare,
                InitialLayout = VkImageLayout.Undefined,
                FinalLayout = VkImageLayout.PresentSrcKHR,
            },
            new()
            {
                Format = depthFormat,
                Samples = VkSampleCountFlags._1Bit,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.DontCare,
                StencilLoadOp = VkAttachmentLoadOp.DontCare,
                StencilStoreOp = VkAttachmentStoreOp.DontCare,
                InitialLayout = VkImageLayout.Undefined,
                FinalLayout = VkImageLayout.DepthStencilAttachmentOptimal,
            },
        };
        var colorRef = new VkAttachmentReference { Attachment = 0, Layout = VkImageLayout.ColorAttachmentOptimal };
        var depthRef = new VkAttachmentReference { Attachment = 1, Layout = VkImageLayout.DepthStencilAttachmentOptimal };
        var subpass = new VkSubpassDescription
        {
            PipelineBindPoint = VkPipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorRef,
            DepthStencilAttachment = &depthRef,
        };
        var dependency = new VkSubpassDependency
        {
            SrcSubpass = VK.SUBPASS_EXTERNAL,
            DstSubpass = 0,
            SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.LateFragmentTestsBit,
            DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
        };
        var renderPass = device.CreateRenderPass(new VkRenderPassCreateInfo
        {
            AttachmentCount = 2,
            Attachments = attachments,
            SubpassCount = 1,
            Subpasses = &subpass,
            DependencyCount = 1,
            Dependencies = &dependency,
        }, null);

        var views = new VkImageView[images.Length];
        var framebuffers = new VkFramebuffer[images.Length];
        var renderDone = new VkSemaphore[images.Length];
        var pair = stackalloc VkImageView[] { default, depthView };
        for (var i = 0; i < images.Length; i++)
        {
            views[i] = device.CreateImageView(View(images[i], surfaceFormat.Format, VkImageAspectFlags.ColorBit), null);
            pair[0] = views[i];
            framebuffers[i] = device.CreateFramebuffer(new VkFramebufferCreateInfo
            {
                RenderPass = renderPass,
                AttachmentCount = 2,
                Attachments = pair,
                Width = extent.Width,
                Height = extent.Height,
                Layers = 1,
            }, null);
            renderDone[i] = device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }

        // The scene pass: 4x MSAA colour and depth, resolved into an HDR image whose mips feed the bloom.
        var (msaaImage, msaaMemory, msaaView) = Target(device, physical, extent, HdrFormat, Msaa, 1, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit, VkImageAspectFlags.ColorBit);
        var (sceneDepthImage, sceneDepthMemory, sceneDepthView) = Target(device, physical, extent, DepthFormat, Msaa, 1, VkImageUsageFlags.DepthStencilAttachmentBit, VkImageAspectFlags.DepthBit);
        var (hdrImage, hdrMemory, hdrView) = Target(device, physical, extent, HdrFormat, VkSampleCountFlags._1Bit, BloomLevels, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit, VkImageAspectFlags.ColorBit);
        var hdrMipsView = device.CreateImageView(View(hdrImage, HdrFormat, VkImageAspectFlags.ColorBit, BloomLevels), null);
        var sceneAttachments = stackalloc VkAttachmentDescription[]
        {
            new()
            {
                Format = HdrFormat,
                Samples = Msaa,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.DontCare,
                StencilLoadOp = VkAttachmentLoadOp.DontCare,
                StencilStoreOp = VkAttachmentStoreOp.DontCare,
                InitialLayout = VkImageLayout.Undefined,
                FinalLayout = VkImageLayout.ColorAttachmentOptimal,
            },
            new()
            {
                Format = DepthFormat,
                Samples = Msaa,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.DontCare,
                StencilLoadOp = VkAttachmentLoadOp.DontCare,
                StencilStoreOp = VkAttachmentStoreOp.DontCare,
                InitialLayout = VkImageLayout.Undefined,
                FinalLayout = VkImageLayout.DepthStencilAttachmentOptimal,
            },
            new()
            {
                Format = HdrFormat,
                Samples = VkSampleCountFlags._1Bit,
                LoadOp = VkAttachmentLoadOp.DontCare,
                StoreOp = VkAttachmentStoreOp.Store,
                StencilLoadOp = VkAttachmentLoadOp.DontCare,
                StencilStoreOp = VkAttachmentStoreOp.DontCare,
                InitialLayout = VkImageLayout.Undefined,
                FinalLayout = VkImageLayout.TransferSrcOptimal,
            },
        };
        var sceneColorRef = new VkAttachmentReference { Attachment = 0, Layout = VkImageLayout.ColorAttachmentOptimal };
        var sceneDepthRef = new VkAttachmentReference { Attachment = 1, Layout = VkImageLayout.DepthStencilAttachmentOptimal };
        var resolveRef = new VkAttachmentReference { Attachment = 2, Layout = VkImageLayout.ColorAttachmentOptimal };
        var sceneSubpass = new VkSubpassDescription
        {
            PipelineBindPoint = VkPipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            ColorAttachments = &sceneColorRef,
            ResolveAttachments = &resolveRef,
            DepthStencilAttachment = &sceneDepthRef,
        };
        // In: the previous frame's pass, blits and composite are done with these images. Out: the resolve is visible to the blits.
        var sceneDependencies = stackalloc VkSubpassDependency[]
        {
            new()
            {
                SrcSubpass = VK.SUBPASS_EXTERNAL,
                DstSubpass = 0,
                SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.LateFragmentTestsBit | VkPipelineStageFlags.FragmentShaderBit | VkPipelineStageFlags.TransferBit,
                DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
                SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
                DstAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
            },
            new()
            {
                SrcSubpass = 0,
                DstSubpass = VK.SUBPASS_EXTERNAL,
                SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = VkPipelineStageFlags.TransferBit,
                SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = VkAccessFlags.TransferReadBit,
            },
        };
        var sceneRenderPass = device.CreateRenderPass(new VkRenderPassCreateInfo
        {
            AttachmentCount = 3,
            Attachments = sceneAttachments,
            SubpassCount = 1,
            Subpasses = &sceneSubpass,
            DependencyCount = 2,
            Dependencies = sceneDependencies,
        }, null);
        var sceneViews = stackalloc VkImageView[] { msaaView, sceneDepthView, hdrView };
        var sceneFramebuffer = device.CreateFramebuffer(new VkFramebufferCreateInfo
        {
            RenderPass = sceneRenderPass,
            AttachmentCount = 3,
            Attachments = sceneViews,
            Width = extent.Width,
            Height = extent.Height,
            Layers = 1,
        }, null);

        var vertexModule = Shader(device, VertexShader, ShaderKind.VertexShader, "swarm.vert");
        var fragmentModule = rtx
            ? Shader(device, FragmentShader.Replace("#version 450\n", "#version 460\n#extension GL_EXT_ray_query : require\n#define RTX 1\n"), ShaderKind.FragmentShader, "swarm-rtx.frag", vulkan13: true)
            : Shader(device, FragmentShader, ShaderKind.FragmentShader, "swarm.frag");
        var compositeVertex = Shader(device, CompositeVertexShader, ShaderKind.VertexShader, "composite.vert");
        var compositeFragment = Shader(device, CompositeFragmentShader, ShaderKind.FragmentShader, "composite.frag");
        var pushRange = new VkPushConstantRange
        {
            StageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit,
            Offset = ByteSize.Zero,
            Size = ByteSize.Of<Push>(),
        };
        // The second pass reads every mip of the HDR image through one sampler; the scene reads its textures through another.
        var sampler = device.CreateSampler(new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.ClampToEdge,
            AddressModeV = VkSamplerAddressMode.ClampToEdge,
            AddressModeW = VkSamplerAddressMode.ClampToEdge,
            MinLod = 0f,
            MaxLod = BloomLevels,
            MaxAnisotropy = 1f,
        }, null);
        var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = [new VkDescriptorSetLayoutBinding { DescriptorType = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit }],
        }, null);
        // The scene's set: its textures and, with RTX, the frame's top-level acceleration structure; one set a frame in flight.
        VkDescriptorSetLayoutBinding[] sceneBindings = rtx
            ? [new VkDescriptorSetLayoutBinding { Binding = 0, DescriptorType = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit },
               new VkDescriptorSetLayoutBinding { Binding = 1, DescriptorType = VkDescriptorType.AccelerationStructureKHR, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit }]
            : [new VkDescriptorSetLayoutBinding { Binding = 0, DescriptorType = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit }];
        var sceneSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutEx.CreateInfo { Bindings = sceneBindings }, null);
        var descriptorPool = device.CreateDescriptorPool(new DescriptorPoolEx.CreateInfo
        {
            MaxSets = 1 + FramesInFlight,
            PoolSizes = [new VkDescriptorPoolSize { Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1 + FramesInFlight },
                         new VkDescriptorPoolSize { Type = VkDescriptorType.AccelerationStructureKHR, DescriptorCount = FramesInFlight }],
        }, null);
        var layout = device.CreatePipelineLayout([sceneSetLayout], [pushRange], null);
        var pipeline = Pipeline(device, layout, sceneRenderPass, vertexModule, fragmentModule, scene: true);
        var hdrSet = device.AllocateDescriptorSet(descriptorPool, setLayout);
        var hdrInfo = new VkDescriptorImageInfo { Sampler = sampler, ImageView = hdrMipsView, ImageLayout = VkImageLayout.ShaderReadOnlyOptimal };
        device.UpdateDescriptorSets([new VkWriteDescriptorSet { DstSet = hdrSet, DescriptorCount = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, ImageInfo = &hdrInfo }], []);
        var compositeLayout = device.CreatePipelineLayout([setLayout], [pushRange], null);
        var compositePipeline = Pipeline(device, compositeLayout, renderPass, compositeVertex, compositeFragment, scene: false);

        // One cube, then the modelled towers, and per frame in flight an instance buffer that stays mapped.
        var cube = new List<MeshVertex>();
        Cube(cube);
        var cubeCount = cube.Count;
        var towerVariants = LoadTowers(Path.Combine(AppContext.BaseDirectory, "city", "towers.glb"), cube);
        var meshSize = new ByteSize((nuint)(cube.Count * sizeof(MeshVertex)));
        VkBuffer meshBuffer;
        VkDeviceMemory meshMemory;
        if (rtx)
        {
            // The ray queries' bottom-level structures read their triangles from this buffer too.
            meshBuffer = AddressBuffer(device, physical, meshSize.Value, VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKHR, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, out meshMemory);
        }
        else
        {
            meshBuffer = device.CreateBuffer(new VkBufferCreateInfo { Size = meshSize, Usage = VkBufferUsageFlags.VertexBufferBit, SharingMode = VkSharingMode.Exclusive }, null);
            meshMemory = device.AllocateMemory(physical, meshBuffer, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(meshBuffer, meshMemory, memoryOffset: ByteSize.Zero);
        }

        var meshMapped = device.MapMemory<MeshVertex>(new MapInfo(meshMemory) { Region = new MapRegion { Size = meshSize } });
        cube.ToArray().AsSpan().CopyTo(new Span<MeshVertex>((MeshVertex*)meshMapped, cube.Count));
        device.UnmapMemory(meshMemory);

        var instanceSize = new ByteSize((nuint)(MaxInstances * sizeof(Drawn)));
        var instanceBuffers = new VkBuffer[FramesInFlight];
        var instanceMemory = new VkDeviceMemory[FramesInFlight];
        var instanceMapped = new nint[FramesInFlight];
        var pools = new VkCommandPool[FramesInFlight];
        var commands = new CommandBuffer[FramesInFlight];
        var fences = new VkFence[FramesInFlight];
        var acquired = new VkSemaphore[FramesInFlight];
        for (var i = 0; i < FramesInFlight; i++)
        {
            instanceBuffers[i] = device.CreateBuffer(new VkBufferCreateInfo { Size = instanceSize, Usage = VkBufferUsageFlags.VertexBufferBit, SharingMode = VkSharingMode.Exclusive }, null);
            instanceMemory[i] = device.AllocateMemory(physical, instanceBuffers[i], VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(instanceBuffers[i], instanceMemory[i], memoryOffset: ByteSize.Zero);
            instanceMapped[i] = (nint)(Drawn*)device.MapMemory<Drawn>(new MapInfo(instanceMemory[i]) { Region = new MapRegion { Size = instanceSize } });
            pools[i] = device.CreateCommandPool(new VkCommandPoolCreateInfo { QueueFamilyIndex = family }, null);
            commands[i] = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo { CommandPool = pools[i], Level = VkCommandBufferLevel.Primary });
            fences[i] = device.CreateFence(new VkFenceCreateInfo { Flags = VkFenceCreateFlags.SignaledBit }, null);
            acquired[i] = device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }

        // The surface textures, made with ComfyUI and Blender (see the README): one 2D array, uploaded once through a
        // staging buffer, then mipmapped by blits.
        string[] textureFiles = ["hull_albedo", "hull_nrh", "facade_albedo", "facade_nrh"];
        var texels = new List<byte[]>();
        var texSize = 0;
        foreach (var name in textureFiles)
        {
            texels.Add(LoadPng(Path.Combine(AppContext.BaseDirectory, "textures", name + ".png"), out texSize, out _));
        }

        var texLevels = (int)Math.Log2(texSize) + 1;
        var layerBytes = texSize * texSize * 4;
        var stagingSize = new ByteSize((nuint)(layerBytes * textureFiles.Length));
        var staging = device.CreateBuffer(new VkBufferCreateInfo { Size = stagingSize, Usage = VkBufferUsageFlags.TransferSrcBit, SharingMode = VkSharingMode.Exclusive }, null);
        var stagingMemory = device.AllocateMemory(physical, staging, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
        device.BindBufferMemory(staging, stagingMemory, memoryOffset: ByteSize.Zero);
        var stagingMapped = (byte*)device.MapMemory<byte>(new MapInfo(stagingMemory) { Region = new MapRegion { Size = stagingSize } });
        for (var i = 0; i < texels.Count; i++)
        {
            texels[i].CopyTo(new Span<byte>(stagingMapped + (i * layerBytes), layerBytes));
        }

        device.UnmapMemory(stagingMemory);
        var texImage = device.CreateImage(new VkImageCreateInfo
        {
            ImageType = VkImageType._2D,
            Format = VkFormat.R8G8B8A8UNorm,
            Extent = new VkExtent3D { Width = texSize, Height = texSize, Depth = 1 },
            MipLevels = texLevels,
            ArrayLayers = textureFiles.Length,
            Samples = VkSampleCountFlags._1Bit,
            Tiling = VkImageTiling.Optimal,
            Usage = VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit,
            SharingMode = VkSharingMode.Exclusive,
            InitialLayout = VkImageLayout.Undefined,
        }, null);
        var texMemory = device.AllocateMemory(physical, texImage, VkMemoryPropertyFlags.DeviceLocalBit, null);
        device.BindImageMemory(texImage, texMemory, ByteSize.Zero);
        var texView = device.CreateImageView(new VkImageViewCreateInfo
        {
            Image = texImage,
            ViewType = VkImageViewType._2DArray,
            Format = VkFormat.R8G8B8A8UNorm,
            SubresourceRange = new VkImageSubresourceRange { AspectMask = VkImageAspectFlags.ColorBit, LevelCount = texLevels, LayerCount = textureFiles.Length },
        }, null);
        var upload = commands[0];
        upload.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
        upload.PipelineBarrier(VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
            [Barrier(texImage, 0, texLevels, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, default, VkAccessFlags.TransferWriteBit, textureFiles.Length)]);
        upload.CopyBufferToImage(staging, texImage, VkImageLayout.TransferDstOptimal,
            [new VkBufferImageCopy { ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = textureFiles.Length }, ImageExtent = new VkExtent3D { Width = texSize, Height = texSize, Depth = 1 } }]);
        upload.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
            [Barrier(texImage, 0, 1, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal, VkAccessFlags.TransferWriteBit, VkAccessFlags.TransferReadBit, textureFiles.Length)]);
        Downsample(upload, texImage, texSize, texSize, texLevels, textureFiles.Length);
        upload.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.FragmentShaderBit, VkDependencyFlags.None, [], [],
            [Barrier(texImage, 0, texLevels, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal, VkAccessFlags.TransferWriteBit, VkAccessFlags.ShaderReadBit, textureFiles.Length)]);
        upload.End();
        var uploadHandle = upload.Handle;
        queue.Submit([new VkSubmitInfo { CommandBufferCount = 1, CommandBuffers = &uploadHandle }], default);
        device.WaitIdle();
        device.DestroyBuffer(staging, null);
        device.FreeMemory(stagingMemory, null);
        var repeatSampler = device.CreateSampler(new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.Repeat,
            AddressModeV = VkSamplerAddressMode.Repeat,
            AddressModeW = VkSamplerAddressMode.Repeat,
            MinLod = 0f,
            MaxLod = texLevels,
            MaxAnisotropy = 1f,
        }, null);
        var sceneSets = new VkDescriptorSet[FramesInFlight];
        var texInfo = new VkDescriptorImageInfo { Sampler = repeatSampler, ImageView = texView, ImageLayout = VkImageLayout.ShaderReadOnlyOptimal };
        for (var i = 0; i < FramesInFlight; i++)
        {
            sceneSets[i] = device.AllocateDescriptorSet(descriptorPool, sceneSetLayout);
            device.UpdateDescriptorSets([new VkWriteDescriptorSet { DstSet = sceneSets[i], DescriptorCount = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, ImageInfo = &texInfo }], []);
        }

        // For --capture: a host buffer the window's image is copied into.
        var readbackSize = new ByteSize((nuint)(extent.Width * extent.Height * 4));
        var readback = device.CreateBuffer(new VkBufferCreateInfo { Size = readbackSize, Usage = VkBufferUsageFlags.TransferDstBit, SharingMode = VkSharingMode.Exclusive }, null);
        var readbackMemory = device.AllocateMemory(physical, readback, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
        device.BindBufferMemory(readback, readbackMemory, memoryOffset: ByteSize.Zero);
        var readbackMapped = (nint)(byte*)device.MapMemory<byte>(new MapInfo(readbackMemory) { Region = new MapRegion { Size = readbackSize } });

        // Dear ImGui and ImPlot, on GLFW and on this render pass.
        ImGui.CreateContext();
        ImPlot.CreateContext();
        ImGui.StyleColorsDark();
        var io = ImGui.GetIO();
        io.IniFilename = default;
        var platform = new GlfwBackend(window, GlfwBackend.ClientApi.Vulkan);
        var renderer = new VulkanBackend(new VulkanBackend.CreateInfo
        {
            Device = device,
            GraphicsQueue = queue,
            RenderPass = renderPass,
            MinImageCount = imageCount,
            ImageCount = images.Length,
            SampleCount = VkSampleCountFlags._1Bit,
            DescriptorPoolSize = 16,
        });

        // Jolt: two object layers, static and moving; the moving one meets both.
        Jolt.RegisterDefaultAllocator();
        Factory.Instance = Factory.Create();
        Jolt.RegisterTypes();
        var threads = Math.Clamp(Environment.ProcessorCount - 2, 1, 6);
        // With the fallback: the package's TempAllocatorImpl.Create(64 MB) aborts the process (0xC0000409) in the first
        // Update that has moving bodies, as if it held far less than asked, while the same size with a malloc fallback runs.
        var temp = TempAllocatorImplWithMallocFallback.Create(64u * 1024 * 1024);
        var jobs = JobSystemThreadPool.Create(2048, 8, threads);
        var broadPhaseLayers = BroadPhaseLayerInterfaceTable.Create(2, 2);
        broadPhaseLayers.MapObjectToBroadPhaseLayer(0, 0);
        broadPhaseLayers.MapObjectToBroadPhaseLayer(1, 1);
        var layerPairs = ObjectLayerPairFilterTable.Create(2);
        layerPairs.EnableCollision(0, 1);
        layerPairs.EnableCollision(1, 1);
        var objectVsBroadPhase = ObjectVsBroadPhaseLayerFilterTable.Create(broadPhaseLayers, 2, layerPairs, 2);
        var physics = PhysicsSystem.Create();
        physics.Init(40_000, 0, 262_144, 131_072, broadPhaseLayers, objectVsBroadPhase, layerPairs);
        physics.SetGravity(new float3(0f, -9.81f, 0f));
        var bodies = physics.GetBodyInterface();


        // The scene: the city is drawn only; the walkway and the figure also stand in the simulation.
        var rng = new Random(7);
        var statics = new List<Drawn>();
        var towers = City(statics, rng, towerVariants);
        foreach (var (c, size, color) in Solids())
        {
            Box(statics, c, size, 0f, color, 0f);
            using var settings = BodyCreationSettings.Create(BoxShapeSettings.Create(F(size / 2f)), F(c), new floatQuat(0f, 0f, 0f, 1f), MotionType.Static, 0);
            bodies.CreateAndAddBody(settings, Activation.DontActivate);
        }

        // The towers first, grouped by variant: one draw call each, then one for every box.
        var towerRanges = new (int First, int Count)[towerVariants.Length];
        var ordered = new List<Drawn>();
        for (var v = 0; v < towerVariants.Length; v++)
        {
            towerRanges[v] = (ordered.Count, 0);
            foreach (var (variant, placed) in towers)
            {
                if (variant == v)
                {
                    ordered.Add(placed);
                }
            }

            towerRanges[v].Count = ordered.Count - towerRanges[v].First;
        }

        var towerInstances = ordered.Count;
        var drawCalls = 1 + towerRanges.Count(r => r.Count > 0);
        ordered.AddRange(statics);
        var staticInstances = ordered.ToArray();
        // RTX: one bottom-level structure for the cube and one per tower variant, built once; per frame in flight, a
        // top-level structure over every instance, rebuilt each frame as the swarm moves. The static instances are written
        // once; lamps are left out of the shadow rays.
        var rt = rtx ? new RayScene(device, physical, queue, commands[0], meshBuffer, cubeCount, towerVariants, FramesInFlight, MaxInstances) : null;
        if (rt is not null)
        {
            for (var i = 0; i < FramesInFlight; i++)
            {
                for (var k = 0; k < staticInstances.Length; k++)
                {
                    var variant = Array.FindIndex(towerRanges, r => k >= r.First && k < r.First + r.Count);
                    var lamp = staticInstances[k].Extra.X > 2.5f && staticInstances[k].Extra.X < 3.5f;
                    rt.Write(i, k, staticInstances[k].Model, variant < 0 || k >= towerInstances ? 0 : 1 + variant, lamp);
                }

                var accel = rt.Tlas[i];
                var accelWrite = new VkWriteDescriptorSetAccelerationStructureKHR { AccelerationStructureCount = 1, AccelerationStructures = &accel };
                device.UpdateDescriptorSets([new VkWriteDescriptorSet { Next = &accelWrite, DstSet = sceneSets[i], DstBinding = 1, DescriptorCount = 1, DescriptorType = VkDescriptorType.AccelerationStructureKHR }], []);
            }
        }

        BuildSwarm(bodies, swarm);
        physics.OptimizeBroadPhase();
        Console.WriteLine($"SWARM ready after {clock.Elapsed.TotalMilliseconds:F0} ms: {swarm} bodies, {staticInstances.Length} static instances, Jolt on {threads} threads, present {presentMode}");

        // State the panel steers.
        bool assembled = false, autoCycle = true, speaking = true, drift = true, rtxOn = rtx && !args.Contains("--rtx-off");
        float speedScale = 1f, cohesion = 1.2f, glow = 0f, yawOffset = 0f, pitchOffset = 0f, zoom = 1f, exposure = 1.0f, bloomStrength = 0.12f, smoothDt = 1f / 60f;
        int requested = swarm, preset = 0, shownPreset = -1;
        var camEye = new SN.Vector3(1.4f, 1.85f, FigureZ + 6.5f);
        var camTarget = FaceCenter;
        var frameHistory = new float2[History];
        var physicsHistory = new float2[History];
        for (var i = 0; i < History; i++)
        {
            frameHistory[i] = new float2(i, 0f);
            physicsHistory[i] = new float2(i, 0f);
        }

        var frameTimes = new List<double>(1 << 16);
        var physicsTimes = new List<double>(1 << 16);
        var physicsWatch = new Stopwatch();
        double simTime = 0, last = clock.Elapsed.TotalSeconds, cycleStart = last, nextLog = last + 1, physicsMs = 0, frameMs = 0;
        var lastLogFrame = 0;
        var worstSecond = int.MaxValue;
        var frame = 0;
        var errors = PhysicsUpdateError.None;
        var reason = "window closed";
        var vsyncNote = presentMode == VkPresentModeKHR.FifoKHR ? "vsync" : "no vsync";

        var clears = stackalloc VkClearValue[2];
        while (!window.ShouldClose)
        {
            Glfw.PollEvents();
            var now = clock.Elapsed.TotalSeconds;
            if (limit > 0 && now >= limit)
            {
                reason = "time limit";
                break;
            }

            // The simulation and the camera step by the smoothed interval: a frame that comes back early or late is still
            // shown for one refresh, so stepping by its raw interval would make the motion judder.
            smoothDt += ((float)Math.Min(now - last, 0.1) - smoothDt) * 0.12f;
            var frameDt = smoothDt;
            frameMs = (now - last) * 1000.0;
            last = now;

            platform.NewFrame();
            renderer.NewFrame();
            ImGui.NewFrame();
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                reason = "Esc";
                window.ShouldClose = true;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Space, false))
            {
                assembled = !assembled;
                autoCycle = false;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                preset = (preset + 1) % 3;
            }

            bool overPanel = io.WantCaptureMouse;
            if (!overPanel)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    yawOffset -= io.MouseDelta.X * 0.004f;
                    pitchOffset += io.MouseDelta.Y * 0.003f;
                }

                zoom = Math.Clamp(zoom * MathF.Pow(0.9f, io.MouseWheel), 0.2f, 3f);
            }

            if (autoCycle)
            {
                var c = (now - cycleStart) % 32.0;
                assembled = c >= 6.0 && c < 24.0;
            }

            // One step a frame, as long as the frame (clamped): steer every body toward its target, step Jolt, read every
            // body back. Catching up with several fixed steps after a hitch made each frame longer than the last.
            var stepDt = Math.Clamp(frameDt, 1f / 240f, 1f / 30f);
            physicsWatch.Restart();
            Steer(bodies, (float)simTime, assembled, speaking, cohesion, speedScale);
            var error = physics.Update(stepDt, 1, temp, jobs);
            if (error != PhysicsUpdateError.None)
            {
                errors |= error;
            }

            ReadStates(bodies);
            simTime += stepDt;
            physicsMs = physicsWatch.Elapsed.TotalMilliseconds;
            physicsTimes.Add(physicsMs);

            glow += ((assembled ? 1f : 0f) - glow) * Math.Min(1f, frameDt * 0.8f);

            // The panel.
            ImGui.SetNextWindowPos(new float2(14f, 14f), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("BRUTAL showcase"u8, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"{io.Framerate:F0} fps ({vsyncNote}), frame {frameMs:F2} ms");
                ImGui.Text($"Jolt: {_ids.Length} bodies, {physicsMs:F2} ms a step, {threads} threads");
                ImGui.Text($"Vulkan: {staticInstances.Length + _ids.Length} instances in {drawCalls} draw calls, HDR, MSAA 4x, bloom");
                ImGui.Text($"Swarm: {(assembled ? "face" : "cloud")}{(errors != PhysicsUpdateError.None ? $", Jolt: {errors}" : string.Empty)}");
                ImGui.Separator();
                if (ImGui.Button(assembled ? "Disperse"u8 : "Assemble"u8))
                {
                    assembled = !assembled;
                    autoCycle = false;
                }

                ImGui.SameLine();
                ImGui.Checkbox("Auto cycle"u8, ref autoCycle);
                ImGui.SameLine();
                ImGui.Checkbox("Speaking"u8, ref speaking);
                ImGui.SliderFloat("Swarm speed"u8, ref speedScale, 0.2f, 3f);
                ImGui.SliderFloat("Cohesion"u8, ref cohesion, 0.2f, 4f);
                ImGui.SliderFloat("Exposure"u8, ref exposure, 0.3f, 4f);
                ImGui.SliderFloat("Bloom"u8, ref bloomStrength, 0f, 0.6f);
                ImGui.SliderInt("Swarm size"u8, ref requested, 500, 20000);
                if (requested != _ids.Length)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Rebuild"u8))
                    {
                        DestroySwarm(bodies);
                        BuildSwarm(bodies, requested);
                        physics.OptimizeBroadPhase();
                    }
                }

                if (ImGui.Button("Shoulder"u8))
                {
                    preset = 0;
                }

                ImGui.SameLine();
                if (ImGui.Button("Wide"u8))
                {
                    preset = 1;
                }

                ImGui.SameLine();
                if (ImGui.Button("Face"u8))
                {
                    preset = 2;
                }

                ImGui.SameLine();
                ImGui.Checkbox("Drift"u8, ref drift);
                if (rtx)
                {
                    ImGui.Checkbox("RTX: traced sun shadows and reflections"u8, ref rtxOn);
                }
                else
                {
                    ImGui.Text("RTX: no ray queries on this GPU, or --no-rtx"u8);
                }
                Array.Copy(frameHistory, 1, frameHistory, 0, History - 1);
                Array.Copy(physicsHistory, 1, physicsHistory, 0, History - 1);
                for (var i = 0; i < History - 1; i++)
                {
                    frameHistory[i] = new float2(i, frameHistory[i].Y);
                    physicsHistory[i] = new float2(i, physicsHistory[i].Y);
                }

                frameHistory[History - 1] = new float2(History - 1, (float)frameMs);
                physicsHistory[History - 1] = new float2(History - 1, (float)physicsMs);
                if (ImPlot.BeginPlot("##timings"u8, new float2(-1f, 130f)))
                {
                    ImPlot.SetupAxes(default, "ms"u8, ImPlotAxisFlags.NoTickLabels);
                    ImPlot.SetupAxisLimits(ImAxis.X1, 0, History - 1, ImPlotCond.Always);
                    ImPlot.SetupAxisLimits(ImAxis.Y1, 0, 25, ImPlotCond.Always);
                    ImPlot.PlotLine("frame"u8, frameHistory);
                    ImPlot.PlotLine("Jolt"u8, physicsHistory);
                    ImPlot.EndPlot();
                }

                ImGui.Text("Space assemble   C camera   drag turn   wheel zoom   Esc quit"u8);
                ImGui.Text("Built with BRUTAL (Ahwoo), Jolt Physics, Dear ImGui and ImPlot"u8);
            }

            ImGui.End();
            ImGui.Render();

            // Camera: a preset, turned by the drag, zoomed by the wheel, drifting slowly, eased in.
            if (preset != shownPreset)
            {
                shownPreset = preset;
                yawOffset = pitchOffset = 0f;
                zoom = 1f;
            }

            var (presetEye, presetTarget) = preset switch
            {
                1 => (new SN.Vector3(-38f, 14f, 78f), new SN.Vector3(0f, 18f, 10f)),
                2 => (new SN.Vector3(6f, 22f, 32f), FaceCenter),
                _ => (new SN.Vector3(1.4f, 1.85f, FigureZ + 6.5f), FaceCenter),
            };
            var offset = presetEye - presetTarget;
            var distance = offset.Length() * zoom;
            var yaw = MathF.Atan2(offset.X, offset.Z) + yawOffset + (drift ? MathF.Sin((float)now * 0.06f) * 0.15f : 0f);
            var pitch = Math.Clamp(MathF.Asin(offset.Y / offset.Length()) + pitchOffset, -1.2f, 1.3f);
            var wantEye = presetTarget + (distance * new SN.Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw)));
            var ease = Math.Min(1f, frameDt * 2.5f);
            camEye = SN.Vector3.Lerp(camEye, wantEye, ease);
            camTarget = SN.Vector3.Lerp(camTarget, presetTarget, ease);
            var projection = SN.Matrix4x4.CreatePerspectiveFieldOfView(55f * MathF.PI / 180f, extent.Width / (float)extent.Height, Near, 1000f);
            projection.M33 = 0f; // reversed, infinite depth: 1 at the near plane, 0 at infinity
            projection.M43 = Near;
            projection.M22 = -projection.M22; // Vulkan's clip space has y pointing down
            var viewProj = SN.Matrix4x4.CreateLookAt(camEye, camTarget, SN.Vector3.UnitY) * projection;
            SN.Matrix4x4.Invert(viewProj, out var inverseViewProj);
            var push = new Push { Matrix = viewProj, Eye = new SN.Vector4(camEye, (float)now), Sun = new SN.Vector4(SunDir, exposure), Params = new SN.Vector4(bloomStrength, rtx && rtxOn ? 1f : 0f, 0f, 0f) };
            var skyPush = push with { Matrix = inverseViewProj };

            // The frame: wait for this slot, fill its instances, record the scene and the panel, submit, present.
            var slot = frame % FramesInFlight;
            device.WaitForFences([fences[slot]], true, -1);
            var result = device.AcquireNextImageKHR(swapchain, -1, acquired[slot], default, out var imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                reason = "swapchain out of date";
                break;
            }

            device.ResetFences([fences[slot]]);
            device.ResetCommandPool(pools[slot], VkCommandPoolResetFlags.None);
            var target = new Span<Drawn>((Drawn*)instanceMapped[slot], MaxInstances);
            staticInstances.AsSpan().CopyTo(target);
            var speech = speaking ? Speech((float)simTime) : 0f;
            var drawn = staticInstances.Length + WriteSwarm(target[staticInstances.Length..], glow, speech, rt, slot, staticInstances.Length);

            var cb = commands[slot];
            cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
            if (rt is not null)
            {
                rt.Build(cb, slot, drawn);
            }

            clears[0] = new VkClearValue { Color = new VkClearColorValue { Float32 = new float4(0f, 0f, 0f, 0f) } };
            clears[1] = new VkClearValue { DepthStencil = new VkClearDepthStencilValue { Depth = 0f } };
            cb.BeginRenderPass(new VkRenderPassBeginInfo
            {
                RenderPass = sceneRenderPass,
                Framebuffer = sceneFramebuffer,
                RenderArea = new VkRect2D { Extent = extent },
                ClearValueCount = 2,
                ClearValues = clears,
            }, VkSubpassContents.Inline);
            cb.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            cb.SetViewport(firstViewport: 0, [new VkViewport { Width = extent.Width, Height = extent.Height, MinDepth = 0f, MaxDepth = 1f }]);
            cb.SetScissor(firstScissor: 0, [new VkRect2D { Extent = extent }]);
            cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, layout, 0, [sceneSets[slot]], []);
            cb.PushConstants(layout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, offset: 0, new ReadOnlySpan<byte>(&push, sizeof(Push)));
            cb.BindVertexBuffer(0, meshBuffer);
            cb.BindVertexBuffer(1, instanceBuffers[slot]);
            cb.Draw(cubeCount, drawn - towerInstances, 0, towerInstances);
            for (var v = 0; v < towerVariants.Length; v++)
            {
                if (towerRanges[v].Count > 0)
                {
                    cb.Draw(towerVariants[v].Count, towerRanges[v].Count, towerVariants[v].First, towerRanges[v].First);
                }
            }
            cb.EndRenderPass();

            // The bloom's mips: each level a linear blit of the one above, then every level for the second pass to read.
            cb.PipelineBarrier(VkPipelineStageFlags.FragmentShaderBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                [Barrier(hdrImage, 1, BloomLevels - 1, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, default, VkAccessFlags.TransferWriteBit)]);
            Downsample(cb, hdrImage, (int)extent.Width, (int)extent.Height, BloomLevels, 1);
            cb.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.FragmentShaderBit, VkDependencyFlags.None, [], [],
                [Barrier(hdrImage, 0, BloomLevels, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal, VkAccessFlags.TransferWriteBit, VkAccessFlags.ShaderReadBit)]);

            // The second pass, on the swapchain image: sky, bloom and tone mapping in one triangle, then the panel.
            clears[0] = new VkClearValue { Color = new VkClearColorValue { Float32 = new float4(0f, 0f, 0f, 1f) } };
            clears[1] = new VkClearValue { DepthStencil = new VkClearDepthStencilValue { Depth = 1f } };
            cb.BeginRenderPass(new VkRenderPassBeginInfo
            {
                RenderPass = renderPass,
                Framebuffer = framebuffers[imageIndex],
                RenderArea = new VkRect2D { Extent = extent },
                ClearValueCount = 2,
                ClearValues = clears,
            }, VkSubpassContents.Inline);
            cb.BindPipeline(VkPipelineBindPoint.Graphics, compositePipeline);
            cb.SetViewport(firstViewport: 0, [new VkViewport { Width = extent.Width, Height = extent.Height, MinDepth = 0f, MaxDepth = 1f }]);
            cb.SetScissor(firstScissor: 0, [new VkRect2D { Extent = extent }]);
            cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, compositeLayout, 0, [hdrSet], []);
            cb.PushConstants(compositeLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, offset: 0, new ReadOnlySpan<byte>(&skyPush, sizeof(Push)));
            cb.Draw(3, 1, 0, 0);
            renderer.RenderDrawData(ImGui.GetDrawData(), cb);
            cb.EndRenderPass();
            var capturing = nextCapture < captures.Length && now >= captures[nextCapture];
            if (capturing)
            {
                cb.PipelineBarrier(VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], 0, 1, VkImageLayout.PresentSrcKHR, VkImageLayout.TransferSrcOptimal, VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.TransferReadBit)]);
                cb.CopyImageToBuffer(images[imageIndex], VkImageLayout.TransferSrcOptimal, readback,
                    [new VkBufferImageCopy { ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 }, ImageExtent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 } }]);
                cb.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.BottomOfPipeBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], 0, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.PresentSrcKHR, default, default)]);
            }

            cb.End();

            var waitStage = VkPipelineStageFlags.ColorAttachmentOutputBit;
            var commandHandle = cb.Handle;
            var waitSemaphore = acquired[slot];
            var signalSemaphore = renderDone[imageIndex];
            queue.Submit(
                [
                    new VkSubmitInfo
                    {
                        WaitSemaphoreCount = 1,
                        WaitSemaphores = &waitSemaphore,
                        WaitDstStageMask = &waitStage,
                        CommandBufferCount = 1,
                        CommandBuffers = &commandHandle,
                        SignalSemaphoreCount = 1,
                        SignalSemaphores = &signalSemaphore,
                    },
                ],
                fences[slot]);
            var presentIndex = imageIndex;
            result = queue.PresentKHR(new VkPresentInfoKHR
            {
                WaitSemaphoreCount = 1,
                WaitSemaphores = &signalSemaphore,
                SwapchainCount = 1,
                Swapchains = &swapchain,
                ImageIndices = &presentIndex,
            });
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                reason = "swapchain out of date";
                break;
            }

            if (capturing)
            {
                device.WaitIdle();
                var file = $"colossus-{captures[nextCapture]:0.#}s.png";
                SavePng(file, new ReadOnlySpan<byte>((byte*)readbackMapped, (int)(extent.Width * extent.Height * 4)), (int)extent.Width, (int)extent.Height);
                Console.WriteLine($"SWARM captured {file} at {now:F1} s ({(assembled ? "face" : "cloud")})");
                nextCapture++;
                last = clock.Elapsed.TotalSeconds; // the capture's wait is not a frame time
            }

            if (frame > 0 && !capturing)
            {
                frameTimes.Add(frameMs);
            }

            frame++;
            if (now >= nextLog)
            {
                Console.WriteLine($"SWARM t={now:F0} s, {frame - lastLogFrame} frames, frame {frameMs:F2} ms, Jolt {physicsMs:F2} ms, {_ids.Length} bodies, {drawn} instances, {(assembled ? "face" : "cloud")}");
                if (now >= 2)
                {
                    worstSecond = Math.Min(worstSecond, frame - lastLogFrame);
                }

                lastLogFrame = frame;
                nextLog += 1;
            }
        }

        var seconds = clock.Elapsed.TotalSeconds;
        device.WaitIdle();
        renderer.Dispose();
        platform.Dispose();
        ImPlot.DestroyContext();
        ImGui.DestroyContext();
        DestroySwarm(bodies);
        physics.Dispose();
        jobs.Dispose();
        temp.Dispose();
        Factory.DestroyInstance();
        for (var i = 0; i < FramesInFlight; i++)
        {
            device.UnmapMemory(instanceMemory[i]);
            device.DestroyBuffer(instanceBuffers[i], null);
            device.FreeMemory(instanceMemory[i], null);
            device.DestroySemaphore(acquired[i], null);
            device.DestroyFence(fences[i], null);
            device.DestroyCommandPool(pools[i], null);
        }

        rt?.Dispose();
        device.DestroyBuffer(meshBuffer, null);
        device.FreeMemory(meshMemory, null);
        device.UnmapMemory(readbackMemory);
        device.DestroyBuffer(readback, null);
        device.FreeMemory(readbackMemory, null);
        device.DestroyPipeline(pipeline, null);
        device.DestroyPipeline(compositePipeline, null);
        device.DestroyPipelineLayout(layout, null);
        device.DestroyPipelineLayout(compositeLayout, null);
        descriptorPool.Dispose();
        setLayout.Dispose();
        sceneSetLayout.Dispose();
        device.DestroySampler(sampler, null);
        device.DestroySampler(repeatSampler, null);
        device.DestroyImageView(texView, null);
        device.DestroyImage(texImage, null);
        device.FreeMemory(texMemory, null);
        device.DestroyShaderModule(vertexModule, null);
        device.DestroyShaderModule(fragmentModule, null);
        device.DestroyShaderModule(compositeVertex, null);
        device.DestroyShaderModule(compositeFragment, null);
        device.DestroyFramebuffer(sceneFramebuffer, null);
        device.DestroyRenderPass(sceneRenderPass, null);
        device.DestroyImageView(hdrMipsView, null);
        foreach (var (image, memory, view) in new[] { (msaaImage, msaaMemory, msaaView), (sceneDepthImage, sceneDepthMemory, sceneDepthView), (hdrImage, hdrMemory, hdrView) })
        {
            device.DestroyImageView(view, null);
            device.DestroyImage(image, null);
            device.FreeMemory(memory, null);
        }
        for (var i = 0; i < images.Length; i++)
        {
            device.DestroySemaphore(renderDone[i], null);
            device.DestroyFramebuffer(framebuffers[i], null);
            device.DestroyImageView(views[i], null);
        }

        device.DestroyRenderPass(renderPass, null);
        device.DestroyImageView(depthView, null);
        device.DestroyImage(depthImage, null);
        device.FreeMemory(depthMemory, null);
        device.DestroySwapchainKHR(swapchain, null);
        device.Dispose();
        instance.DestroySurfaceKHR(surface, null);
        instance.Dispose();
        window.Dispose();
        Glfw.Terminate();

        frameTimes.Sort();
        physicsTimes.Sort();
        static double P(List<double> xs, double q) => xs.Count == 0 ? double.NaN : xs[Math.Min(xs.Count - 1, (int)(q * xs.Count))];
        Console.WriteLine($"SWARM summary: {frame} frames in {seconds:F1} s; frame ms p50 {P(frameTimes, 0.5):F2} p95 {P(frameTimes, 0.95):F2} p99 {P(frameTimes, 0.99):F2}, worst second {(worstSecond == int.MaxValue ? 0 : worstSecond)} frames; Jolt step ms p50 {P(physicsTimes, 0.5):F2} p95 {P(physicsTimes, 0.95):F2} p99 {P(physicsTimes, 0.99):F2} max {(physicsTimes.Count > 0 ? physicsTimes[^1] : double.NaN):F2}; Jolt errors: {errors}");
        Console.WriteLine($"SWARM peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024)} MB; closed by {reason}");
        return 0;
    }

    private static double Arg(string[] args, string name, double fallback)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && double.TryParse(args[at + 1], out var v) ? v : fallback;
    }

    private static float3 F(SN.Vector3 v) => new(v.X, v.Y, v.Z);

    private static SN.Vector3 V(float3 v) => new(v.X, v.Y, v.Z);

    // The swarm: one sphere body per machine, with no gravity. Each gets a place in the circling cloud and a point on
    // the face; the bodies are sized so that the face's surface takes them all.
    private static void BuildSwarm(BodyInterface bodies, int n)
    {
        var rng = new Random(11);
        _memberRadius = Math.Clamp(0.42f * MathF.Sqrt(1000f / n), 0.08f, 0.45f);
        // Through ShapeSettings: the package's jolt.dll does not export SphereShape_Create_3 (nor BoxShape_Create_3).
        var sphere = SphereShapeSettings.Create(_memberRadius);
        _ids = new uint[n];
        _states = new BodyState[n];
        _faceP = new SN.Vector3[n];
        _faceN = new SN.Vector3[n];
        _dirs = new SN.Vector3[n];
        _eye = new float[n];
        _mouth = new float[n];
        _jaw = new float[n];
        _phase = new float[n];
        _radius = new float[n];
        _height = new float[n];
        _speed = new float[n];
        for (var i = 0; i < n; i++)
        {
            _phase[i] = rng.NextSingle() * MathF.Tau;
            _radius[i] = 18f + (rng.NextSingle() * 24f);
            _height[i] = 12f + (rng.NextSingle() * 36f);
            _speed[i] = 0.12f + (rng.NextSingle() * 0.2f);
            if (Head.Length > 0)
            {
                // Past the file's last point, reuse the points with a little offset.
                var k = i % (Head.Length / 9) * 9;
                var jitter = i < Head.Length / 9 ? SN.Vector3.Zero : new SN.Vector3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * 0.004f;
                _faceP[i] = HeadCenter + (HeadScale * (new SN.Vector3(Head[k], Head[k + 1], Head[k + 2]) + jitter));
                _faceN[i] = new SN.Vector3(Head[k + 3], Head[k + 4], Head[k + 5]);
                _eye[i] = Head[k + 6];
                _mouth[i] = Head[k + 7];
                _jaw[i] = Head[k + 8];
            }
            else
            {
                float x, y;
                do
                {
                    x = ((rng.NextSingle() * 2f) - 1f) * FaceA;
                    y = ((rng.NextSingle() * 2f) - 1f) * FaceB;
                }
                while ((x * x / (FaceA * FaceA)) + (y * y / (FaceB * FaceB)) > 1f);

                var z = Depth(x, y);
                const float e = 0.05f;
                _faceP[i] = FaceCenter + new SN.Vector3(x, y, z);
                _faceN[i] = SN.Vector3.Normalize(new SN.Vector3(-(Depth(x + e, y) - z) / e, -(Depth(x, y + e) - z) / e, 1f));
                _eye[i] = MathF.Max(G(x - 4.6f, y - 3.5f, 1.7f, 1.0f), G(x + 4.6f, y - 3.5f, 1.7f, 1.0f));
                _mouth[i] = G(x, y + 8.6f, 4.5f, 1.3f);
                _jaw[i] = y < -8.6f ? G(x, y + 11f, 5f, 2.5f) : 0f;
            }

            _dirs[i] = SN.Vector3.UnitZ;
            var start = Cloud(i, 0f) + new SN.Vector3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f);
            using var settings = BodyCreationSettings.Create(sphere, F(start), new floatQuat(0f, 0f, 0f, 1f), MotionType.Dynamic, 1);
            settings.SetGravityFactor(0f);
            settings.SetLinearDamping(0.05f);
            _ids[i] = bodies.CreateAndAddBody(settings, Activation.Activate);
        }

        ReadStates(bodies);
    }

    // Every body's pose and velocity, one call each. The bulk read, BodyLockInterface.GetBodyStates, is declared by the
    // binding but its entry point is missing from the package's jolt.dll (2026.10.2, win-x64).
    private static void ReadStates(BodyInterface bodies)
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            bodies.GetPositionAndRotation(_ids[i], out _states[i].Position, out _states[i].Rotation);
            _states[i].LinearVelocity = bodies.GetLinearVelocity(_ids[i]);
        }
    }

    private static void DestroySwarm(BodyInterface bodies)
    {
        foreach (var id in _ids)
        {
            bodies.RemoveBody(id);
            bodies.DestroyBody(id);
        }

        _ids = [];
        _states = [];
    }

    // Where member i circles at time t: a slow vortex above the walkway.
    private static SN.Vector3 Cloud(int i, float t)
    {
        var angle = _phase[i] + (t * _speed[i]);
        return new SN.Vector3(MathF.Cos(angle) * _radius[i], _height[i] + (MathF.Sin((t * 0.6f) + (_phase[i] * 3f)) * 3f), -6f + (MathF.Sin(angle) * _radius[i] * 0.7f));
    }

    // How open the mouth is while the face speaks, from 0 to 1: syllables, in phrases.
    private static float Speech(float t) => MathF.Max(0f, MathF.Sin(t * 7f)) * (0.55f + (0.45f * MathF.Sin(t * 1.3f)));

    // Each body's new velocity: toward its target, capped, with a little turbulence so the swarm never looks rigid.
    // When the face speaks, its jaw drops with each syllable.
    private static void Steer(BodyInterface bodies, float t, bool assembled, bool speaking, float cohesion, float speedScale)
    {
        var max = 28f * speedScale;
        var stir = assembled ? 0.25f : 2f;
        for (var i = 0; i < _ids.Length; i++)
        {
            var p = V(_states[i].Position);
            var v = V(_states[i].LinearVelocity);
            SN.Vector3 target;
            if (assembled)
            {
                target = _faceP[i];
                if (speaking)
                {
                    var open = Speech(t) * _jaw[i];
                    target += new SN.Vector3(0f, -1.6f, -0.4f) * open;
                }
            }
            else
            {
                target = Cloud(i, t);
            }

            var desired = (target - p) * cohesion;
            var length = desired.Length();
            if (length > max)
            {
                desired *= max / length;
            }

            desired += stir * new SN.Vector3(MathF.Sin((t * 1.3f) + (i * 0.37f)), MathF.Sin((t * 1.7f) + (i * 0.73f)), MathF.Cos((t * 1.1f) + (i * 0.51f)));
            bodies.SetLinearVelocity(_ids[i], F(v + ((desired - v) * 0.15f)));
        }
    }

    // The swarm's instances: each machine a long box, pointing where it flies, or out of the face once it has settled;
    // the face's eyes glow as it forms, and its mouth with each syllable.
    private static int WriteSwarm(Span<Drawn> o, float glow, float speech, RayScene? rt, int slot, int first)
    {
        var n = Math.Min(_ids.Length, o.Length);
        var scale = SN.Matrix4x4.CreateScale(_memberRadius * 1.7f, _memberRadius * 0.9f, _memberRadius * 3.8f);
        var flat = SN.Matrix4x4.CreateScale(_memberRadius * 2.4f, _memberRadius * 0.7f, _memberRadius * 4.2f);
        for (var i = 0; i < n; i++)
        {
            var p = V(_states[i].Position);
            var v = V(_states[i].LinearVelocity);
            // Settled on the face, a machine lies flat like a scale: its length down the slope, its thin side out.
            var normal = _faceN[i];
            var settled = glow > 0.5f && v.LengthSquared() <= 4f;
            var slope = MathF.Abs(normal.Y) > 0.95f ? SN.Vector3.UnitZ - (normal * normal.Z) : (normal * normal.Y) - SN.Vector3.UnitY;
            var want = v.LengthSquared() > 4f ? SN.Vector3.Normalize(v) : settled ? SN.Vector3.Normalize(slope) : _dirs[i];
            var d = SN.Vector3.Normalize(SN.Vector3.Lerp(_dirs[i], want, 0.2f));
            _dirs[i] = d;
            var up = settled && MathF.Abs(SN.Vector3.Dot(d, normal)) < 0.9f ? normal : MathF.Abs(d.Y) > 0.95f ? SN.Vector3.UnitX : SN.Vector3.UnitY;
            var emission = ((_eye[i] * 5f) + (_mouth[i] * 2.5f * speech)) * glow;
            var model = (settled ? flat : scale) * SN.Matrix4x4.CreateWorld(p, d, up);
            rt?.Write(slot, first + i, model, 0, false, swarm: true);
            o[i] = new Drawn(model, new SN.Vector4(SN.Vector3.Lerp(Metal, Amber, MathF.Min(1f, _eye[i] + (_mouth[i] * speech)) * glow), emission), new SN.Vector4(1f, i * 0.618034f % 1f, 0f, 0f));
        }

        return n;
    }

    // The face's depth, in metres, at (x, y) from its centre: an ellipsoid with eye sockets, a brow, a nose, a mouth,
    // hollow cheeks and a chin. Original, built from Gaussian bumps.
    private static float Depth(float x, float y)
    {
        var e = 1f - (x * x / (FaceA * FaceA)) - (y * y / (FaceB * FaceB));
        if (e <= 0f)
        {
            return 0f;
        }

        var z = MathF.Sqrt(e) * 8f;
        z -= 2.8f * (G(x - 4.6f, y - 3.5f, 2.3f, 1.5f) + G(x + 4.6f, y - 3.5f, 2.3f, 1.5f));
        z += 1.4f * G(x, y - 6.2f, 7.5f, 1.0f);
        z += 3.2f * G(x, y + 0.5f, 1.3f, 4.2f);
        z += 1.2f * G(x, y + 3.8f, 2.2f, 0.9f);
        z -= 1.3f * G(x, y + 8.3f, 4.2f, 0.45f);
        z += 0.8f * G(x, y + 9.6f, 4.0f, 0.9f);
        z -= 1.0f * (G(x - 8.5f, y + 2f, 2f, 4f) + G(x + 8.5f, y + 2f, 2f, 4f));
        z += 1.0f * G(x, y + 15f, 4.5f, 2.5f);
        return z;
    }

    private static float G(float dx, float dy, float sx, float sy) => MathF.Exp(-((dx * dx / (sx * sx)) + (dy * dy / (sy * sy))));

    // The head's points (see Head), or none when the file is missing or not one of ours: the face is then procedural.
    private static float[] LoadHead(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var bytes = File.ReadAllBytes(path);
        var count = bytes.Length >= 8 && bytes.AsSpan(0, 4).SequenceEqual("CHD1"u8) ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) : 0;
        if (count <= 0 || bytes.Length != 8 + (count * 36))
        {
            return [];
        }

        var floats = new float[count * 9];
        System.Buffer.BlockCopy(bytes, 8, floats, 0, floats.Length * 4);
        return floats;
    }

    // What stands in the simulation as well as on screen: the walkway, its piers, and the figure at its end.
    private static IEnumerable<(SN.Vector3 Center, SN.Vector3 Size, SN.Vector3 Color)> Solids()
    {
        var walk = new SN.Vector3(0.09f, 0.08f, 0.07f);
        var cloth = new SN.Vector3(0.015f, 0.015f, 0.018f);
        yield return (new SN.Vector3(0f, -0.75f, FigureZ + 46f), new SN.Vector3(3f, 1.5f, 92f), walk);
        for (var z = FigureZ + 6f; z < FigureZ + 92f; z += 20f)
        {
            yield return (new SN.Vector3(0f, -46f, z), new SN.Vector3(1.4f, 89f, 1.4f), walk);
        }

        yield return (new SN.Vector3(-0.11f, 0.42f, FigureZ), new SN.Vector3(0.16f, 0.84f, 0.18f), cloth);
        yield return (new SN.Vector3(0.11f, 0.42f, FigureZ), new SN.Vector3(0.16f, 0.84f, 0.18f), cloth);
        yield return (new SN.Vector3(0f, 0.8f, FigureZ), new SN.Vector3(0.6f, 0.95f, 0.34f), cloth);
        yield return (new SN.Vector3(0f, 1.42f, FigureZ), new SN.Vector3(0.5f, 0.5f, 0.3f), cloth);
        yield return (new SN.Vector3(-0.33f, 1.25f, FigureZ), new SN.Vector3(0.13f, 0.7f, 0.14f), cloth);
        yield return (new SN.Vector3(0.33f, 1.25f, FigureZ), new SN.Vector3(0.13f, 0.7f, 0.14f), cloth);
        yield return (new SN.Vector3(0f, 1.78f, FigureZ), new SN.Vector3(0.2f, 0.24f, 0.22f), cloth);
    }

    // The city: towers on a wide ring rising from the abyss, leaving the walkway's view open. With the modelled towers
    // (city/towers.glb), each is one of their variants, turned and scaled; without them, a box with amber lights.
    private static List<(int Variant, Drawn Instance)> City(List<Drawn> o, Random rng, (int First, int Count, float Width)[] variants)
    {
        var tower = new SN.Vector3(0.05f, 0.045f, 0.04f);
        var placed = new List<(int, Drawn)>();
        for (var i = 0; i < 420; i++)
        {
            var a = rng.NextSingle() * MathF.Tau;
            var r = 60f + (rng.NextSingle() * 170f);
            var x = MathF.Cos(a) * r;
            var z = MathF.Sin(a) * r;
            if (MathF.Abs(x) < 14f && z > 0f)
            {
                continue;
            }

            if (variants.Length > 0)
            {
                var v = rng.Next(variants.Length);
                var s = 0.6f + (rng.NextSingle() * 0.45f);
                if (MathF.Abs(x) < 14f + (variants[v].Width * s) && z > 0f)
                {
                    continue;
                }

                var model = SN.Matrix4x4.CreateScale(s) * SN.Matrix4x4.CreateRotationY(rng.NextSingle() * MathF.Tau) * SN.Matrix4x4.CreateTranslation(x, -90f, z);
                placed.Add((v, new Drawn(model, new SN.Vector4(tower, 0f), new SN.Vector4(4f, i * 0.1373f % 1f, 0f, 0f))));
                continue;
            }

            var w = 4f + (rng.NextSingle() * 10f);
            var d = 4f + (rng.NextSingle() * 10f);
            var h = 60f + (rng.NextSingle() * 140f);
            var yaw = rng.NextSingle() * MathF.PI;
            var center = new SN.Vector3(x, -90f + (h / 2f), z);
            Box(o, center, new SN.Vector3(w, h, d), yaw, tower, 0f, 2f, i * 0.1373f % 1f);
            var lights = 3 + rng.Next(6);
            var rot = SN.Matrix4x4.CreateRotationY(-yaw);
            for (var k = 0; k < lights; k++)
            {
                var side = rng.Next(4);
                var local = side switch
                {
                    0 => new SN.Vector3(w / 2f + 0.05f, 0f, (rng.NextSingle() - 0.5f) * d * 0.8f),
                    1 => new SN.Vector3(-w / 2f - 0.05f, 0f, (rng.NextSingle() - 0.5f) * d * 0.8f),
                    2 => new SN.Vector3((rng.NextSingle() - 0.5f) * w * 0.8f, 0f, d / 2f + 0.05f),
                    _ => new SN.Vector3((rng.NextSingle() - 0.5f) * w * 0.8f, 0f, -d / 2f - 0.05f),
                };
                local.Y = (rng.NextSingle() - 0.3f) * h * 0.8f;
                Box(o, center + SN.Vector3.Transform(local, rot), new SN.Vector3(0.7f, 0.7f, 0.7f), yaw, Amber, 6f, 3f);
            }
        }

        return placed;
    }

    // The modelled towers, through BRUTAL's glTF loader: every mesh of the file is a variant, appended to the vertices as
    // plain triangles; each primitive's material name gives its surface. Returns each variant's vertex range and its
    // largest half width, or nothing when the file is missing.
    private static (int First, int Count, float Width)[] LoadTowers(string path, List<MeshVertex> o)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var gltf = new GltfLoader(path);
        var json = gltf.GltfJson;
        var variants = new List<(int, int, float)>();
        foreach (var mesh in json.Meshes.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var first = o.Count;
            var width = 0f;
            foreach (var primitive in mesh.Primitives)
            {
                var material = primitive.Material is int m ? json.Materials[m].Name switch
                {
                    "glass" => 1f,
                    "metal" => 2f,
                    "lamp" => 3f,
                    _ => 0f,
                } : 0f;
                var positions = MemoryMarshal.Cast<byte, SN.Vector3>(gltf.GetAccessorBufferSpan(primitive.Attributes["POSITION"]));
                var normals = MemoryMarshal.Cast<byte, SN.Vector3>(gltf.GetAccessorBufferSpan(primitive.Attributes["NORMAL"]));
                var indices = json.Accessors[primitive.Indices!.Value];
                var data = gltf.GetAccessorBufferSpan(primitive.Indices.Value);
                for (var k = 0; k < indices.Count; k++)
                {
                    var at = indices.ComponentType switch
                    {
                        Accessor.ComponentTypeEnum.UnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(data[(k * 2)..]),
                        Accessor.ComponentTypeEnum.UnsignedByte => data[k],
                        _ => (int)BinaryPrimitives.ReadUInt32LittleEndian(data[(k * 4)..]),
                    };
                    o.Add(new MeshVertex(positions[at], normals[at], material));
                    width = MathF.Max(width, MathF.Max(MathF.Abs(positions[at].X), MathF.Abs(positions[at].Z)));
                }
            }

            variants.Add((first, o.Count - first, width));
        }

        return [.. variants];
    }

    private static void Box(List<Drawn> o, SN.Vector3 c, SN.Vector3 size, float yaw, SN.Vector3 color, float emission, float kind = 0f, float seed = 0f)
    {
        var model = SN.Matrix4x4.CreateScale(size) * SN.Matrix4x4.CreateRotationY(-yaw) * SN.Matrix4x4.CreateTranslation(c);
        o.Add(new Drawn(model, new SN.Vector4(color, emission), new SN.Vector4(kind, seed, 0f, 0f)));
    }

    // A unit cube centred on the origin, 12 triangles with one normal per face.
    private static void Cube(List<MeshVertex> o)
    {
        ReadOnlySpan<(SN.Vector3 N, SN.Vector3 U, SN.Vector3 V)> faces =
        [
            (SN.Vector3.UnitX, SN.Vector3.UnitY, SN.Vector3.UnitZ),
            (-SN.Vector3.UnitX, SN.Vector3.UnitZ, SN.Vector3.UnitY),
            (SN.Vector3.UnitY, SN.Vector3.UnitZ, SN.Vector3.UnitX),
            (-SN.Vector3.UnitY, SN.Vector3.UnitX, SN.Vector3.UnitZ),
            (SN.Vector3.UnitZ, SN.Vector3.UnitX, SN.Vector3.UnitY),
            (-SN.Vector3.UnitZ, SN.Vector3.UnitY, SN.Vector3.UnitX),
        ];
        foreach (var (n, u, v) in faces)
        {
            SN.Vector3 Corner(float su, float sv) => 0.5f * (n + (su * u) + (sv * v));
            o.AddRange([new(Corner(-1, -1), n), new(Corner(1, -1), n), new(Corner(1, 1), n), new(Corner(-1, -1), n), new(Corner(1, 1), n), new(Corner(-1, 1), n)]);
        }
    }

    private static VkImageViewCreateInfo View(VkImage image, VkFormat format, VkImageAspectFlags aspect, int levels = 1) => new()
    {
        Image = image,
        ViewType = VkImageViewType._2D,
        Format = format,
        SubresourceRange = new VkImageSubresourceRange { AspectMask = aspect, LevelCount = levels, LayerCount = 1 },
    };

    // A render target in device memory, with a view of its first level.
    private static (VkImage Image, VkDeviceMemory Memory, VkImageView View) Target(Device device, PhysicalDevice physical, VkExtent2D extent, VkFormat format, VkSampleCountFlags samples, int levels, VkImageUsageFlags usage, VkImageAspectFlags aspect)
    {
        var image = device.CreateImage(new VkImageCreateInfo
        {
            ImageType = VkImageType._2D,
            Format = format,
            Extent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 },
            MipLevels = levels,
            ArrayLayers = 1,
            Samples = samples,
            Tiling = VkImageTiling.Optimal,
            Usage = usage,
            SharingMode = VkSharingMode.Exclusive,
            InitialLayout = VkImageLayout.Undefined,
        }, null);
        var memory = device.AllocateMemory(physical, image, VkMemoryPropertyFlags.DeviceLocalBit, null);
        device.BindImageMemory(image, memory, ByteSize.Zero);
        return (image, memory, device.CreateImageView(View(image, format, aspect), null));
    }

    // Each level from 1 on becomes a linear blit of the one above. Level 0 must be in TransferSrc and the others in
    // TransferDst; all of them end in TransferSrc.
    private static void Downsample(CommandBuffer cb, VkImage image, int width, int height, int levels, int layers)
    {
        for (var level = 1; level < levels; level++)
        {
            var blit = new VkImageBlit
            {
                SrcSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = level - 1, LayerCount = layers },
                DstSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = level, LayerCount = layers },
            };
            blit.SrcOffsets[1] = new VkOffset3D { X = width, Y = height, Z = 1 };
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
            blit.DstOffsets[1] = new VkOffset3D { X = width, Y = height, Z = 1 };
            cb.BlitImage(image, VkImageLayout.TransferSrcOptimal, image, VkImageLayout.TransferDstOptimal, [blit], VkFilter.Linear);
            cb.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                [Barrier(image, level, 1, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal, VkAccessFlags.TransferWriteBit, VkAccessFlags.TransferReadBit, layers)]);
        }
    }

    // A small PNG reader for the textures: 8-bit RGB or RGBA, not interlaced, as Blender writes them. Returns RGBA.
    private static byte[] LoadPng(string path, out int width, out int height)
    {
        var bytes = File.ReadAllBytes(path);
        var idat = new MemoryStream();
        var channels = 0;
        width = height = 0;
        for (var at = 8; at + 8 <= bytes.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at));
            var type = Encoding.ASCII.GetString(bytes, at + 4, 4);
            var data = bytes.AsSpan(at + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || (data[9] != 2 && data[9] != 6) || data[12] != 0)
                {
                    throw new InvalidDataException($"{path}: only 8-bit RGB or RGBA PNGs, not interlaced");
                }

                channels = data[9] == 6 ? 4 : 3;
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }

            at += 12 + length;
        }

        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            z.ReadExactly(raw);
        }

        var pixels = new byte[width * height * 4];
        var prev = new byte[stride];
        var line = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            raw.AsSpan((y * (stride + 1)) + 1, stride).CopyTo(line);
            for (var x = 0; x < stride; x++)
            {
                int a = x >= channels ? line[x - channels] : 0, b = prev[x], c = x >= channels ? prev[x - channels] : 0;
                line[x] += filter switch
                {
                    1 => (byte)a,
                    2 => (byte)b,
                    3 => (byte)((a + b) / 2),
                    4 => (byte)Paeth(a, b, c),
                    _ => (byte)0,
                };
            }

            for (var x = 0; x < width; x++)
            {
                var o = ((y * width) + x) * 4;
                pixels[o] = line[x * channels];
                pixels[o + 1] = line[(x * channels) + 1];
                pixels[o + 2] = line[(x * channels) + 2];
                pixels[o + 3] = channels == 4 ? line[(x * channels) + 3] : (byte)255;
            }

            (prev, line) = (line, prev);
        }

        return pixels;
    }

    // Writes the swapchain's BGRA pixels as an 8-bit RGB PNG, every row unfiltered.
    private static void SavePng(string path, ReadOnlySpan<byte> bgra, int width, int height)
    {
        var raw = new byte[height * ((width * 3) + 1)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 4, o = (y * ((width * 3) + 1)) + 1 + (x * 3);
                raw[o] = bgra[i + 2];
                raw[o + 1] = bgra[i + 1];
                raw[o + 2] = bgra[i];
            }
        }

        var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);

        static void Chunk(Stream s, string type, ReadOnlySpan<byte> data)
        {
            Span<byte> word = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
            s.Write(word);
            var body = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type, body);
            data.CopyTo(body.AsSpan(4));
            s.Write(body);
            var crc = 0xFFFFFFFFu;
            foreach (var b in body)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }

            BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
            s.Write(word);
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static VkImageMemoryBarrier Barrier(VkImage image, int baseLevel, int levels, VkImageLayout from, VkImageLayout to, VkAccessFlags src, VkAccessFlags dst, int layers = 1) => new()
    {
        SrcAccessMask = src,
        DstAccessMask = dst,
        OldLayout = from,
        NewLayout = to,
        SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
        DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
        Image = image,
        SubresourceRange = new VkImageSubresourceRange { AspectMask = VkImageAspectFlags.ColorBit, BaseMipLevel = baseLevel, LevelCount = levels, LayerCount = layers },
    };

    // A buffer whose device address the GPU may take (the RTX path's buffers); its memory is allocated to allow that.
    private static VkBuffer AddressBuffer(Device device, PhysicalDevice physical, ulong size, VkBufferUsageFlags usage, VkMemoryPropertyFlags properties, out VkDeviceMemory memory)
    {
        var buffer = device.CreateBuffer(new VkBufferCreateInfo { Size = new ByteSize((nuint)size), Usage = usage | VkBufferUsageFlags.ShaderDeviceAddressBit, SharingMode = VkSharingMode.Exclusive }, null);
        var needs = device.GetBufferMemoryRequirements(buffer);
        var flags = new VkMemoryAllocateFlagsInfo { Flags = VkMemoryAllocateFlags.DeviceAddressBit };
        memory = device.AllocateMemory(new VkMemoryAllocateInfo { Next = &flags, AllocationSize = needs.Size, MemoryTypeIndex = physical.FindMemoryType(needs.MemoryTypeBits, properties) }, null);
        device.BindBufferMemory(buffer, memory, memoryOffset: ByteSize.Zero);
        return buffer;
    }

    // The ray queries' scene. Bottom-level acceleration structures for the cube and for each tower variant, built once
    // from the mesh buffer; per frame in flight, a top-level one over every instance, from an instance buffer that stays
    // mapped. Instance masks: 1 for the city and the walkway, 4 for the swarm, 2 for lamps, which only reflections see.
    private sealed class RayScene : IDisposable
    {
        private readonly Device _device;
        private readonly List<(VkBuffer Buffer, VkDeviceMemory Memory)> _buffers = [];
        private readonly List<VkAccelerationStructureKHR> _structures = [];
        private readonly ulong[] _blas;
        private readonly VkDeviceAddress[] _scratch;
        private readonly VkDeviceAddress[] _instances;
        private readonly nint[] _mapped;

        public RayScene(Device device, PhysicalDevice physical, Queue queue, CommandBuffer cb, VkBuffer mesh, int cubeCount, (int First, int Count, float Width)[] variants, int frames, int maxInstances)
        {
            _device = device;
            var meshAddress = device.GetBufferDeviceAddress(mesh);
            var ranges = new List<(int First, int Count)> { (0, cubeCount) };
            ranges.AddRange(variants.Select(v => (v.First, v.Count)));
            var vertices = ranges.Max(r => r.First + r.Count);
            var n = ranges.Count;
            var geometries = new VkAccelerationStructureGeometryKHR[n];
            var infos = new VkAccelerationStructureBuildGeometryInfoKHR[n];
            var rangeInfos = new VkAccelerationStructureBuildRangeInfoKHR[n];
            var scratchOffsets = new ulong[n];
            ulong scratchTotal = 0;
            _blas = new ulong[n];
            for (var i = 0; i < n; i++)
            {
                geometries[i] = new VkAccelerationStructureGeometryKHR
                {
                    GeometryType = VkGeometryTypeKHR.TrianglesKHR,
                    Flags = VkGeometryFlagsKHR.OpaqueBitKHR,
                    Geometry = new VkAccelerationStructureGeometryDataKHR
                    {
                        Triangles = new VkAccelerationStructureGeometryTrianglesDataKHR
                        {
                            VertexFormat = VkFormat.R32G32B32SFloat,
                            VertexData = new VkDeviceOrHostAddressConstKHR { DeviceAddress = meshAddress },
                            VertexStride = (ByteSize64)(ulong)sizeof(MeshVertex),
                            MaxVertex = vertices - 1,
                            IndexType = VkIndexType.NoneKHR,
                        },
                    },
                };
                rangeInfos[i] = new VkAccelerationStructureBuildRangeInfoKHR { PrimitiveCount = ranges[i].Count / 3, FirstVertex = ranges[i].First };
            }

            fixed (VkAccelerationStructureGeometryKHR* g = geometries)
            fixed (VkAccelerationStructureBuildRangeInfoKHR* r = rangeInfos)
            {
                for (var i = 0; i < n; i++)
                {
                    infos[i] = new VkAccelerationStructureBuildGeometryInfoKHR
                    {
                        Type = VkAccelerationStructureTypeKHR.BottomLevelKHR,
                        Flags = VkBuildAccelerationStructureFlagsKHR.PreferFastTraceBitKHR,
                        Mode = VkBuildAccelerationStructureModeKHR.BuildKHR,
                        GeometryCount = 1,
                        Geometries = g + i,
                    };
                    var sizes = new VkAccelerationStructureBuildSizesInfoKHR();
                    device.GetAccelerationStructureBuildSizesKHR(VkAccelerationStructureBuildTypeKHR.DeviceKHR, infos[i], [rangeInfos[i].PrimitiveCount], sizes);
                    infos[i].DstAccelerationStructure = Create(physical, VkAccelerationStructureTypeKHR.BottomLevelKHR, sizes.AccelerationStructureSize.Value, out _blas[i]);
                    scratchOffsets[i] = scratchTotal;
                    scratchTotal += (sizes.BuildScratchSize.Value + 255) & ~255ul;
                }

                var scratch = Track(AddressBuffer(device, physical, scratchTotal, VkBufferUsageFlags.StorageBufferBit, VkMemoryPropertyFlags.DeviceLocalBit, out var scratchMemory), scratchMemory);
                var scratchAddress = device.GetBufferDeviceAddress(scratch).Value;
                var pointers = new Ptr<VkAccelerationStructureBuildRangeInfoKHR>[n];
                for (var i = 0; i < n; i++)
                {
                    infos[i].ScratchData = new VkDeviceOrHostAddressKHR { DeviceAddress = new VkDeviceAddress(scratchAddress + scratchOffsets[i]) };
                    pointers[i] = new Ptr<VkAccelerationStructureBuildRangeInfoKHR>(r + i);
                }

                cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
                cb.BuildAccelerationStructuresKHR(infos, pointers);
                cb.End();
                var handle = cb.Handle;
                queue.Submit([new VkSubmitInfo { CommandBufferCount = 1, CommandBuffers = &handle }], default);
                device.WaitIdle();
            }

            Tlas = new VkAccelerationStructureKHR[frames];
            _scratch = new VkDeviceAddress[frames];
            _instances = new VkDeviceAddress[frames];
            _mapped = new nint[frames];
            for (var f = 0; f < frames; f++)
            {
                var size = (ulong)(maxInstances * sizeof(VkAccelerationStructureInstanceKHR));
                var instances = Track(AddressBuffer(device, physical, size, VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKHR, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, out var instanceMemory), instanceMemory);
                _mapped[f] = (nint)device.MapMemory<VkAccelerationStructureInstanceKHR>(new MapInfo(instanceMemory) { Region = new MapRegion { Size = new ByteSize((nuint)size) } });
                _instances[f] = device.GetBufferDeviceAddress(instances);
                var geometry = TopGeometry(_instances[f]);
                var info = new VkAccelerationStructureBuildGeometryInfoKHR
                {
                    Type = VkAccelerationStructureTypeKHR.TopLevelKHR,
                    Flags = VkBuildAccelerationStructureFlagsKHR.PreferFastBuildBitKHR,
                    Mode = VkBuildAccelerationStructureModeKHR.BuildKHR,
                    GeometryCount = 1,
                    Geometries = &geometry,
                };
                var sizes = new VkAccelerationStructureBuildSizesInfoKHR();
                device.GetAccelerationStructureBuildSizesKHR(VkAccelerationStructureBuildTypeKHR.DeviceKHR, info, [maxInstances], sizes);
                Tlas[f] = Create(physical, VkAccelerationStructureTypeKHR.TopLevelKHR, sizes.AccelerationStructureSize.Value, out _);
                var tlasScratch = Track(AddressBuffer(device, physical, sizes.BuildScratchSize.Value, VkBufferUsageFlags.StorageBufferBit, VkMemoryPropertyFlags.DeviceLocalBit, out var tlasScratchMemory), tlasScratchMemory);
                _scratch[f] = device.GetBufferDeviceAddress(tlasScratch);
            }
        }

        public VkAccelerationStructureKHR[] Tlas { get; }

        // Instance k of frame slot f: its model matrix (System.Numerics, row vectors) as Vulkan's 3 by 4 rows, and the
        // structure of mesh m (0 the cube, 1 + v tower variant v).
        // Written field by field into the mapped buffer, in VkAccelerationStructureInstanceKHR's layout: the binding's
        // bit-field setters cost about 2 µs an instance, too much for 10,000 a frame.
        public void Write(int f, int k, in SN.Matrix4x4 m, int mesh, bool lamp, bool swarm = false)
        {
            var p = (RawInstance*)_mapped[f] + k;
            p->X = new SN.Vector4(m.M11, m.M21, m.M31, m.M41);
            p->Y = new SN.Vector4(m.M12, m.M22, m.M32, m.M42);
            p->Z = new SN.Vector4(m.M13, m.M23, m.M33, m.M43);
            p->CustomAndMask = (lamp ? 1u : 0u) | ((lamp ? 0x02u : swarm ? 0x04u : 0x01u) << 24);
            p->OffsetAndFlags = (uint)VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableBitKHR << 24;
            p->Structure = _blas[mesh];
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInstance
        {
            public SN.Vector4 X, Y, Z;
            public uint CustomAndMask, OffsetAndFlags;
            public ulong Structure;
        }

        // Rebuilds slot f's top-level structure over its first count instances, before the scene pass reads it.
        public void Build(CommandBuffer cb, int f, int count)
        {
            var geometry = TopGeometry(_instances[f]);
            var info = new VkAccelerationStructureBuildGeometryInfoKHR
            {
                Type = VkAccelerationStructureTypeKHR.TopLevelKHR,
                Flags = VkBuildAccelerationStructureFlagsKHR.PreferFastBuildBitKHR,
                Mode = VkBuildAccelerationStructureModeKHR.BuildKHR,
                DstAccelerationStructure = Tlas[f],
                GeometryCount = 1,
                Geometries = &geometry,
                ScratchData = new VkDeviceOrHostAddressKHR { DeviceAddress = _scratch[f] },
            };
            var range = new VkAccelerationStructureBuildRangeInfoKHR { PrimitiveCount = count };
            cb.BuildAccelerationStructuresKHR([info], [new Ptr<VkAccelerationStructureBuildRangeInfoKHR>(&range)]);
            cb.PipelineBarrier(VkPipelineStageFlags.AccelerationStructureBuildBitKHR, VkPipelineStageFlags.FragmentShaderBit, VkDependencyFlags.None,
                [new VkMemoryBarrier { SrcAccessMask = VkAccessFlags.AccelerationStructureWriteBitKHR, DstAccessMask = VkAccessFlags.AccelerationStructureReadBitKHR }], [], []);
        }

        public void Dispose()
        {
            foreach (var s in _structures)
            {
                _device.DestroyAccelerationStructureKHR(s, null);
            }

            foreach (var (buffer, memory) in _buffers)
            {
                _device.DestroyBuffer(buffer, null);
                _device.FreeMemory(memory, null);
            }
        }

        private static VkAccelerationStructureGeometryKHR TopGeometry(VkDeviceAddress instances) => new()
        {
            GeometryType = VkGeometryTypeKHR.InstancesKHR,
            Geometry = new VkAccelerationStructureGeometryDataKHR
            {
                Instances = new VkAccelerationStructureGeometryInstancesDataKHR { Data = new VkDeviceOrHostAddressConstKHR { DeviceAddress = instances } },
            },
        };

        private VkAccelerationStructureKHR Create(PhysicalDevice physical, VkAccelerationStructureTypeKHR type, ulong size, out ulong address)
        {
            var buffer = Track(AddressBuffer(_device, physical, size, VkBufferUsageFlags.AccelerationStructureStorageBitKHR, VkMemoryPropertyFlags.DeviceLocalBit, out var memory), memory);
            var structure = _device.CreateAccelerationStructureKHR(new VkAccelerationStructureCreateInfoKHR { Buffer = buffer, Size = (ByteSize64)size, Type = type }, null);
            _structures.Add(structure);
            address = _device.GetAccelerationStructureDeviceAddressKHR(new VkAccelerationStructureDeviceAddressInfoKHR { AccelerationStructure = structure }).Value;
            return structure;
        }

        private VkBuffer Track(VkBuffer buffer, VkDeviceMemory memory)
        {
            _buffers.Add((buffer, memory));
            return buffer;
        }
    }

    private static VkShaderModule Shader(Device device, string source, ShaderKind kind, string name, bool vulkan13 = false)    {
        var compiler = ShaderC.CreateCompiler();
        var options = ShaderC.CreateCompilerOptions();
        if (vulkan13)
        {
            // Ray queries need SPIR-V 1.4 or later.
            options.SetTargetEnv(TargetEnv.Vulkan, (uint)EnvVersion.Vulkan_1_3);
        }

        using var result = compiler.Compile(Encoding.UTF8.GetBytes(source), kind, Utf8.GetBytes(name + "\0"), Utf8.GetBytes("main\0"), options);
        var module = device.CreateShaderModule(new VkShaderModuleCreateInfo { CodeSize = result.GetLength(), Code = (int*)result.GetBytes() }, null);
        options.Dispose();
        compiler.Dispose();
        return module;
    }

    // The scene's pipeline (instanced cubes, MSAA, reversed depth) or the second pass's (one triangle, no vertex input).
    private static VkPipeline Pipeline(Device device, VkPipelineLayout layout, VkRenderPass renderPass, VkShaderModule vertex, VkShaderModule fragment, bool scene)
    {
        var entry = "main"u8;
        fixed (byte* entryName = entry)
        {
            var stages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                new() { Stage = VkShaderStageFlags.VertexBit, Module = vertex, Name = entryName },
                new() { Stage = VkShaderStageFlags.FragmentBit, Module = fragment, Name = entryName },
            };
            var bindings = stackalloc VkVertexInputBindingDescription[]
            {
                new() { Binding = 0, Stride = ByteSize.Of<MeshVertex>(), InputRate = VkVertexInputRate.Vertex },
                new() { Binding = 1, Stride = ByteSize.Of<Drawn>(), InputRate = VkVertexInputRate.Instance },
            };
            var attributes = stackalloc VkVertexInputAttributeDescription[]
            {
                new() { Location = 0, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = ByteSize.Zero },
                new() { Location = 1, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = ByteSize.Of<float>(3) },
                new() { Location = 2, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Zero },
                new() { Location = 3, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(4) },
                new() { Location = 4, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(8) },
                new() { Location = 5, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(12) },
                new() { Location = 6, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(16) },
                new() { Location = 7, Binding = 1, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(20) },
                new() { Location = 8, Binding = 0, Format = VkFormat.R32SFloat, Offset = ByteSize.Of<float>(6) },
            };
            var vertexInput = scene
                ? new VkPipelineVertexInputStateCreateInfo
                {
                    VertexBindingDescriptionCount = 2,
                    VertexBindingDescriptions = bindings,
                    VertexAttributeDescriptionCount = 9,
                    VertexAttributeDescriptions = attributes,
                }
                : new VkPipelineVertexInputStateCreateInfo();
            var assembly = new VkPipelineInputAssemblyStateCreateInfo { Topology = VkPrimitiveTopology.TriangleList };
            var viewport = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
            var raster = new VkPipelineRasterizationStateCreateInfo
            {
                PolygonMode = VkPolygonMode.Fill,
                CullMode = VkCullModeFlags.None,
                FrontFace = VkFrontFace.CounterClockwise,
                LineWidth = 1f,
            };
            var multisample = new VkPipelineMultisampleStateCreateInfo { RasterizationSamples = scene ? Msaa : VkSampleCountFlags._1Bit };
            var depth = new VkPipelineDepthStencilStateCreateInfo { DepthTestEnable = scene, DepthWriteEnable = scene, DepthCompareOp = VkCompareOp.Greater };
            var blendAttachment = new VkPipelineColorBlendAttachmentState
            {
                ColorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit,
            };
            var blend = new VkPipelineColorBlendStateCreateInfo { AttachmentCount = 1, Attachments = &blendAttachment };
            var dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
            var dynamic = new VkPipelineDynamicStateCreateInfo { DynamicStateCount = 2, DynamicStates = dynamicStates };

            VkPipeline pipeline;
            device.CreateGraphicsPipelines(
                default,
                [
                    new VkGraphicsPipelineCreateInfo
                    {
                        StageCount = 2,
                        Stages = stages,
                        VertexInputState = &vertexInput,
                        InputAssemblyState = &assembly,
                        ViewportState = &viewport,
                        RasterizationState = &raster,
                        MultisampleState = &multisample,
                        DepthStencilState = &depth,
                        ColorBlendState = &blend,
                        DynamicState = &dynamic,
                        Layout = layout,
                        RenderPass = renderPass,
                        Subpass = 0,
                    },
                ],
                default,
                new ReadOnlySpan<VkPipeline>(&pipeline, 1));
            return pipeline;
        }
    }
}
