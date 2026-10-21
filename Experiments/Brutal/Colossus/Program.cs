// A BRUTAL showcase for the evaluation (GuitarAlchemist/gaia#257). An original scene: a lone figure stands at the end
// of a walkway in a city of dark towers, and a swarm of machines, each one a Jolt rigid body, circles above, then
// gathers into a colossal head that looks at the figure and speaks. The towers are modelled in Blender and loaded with
// BRUTAL's glTF reader; everything else is an instance of one cube. Vulkan draws it all in one draw call per tower
// variant plus one; a Dear ImGui panel with an ImPlot graph shows the timings and steers the swarm.
// The scene renders in HDR with 4x MSAA and reversed depth; a second pass adds the sky, a bloom built from the image's own
// mips, ACES tone mapping, a vignette and grain, then the panel.
// Keys: Space assembles or disperses, C changes the camera, H hides the panel, a drag turns it, the wheel zooms, Esc
// quits.
// Arguments: --swarm N (bodies, default 10000), --seconds T (time limit, 0 = none), --immediate (no vsync),
// --no-rtx (no ray queries), --rtx-off (ray queries unticked at start), --images N (swapchain images, default 2),
// --capture T1,T2,... (save the window's image at those seconds, as
// colossus-<T>s.png in the working directory), --mute (sound at zero), --no-sound (no audio device),
// --no-voice (don't mirror the voice plugins), --face-file PATH (voice-avatar's faceFile, to follow its state),
// --hidden (an invisible window, for measuring without covering the desktop), --mailbox (no vsync and no tearing:
// each new image replaces the one waiting, where the driver offers it), --no-panel (the panel starts hidden, for a
// stream: H shows it). To compare speeds: --physics-hz N (Jolt steps a
// second, default 120, 0 = every frame), --jolt-threads N, --body-threads N (tasks steering and reading the bodies),
// --serial (wait for each step), --locking (the locking body interface), --face-collide (machines collide in the face).
// The sound is OpenAL (Sound.cs): a synthesised swarm, wind, chant and rushes, placed in the scene, under a reverb.
// The colossus speaks its lines (voice/lines.fr.txt) in the panel's language, from recordings made ahead with free voices
// (voice/fr, voice/en: see voice/README.md), and says what is typed in the panel through Piper, a local neural
// text-to-speech (Speech.cs): --piper PATH and --voice MODEL choose the engine and model, --lines FILE other lines (which
// Piper speaks), --no-tts keeps the wordless chant. The subtitles follow the panel's language (--lang en|fr, L), in English from
// voice/lines.en.txt; --no-subtitles hides them, and --cues FILE appends each line, timed and in both languages, for a
// stream's player to subtitle in its viewer's language. C cycles the cameras to a cinematic (--cinematic starts in it): hard cuts between slow
// moves, in black bands.
// --record FILE.mp4 renders a video at a fixed 60 frames a second through ffmpeg (--ffmpeg PATH), its sound mixed by
// OpenAL for each frame, in the cinematic, for --seconds (default 60), in about --record-mb megabytes (default 8).
// --dlss [dlaa|quality|balanced|performance|ultra] replaces the 4x MSAA with NVIDIA DLSS on a GeForce RTX (Streamline.cs):
// DLAA at the window's size by default, or rendered smaller and upscaled. NVIDIA's DLLs are not in the repository: --sl-path
// DIR names their folder (Streamline 2.14.1's bin), --sl-verbose prints Streamline's log. Without them, the MSAA as before.
// --rr adds DLSS Ray Reconstruction (in --dlss's mode, DLAA by default), which denoises the RTX rays: each pixel traces
// fewer of them, and glass and metal reflect through their roughness.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Brutal;
using Brutal.GlfwApi;
using Brutal.GltfApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.ImGuiApi.FreeType;
using Brutal.ImPlotApi;
using Brutal.JoltApi;
using Brutal.Localization;
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

// What DLSS adds to the scene's constants, after them: the previous frame's view-projection, without jitter, and this
// frame's jitter in clip units (x, y), with the frame's number for the rays' noise (z).
[StructLayout(LayoutKind.Sequential)]
internal struct MotionPush
{
    public SN.Matrix4x4 PrevViewProj;
    public SN.Vector4 Jitter;
}

internal static unsafe class Colossus
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int FramesInFlight = 2;
    // The largest swarm, and every instance drawn: the swarm, the city's 400-odd towers and solids, and room to spare.
    private const int MaxSwarm = 40_000;
    private const int MaxInstances = MaxSwarm + 1_024;
    private const int History = 300;
    private const int ApiVersion13 = (1 << 22) | (3 << 12);
    private const VkFormat HdrFormat = VkFormat.R16G16B16A16SFloat;
    private const VkFormat DepthFormat = VkFormat.D32SFloat;
    private const VkSampleCountFlags Msaa = VkSampleCountFlags._4Bit;
    private const VkFormat MotionFormat = VkFormat.R16G16SFloat;
    // Ray Reconstruction's guides: diffuse and specular reflectance, normal with roughness, the reflection's hit distance.
    private static readonly VkFormat[] GuideFormats = [VkFormat.R16G16B16A16SFloat, VkFormat.R16G16B16A16SFloat, VkFormat.R16G16B16A16SFloat, VkFormat.R16SFloat];
    private const int BloomLevels = 7;
    private const float Near = 0.1f;

    // The face: its centre, its half width and half height, in metres. It looks down the walkway (+z).
    private static readonly SN.Vector3 FaceCenter = new(0f, 24f, -2f);
    private const float FaceA = 13f;
    private const float FaceB = 18f;
    private const float FigureZ = 40f;

    // The cinematic's black bands, each this share of the height: 2.39:1 on a 16:9 window.
    private const float Letterbox = 0.128f;
    private const string Credits = "Colossus  ·  BRUTAL (Ahwoo), Jolt Physics, Dear ImGui, OpenAL Soft  ·  ";

    // The voice's credit, after the others: the recorded lines' (voice/README.md), or Piper's.
    private const string RecordedVoice = "Chatterbox (Resemble AI, MIT), Erick & Darius (Kyutai voice donations, CC0)";
    private const string PiperVoice = "Piper, SIWIS (CC BY 4.0)";

    // The cinematic: shots cut hard one to the next, each a slow move between two framings with its own focal length and
    // a little handheld sway, over two of the swarm's 32-second cycles; time 0 is 3 s before the face forms. Three of
    // them are remade around towers of the city once it is built (DirectedShots).
    private readonly record struct Shot(double Start, SN.Vector3 EyeFrom, SN.Vector3 EyeTo, SN.Vector3 LookFrom, SN.Vector3 LookTo, float Fov, float Sway);

    private static Shot[] Shots =
    [
        // A crane over the city toward the swarm.
        new(0.0, new(-170f, 175f, 210f), new(-125f, 140f, 155f), new(0f, 30f, 0f), new(0f, 26f, 0f), 45f, 0.15f),
        // Low behind the figure, looking up as the face forms.
        new(4.0, new(2.2f, 1.1f, 47f), new(1.5f, 1.4f, 44.5f), FaceCenter + new SN.Vector3(0f, 4f, 0f), FaceCenter, 46f, 0.03f),
        // The face from below and to the left.
        new(8.5, FaceCenter + new SN.Vector3(-18f, -16f, 28f), FaceCenter + new SN.Vector3(-13f, -14f, 24f), FaceCenter + new SN.Vector3(0f, 1f, 0f), FaceCenter + new SN.Vector3(0f, 2f, 0f), 30f, 0.08f),
        // Round the head.
        new(12.5, FaceCenter + new SN.Vector3(52f, 4f, 30f), FaceCenter + new SN.Vector3(-36f, 8f, 50f), FaceCenter, FaceCenter, 36f, 0.05f),
        // The eyes.
        new(17.0, FaceCenter + new SN.Vector3(5f, 6f, 54f), FaceCenter + new SN.Vector3(2f, 5f, 46f), FaceCenter + new SN.Vector3(0f, 4f, 0f), FaceCenter + new SN.Vector3(0f, 4f, 0f), 20f, 0.04f),
        // A crane back and up from the figure as the swarm lets go.
        new(21.0, new(1.5f, 2f, 46f), new(-25f, 40f, 110f), FaceCenter, new(0f, 26f, 0f), 44f, 0.06f),
        // Down in the street among the towers.
        new(27.0, new(-48f, 3f, 52f), new(-30f, 4f, 46f), new(0f, 30f, 0f), new(0f, 24f, 0f), 36f, 0.08f),
        // High above as it gathers again.
        new(32.5, new(55f, 120f, 70f), new(35f, 95f, 55f), new(0f, 22f, -4f), FaceCenter, 42f, 0.1f),
        // Over the figure's other shoulder.
        new(37.5, new(-2.2f, 2.3f, 46f), new(-1.6f, 2f, 44.5f), FaceCenter + new SN.Vector3(0f, 2f, 0f), FaceCenter, 32f, 0.03f),
        // In profile.
        new(43.5, FaceCenter + new SN.Vector3(-34f, 0f, 16f), FaceCenter + new SN.Vector3(-30f, 2f, 10f), FaceCenter, FaceCenter, 40f, 0.05f),
        // A slow push in, face on.
        new(49.5, FaceCenter + new SN.Vector3(0f, 2f, 62f), FaceCenter + new SN.Vector3(0f, 3f, 36f), FaceCenter + new SN.Vector3(0f, 2f, 0f), FaceCenter + new SN.Vector3(0f, 3f, 0f), 30f, 0.03f),
        // A last crane up and away.
        new(56.0, new(0f, 5f, 58f), new(0f, 75f, 150f), new(0f, 24f, 0f), new(0f, 30f, 0f), 50f, 0.08f),
    ];

    private const double CinematicLength = 64.0;

    private static (SN.Vector3 Eye, SN.Vector3 Target, float Fov) Cinematic(double time)
    {
        var t = ((time % CinematicLength) + CinematicLength) % CinematicLength;
        var k = Shots.Length - 1;
        while (k > 0 && Shots[k].Start > t)
        {
            k--;
        }

        var shot = Shots[k];
        var end = k + 1 < Shots.Length ? Shots[k + 1].Start : CinematicLength;
        var u = (float)((t - shot.Start) / (end - shot.Start));
        // Half steady, half eased: the move starts and ends gently but never stops.
        var s = 0.5f * (u + (u * u * (3f - (2f * u))));
        var w = (float)time;
        var sway = new SN.Vector3(
            MathF.Sin(w * 1.3f) + (0.5f * MathF.Sin((w * 2.9f) + 1f)),
            MathF.Sin((w * 1.7f) + 2f) + (0.4f * MathF.Sin(w * 3.7f)),
            MathF.Sin((w * 1.1f) + 4f)) * shot.Sway;
        var eye = SN.Vector3.Lerp(shot.EyeFrom, shot.EyeTo, s) + sway;
        var target = SN.Vector3.Lerp(shot.LookFrom, shot.LookTo, s) + (sway * 0.5f);
        return (eye, target, shot.Fov);
    }

    // A modelled tower's style, as the scene's shader picks it from its seed (surface): glass walls under 0.3, bronze
    // under 0.375, steel under 0.45, raw concrete under 0.8, the facade texture's panels above.
    private static float Style(float seed) => seed * 7.13f % 1f;

    // Remakes three of the cinematic's shots round towers of the city as built, and casts each tower in the style its
    // shot shows, through its seed: up the foot of a glass curtain wall, then a tilt down to the face; a track behind a
    // concrete tower that hides the face, until it shows past its edge; a track beside bronze cladding, panning from the
    // tower to the face. Each shot takes the tower nearest the face, in front of it, tall enough and not yet cast, whose
    // shot passes clear of every tower; a shot with no tower to fit it stays as it was. Each tower: its instance, its
    // mesh's top and half width before scaling. Returns the shots, and the towers cast with their new seeds.
    private static (Shot[] Shots, List<(int Tower, float Seed)> Cast) DirectedShots(Shot[] shots, List<(Drawn Tower, float Top, float Width)> towers)
    {
        var placed = towers.Select((t, i) =>
        {
            var m = t.Tower.Model;
            var scale = new SN.Vector3(m.M11, m.M12, m.M13).Length();
            return (Index: i, Center: new SN.Vector3(m.M41, 0f, m.M43), Half: t.Width * scale, Top: m.M42 + (t.Top * scale));
        }).ToList();

        // Clear of a tower: above it, or outside the circle round its footprint, with 3 m to spare.
        bool Clear(SN.Vector3 p) => placed.All(t => p.Y > t.Top + 3f || SN.Vector2.Distance(new(p.X, p.Z), new(t.Center.X, t.Center.Z)) > (t.Half * 1.42f) + 3f);
        bool PathClear(SN.Vector3 a, SN.Vector3 b) => Enumerable.Range(0, 9).All(i => Clear(SN.Vector3.Lerp(a, b, i / 8f)));
        var face = new SN.Vector3(FaceCenter.X, 0f, FaceCenter.Z);
        var cast = new List<(int Tower, float Seed)>();

        // The first shot make frames round a tower, on either side of it, and that tower cast with seed.
        Shot? Cast(float minTop, float seed, Func<SN.Vector3, float, float, SN.Vector3, SN.Vector3, Shot?> make)
        {
            foreach (var t in placed.Where(t => t.Top > minTop && t.Center.Z > 10f && cast.All(c => c.Tower != t.Index)).OrderBy(t => SN.Vector3.Distance(t.Center, face)))
            {
                var toFace = SN.Vector3.Normalize(face - t.Center);
                var across = new SN.Vector3(-toFace.Z, 0f, toFace.X);
                if ((make(t.Center, t.Half, t.Top, toFace, across) ?? make(t.Center, t.Half, t.Top, toFace, -across)) is { } shot)
                {
                    cast.Add((t.Index, seed));
                    return shot;
                }
            }

            return null;
        }

        var result = shots.ToArray();
        // 0: up the foot of a glass curtain wall, looking high up it, then down to the swarm as it gathers. Seed 0.4418
        // is the glass style (0.15).
        result[0] = Cast(40f, 0.4418f, (c, half, top, toFace, across) =>
        {
            var from = c + (across * ((half * 1.42f) + 5f)) + (toFace * half * 0.3f) + new SN.Vector3(0f, -24f, 0f);
            var to = from + (toFace * 8f) + new SN.Vector3(0f, 12f, 0f);
            return PathClear(from, to) ? new Shot(shots[0].Start, from, to, c + (toFace * half * 0.3f) + new SN.Vector3(0f, top * 0.8f, 0f), FaceCenter, 55f, 0.05f) : null;
        }) ?? shots[0];
        // 6: behind a concrete tower that hides the face, tracking sideways until the face shows past its edge; at the
        // face's height, or higher to pass over the towers around, as long as the tower still rises 20 m above the
        // camera. Seed 0.505 is raw concrete (0.6).
        result[6] = Cast(FaceCenter.Y + FaceB, 0.505f, (c, half, top, toFace, across) =>
        {
            foreach (var height in new[] { FaceCenter.Y, FaceCenter.Y + 14f, FaceCenter.Y + 28f })
            {
                var back = c - (toFace * ((half * 1.42f) + 14f)) + new SN.Vector3(0f, height, 0f);
                var from = back - (across * half * 0.2f);
                var to = back + (across * ((half * 2.2f) + 14f));
                if (top > height + 20f && PathClear(from, to))
                {
                    return new Shot(shots[6].Start, from, to, FaceCenter, FaceCenter + new SN.Vector3(0f, 2f, 0f), 34f, 0.04f);
                }
            }

            return null;
        }) ?? shots[6];
        // 9: beside bronze cladding, panning from the tower's upper corner to the face. Seed 0.4684 is bronze (0.34).
        result[9] = Cast(FaceCenter.Y + 10f, 0.4684f, (c, half, top, toFace, across) =>
        {
            var side = c + (across * ((half * 1.42f) + 4f));
            var from = side - (toFace * half * 0.8f) + new SN.Vector3(0f, 8f, 0f);
            var to = side + (toFace * half * 0.6f) + new SN.Vector3(0f, 14f, 0f);
            var corner = c + (toFace * half) + new SN.Vector3(0f, MathF.Min(top - 4f, FaceCenter.Y + 10f), 0f);
            return PathClear(from, to) ? new Shot(shots[9].Start, from, to, corner, FaceCenter, 45f, 0.05f) : null;
        }) ?? shots[9];
        Console.WriteLine($"CINEMATIC towers cast: {string.Join(", ", cast.Select(c => $"{c.Tower} at {placed[c.Tower].Center} as {Style(c.Seed):0.00}"))}");

        // Any other shot whose move would pass through a tower moves in toward what it looks at until it is clear, or
        // else rises, so that the camera never ends up inside a wall.
        for (var i = 0; i < result.Length; i++)
        {
            var s = result[i];
            if (PathClear(s.EyeFrom, s.EyeTo))
            {
                continue;
            }

            var moved = Enumerable.Range(1, 7).Select(k => s with { EyeFrom = SN.Vector3.Lerp(s.EyeFrom, s.LookFrom, k / 10f), EyeTo = SN.Vector3.Lerp(s.EyeTo, s.LookTo, k / 10f) })
                .Concat(Enumerable.Range(1, 8).Select(k => s with { EyeFrom = s.EyeFrom + new SN.Vector3(0f, k * 10f, 0f), EyeTo = s.EyeTo + new SN.Vector3(0f, k * 10f, 0f) }))
                .FirstOrDefault(m => PathClear(m.EyeFrom, m.EyeTo), s);
            Console.WriteLine($"CINEMATIC shot {i} at {s.Start} s passed through a tower: {(moved == s ? "left as it is" : $"now from {moved.EyeFrom} to {moved.EyeTo}")}");
            result[i] = moved;
        }

        return (result, cast);
    }

    // ffmpeg: the frames come raw on its standard input, 60 a second; each pair is blended into one frame of a 30-frame
    // video (a light motion blur), scaled to 1280×720 and kept nearly lossless until Finish makes the small one.
    private static Process StartEncoder(string ffmpeg, string video, int width, int height)
    {
        var info = new ProcessStartInfo(ffmpeg) { RedirectStandardInput = true, UseShellExecute = false };
        foreach (var arg in new[]
        {
            "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "bgra", "-s", $"{width}x{height}", "-r", "60", "-i", "-",
            "-vf", "tmix=frames=2,fps=30,scale=1280:720:flags=lanczos,format=yuv420p", "-c:v", "libx264", "-preset", "veryfast",
            "-crf", "14", video,
        })
        {
            info.ArgumentList.Add(arg);
        }

        return Process.Start(info) ?? throw new InvalidOperationException("ffmpeg did not start");
    }

    // The small video: H.264 in two passes at the bitrate that fits --record-mb (default 8 MB), with the sound as AAC at
    // 96 kb/s, brought to -16 LUFS for a phone's speaker.
    private static bool Finish(string ffmpeg, string video, string audio, string output, double seconds, double megabytes)
    {
        var kbps = (int)(((megabytes * 8192) / seconds) - 96 - 24);
        var log = output + ".pass";
        string[] input = ["-y", "-loglevel", "error", "-i", video];
        string[] encode = ["-c:v", "libx264", "-preset", "slow", "-b:v", $"{kbps}k", "-passlogfile", log];
        var ok = Run([.. input, .. encode, "-pass", "1", "-an", "-f", "null", "-"])
            && Run([.. input, "-f", "s16le", "-ar", "48000", "-ac", "2", "-i", audio, .. encode, "-pass", "2",
                "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-ar", "48000", "-c:a", "aac", "-b:a", "96k", "-shortest", "-movflags", "+faststart", output]);
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(output)!, Path.GetFileName(log) + "*"))
        {
            File.Delete(file);
        }

        return ok;

        bool Run(string[] args)
        {
            var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false };
            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info) ?? throw new InvalidOperationException("ffmpeg did not start");
            process.WaitForExit();
            return process.ExitCode == 0;
        }
    }

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
        #ifdef DLSS
        // DLSS: the instance's model matrix in the previous frame, the previous view-projection, and this frame's jitter.
        layout(location = 9) in vec4 pModel0;
        layout(location = 10) in vec4 pModel1;
        layout(location = 11) in vec4 pModel2;
        layout(location = 12) in vec4 pModel3;
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; mat4 prevViewProj; vec4 jitter; } pc;
        layout(location = 10) out vec4 vClip;
        layout(location = 11) out vec4 vPrevClip;
        #else
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; } pc;
        #endif
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
            #ifdef DLSS
            vClip = gl_Position;
            vPrevClip = pc.prevViewProj * (mat4(pModel0, pModel1, pModel2, pModel3) * vec4(inPos, 1.0));
            gl_Position.xy += pc.jitter.xy * gl_Position.w;
            #endif
        }
        """;

    // The scene in HDR. One function, surface, gives every material, and the RTX reflections call it again where their
    // rays land: raw concrete cast in board formwork, glass curtain walls, bronze and steel cladding, the facade
    // texture's panels and lamps on the modelled towers, each tower in a style of its own; machines with their hull and
    // pulsing eye; the walkway's concrete. Lighting: the low sun, a warm bounce from the abyss, a cool fill from the city,
    // a backlit rim, GGX highlights and Schlick's Fresnel. With RTX: soft sun shadows (two rays in the sun's disc),
    // ambient occlusion (four short rays), and a mirror ray on glass and metal whose hit is shaded in full: its surface,
    // the sun on it and its own shadow ray. The rays' noise is fixed on screen, so that a still view does not shimmer.
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
        // Where a reflection lands: the mesh's vertices (MeshVertex: position, normal, material, seven floats) and the
        // frame's instances (Drawn), in the order of the top-level structure.
        layout(std430, set = 0, binding = 2) readonly buffer Mesh { float mesh[]; };
        struct Instance { mat4 model; vec4 color; vec4 extra; };
        layout(std430, set = 0, binding = 3) readonly buffer Instances { Instance instances[]; };
        #endif
        #ifdef DLSS
        layout(location = 10) in vec4 vClip;
        layout(location = 11) in vec4 vPrevClip;
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; mat4 prevViewProj; vec4 jitter; } pc;
        layout(location = 1) out vec4 outMotion;
        #ifdef RR
        layout(location = 2) out vec4 outDiffuse;
        layout(location = 3) out vec4 outSpecular;
        layout(location = 4) out vec4 outNormal;
        layout(location = 5) out float outHit;
        #endif
        #else
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; } pc;
        #endif
        layout(location = 0) out vec4 outColor;
        const vec3 SunColor = vec3(1.0, 0.56, 0.26);
        const vec3 Bronze = vec3(0.55, 0.36, 0.2);
        const vec3 Steel = vec3(0.42, 0.43, 0.45);
        const vec3 DarkSteel = vec3(0.22, 0.22, 0.23);

        // A surface: its diffuse colour, or its reflectance where it is metal; its glow; a bump along the face's two
        // texture axes; its roughness; its reflectance head-on where it is not metal; how much it is metal.
        struct Surface { vec3 albedo; vec3 emit; vec2 bump; float rough; float f0; float metal; };

        // Coordinates on the face's plane, in the same box mapping as the vertex shader's tangents.
        vec2 boxST(vec3 p, vec3 un)
        {
            return vec2(dot(p, abs(un.x) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0)),
                dot(p, abs(un.y) > 0.5 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0)));
        }

        // A texture layer tiled every tile metres: with the pixel's own derivatives (footprint < 0), or, where a ray
        // lands, at the level of detail for a footprint of that many metres.
        vec4 tex(vec2 uv, float layer, float tile, float footprint)
        {
            if (footprint < 0.0)
            {
                return texture(surfaces, vec3(uv, layer));
            }

            return textureLod(surfaces, vec3(uv, layer), log2(max(footprint * float(textureSize(surfaces, 0).x) / tile, 1.0)));
        }

        // A curtain wall in cells 1.6 m wide and one floor high: panes between mullions, a spandrel at each slab, a few
        // offices lit (most warm, some cool), some panes with their blinds down. Each pane leans a little, as real glass
        // does, so that reflections break from pane to pane. px is a pixel's size in metres: as a cell shrinks under two
        // pixels, the panes fade to their average, so that nothing shimmers far away. f0 is the glass's reflectance head
        // on: mirror glass reflects more and shows less of the offices behind it.
        Surface curtain(vec2 st, vec3 un, float seed, float floorHeight, float px, float f0)
        {
            vec2 cell = vec2(st.x / 1.6, st.y / floorHeight);
            vec2 f = fract(cell);
            vec2 id = floor(cell) + vec2(seed * 97.0, dot(un, vec3(1.0, 2.0, 3.0)) * 13.0);
            float r = hash12(id);
            float r2 = hash12(id + 17.0);
            float fade = clamp(1.0 - (px / 1.6 - 0.25) * 4.0, 0.0, 1.0);
            float frame = (1.0 - step(0.05, f.x) * step(f.x, 0.95) * step(0.03, f.y) * step(f.y, 0.8)) * fade;
            float blinds = step(0.84, fract(r * 23.7)) * (1.0 - f0);
            float lit = step(r, 0.07) * (1.0 - f0);
            vec3 light = fract(r * 9.1) < 0.7 ? mix(vec3(1.0, 0.42, 0.12), vec3(1.0, 0.72, 0.42), fract(r * 5.3)) * 2.0 : vec3(0.7, 0.82, 1.0) * 1.4;
            // An office's light: brightest under its ceiling, dimmer toward its floor and the pane's sides.
            float room = (0.3 + 0.7 * smoothstep(0.0, 0.8, f.y)) * (0.7 + 0.3 * sin(3.14159 * f.x));
            Surface s;
            s.albedo = mix(mix(vec3(0.012, 0.016, 0.02) * (0.7 + 0.6 * r2), vec3(0.11, 0.095, 0.08), blinds * fade), DarkSteel, frame);
            s.emit = mix(vec3(1.0, 0.56, 0.26) * 0.1 * (1.0 - f0), lit * (1.0 - frame) * light * room, fade);
            s.bump = (vec2(r, r2) - 0.5) * 0.05 * fade * (1.0 - frame);
            s.rough = mix(0.04 + 0.06 * r2, 0.35, frame);
            s.f0 = f0;
            s.metal = frame;
            return s;
        }

        // Raw concrete cast in formwork, from the concrete texture (panels of 2.4 by 1.2 m with their tie holes, a tile
        // every 4.8 m), its tone mottled over tens of metres so that the tiles do not repeat, and rain streaks running
        // down, which fade to their average as they shrink under a pixel (px, in metres).
        Surface concrete(vec2 st, float seed, float footprint, float px)
        {
            vec2 q = st + seed * vec2(37.0, 11.0);
            vec2 uv = vec2(q.x, -q.y) / 4.8;
            vec4 map = tex(uv, 5.0, 4.8, footprint);
            float tone = 0.65 + 0.45 * noise(q * 0.08) + 0.15 * noise(q * 0.6);
            float rain = mix(0.06, smoothstep(0.5, 0.9, noise(vec2(q.x * 1.7, q.y * 0.045))) * 0.4, clamp(1.0 - px / 0.3, 0.0, 1.0));
            Surface s;
            s.albedo = pow(tex(uv, 4.0, 4.8, footprint).rgb, vec3(2.2)) * 0.32 * tone * (1.0 - rain);
            s.emit = vec3(0.0);
            s.bump = (map.rg * 2.0 - 1.0) * 1.5;
            s.rough = mix(0.75, 0.95, map.b) - rain * 0.3;
            s.f0 = 0.03;
            s.metal = 0.0;
            return s;
        }

        // Metal cladding, from the cladding texture (brushed panels of 1.2 by 2.4 m, a tile every 4.8 m): bronze, steel
        // or dark steel.
        Surface cladding(vec2 st, float seed, vec3 tint, float footprint)
        {
            vec2 uv = vec2(st.x, -st.y) / 4.8 + seed * vec2(5.1, 2.9);
            vec4 map = tex(uv, 7.0, 4.8, footprint);
            Surface s;
            s.albedo = tint * (0.45 + 0.7 * dot(tex(uv, 6.0, 4.8, footprint).rgb, vec3(0.3, 0.6, 0.1)));
            s.emit = vec3(0.0);
            s.bump = (map.rg * 2.0 - 1.0) * 1.0;
            s.rough = 0.15 + 0.35 * map.b;
            s.f0 = 0.04;
            s.metal = 1.0;
            return s;
        }

        // The facade texture's concrete panels, a tile every 12.8 m.
        Surface facade(vec2 st, float seed, float footprint)
        {
            vec2 uv = vec2(st.x / 12.8, -st.y / 12.8) + seed * vec2(3.7, 1.3);
            vec4 map = tex(uv, 3.0, 12.8, footprint);
            Surface s;
            s.albedo = pow(tex(uv, 2.0, 12.8, footprint).rgb, vec3(2.2)) * 0.32;
            s.emit = vec3(0.0);
            s.bump = (map.rg * 2.0 - 1.0) * 3.0;
            s.rough = map.b;
            s.f0 = 0.04;
            s.metal = 0.0;
            return s;
        }

        // The surface of an instance of kind (0 plain, 1 machine, 2 box tower, 3 lamp, 4 modelled tower) where the mesh's
        // material is mat: unit is the point in the mesh's own space, p in metres on the instance, st on the face's plane,
        // un the mesh's normal. footprint and px are a pixel's size in metres (footprint < 0: the pixel's derivatives).
        Surface surface(float kind, float mat, vec4 color, float seed, vec3 unit, vec3 p, vec2 st, vec3 un, vec3 scale, float footprint, float px, float t)
        {
            Surface s = Surface(color.rgb, color.rgb * color.a, vec2(0.0), 0.8, 0.04, 0.0);
            bool side = abs(un.y) < 0.5;
            if (kind > 0.5 && kind < 1.5)
            {
                // A machine: its hull, and its eye, which pulses.
                vec2 uv = st / 1.2 + seed * vec2(7.31, 3.17);
                vec4 map = tex(uv, 1.0, 1.2, footprint);
                s.albedo = pow(tex(uv, 0.0, 1.2, footprint).rgb, vec3(2.2)) * 0.45;
                s.bump = (map.rg * 2.0 - 1.0) * 2.0;
                s.rough = map.b;
                s.f0 = 0.15;
                float eye = (1.0 - smoothstep(0.18, 0.32, length(unit.xy))) * step(0.49, unit.z);
                s.emit += vec3(1.0, 0.3, 0.06) * eye * (2.5 + 1.5 * sin(t * 6.0 + seed * 40.0));
            }
            else if (kind > 1.5 && kind < 2.5)
            {
                // A box tower, when towers.glb is missing: glass on its sides, concrete on its roof.
                s = side ? curtain(st, un, seed, 3.0, px, 0.2) : concrete(st, seed, footprint, px);
            }
            else if (kind > 3.5)
            {
                // A modelled tower, in a style chosen by its seed: its walls in glass, clad in bronze or steel, in raw
                // concrete, or in the facade texture's panels. The mesh's own glass, metal and lamps stay as modelled.
                float style = fract(seed * 7.13);
                if (mat > 2.5)
                {
                    s.albedo = vec3(0.0);
                    s.emit = vec3(1.0, 0.45, 0.12) * (4.0 + 2.0 * sin(t * 2.0 + seed * 31.0));
                }
                else if ((mat > 0.5 && mat < 1.5) || (side && style < 0.3))
                {
                    s = curtain(st, un, seed, 3.2, px, style < 0.3 ? 0.45 : 0.2);
                }
                else if (mat > 1.5 || (side && style < 0.45))
                {
                    s = cladding(st, seed, mat > 1.5 ? DarkSteel : style < 0.375 ? Bronze : Steel, footprint);
                }
                else
                {
                    s = style < 0.8 ? concrete(st, seed, footprint, px) : facade(st, seed, footprint);
                }
            }
            else if (kind < 0.5 && min(scale.x, min(scale.y, scale.z)) > 1.0)
            {
                // The walkway and its piers; the figure, smaller, keeps its cloth.
                s = concrete(st, seed, footprint, px);
            }

            return s;
        }

        // The light that does not come from the sun: a warm bounce from the abyss below, the cool sky above.
        vec3 skylight(vec3 n)
        {
            return mix(vec3(0.07, 0.035, 0.015), vec3(0.03, 0.04, 0.065), n.y * 0.5 + 0.5);
        }

        // Interleaved gradient noise: a fixed pattern on screen, which spreads a few rays evenly between neighbours.
        float ign(vec2 p)
        {
            #ifdef DLSS
            // Under DLSS the pattern moves each frame, and DLSS averages the rays' noise over the frames.
            p += 5.588238 * pc.jitter.z;
            #endif
            return fract(52.9829189 * fract(dot(p, vec2(0.06711056, 0.00583715))));
        }

        #ifdef RR
        // Ray Reconstruction gathers the rays over the frames and between neighbours: one through the sun's disc and two
        // for the occlusion suffice. Without it, two and four.
        const int ShadowRays = 1;
        const int OcclusionRays = 2;

        // A microfacet's normal around n, drawn from the GGX normals that the view v sees (Heitz, 2018) at roughness
        // alpha; u is two numbers in [0, 1). A reflection about it follows the surface's own spread.
        vec3 ggxNormal(vec3 n, vec3 v, float alpha, vec2 u)
        {
            vec3 tx = normalize(abs(n.y) < 0.99 ? cross(n, vec3(0.0, 1.0, 0.0)) : cross(n, vec3(1.0, 0.0, 0.0)));
            vec3 ty = cross(n, tx);
            vec3 vh = normalize(vec3(alpha * dot(v, tx), alpha * dot(v, ty), max(dot(v, n), 1e-4)));
            float len2 = dot(vh.xy, vh.xy);
            vec3 t1 = len2 > 0.0 ? vec3(-vh.y, vh.x, 0.0) * inversesqrt(len2) : vec3(1.0, 0.0, 0.0);
            vec3 t2 = cross(vh, t1);
            float r = sqrt(u.x);
            float phi = 6.2831853 * u.y;
            float p1 = r * cos(phi);
            float p2 = mix(sqrt(1.0 - p1 * p1), r * sin(phi), 0.5 * (1.0 + vh.z));
            vec3 nh = p1 * t1 + p2 * t2 + sqrt(max(0.0, 1.0 - p1 * p1 - p2 * p2)) * vh;
            vec3 m = normalize(vec3(alpha * nh.x, alpha * nh.y, max(nh.z, 1e-4)));
            return normalize(tx * m.x + ty * m.y + n * m.z);
        }

        // The specular reflectance over the hemisphere for reflectance f0 head on, at roughness alpha, seen at nv: the
        // fit that NVIDIA's guide to Ray Reconstruction gives (Ray Tracing Gems, chapter 32). v * mat2(...) is HLSL's
        // mul(matrix, v) with the matrix's rows in that order.
        vec3 specularAlbedo(vec3 f0, float alpha, float nv)
        {
            vec4 x = vec4(1.0, nv, nv * nv, nv * nv * nv);
            vec4 y = vec4(1.0, alpha, alpha * alpha, alpha * alpha * alpha);
            float bias = dot(x.xy * mat2(0.99044, -1.28514, 1.29678, -0.755907), y.xy)
                / dot(x.xyw * mat3(1.0, 2.92338, 59.4188, 20.3225, -27.0302, 222.592, 121.563, 626.13, 316.627), y.xyw);
            float scale = dot(x.xy * mat2(0.0365463, 3.32707, 9.0632, -9.04756), y.xy)
                / dot(x.xzw * mat3(1.0, 3.59685, -1.36772, 9.04401, -16.3174, 9.22949, 5.56589, 19.7886, -20.2123), y.xyw);
            bias *= clamp(f0.g * 50.0, 0.0, 1.0);
            return f0 * max(scale, 0.0) + max(bias, 0.0);
        }
        #else
        const int ShadowRays = 2;
        const int OcclusionRays = 4;
        #endif

        void main()
        {
            vec3 ng = normalize(vNormal);
            vec3 toEye = pc.eye.xyz - vWorld;
            float dist = length(toEye);
            vec3 v = toEye / dist;
            vec3 sun = pc.sun.xyz;
            float t = pc.eye.w;
            float kind = vExtra.x;
            // Modelled towers map in their own metres, so that window cells keep to the floors at any scale.
            vec3 p = kind > 3.5 ? vUnit : vUnit * vScale;
            vec2 st = boxST(p, vUnitNormal);
            float px = max(max(fwidth(st.x), fwidth(st.y)), 1e-4);
            Surface s = surface(kind, vMaterial, vColor, vExtra.y, vUnit, p, st, vUnitNormal, vScale, -1.0, px, t);
            vec3 n = normalize(ng + vTangent * s.bump.x + vBitangent * s.bump.y);
            float mirror = (1.0 - s.rough) * (1.0 - s.rough);
            float fog = 1.0 - exp(-dist * 0.0028);
            fog = max(fog, clamp(-vWorld.y / 140.0, 0.0, 1.0) * (1.0 - exp(-dist * 0.012)));

            float shadow = 1.0;
            float ao = 1.0;
            vec3 traced = vec3(-1.0);
            vec3 mirrored = reflect(-v, n);
            float hitDistance = -1.0;
            #ifdef RTX
            if (pc.params.y > 0.5)
            {
                vec3 origin = vWorld + ng * 0.04;
                vec2 noise2 = vec2(ign(gl_FragCoord.xy), ign(gl_FragCoord.xy + vec2(47.0, 17.0)));
                // The swarm shades the city, the walkway and the figure, but not itself: its machines would hide each
                // other's sunlight and the head would read as a silhouette. Rays through the sun's disc, widened to 1.4
                // degrees by the haze, soften the shadows' edges.
                uint casters = kind > 0.5 && kind < 1.5 ? 0x01u : 0x05u;
                vec3 sx = normalize(cross(sun, vec3(0.0, 1.0, 0.0)));
                vec3 sy = cross(sun, sx);
                shadow = 0.0;
                for (int i = 0; i < ShadowRays; i++)
                {
                    float a = 6.2831853 * fract(noise2.x + float(i) / float(ShadowRays));
                    vec3 d = normalize(sun + (sx * cos(a) + sy * sin(a)) * sqrt(fract(noise2.y + float(i) / float(ShadowRays))) * 0.012);
                    rayQueryEXT q;
                    rayQueryInitializeEXT(q, scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT, casters, origin, 0.02, d, 800.0);
                    while (rayQueryProceedEXT(q)) {}
                    shadow += rayQueryGetIntersectionTypeEXT(q, true) == gl_RayQueryCommittedIntersectionNoneEXT ? 1.0 / float(ShadowRays) : 0.0;
                }

                // Ambient occlusion: rays over the hemisphere, cosine-weighted, up to 6 m, against the city and the
                // walkway; a near hit darkens more than a far one. Not on the machines, nor deep in the haze.
                if ((kind < 0.5 || kind > 1.5) && fog < 0.9)
                {
                    vec3 bx = normalize(abs(ng.y) < 0.99 ? cross(ng, vec3(0.0, 1.0, 0.0)) : cross(ng, vec3(1.0, 0.0, 0.0)));
                    vec3 by = cross(ng, bx);
                    float occluded = 0.0;
                    for (int i = 0; i < OcclusionRays; i++)
                    {
                        float u = fract(noise2.x + float(i) / float(OcclusionRays));
                        float a = 6.2831853 * fract(noise2.y + float(i) * 0.618034);
                        vec3 d = normalize((bx * cos(a) + by * sin(a)) * sqrt(u) + ng * sqrt(1.0 - u));
                        rayQueryEXT q;
                        rayQueryInitializeEXT(q, scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT, 0x01u, origin, 0.02, d, 6.0);
                        while (rayQueryProceedEXT(q)) {}
                        if (rayQueryGetIntersectionTypeEXT(q, true) != gl_RayQueryCommittedIntersectionNoneEXT)
                        {
                            occluded += 1.0 - rayQueryGetIntersectionTEXT(q, true) / 6.0;
                        }
                    }

                    ao = 1.0 - occluded / float(OcclusionRays) * 0.85;
                }

                // The mirror ray, on glass and metal: where it lands, that surface lit by the sun (behind its own shadow
                // ray) and the abyss's bounce, glowing if it glows, with the sky in it if it shines, hazed by distance.
                // Under Ray Reconstruction it leaves about one of the surface's microfacets, drawn anew each frame, and
                // the reflections blur as the surfaces are rough.
                if (mirror > 0.2)
                {
                    #ifdef RR
                    vec2 noise3 = vec2(ign(gl_FragCoord.xy + vec2(13.0, 71.0)), ign(gl_FragCoord.xy + vec2(89.0, 29.0)));
                    vec3 r = reflect(-v, ggxNormal(n, v, s.rough * s.rough, noise3));
                    #else
                    vec3 r = reflect(-v, n);
                    #endif
                    r = normalize(r + ng * max(0.02 - dot(r, ng), 0.0));
                    #ifdef RR
                    mirrored = r;
                    #endif
                    rayQueryEXT m;
                    rayQueryInitializeEXT(m, scene, gl_RayFlagsOpaqueEXT, 0xFF, origin, 0.05, r, 900.0);
                    while (rayQueryProceedEXT(m)) {}
                    if (rayQueryGetIntersectionTypeEXT(m, true) != gl_RayQueryCommittedIntersectionNoneEXT)
                    {
                        float d = rayQueryGetIntersectionTEXT(m, true);
                        hitDistance = d;
                        Instance hit = instances[rayQueryGetIntersectionInstanceIdEXT(m, true)];
                        uint at = (uint(rayQueryGetIntersectionInstanceCustomIndexEXT(m, true)) + uint(rayQueryGetIntersectionPrimitiveIndexEXT(m, true)) * 3u) * 7u;
                        vec2 bc = rayQueryGetIntersectionBarycentricsEXT(m, true);
                        vec3 hun = normalize(vec3(mesh[at + 3u], mesh[at + 4u], mesh[at + 5u]) * (1.0 - bc.x - bc.y)
                            + vec3(mesh[at + 10u], mesh[at + 11u], mesh[at + 12u]) * bc.x
                            + vec3(mesh[at + 17u], mesh[at + 18u], mesh[at + 19u]) * bc.y);
                        mat4x3 w2o = rayQueryGetIntersectionWorldToObjectEXT(m, true);
                        vec3 hp = origin + r * d;
                        vec3 hunit = w2o * vec4(hp, 1.0);
                        vec3 hscale = vec3(length(hit.model[0].xyz), length(hit.model[1].xyz), length(hit.model[2].xyz));
                        vec3 hpm = hit.extra.x > 3.5 ? hunit : hunit * hscale;
                        float foot = (dist + d) * 0.0012;
                        Surface hs = surface(hit.extra.x, mesh[at + 6u], hit.color, hit.extra.y, hunit, hpm, boxST(hpm, hun), hun, hscale, foot, foot, t);
                        vec3 hn = normalize(hun * mat3(w2o));
                        hn = dot(hn, r) > 0.0 ? -hn : hn;
                        float hl = max(dot(hn, sun), 0.0);
                        if (hl > 0.0)
                        {
                            rayQueryEXT q;
                            rayQueryInitializeEXT(q, scene, gl_RayFlagsTerminateOnFirstHitEXT | gl_RayFlagsOpaqueEXT, 0x05u, hp + hn * 0.05, 0.02, sun, 800.0);
                            while (rayQueryProceedEXT(q)) {}
                            hl = rayQueryGetIntersectionTypeEXT(q, true) == gl_RayQueryCommittedIntersectionNoneEXT ? hl : 0.0;
                        }

                        vec3 hf0 = mix(vec3(hs.f0), hs.albedo, hs.metal);
                        vec3 hF = hf0 + (1.0 - hf0) * pow(1.0 - max(dot(hn, -r), 0.0), 5.0);
                        vec3 lit = hs.albedo * (1.0 - hs.metal) * (skylight(hn) + SunColor * 1.8 * hl) + hs.emit
                            + hF * sky(reflect(r, hn), sun, t) * (1.0 - hs.rough) * (1.0 - hs.rough);
                        traced = mix(lit, sky(r, sun, t) * 0.55, 1.0 - exp(-d * 0.0028));
                    }
                }
            }
            #endif

            // The sun's GGX highlight and the reflection, weighed by Schlick's Fresnel; the reflection of a rough surface
            // fades to the ambient light. Metals reflect their colour and are not diffuse.
            float ndl = max(dot(n, sun), 0.0) * shadow;
            float nv = max(dot(n, v), 1e-3);
            vec3 f0 = mix(vec3(s.f0), s.albedo, s.metal);
            vec3 fresnel = f0 + (1.0 - f0) * pow(1.0 - nv, 5.0);
            vec3 h = normalize(sun + v);
            float a2 = pow(max(s.rough * s.rough, 0.03), 2.0);
            float nh = max(dot(n, h), 0.0);
            float lh = max(dot(sun, h), 0.1);
            float dd = nh * nh * (a2 - 1.0) + 1.0;
            vec3 spec = SunColor * 1.8 * (a2 / (3.14159 * dd * dd)) * 0.25 / (lh * lh) * (f0 + (1.0 - f0) * pow(1.0 - lh, 5.0)) * ndl;
            vec3 ambient = skylight(n) * ao;
            vec3 fill = vec3(0.2, 0.25, 0.38) * max(dot(n, normalize(vec3(-0.3, 0.45, 1.0))), 0.0) * ao;
            float back = pow(1.0 - nv, 4.0) * pow(max(dot(-v, sun), 0.0), 3.0);
            vec3 reflected = traced.x >= 0.0 ? traced : sky(mirrored, sun, t);
            vec3 env = fresnel * mix(ambient * 2.0, reflected, mirror);
            vec3 c = s.albedo * (1.0 - s.metal) * (1.0 - fresnel) * (ambient + fill + SunColor * 1.8 * ndl) + spec + env
                + SunColor * back * 1.4 * (0.6 + 0.4 * shadow) + s.emit;
            c = mix(c, sky(-v, sun, t) * 0.55, fog);
            outColor = vec4(c, 1.0);
            #ifdef DLSS
            // Where this point was on screen in the previous frame, from where it is now, in UV units.
            outMotion = vec4((vPrevClip.xy / vPrevClip.w - vClip.xy / vClip.w) * 0.5, 0.0, 0.0);
            #endif
            #ifdef RR
            // Ray Reconstruction's guides: the surface without its light. Its diffuse reflectance (at least 0.04, as in
            // NVIDIA's own renderer) and its specular one over the hemisphere, which the haze turns toward the sky's, as it
            // does the colour; its shading normal in world space and its roughness; how far its reflection ray went, or
            // -1 where none went or it met only sky.
            outDiffuse = vec4(mix(max(s.albedo * (1.0 - s.metal), vec3(0.04)), sky(-v, sun, t) * 0.55, fog), 1.0);
            outSpecular = vec4(clamp(specularAlbedo(f0, s.rough * s.rough, nv), 0.0, 1.0) * (1.0 - fog), 1.0);
            outNormal = vec4(n, s.rough);
            outHit = hitDistance;
            #endif
        }
        """;

    // DLSS: the sky, where nothing was drawn (the depth is still the clear's 0, infinitely far), jittered as the scene,
    // so that DLSS smooths the towers' edges against it too; the second pass then adds no sky of its own. Its motion: a
    // direction at infinity moves on screen with the camera's turn alone.
    private const string SkyMotionVertexShader = """
        #version 450
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; mat4 prevViewProj; vec4 jitter; } pc;
        layout(location = 0) out vec3 vNear;
        layout(location = 1) out vec2 vNdc;
        void main()
        {
            vec2 p = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2) * 2.0 - 1.0;
            vec4 near = inverse(pc.viewProj) * vec4(p, 1.0, 1.0);
            vNear = near.xyz / near.w;
            vNdc = p;
            gl_Position = vec4(p + pc.jitter.xy, 0.0, 1.0);
        }
        """;

    private const string SkyMotionFragmentShader = "#version 450\n" + SkyGlsl + """
        layout(push_constant) uniform Push { mat4 viewProj; vec4 eye; vec4 sun; vec4 params; mat4 prevViewProj; vec4 jitter; } pc;
        layout(location = 0) in vec3 vNear;
        layout(location = 1) in vec2 vNdc;
        layout(location = 0) out vec4 outColor;
        layout(location = 1) out vec4 outMotion;
        #ifdef RR
        layout(location = 2) out vec4 outDiffuse;
        layout(location = 3) out vec4 outSpecular;
        layout(location = 4) out vec4 outNormal;
        layout(location = 5) out float outHit;
        #endif
        void main()
        {
            vec3 dir = normalize(vNear - pc.eye.xyz);
            vec4 prev = pc.prevViewProj * vec4(dir, 0.0);
            prev.w = abs(prev.w) < 1e-6 ? 1e-6 : prev.w;
            outColor = vec4(sky(dir, pc.sun.xyz, pc.eye.w), 1.0);
            outMotion = vec4((prev.xy / prev.w - vNdc) * 0.5, 0.0, 0.0);
            #ifdef RR
            // Ray Reconstruction's guides for the sky, as NVIDIA's own renderer gives them: its colour as the diffuse
            // reflectance, no specular, a normal facing the camera, no reflection ray.
            outDiffuse = vec4(max(outColor.rgb, vec3(0.04)), 1.0);
            outSpecular = vec4(0.0);
            outNormal = vec4(-dir, 0.0);
            outHit = -1.0;
            #endif
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
            #ifdef DLSS
            // DLSS's image has the sky already.
            vec3 c = base.rgb;
            #else
            vec3 c = base.rgb + sky(dir, pc.sun.xyz, pc.eye.w) * (1.0 - base.a);
            #endif
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
            c += (hash12(gl_FragCoord.xy + fract(pc.eye.w) * 640.0) - 0.5) / 160.0 * (1.0 - pc.params.z);
            c = abs(vUv.y - 0.5) > 0.5 - pc.params.w ? vec3(0.0) : c;
            outColor = vec4(c, 1.0);
        }
        """;

    // The swarm, in arrays indexed alike.
    private static uint[] _ids = [];
    // Two buffers of every body's state: the one drawn, and the one Jolt's worker reads the next step into.
    private static BodyState[] _states = [], _statesBack = [];
    private static SN.Vector3[] _faceP = [], _faceN = [], _dirs = [];
    private static float[] _eye = [], _mouth = [], _jaw = [], _phase = [], _radius = [], _height = [], _speed = [];
    private static float _memberRadius;

    // How many tasks steer and read the bodies, 2,048 at a time (--body-threads; 1 is one loop). They run on the step's
    // worker while Jolt's own threads are idle.
    private static int _bodyThreads = 4;

    // The head the swarm forms, when head/head_points.bin is there: points on a generated head (see the README), 9 floats
    // each (position, normal, eye, mouth and jaw weights), in an order where any first n cover it evenly.
    private static readonly float[] Head = LoadHead(Path.Combine(AppContext.BaseDirectory, "head", "head_points.bin"));
    private const float HeadScale = 42f;
    private static readonly SN.Vector3 HeadCenter = new(0f, 22f, -6f);

    public static int Run(string[] args)
    {
        var limit = Arg(args, "--seconds", 0);
        var swarm = Math.Clamp((int)Arg(args, "--swarm", 10000), 1, MaxSwarm);
        var immediate = args.Contains("--immediate");
        var mailbox = args.Contains("--mailbox");
        string? ArgText(string name)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        // --record FILE: a video at a fixed 60 frames a second, whatever the machine's pace, encoded by ffmpeg.
        var recordFile = ArgText("--record") is { } recordArg ? Path.GetFullPath(recordArg) : null;
        var recording = recordFile is not null;
        if (recording && limit <= 0)
        {
            limit = 60;
        }
        var captureAt = Array.IndexOf(args, "--capture");
        var captures = captureAt >= 0 && captureAt + 1 < args.Length ? args[captureAt + 1].Split(',').Select(double.Parse).Order().ToArray() : [];
        var nextCapture = 0;
        var clock = Stopwatch.StartNew();
        var faceAt = Array.IndexOf(args, "--face-file");
        var mirror = new VoiceMirror(faceAt >= 0 && faceAt + 1 < args.Length ? args[faceAt + 1] : null);
        // The sound is synthesised on a worker while the window and the device come up.
        var soundTask = args.Contains("--no-sound")
            ? Task.FromResult<(Soundscape? Sound, string Note)>((null, "off (--no-sound)"))
            : Task.Run(() =>
            {
                string note;
                var opened = recording ? Soundscape.TryOpenLoopback(out note) : Soundscape.TryOpen(out note);
                return (Sound: opened, Note: note);
            });

        // The colossus's voice (Speech.cs): Piper makes its lines on a worker while the window comes up.
        var ttsNote = "off (--no-tts)";
        var tts = args.Contains("--no-tts") ? null : Voice.Find(ArgText("--piper"), ArgText("--voice"), out ttsNote);
        var linesFile = ArgText("--lines") ?? Path.Combine(AppContext.BaseDirectory, "voice", "lines.fr.txt");
        static string[] ReadLines(string file) => File.Exists(file) ? [.. File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))] : [];
        var script = ReadLines(linesFile);

        // The same lines in English, line for line, from lines.en.txt beside them: the subtitles in English, and cues.
        var inEnglish = ReadLines(Path.Combine(Path.GetDirectoryName(linesFile) ?? ".", "lines.en.txt"));
        var english = new Dictionary<string, string>();
        for (var i = 0; i < script.Length && script.Length == inEnglish.Length; i++)
        {
            english.TryAdd(script[i], inEnglish[i]);
        }

        // The lines recorded ahead, line for line, in each language (voice/fr/1.wav, voice/en/1.wav, ...; voice/README.md
        // says with what voices), when all of them are there: the colossus then speaks in the panel's language, and Piper
        // only what is typed. Not with --lines or --no-tts.
        (short[] Pcm, int Rate)[]? Recorded(string lang)
        {
            var found = new (short[] Pcm, int Rate)[script.Length];
            for (var i = 0; i < found.Length; i++)
            {
                if (Voice.ReadWav(Path.Combine(AppContext.BaseDirectory, "voice", lang, $"{i + 1}.wav")) is not { } line)
                {
                    return null;
                }

                found[i] = line;
            }

            return found.Length > 0 ? found : null;
        }

        var ownLines = args.Contains("--no-tts") || ArgText("--lines") is not null;
        var recordedFr = ownLines ? null : Recorded("fr");
        var recordedEn = ownLines ? null : Recorded("en");
        var cuesFile = ArgText("--cues");
        var subtitlesOn = !args.Contains("--no-subtitles");
        var linesTask = tts is null || script.Length == 0 || (recordedFr is not null && recordedEn is not null)
            ? Task.FromResult(Array.Empty<(string Text, short[] Pcm)>())
            : Task.Run(() => script.Select(line => (Text: line, Pcm: tts.Say(line))).ToArray());

        if (!Glfw.Init())
        {
            Console.Error.WriteLine("SWARM FAIL: Glfw.Init returned false");
            return 1;
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
        Glfw.WindowHint(GlfwWindowHint.Resizable, 0);
        if (args.Contains("--hidden"))
        {
            Glfw.WindowHint(GlfwWindowHint.Visible, 0);
        }

        var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = "BRUTAL showcase: the swarm", Size = new int2(Width, Height) });

        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var extensionCount);
        var extensions = new HashSet<string>();
        for (var i = 0; i < extensionCount; i++)
        {
            extensions.Add(Marshal.PtrToStringUTF8((nint)glfwExtensions[i])!);
        }

        // --dlss: Streamline starts before the instance, which carries the extensions DLSS asks for. If the instance
        // refuses them, Streamline stops and the instance is made as without --dlss.
        var dlssAt = Array.IndexOf(args, "--dlss");
        var dlssName = dlssAt >= 0 && dlssAt + 1 < args.Length && Dlss.Modes.ContainsKey(args[dlssAt + 1]) ? args[dlssAt + 1] : "dlaa";
        var rayAsked = args.Contains("--rr");
        var dlssNote = "";
        Dlss? dlss = null;
        if (dlssAt >= 0 || rayAsked)
        {
            dlss = Dlss.Load(ArgText("--sl-path") ?? Dlss.DefaultFolder, args.Contains("--sl-verbose"), rayAsked, out var why);
            dlssNote = why;
        }

        Instance instance;
        try
        {
            instance = Vulkan.CreateInstance(new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = ApiVersion13 },
                EnabledExtensions = dlss is null ? extensions : [.. extensions, .. dlss.InstanceExtensions],
                EnabledLayers = [],
            });
        }
        catch (Exception e) when (dlss is not null)
        {
            dlssNote = $"the Vulkan instance refused DLSS's extensions ({e.Message})";
            dlss.Shutdown();
            dlss = null;
            instance = Vulkan.CreateInstance(new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = ApiVersion13 },
                EnabledExtensions = extensions,
                EnabledLayers = [],
            });
        }

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
        string[] deviceExtensions = rtx ? ["VK_KHR_swapchain", "VK_KHR_acceleration_structure", "VK_KHR_ray_query", "VK_KHR_deferred_host_operations"] : ["VK_KHR_swapchain"];

        // DLSS: its device extensions, its Vulkan 1.2 and 1.3 features and any queues of Streamline's own, after ours in
        // the same family. If the GPU lacks one, no DLSS.
        var priorities = stackalloc float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        var queueCount = 1;
        var vulkan13On = new VkPhysicalDeviceVulkan13Features();
        if (dlss is not null)
        {
            var has13 = new VkPhysicalDeviceVulkan13Features();
            var query = new VkPhysicalDeviceFeatures2 { Next = &has13 };
            physical.GetFeatures2(ref query);
            var dlss12 = rtx ? vulkan12On : new VkPhysicalDeviceVulkan12Features();
            var dlss13 = new VkPhysicalDeviceVulkan13Features();
            var missing = dlss.DeviceExtensions.Where(e => !physical.ExtensionNames.Contains(e)).ToList();
            EnableFeatures(&dlss12, &has12, dlss.Features12, missing);
            EnableFeatures(&dlss13, &has13, dlss.Features13, missing);
            var familyCount = physical.GetQueueFamilyProperties_Count();
            var families = new VkQueueFamilyProperties[familyCount];
            physical.GetQueueFamilyProperties_Write(families);
            var wanted = 1 + (int)(dlss.GraphicsQueues + dlss.ComputeQueues);
            if (missing.Count > 0 || wanted > Math.Min(8, (int)families[family].QueueCount))
            {
                dlssNote = missing.Count > 0 ? $"the GPU lacks {string.Join(", ", missing)}" : $"DLSS asks for {wanted - 1} more queues than the GPU has";
                dlss.Shutdown();
                dlss = null;
            }
            else
            {
                vulkan12On = dlss12;
                vulkan13On = dlss13;
                vulkan13On.Next = &vulkan12On;
                queueCount = wanted;
                deviceExtensions = [.. deviceExtensions.Union(dlss.DeviceExtensions)];
            }
        }

        var queueInfo = new VkDeviceQueueCreateInfo { QueueFamilyIndex = family, QueueCount = queueCount, QueuePriorities = priorities };
        var device = physical.CreateDevice(new DeviceEx.CreateInfo
        {
            Extensions = [.. deviceExtensions],
            QueueCreateInfos = new ReadOnlySpan<VkDeviceQueueCreateInfo>(&queueInfo, 1),
            Next = dlss is not null ? &vulkan13On : rtx ? &vulkan12On : null,
        });
        var queue = device.GetQueue(family, 0);
        if (dlss is not null && !dlss.Attach(instance.VkHandle, physical.VkHandle, device.VkHandle, (uint)family, out var attachWhy))
        {
            dlssNote = attachWhy;
            dlss.Shutdown();
            dlss = null;
        }


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
        var presentMode = mailbox && modes.Contains(VkPresentModeKHR.MailboxKHR) ? VkPresentModeKHR.MailboxKHR
            : immediate && modes.Contains(VkPresentModeKHR.ImmediateKHR) ? VkPresentModeKHR.ImmediateKHR : VkPresentModeKHR.FifoKHR;

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
        var hdrUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit
            | (dlss is not null ? VkImageUsageFlags.StorageBit : 0);
        var (hdrImage, hdrMemory, hdrView) = Target(device, physical, extent, HdrFormat, VkSampleCountFlags._1Bit, BloomLevels, hdrUsage, VkImageAspectFlags.ColorBit);
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
        // The scene's set: its textures and, with RTX, the frame's top-level acceleration structure, the mesh's vertices and
        // the frame's instances, for the surfaces the reflections land on; one set a frame in flight.
        VkDescriptorSetLayoutBinding[] sceneBindings = rtx
            ? [new VkDescriptorSetLayoutBinding { Binding = 0, DescriptorType = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit },
               new VkDescriptorSetLayoutBinding { Binding = 1, DescriptorType = VkDescriptorType.AccelerationStructureKHR, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit },
               new VkDescriptorSetLayoutBinding { Binding = 2, DescriptorType = VkDescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit },
               new VkDescriptorSetLayoutBinding { Binding = 3, DescriptorType = VkDescriptorType.StorageBuffer, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit }]
            : [new VkDescriptorSetLayoutBinding { Binding = 0, DescriptorType = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = VkShaderStageFlags.FragmentBit }];
        var sceneSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutEx.CreateInfo { Bindings = sceneBindings }, null);
        var descriptorPool = device.CreateDescriptorPool(new DescriptorPoolEx.CreateInfo
        {
            MaxSets = 1 + FramesInFlight,
            PoolSizes = [new VkDescriptorPoolSize { Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1 + FramesInFlight },
                         new VkDescriptorPoolSize { Type = VkDescriptorType.AccelerationStructureKHR, DescriptorCount = FramesInFlight },
                         new VkDescriptorPoolSize { Type = VkDescriptorType.StorageBuffer, DescriptorCount = 2 * FramesInFlight }],
        }, null);
        var layout = device.CreatePipelineLayout([sceneSetLayout], [pushRange], null);
        var pipeline = Pipeline(device, layout, sceneRenderPass, vertexModule, fragmentModule, scene: true);
        var hdrSet = device.AllocateDescriptorSet(descriptorPool, setLayout);
        var hdrInfo = new VkDescriptorImageInfo { Sampler = sampler, ImageView = hdrMipsView, ImageLayout = VkImageLayout.ShaderReadOnlyOptimal };
        device.UpdateDescriptorSets([new VkWriteDescriptorSet { DstSet = hdrSet, DescriptorCount = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, ImageInfo = &hdrInfo }], []);
        var compositeLayout = device.CreatePipelineLayout([setLayout], [pushRange], null);
        var compositePipeline = Pipeline(device, compositeLayout, renderPass, compositeVertex, compositeFragment, scene: false);

        // DLSS: the scene renders without MSAA, at DLSS's render size, into colour, motion and depth, which DLSS reads and
        // turns into the HDR image's first mip at the window's size; the bloom and the second pass go on from there as
        // without DLSS. The colour's alpha still marks geometry: DLSS upscales it, and the second pass puts the sky where it
        // is not. The previous frame's instances, kept on the CPU, go to a buffer of each frame in flight, for the motion.
        // With Ray Reconstruction (and RTX), the pass has four more targets, its guides, which its own pipelines write.
        var dlssMode = Dlss.Modes[dlssName];
        var renderExtent = extent;
        var ray = dlss is not null && dlss.RayReconstruction && rtx;
        VkImage dlssColor = default, dlssMotion = default, dlssDepth = default;
        VkDeviceMemory dlssColorMemory = default, dlssMotionMemory = default, dlssDepthMemory = default;
        VkImageView dlssColorView = default, dlssMotionView = default, dlssDepthView = default;
        var guideImages = new (VkImage Image, VkDeviceMemory Memory, VkImageView View)[ray ? 4 : 0];
        VkRenderPass dlssRenderPass = default;
        VkFramebuffer dlssFramebuffer = default;
        VkPipelineLayout dlssLayout = default;
        VkShaderModule dlssVertexModule = default, dlssFragmentModule = default, skyMotionVertex = default, skyMotionFragment = default;
        VkShaderModule rayFragmentModule = default, raySkyFragment = default;
        VkPipeline dlssPipeline = default, skyMotionPipeline = default, dlssCompositePipeline = default, rayPipeline = default, raySkyPipeline = default;
        VkShaderModule dlssCompositeFragment = default;
        var previousBuffers = new VkBuffer[FramesInFlight];
        var previousMemory = new VkDeviceMemory[FramesInFlight];
        var previousMapped = new nint[FramesInFlight];
        Drawn[] thisFrame = [], lastFrame = [];
        const VkImageUsageFlags dlssInputUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit;
        const VkImageUsageFlags dlssDepthUsage = VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.SampledBit;
        if (dlss is not null)
        {
            var (renderWidth, renderHeight) = dlss.RenderSize(dlssMode, extent.Width, extent.Height);
            renderExtent = new VkExtent2D { Width = renderWidth, Height = renderHeight };
            (dlssColor, dlssColorMemory, dlssColorView) = Target(device, physical, renderExtent, HdrFormat, VkSampleCountFlags._1Bit, 1, dlssInputUsage, VkImageAspectFlags.ColorBit);
            (dlssMotion, dlssMotionMemory, dlssMotionView) = Target(device, physical, renderExtent, MotionFormat, VkSampleCountFlags._1Bit, 1, dlssInputUsage, VkImageAspectFlags.ColorBit);
            (dlssDepth, dlssDepthMemory, dlssDepthView) = Target(device, physical, renderExtent, DepthFormat, VkSampleCountFlags._1Bit, 1, dlssDepthUsage, VkImageAspectFlags.DepthBit);
            for (var i = 0; i < guideImages.Length; i++)
            {
                guideImages[i] = Target(device, physical, renderExtent, GuideFormats[i], VkSampleCountFlags._1Bit, 1, dlssInputUsage, VkImageAspectFlags.ColorBit);
            }

            // Colour, motion, depth, then the guides, which every pixel of Ray Reconstruction's frames writes.
            var attachmentCount = 3 + guideImages.Length;
            var dlssAttachments = stackalloc VkAttachmentDescription[7];
            var formats7 = stackalloc VkFormat[] { HdrFormat, MotionFormat, DepthFormat, GuideFormats[0], GuideFormats[1], GuideFormats[2], GuideFormats[3] };
            for (var i = 0; i < attachmentCount; i++)
            {
                dlssAttachments[i] = new VkAttachmentDescription
                {
                    Format = formats7[i],
                    Samples = VkSampleCountFlags._1Bit,
                    LoadOp = i < 3 ? VkAttachmentLoadOp.Clear : VkAttachmentLoadOp.DontCare,
                    StoreOp = VkAttachmentStoreOp.Store,
                    StencilLoadOp = VkAttachmentLoadOp.DontCare,
                    StencilStoreOp = VkAttachmentStoreOp.DontCare,
                    InitialLayout = VkImageLayout.Undefined,
                    FinalLayout = VkImageLayout.ShaderReadOnlyOptimal,
                };
            }

            var dlssColorRefs = stackalloc VkAttachmentReference[]
            {
                new() { Attachment = 0, Layout = VkImageLayout.ColorAttachmentOptimal },
                new() { Attachment = 1, Layout = VkImageLayout.ColorAttachmentOptimal },
                new() { Attachment = 3, Layout = VkImageLayout.ColorAttachmentOptimal },
                new() { Attachment = 4, Layout = VkImageLayout.ColorAttachmentOptimal },
                new() { Attachment = 5, Layout = VkImageLayout.ColorAttachmentOptimal },
                new() { Attachment = 6, Layout = VkImageLayout.ColorAttachmentOptimal },
            };
            var dlssDepthRef = new VkAttachmentReference { Attachment = 2, Layout = VkImageLayout.DepthStencilAttachmentOptimal };
            var dlssSubpass = new VkSubpassDescription
            {
                PipelineBindPoint = VkPipelineBindPoint.Graphics,
                ColorAttachmentCount = 2 + guideImages.Length,
                ColorAttachments = dlssColorRefs,
                DepthStencilAttachment = &dlssDepthRef,
            };
            // In: the previous frame's DLSS is done reading these images. Out: this frame's DLSS reads them.
            var dlssDependencies = stackalloc VkSubpassDependency[]
            {
                new()
                {
                    SrcSubpass = VK.SUBPASS_EXTERNAL,
                    DstSubpass = 0,
                    SrcStageMask = VkPipelineStageFlags.ComputeShaderBit | VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.LateFragmentTestsBit,
                    DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
                    SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
                    DstAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
                },
                new()
                {
                    SrcSubpass = 0,
                    DstSubpass = VK.SUBPASS_EXTERNAL,
                    SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit | VkPipelineStageFlags.LateFragmentTestsBit,
                    DstStageMask = VkPipelineStageFlags.ComputeShaderBit,
                    SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.DepthStencilAttachmentWriteBit,
                    DstAccessMask = VkAccessFlags.ShaderReadBit,
                },
            };
            dlssRenderPass = device.CreateRenderPass(new VkRenderPassCreateInfo
            {
                AttachmentCount = attachmentCount,
                Attachments = dlssAttachments,
                SubpassCount = 1,
                Subpasses = &dlssSubpass,
                DependencyCount = 2,
                Dependencies = dlssDependencies,
            }, null);
            var dlssViews = stackalloc VkImageView[7];
            dlssViews[0] = dlssColorView;
            dlssViews[1] = dlssMotionView;
            dlssViews[2] = dlssDepthView;
            for (var i = 0; i < guideImages.Length; i++)
            {
                dlssViews[3 + i] = guideImages[i].View;
            }

            dlssFramebuffer = device.CreateFramebuffer(new VkFramebufferCreateInfo
            {
                RenderPass = dlssRenderPass,
                AttachmentCount = attachmentCount,
                Attachments = dlssViews,
                Width = renderExtent.Width,
                Height = renderExtent.Height,
                Layers = 1,
            }, null);

            var dlssPushRange = pushRange with { Size = new ByteSize((nuint)(sizeof(Push) + sizeof(MotionPush))) };
            dlssLayout = device.CreatePipelineLayout([sceneSetLayout], [dlssPushRange], null);
            dlssVertexModule = Shader(device, VertexShader.Replace("#version 450\n", "#version 450\n#define DLSS 1\n"), ShaderKind.VertexShader, "swarm-dlss.vert");
            dlssFragmentModule = rtx
                ? Shader(device, FragmentShader.Replace("#version 450\n", "#version 460\n#extension GL_EXT_ray_query : require\n#define RTX 1\n#define DLSS 1\n"), ShaderKind.FragmentShader, "swarm-rtx-dlss.frag", vulkan13: true)
                : Shader(device, FragmentShader.Replace("#version 450\n", "#version 450\n#define DLSS 1\n"), ShaderKind.FragmentShader, "swarm-dlss.frag");
            skyMotionVertex = Shader(device, SkyMotionVertexShader, ShaderKind.VertexShader, "sky-motion.vert");
            skyMotionFragment = Shader(device, SkyMotionFragmentShader, ShaderKind.FragmentShader, "sky-motion.frag");
            dlssPipeline = Pipeline(device, dlssLayout, dlssRenderPass, dlssVertexModule, dlssFragmentModule, scene: true, dlss: true, guides: guideImages.Length);
            skyMotionPipeline = Pipeline(device, dlssLayout, dlssRenderPass, skyMotionVertex, skyMotionFragment, scene: false, dlss: true, guides: guideImages.Length);
            if (ray)
            {
                rayFragmentModule = Shader(device, FragmentShader.Replace("#version 450\n", "#version 460\n#extension GL_EXT_ray_query : require\n#define RTX 1\n#define DLSS 1\n#define RR 1\n"), ShaderKind.FragmentShader, "swarm-rtx-rr.frag", vulkan13: true);
                raySkyFragment = Shader(device, SkyMotionFragmentShader.Replace("#version 450\n", "#version 450\n#define RR 1\n"), ShaderKind.FragmentShader, "sky-motion-rr.frag");
                rayPipeline = Pipeline(device, dlssLayout, dlssRenderPass, dlssVertexModule, rayFragmentModule, scene: true, dlss: true, guides: guideImages.Length, writeGuides: true);
                raySkyPipeline = Pipeline(device, dlssLayout, dlssRenderPass, skyMotionVertex, raySkyFragment, scene: false, dlss: true, guides: guideImages.Length, writeGuides: true);
            }
            dlssCompositeFragment = Shader(device, CompositeFragmentShader.Replace("#version 450\n", "#version 450\n#define DLSS 1\n"), ShaderKind.FragmentShader, "composite-dlss.frag");
            dlssCompositePipeline = Pipeline(device, compositeLayout, renderPass, compositeVertex, dlssCompositeFragment, scene: false);

            var previousSize = new ByteSize((nuint)(MaxInstances * sizeof(Drawn)));
            for (var i = 0; i < FramesInFlight; i++)
            {
                previousBuffers[i] = device.CreateBuffer(new VkBufferCreateInfo { Size = previousSize, Usage = VkBufferUsageFlags.VertexBufferBit, SharingMode = VkSharingMode.Exclusive }, null);
                previousMemory[i] = device.AllocateMemory(physical, previousBuffers[i], VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
                device.BindBufferMemory(previousBuffers[i], previousMemory[i], memoryOffset: ByteSize.Zero);
                previousMapped[i] = (nint)(Drawn*)device.MapMemory<Drawn>(new MapInfo(previousMemory[i]) { Region = new MapRegion { Size = previousSize } });
            }

            thisFrame = new Drawn[MaxInstances];
            lastFrame = new Drawn[MaxInstances];
        }

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
            // The ray queries' bottom-level structures read their triangles from this buffer too, and the scene's shader
            // the normals and materials where a reflection lands.
            meshBuffer = AddressBuffer(device, physical, meshSize.Value, VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.StorageBufferBit | VkBufferUsageFlags.AccelerationStructureBuildInputReadOnlyBitKHR, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, out meshMemory);
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
            instanceBuffers[i] = device.CreateBuffer(new VkBufferCreateInfo { Size = instanceSize, Usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.StorageBufferBit, SharingMode = VkSharingMode.Exclusive }, null);
            instanceMemory[i] = device.AllocateMemory(physical, instanceBuffers[i], VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(instanceBuffers[i], instanceMemory[i], memoryOffset: ByteSize.Zero);
            instanceMapped[i] = (nint)(Drawn*)device.MapMemory<Drawn>(new MapInfo(instanceMemory[i]) { Region = new MapRegion { Size = instanceSize } });
            pools[i] = device.CreateCommandPool(new VkCommandPoolCreateInfo { QueueFamilyIndex = family }, null);
            commands[i] = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo { CommandPool = pools[i], Level = VkCommandBufferLevel.Primary });
            fences[i] = device.CreateFence(new VkFenceCreateInfo { Flags = VkFenceCreateFlags.SignaledBit }, null);
            acquired[i] = device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }

        // Four GPU timestamps a frame in flight: start, ray-tracing structure built, scene drawn, end. Each slot's are
        // read once its fence says the frame is done, for the summary.
        var queryPool = device.CreateQueryPool(new VkQueryPoolCreateInfo { QueryType = VkQueryType.Timestamp, QueryCount = 4 * FramesInFlight }, null);
        var tickMs = physical.GetProperties().Limits.TimestampPeriod / 1e6;
        string[] gpuParts = ["tlas", "scene", "post", "total"];
        var gpuTimes = gpuParts.Select(_ => new List<double>(1 << 16)).ToArray();

        // The surface textures, made with ComfyUI and Blender (see the README): one 2D array, uploaded once through a
        // staging buffer, then mipmapped by blits.
        string[] textureFiles = ["hull_albedo", "hull_nrh", "facade_albedo", "facade_nrh", "concrete_albedo", "concrete_nrh", "cladding_albedo", "cladding_nrh"];
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
        // The panel's font: Segoe UI from the system, rasterised by FreeType (ImGui.FreeType), when it is there.
        var fontFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(fontFile))
        {
            io.Fonts.SetFontLoader(ImGuiFreeType.GetFontLoader());
            io.Fonts.AddFontFromFileTTF(fontFile, 17f);
        }

        var langAt = Array.IndexOf(args, "--lang");
        _french = langAt >= 0 && langAt + 1 < args.Length ? args[langAt + 1] == "fr" : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";
        LStrings.SetLocalizedStringProvider(Translate);
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
        var threads = (int)Arg(args, "--jolt-threads", Math.Clamp(Environment.ProcessorCount - 2, 1, 6));
        // With the fallback: the package's TempAllocatorImpl.Create(64 MB) aborts the process (0xC0000409) in the first
        // Update that has moving bodies, as if it held far less than asked, while the same size with a malloc fallback runs.
        var temp = TempAllocatorImplWithMallocFallback.Create(64u * 1024 * 1024);
        var jobs = JobSystemThreadPool.Create(2048, 8, threads);
        var broadPhaseLayers = BroadPhaseLayerInterfaceTable.Create(2, 2);
        broadPhaseLayers.MapObjectToBroadPhaseLayer(0, 0);
        broadPhaseLayers.MapObjectToBroadPhaseLayer(1, 1);
        var layerPairs = ObjectLayerPairFilterTable.Create(2);
        layerPairs.EnableCollision(0, 1);
        if (!args.Contains("--no-self-collide"))
        {
            layerPairs.EnableCollision(1, 1);
        }
        var objectVsBroadPhase = ObjectVsBroadPhaseLayerFilterTable.Create(broadPhaseLayers, 2, layerPairs, 2);
        var physics = PhysicsSystem.Create();
        // Room for the walkway and the figure beside a full swarm: at 40,000 machines and 40,000 bodies, the last ones
        // came back invalid, and destroying them crashed.
        physics.Init(MaxInstances, 0, 262_144, 131_072, broadPhaseLayers, objectVsBroadPhase, layerPairs);
        physics.SetGravity(new float3(0f, -9.81f, 0f));
        // The bodies are steered and read only by the step's worker, before and after its step, and this thread touches
        // them only once that worker is done, so the interface that takes no lock is safe; it halves the cost of the
        // 30,000 calls a step. --locking compares.
        var bodies = args.Contains("--locking") ? physics.GetBodyInterface() : physics.GetBodyInterfaceNoLock();

        // The scene: the city is drawn only; the walkway and the figure also stand in the simulation.
        var rng = new Random(7);
        var statics = new List<Drawn>();
        var towers = City(statics, rng, towerVariants);
        // The cinematic's shots round towers, and the towers they cast in the style each shot shows.
        var tops = towerVariants.Select(v => cube.Skip(v.First).Take(v.Count).Max(m => m.Pos.Y)).ToArray();
        (Shots, var cast) = DirectedShots(Shots, [.. towers.Select(p => (p.Instance, tops[p.Variant], towerVariants[p.Variant].Width))]);
        foreach (var (k, seed) in cast)
        {
            var (variant, hero) = towers[k];
            hero.Extra.Y = seed;
            towers[k] = (variant, hero);
        }
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
                var meshInfo = new VkDescriptorBufferInfo { Buffer = meshBuffer, Range = meshSize };
                var instanceInfo = new VkDescriptorBufferInfo { Buffer = instanceBuffers[i], Range = instanceSize };
                device.UpdateDescriptorSets(
                    [new VkWriteDescriptorSet { Next = &accelWrite, DstSet = sceneSets[i], DstBinding = 1, DescriptorCount = 1, DescriptorType = VkDescriptorType.AccelerationStructureKHR },
                     new VkWriteDescriptorSet { DstSet = sceneSets[i], DstBinding = 2, DescriptorCount = 1, DescriptorType = VkDescriptorType.StorageBuffer, BufferInfo = &meshInfo },
                     new VkWriteDescriptorSet { DstSet = sceneSets[i], DstBinding = 3, DescriptorCount = 1, DescriptorType = VkDescriptorType.StorageBuffer, BufferInfo = &instanceInfo }], []);
            }
        }

        BuildSwarm(bodies, swarm);
        // Jolt asks for this after adding bodies one at a time, which leaves the broad phase's tree unbalanced. At 40,000
        // machines it changed nothing measurable: Jolt rebuilds the tree as it steps.
        physics.OptimizeBroadPhase();

        _bodyThreads = (int)Arg(args, "--body-threads", 4);
        physics.OptimizeBroadPhase();
        Console.WriteLine($"SWARM ready after {clock.Elapsed.TotalMilliseconds:F0} ms: {swarm} bodies, {staticInstances.Length} static instances, Jolt on {threads} threads, present {presentMode}");
        var dlssLabel = dlss is null ? "" : $"DLSS {(dlssName == "dlaa" ? "DLAA" : dlssName)}, {renderExtent.Width}x{renderExtent.Height} > {extent.Width}x{extent.Height}";
        if (dlssAt >= 0 || rayAsked)
        {
            Console.WriteLine(dlss is null ? $"DLSS off: {dlssNote}; 4x MSAA" : dlssLabel);
        }

        if (rayAsked && dlss is not null)
        {
            Console.WriteLine(ray ? "Ray Reconstruction on" : $"Ray Reconstruction off: {(dlss.RayNote.Length > 0 ? dlss.RayNote : "no RTX rays to denoise")}");
        }

        // State the panel steers.
        // With the voice plugins running, the face speaks when Claude's voice does; without them, on its own.
        mirror.Poll(0);
        var (sound, soundNote) = soundTask.Result;
        Console.WriteLine($"SOUND {soundNote}; voice plugins {(mirror.Present ? "running" : "not running")}");
        Console.WriteLine($"VOICE {ttsNote}; {script.Length} lines{(recordedFr is null ? "" : ", recorded in French")}{(recordedEn is null ? "" : ", recorded in English")}");
        var hasVoice = tts is not null || recordedFr is not null || recordedEn is not null;
        bool assembled = false, autoCycle = true, speaking = hasVoice || !mirror.Present, drift = true, rtxOn = rtx && !args.Contains("--rtx-off");
        bool mirrorOn = !args.Contains("--no-voice") && !recording, soundOn = !args.Contains("--mute"), panelOn = !args.Contains("--no-panel");
        float volume = recording ? 0.9f : 0.6f, hearing = 0f;
        // DLSS's and Ray Reconstruction's switches, and what the next frame needs of this one: whether DLSS ran and which,
        // the instances drawn, the camera.
        bool dlssOn = dlss is not null, dlssWasOn = false, rayOn = ray, rayWasOn = false;
        var lastDrawn = 0;
        SN.Vector3 lastEye = default, lastForward = default;
        var lastViewProj = SN.Matrix4x4.Identity;
        float speedScale = 1f, cohesion = 1.2f, glow = 0f, yawOffset = 0f, pitchOffset = 0f, zoom = 1f, exposure = 1.0f, bloomStrength = 0.12f, smoothDt = 1f / 60f;
        int requested = swarm, preset = recording || args.Contains("--cinematic") ? 3 : 0, shownPreset = -1;
        // The lines made, the next one and when; a line typed in the panel while Piper makes it; the subtitle shown.
        var lines = Array.Empty<(string Text, short[] Pcm)>();
        var nextLine = 0;
        double nextLineAt = 0;
        var wasTalking = false;
        Task<(string Text, short[] Pcm)>? saying = null;
        var typed = new ImInputString(240);
        var subtitle = string.Empty;
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
        var serial = args.Contains("--serial") || recording;
        Task<PhysicsUpdateError>? stepping = null;
        var physicsHz = Arg(args, "--physics-hz", 120);
        // The machines stop colliding with each other once they form the face: each flies to its own point of the head's
        // blue noise, so they hardly touch, and the contacts doubled Jolt's step. --face-collide keeps them.
        var faceNoCollide = !args.Contains("--face-collide");
        bool collideOff = false;
        double lastKick = 0, lastRead = 0;
        var kickDt = 1f / 60f;
        var stepMs = 0.0;
        // The swarm's centre and mean speed, for the sound: worked out by the worker after each step, off the frame.
        (SN.Vector3 Centre, float Speed) stepMotion = default, motion = default;
        // The worker's step in its three parts, for the summary.
        string[] stepParts = ["steer", "update", "read"];
        var stepTimes = stepParts.Select(_ => new List<double>(1 << 16)).ToArray();
        double simTime = 0, last = clock.Elapsed.TotalSeconds, cycleStart = last, nextLog = last + 1, physicsMs = 0, frameMs = 0;
        var lastLogFrame = 0;
        var worstSecond = int.MaxValue;
        var frame = 0;
        var errors = PhysicsUpdateError.None;
        var reason = "window closed";
        var vsyncNote = presentMode == VkPresentModeKHR.FifoKHR ? "vsync" : presentMode == VkPresentModeKHR.MailboxKHR ? "mailbox" : "no vsync";

        // The recording: each frame's image goes to ffmpeg as it is made, and OpenAL's mix of the same sixtieth of a
        // second to a raw file; the two are put together when the scene ends. The face forms 3 s in.
        Process? encoder = null;
        FileStream? audioOut = null;
        var audioFrame = new short[Soundscape.SampleRate / 60 * 2];
        var ffmpeg = ArgText("--ffmpeg") ?? "ffmpeg";
        if (recording)
        {
            try
            {
                linesTask.Wait();
            }
            catch (AggregateException e)
            {
                Console.WriteLine($"VOICE failed: {e.InnerException?.Message}");
            }

            cycleStart = -3.0;
            last = -1.0 / 60.0;
            encoder = StartEncoder(ffmpeg, recordFile + ".video.mp4", (int)extent.Width, (int)extent.Height);
            audioOut = File.Create(recordFile + ".pcm");
        }

        // Where each frame's time goes, phase by phase, for the summary.
        string[] phases = ["steer", "step", "read", "panel", "wait", "write", "record", "present", "events"];
        var phaseTimes = phases.Select(_ => new List<double>(1 << 16)).ToArray();
        var mark = Stopwatch.GetTimestamp();
        void Lap(int k)
        {
            var t = Stopwatch.GetTimestamp();
            phaseTimes[k].Add((t - mark) * 1000.0 / Stopwatch.Frequency);
            mark = t;
        }

        var clears = stackalloc VkClearValue[3];
        Span<ulong> ticks = stackalloc ulong[4];
        while (!window.ShouldClose)
        {
            Glfw.PollEvents();
            var now = recording ? frame / 60.0 : clock.Elapsed.TotalSeconds;
            if (limit > 0 && now >= limit)
            {
                reason = "time limit";
                break;
            }

            // The simulation and the camera step by the smoothed interval: a frame that comes back early or late is still
            // shown for one refresh, so stepping by its raw interval would make the motion judder.
            smoothDt += ((float)Math.Min(now - last, 0.1) - smoothDt) * 0.12f;
            var frameDt = recording ? 1f / 60f : smoothDt;
            frameMs = (now - last) * 1000.0;
            last = now;

            // ImString's shared arena (8 MB) holds each frame's formatted panel text and is never emptied by the binding:
            // without this, the panel throws once it fills, after some thousands of frames.
            ImString.SharedStorage.Clear();
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

            if (ImGui.IsKeyPressed(ImGuiKey.L, false))
            {
                _french = !_french;
                LStrings.RemoveAll();
            }

            if (ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                preset = (preset + 1) % 4;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.H, false))
            {
                panelOn = !panelOn;
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

            // The voice plugins: while a voice speaks, or voice-avatar works or waits, the cycle holds the face.
            mirror.Poll(now);
            var voice = mirrorOn && mirror.Speaking;
            // The colossus speaks its lines once the face has formed, one at a time with a pause between them, while no
            // other voice speaks; a typed line as soon as Piper has made it. The face holds while it speaks.
            if (lines.Length == 0 && linesTask.IsCompletedSuccessfully)
            {
                lines = linesTask.Result;
            }

            var talking = sound?.Speaking ?? false;
            if (wasTalking && !talking)
            {
                nextLineAt = now + 2.0;
                subtitle = string.Empty;
            }

            if (sound is not null && saying is { IsCompleted: true })
            {
                if (saying.IsCompletedSuccessfully)
                {
                    sound.Speak(saying.Result.Pcm, tts!.Rate, Soundscape.SpeechPitch);
                    subtitle = saying.Result.Text;
                    Cue(cuesFile, subtitle, english, saying.Result.Pcm.Length / (double)tts.Rate / Soundscape.SpeechPitch);
                    talking = true;
                }
                else
                {
                    Console.WriteLine($"VOICE failed: {saying.Exception?.InnerException?.Message}");
                }

                saying = null;
            }
            else if (sound is not null && ((_french ? recordedFr : recordedEn) is not null || lines.Length > 0) && speaking && !voice && assembled && glow > 0.85f && !talking && now >= nextLineAt)
            {
                // The recording in the panel's language, or Piper's French.
                var (pcm, rate, pitch) = (_french ? recordedFr : recordedEn) is { } recorded
                    ? (recorded[nextLine].Pcm, recorded[nextLine].Rate, _french ? Soundscape.FrenchPitch : Soundscape.EnglishPitch)
                    : (lines[nextLine].Pcm, tts!.Rate, Soundscape.SpeechPitch);
                sound.Speak(pcm, rate, pitch);
                subtitle = script[nextLine];
                Cue(cuesFile, subtitle, english, pcm.Length / (double)rate / pitch);
                nextLine = (nextLine + 1) % script.Length;
                talking = true;
            }

            wasTalking = talking;
            if (autoCycle)
            {
                var c = (now - cycleStart) % 32.0;
                assembled = (c >= 6.0 && c < 24.0) || voice || talking || saying is not null || (mirrorOn && mirror.FaceState is "working" or "waiting");
            }

            // Jolt steps on a worker while this thread draws, at most --physics-hz times a second (default 120; 0 steps
            // every frame and waits for each). When a step is due and the last one is done, its bodies are swapped in for
            // drawing and the next starts on the worker: steer every body toward its target, step as long as the time
            // since the last step (smoothed and clamped), read every body back into the other buffer. A step longer than
            // its period does not hold the frame: until it is done, the frames go on drawing the last one, each machine
            // moved on along its velocity. Catching up with several fixed steps after a hitch made each frame longer than
            // the last. --serial waits for each step as soon as it starts.
            var stepDt = Math.Clamp(frameDt, 1f / 240f, 1f / 30f);
            // The jaw follows the spoken line's loudness; without a voice, or while another voice speaks, the syllables of
            // the wordless chant.
            var mouth = talking ? MathF.Min(1f, (sound?.SpeechLevel ?? 0f) * 1.3f) : voice || (speaking && !hasVoice) ? Speech((float)simTime) : 0f;
            Lap(8);
            if (stepping is null || physicsHz <= 0 || (stepping.IsCompleted && now - lastKick >= 1.0 / physicsHz))
            {
                // The step that ran has read every body back into the second buffer: swap it in for drawing.
                if (stepping is not null)
                {
                    errors |= stepping.Result;
                    physicsMs = stepMs;
                    motion = stepMotion;
                    physicsTimes.Add(physicsMs);
                    (_states, _statesBack) = (_statesBack, _states);
                    lastRead = now;
                }

                Lap(1);
                Lap(2);
                kickDt = physicsHz <= 0 ? stepDt : kickDt + ((Math.Clamp((float)(now - lastKick), 1f / 240f, 1f / 30f) - kickDt) * 0.12f);
                lastKick = now;
                var (dt, t0, together, open, pull, pace) = (kickDt, (float)simTime, assembled, mouth, cohesion, speedScale);
                var (from, into) = (_states, _statesBack);
                simTime += dt;
                stepping = Task.Run(() =>
                {
                    // On the worker, while this thread draws from the same buffer: steer from the bodies as drawn, step,
                    // and read every body back into the other buffer.
                    var started = Stopwatch.GetTimestamp();
                    if (faceNoCollide && together != collideOff)
                    {
                        collideOff = together;
                        if (together)
                        {
                            layerPairs.DisableCollision(1, 1);
                        }
                        else
                        {
                            layerPairs.EnableCollision(1, 1);
                        }
                    }

                    Steer(bodies, from, t0, together, open, pull, pace);
                    var steered = Stopwatch.GetTimestamp();
                    var stepError = physics.Update(dt, 1, temp, jobs);
                    var updated = Stopwatch.GetTimestamp();
                    ReadStates(bodies, into);
                    stepMotion = Motion(into);
                    var done = Stopwatch.GetTimestamp();
                    stepMs = (done - started) * 1000.0 / Stopwatch.Frequency;
                    stepTimes[0].Add((steered - started) * 1000.0 / Stopwatch.Frequency);
                    stepTimes[1].Add((updated - steered) * 1000.0 / Stopwatch.Frequency);
                    stepTimes[2].Add((done - updated) * 1000.0 / Stopwatch.Frequency);
                    return stepError;
                });
                if (serial)
                {
                    stepping.Wait();
                }

                Lap(0);
            }

            var ahead = (float)Math.Min(now - lastRead, 1.0 / 30.0);

            glow += ((assembled ? 1f : 0f) - glow) * Math.Min(1f, frameDt * 0.8f);
            // The eyes brighten as the microphone hears the user, and pulse while voice-avatar waits for them.
            var listening = !mirrorOn ? 0f : Math.Max(mirror.Hearing, mirror.FaceState == "waiting" ? 0.5f + (0.5f * MathF.Sin((float)now * 3f)) : 0f);
            hearing += (listening - hearing) * Math.Min(1f, frameDt * 8f);

            // The panel, but not in a recording.
            if (!recording && panelOn)
            {
                ImGui.SetNextWindowPos(new float2(14f, 14f), ImGuiCond.FirstUseEver);
                if (ImGui.Begin($"{L("BRUTAL showcase")}###panel", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text(string.Format(L("{0:F0} fps ({1}), frame {2:F2} ms"), io.Framerate, L(vsyncNote), frameMs));
                    ImGui.Text(string.Format(L("Jolt: {0} bodies, {1:F2} ms a step, {2} threads"), _ids.Length, physicsMs, threads));
                    ImGui.Text(string.Format(L("Vulkan: {0} instances in {1} draw calls, HDR, {2}, bloom"), staticInstances.Length + _ids.Length, drawCalls, dlss is not null && dlssOn ? (ray && rayOn ? "DLSS RR" : "DLSS") : "MSAA 4x"));
                    ImGui.Text(string.Format(L("Swarm: {0}"), L(assembled ? "face" : "cloud")) + (errors != PhysicsUpdateError.None ? $", Jolt: {errors}" : string.Empty));
                    ImGui.Separator();
                    if (ImGui.Button($"{L(assembled ? "Disperse" : "Assemble")}###assemble"))
                    {
                        assembled = !assembled;
                        autoCycle = false;
                    }

                    ImGui.SameLine();
                    ImGui.Checkbox($"{L("Auto cycle")}###cycle", ref autoCycle);
                    ImGui.SameLine();
                    ImGui.Checkbox($"{L("Speaking")}###speaking", ref speaking);
                    ImGui.SliderFloat($"{L("Swarm speed")}###speed", ref speedScale, 0.2f, 3f);
                    ImGui.SliderFloat($"{L("Cohesion")}###cohesion", ref cohesion, 0.2f, 4f);
                    ImGui.SliderFloat($"{L("Exposure")}###exposure", ref exposure, 0.3f, 4f);
                    ImGui.SliderFloat($"{L("Bloom")}###bloom", ref bloomStrength, 0f, 0.6f);
                    ImGui.SliderInt($"{L("Swarm size")}###size", ref requested, 500, MaxSwarm);
                    if (requested != _ids.Length)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button($"{L("Rebuild")}###rebuild"))
                        {
                            stepping?.Wait();
                            stepping = null;
                            DestroySwarm(bodies);
                            BuildSwarm(bodies, requested);
                            physics.OptimizeBroadPhase();
                        }
                    }

                    if (ImGui.Button($"{L("Shoulder")}###shoulder"))
                    {
                        preset = 0;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"{L("Wide")}###wide"))
                    {
                        preset = 1;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"{L("Face")}###faceview"))
                    {
                        preset = 2;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button($"{L("Cinematic")}###cinematic"))
                    {
                        preset = 3;
                    }

                    ImGui.SameLine();
                    ImGui.Checkbox($"{L("Drift")}###drift", ref drift);
                    if (rtx)
                    {
                        ImGui.Checkbox($"{L("RTX: traced sun shadows and reflections")}###rtx", ref rtxOn);
                    }
                    else
                    {
                        ImGui.Text(L("RTX: no ray queries on this GPU, or --no-rtx"));
                    }

                    if (dlss is not null)
                    {
                        ImGui.Checkbox($"{dlssLabel}###dlss", ref dlssOn);
                        if (ray)
                        {
                            ImGui.SameLine();
                            ImGui.Checkbox("Ray Reconstruction###rr"u8, ref rayOn);
                        }
                    }

                    if (sound is not null)
                    {
                        ImGui.Checkbox($"{L("Sound")}###sound", ref soundOn);
                        ImGui.SameLine();
                        ImGui.SliderFloat($"{L("Volume")}###volume", ref volume, 0f, 1f);
                    }
                    else
                    {
                        ImGui.Text(string.Format(L("Sound: {0}"), soundNote));
                    }

                    var heard = !mirror.Present ? "not running" : voice ? "a voice speaks" : hearing > 0.3f ? "hearing you" : mirror.FaceState ?? "quiet";
                    ImGui.Checkbox($"{L("Mirror the voice plugins")}###mirror", ref mirrorOn);
                    ImGui.SameLine();
                    ImGui.Text($"({L(heard)})");
                    if (tts is not null)
                    {
                        ImGui.SetNextItemWidth(320f);
                        if (ImGui.InputText($"{L("Say")}###say", typed, ImGuiInputTextFlags.EnterReturnsTrue, default, default) && !typed.IsEmpty && saying is null)
                        {
                            var text = typed.ToString();
                            typed.Clear();
                            saying = Task.Run(() => (text, tts.Say(text)));
                        }
                    }
                    else
                    {
                        ImGui.Text(string.Format(L("Voice: {0}"), ttsNote));
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
                        ImPlot.PlotLine(L("frame"), frameHistory);
                        ImPlot.PlotLine("Jolt"u8, physicsHistory);
                        ImPlot.EndPlot();
                    }

                    ImGui.Text(L("Space assemble   C camera   H panel   drag turn   wheel zoom   L language   Esc quit"));
                    ImGui.Text(L("Built with BRUTAL (Ahwoo), Jolt Physics, Dear ImGui, ImPlot and OpenAL Soft"));
                }

                ImGui.End();
            }

            // The subtitle, under the face's line: in the cinematic, on the lower black band. While recording, the credits
            // on the upper one.
            const ImGuiWindowFlags Overlay = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;
            if (subtitle.Length > 0 && subtitlesOn)
            {
                ImGui.SetNextWindowPos(new float2(extent.Width / 2f, extent.Height - (preset == 3 ? Letterbox * extent.Height / 2f : 80f)), ImGuiCond.Always, new float2(0.5f, 0.5f));
                ImGui.SetNextWindowBgAlpha(0.45f);
                if (ImGui.Begin("###subtitle"u8, Overlay | (preset == 3 ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None)))
                {
                    ImGui.PushFont(default, 30f);
                    ImGui.PushTextWrapPos(extent.Width * 0.72f);
                    ImGui.Text(_french || !english.TryGetValue(subtitle, out var translated) ? subtitle : translated);
                    ImGui.PopTextWrapPos();
                    ImGui.PopFont();
                }

                ImGui.End();
            }

            if (recording)
            {
                ImGui.SetNextWindowPos(new float2(extent.Width / 2f, Letterbox * extent.Height / 2f), ImGuiCond.Always, new float2(0.5f, 0.5f));
                if (ImGui.Begin("###credits"u8, Overlay | ImGuiWindowFlags.NoBackground))
                {
                    ImGui.TextDisabled($"{Credits}{(_french ? "voix : " : "voice: ")}{((_french ? recordedFr : recordedEn) is not null ? RecordedVoice : PiperVoice)}");
                }

                ImGui.End();
            }

            ImGui.Render();

            // Camera: a preset, turned by the drag, zoomed by the wheel, drifting slowly, eased in; or the cinematic, its shots
            // cut hard, in time with the swarm's cycle.
            var fov = 55f;
            if (preset == 3)
            {
                (camEye, camTarget, fov) = Cinematic(now - cycleStart - 3.0);
                shownPreset = 3;
            }
            else
            {
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
            }
            var projection = SN.Matrix4x4.CreatePerspectiveFieldOfView(fov * MathF.PI / 180f, extent.Width / (float)extent.Height, Near, 1000f);
            projection.M33 = 0f; // reversed, infinite depth: 1 at the near plane, 0 at infinity
            projection.M43 = Near;
            projection.M22 = -projection.M22; // Vulkan's clip space has y pointing down
            var view = SN.Matrix4x4.CreateLookAt(camEye, camTarget, SN.Vector3.UnitY);
            var viewProj = view * projection;
            SN.Matrix4x4.Invert(viewProj, out var inverseViewProj);
            // A recording fades in from black and out to it, without grain so it compresses; the cinematic's black bands.
            var fade = recording ? (float)Math.Clamp(Math.Min(now / 1.2, (limit - now) / 2.0), 0.0, 1.0) : 1f;
            var push = new Push
            {
                Matrix = viewProj,
                Eye = new SN.Vector4(camEye, (float)now),
                Sun = new SN.Vector4(SunDir, exposure * fade),
                Params = new SN.Vector4(bloomStrength, rtx && rtxOn ? 1f : 0f, recording ? 1f : 0f, preset == 3 ? Letterbox : 0f),
            };
            var skyPush = push with { Matrix = inverseViewProj };
            if (sound is not null)
            {
                // The colossus's own chant stays silent while another voice speaks, and everything ducks under it.
                var (centre, speed) = motion;
                sound.Update(camEye, camTarget, centre, speed, FaceCenter, assembled, voice || talking ? 0f : mouth, voice || hearing > 0.3f ? 1f : 0f, (soundOn ? volume : 0f) * fade, frameDt);
            }

            // The frame: wait for this slot, fill its instances, record the scene and the panel, submit, present.
            Lap(3);
            var slot = frame % FramesInFlight;
            device.WaitForFences([fences[slot]], true, -1);
            if (frame >= FramesInFlight)
            {
                if (device.GetQueryPoolResults(queryPool, slot * 4, 4, MemoryMarshal.AsBytes(ticks), (ByteSize64)8ul, VkQueryResultFlags._64Bit) == VkResult.Success)
                {
                    gpuTimes[0].Add((ticks[1] - ticks[0]) * tickMs);
                    gpuTimes[1].Add((ticks[2] - ticks[1]) * tickMs);
                    gpuTimes[2].Add((ticks[3] - ticks[2]) * tickMs);
                    gpuTimes[3].Add((ticks[3] - ticks[0]) * tickMs);
                }
            }

            var result = device.AcquireNextImageKHR(swapchain, -1, acquired[slot], default, out var imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                reason = "swapchain out of date";
                break;
            }

            Lap(4);
            device.ResetFences([fences[slot]]);
            device.ResetCommandPool(pools[slot], VkCommandPoolResetFlags.None);
            // Under DLSS the instances are written on the CPU first, so that the next frame still has them for its motion.
            // A first frame, a cut or a jump of the camera starts DLSS's history again, its motion then nil.
            var useDlss = dlss is not null && dlssOn;
            var useRay = useDlss && rayOn;
            var target = useDlss ? thisFrame.AsSpan() : new Span<Drawn>((Drawn*)instanceMapped[slot], MaxInstances);
            staticInstances.AsSpan().CopyTo(target);
            var drawn = staticInstances.Length + WriteSwarm(target[staticInstances.Length..], glow, mouth, hearing, ahead, frameDt, rt, slot, staticInstances.Length);
            var forward = SN.Vector3.Normalize(camTarget - camEye);
            var dlssReset = !dlssWasOn || useRay != rayWasOn || drawn != lastDrawn || SN.Vector3.Distance(camEye, lastEye) > 4f || SN.Vector3.Dot(forward, lastForward) < 0.97f;
            if (useDlss)
            {
                thisFrame.AsSpan(0, drawn).CopyTo(new Span<Drawn>((Drawn*)instanceMapped[slot], drawn));
                (dlssReset ? thisFrame : lastFrame).AsSpan(0, drawn).CopyTo(new Span<Drawn>((Drawn*)previousMapped[slot], drawn));
                (thisFrame, lastFrame) = (lastFrame, thisFrame);
            }

            var jitter = Dlss.Jitter(frame, Dlss.Phases(renderExtent.Width, extent.Width));
            var motionPush = new MotionPush
            {
                PrevViewProj = dlssReset ? viewProj : lastViewProj,
                Jitter = new SN.Vector4(jitter.X * 2f / renderExtent.Width, jitter.Y * 2f / renderExtent.Height, frame % 64, 0f),
            };
            dlssWasOn = useDlss;
            rayWasOn = useRay;
            lastDrawn = drawn;
            lastEye = camEye;
            lastForward = forward;
            lastViewProj = viewProj;
            Lap(5);

            var cb = commands[slot];
            cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
            cb.ResetQueryPool(queryPool, slot * 4, 4);
            cb.WriteTimestamp(VkPipelineStageFlags.TopOfPipeBit, queryPool, slot * 4);
            if (rt is not null)
            {
                rt.Build(cb, slot, drawn);
            }

            cb.WriteTimestamp(VkPipelineStageFlags.BottomOfPipeBit, queryPool, (slot * 4) + 1);

            // Under DLSS: no MSAA, the render size, the motion as a second target and the previous instances as a third input.
            var sceneExtent = useDlss ? renderExtent : extent;
            var sceneLayout = useDlss ? dlssLayout : layout;
            clears[0] = new VkClearValue { Color = new VkClearColorValue { Float32 = new float4(0f, 0f, 0f, 0f) } };
            clears[1] = useDlss ? clears[0] : new VkClearValue { DepthStencil = new VkClearDepthStencilValue { Depth = 0f } };
            clears[2] = new VkClearValue { DepthStencil = new VkClearDepthStencilValue { Depth = 0f } };
            cb.BeginRenderPass(new VkRenderPassBeginInfo
            {
                RenderPass = useDlss ? dlssRenderPass : sceneRenderPass,
                Framebuffer = useDlss ? dlssFramebuffer : sceneFramebuffer,
                RenderArea = new VkRect2D { Extent = sceneExtent },
                ClearValueCount = useDlss ? 3 : 2,
                ClearValues = clears,
            }, VkSubpassContents.Inline);
            cb.BindPipeline(VkPipelineBindPoint.Graphics, useRay ? rayPipeline : useDlss ? dlssPipeline : pipeline);
            cb.SetViewport(firstViewport: 0, [new VkViewport { Width = sceneExtent.Width, Height = sceneExtent.Height, MinDepth = 0f, MaxDepth = 1f }]);
            cb.SetScissor(firstScissor: 0, [new VkRect2D { Extent = sceneExtent }]);
            cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, sceneLayout, 0, [sceneSets[slot]], []);
            cb.PushConstants(sceneLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, offset: 0, new ReadOnlySpan<byte>(&push, sizeof(Push)));
            if (useDlss)
            {
                cb.PushConstants(sceneLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, offset: sizeof(Push), new ReadOnlySpan<byte>(&motionPush, sizeof(MotionPush)));
                cb.BindVertexBuffer(2, previousBuffers[slot]);
            }

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

            if (useDlss)
            {
                cb.BindPipeline(VkPipelineBindPoint.Graphics, useRay ? raySkyPipeline : skyMotionPipeline);
                cb.Draw(3, 1, 0, 0);
            }

            cb.EndRenderPass();
            cb.WriteTimestamp(VkPipelineStageFlags.BottomOfPipeBit, queryPool, (slot * 4) + 2);

            // DLSS writes the HDR image's first mip at the window's size, which the bloom then reads as it read the resolve.
            if (useDlss)
            {
                cb.PipelineBarrier(VkPipelineStageFlags.FragmentShaderBit, VkPipelineStageFlags.ComputeShaderBit, VkDependencyFlags.None, [], [],
                    [Barrier(hdrImage, 0, 1, VkImageLayout.Undefined, VkImageLayout.General, default, VkAccessFlags.ShaderWriteBit)]);
                Dlss.Image Input(VkImage image, VkImageView imageView, VkDeviceMemory memory, VkFormat format, VkImageUsageFlags usage) =>
                    new((ulong)image.VkHandle, (ulong)imageView.VkHandle, (ulong)memory.VkHandle, renderExtent.Width, renderExtent.Height, (uint)format, (uint)usage, (uint)VkImageLayout.ShaderReadOnlyOptimal);
                Dlss.Image Guide(int i) => Input(guideImages[i].Image, guideImages[i].View, guideImages[i].Memory, GuideFormats[i], dlssInputUsage);
                dlss!.Evaluate(cb.VkHandle, (uint)frame, dlssMode, view, projection, motionPush.PrevViewProj, jitter, dlssReset, fov * MathF.PI / 180f, Near,
                    Input(dlssDepth, dlssDepthView, dlssDepthMemory, DepthFormat, dlssDepthUsage),
                    Input(dlssMotion, dlssMotionView, dlssMotionMemory, MotionFormat, dlssInputUsage),
                    Input(dlssColor, dlssColorView, dlssColorMemory, HdrFormat, dlssInputUsage),
                    new Dlss.Image((ulong)hdrImage.VkHandle, (ulong)hdrView.VkHandle, (ulong)hdrMemory.VkHandle, extent.Width, extent.Height, (uint)HdrFormat, (uint)hdrUsage, (uint)VkImageLayout.General),
                    useRay ? new Dlss.Guides(Guide(0), Guide(1), Guide(2), Guide(3)) : null);
                cb.PipelineBarrier(VkPipelineStageFlags.ComputeShaderBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                    [Barrier(hdrImage, 0, 1, VkImageLayout.General, VkImageLayout.TransferSrcOptimal, VkAccessFlags.ShaderWriteBit, VkAccessFlags.TransferReadBit)]);
            }

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
            cb.BindPipeline(VkPipelineBindPoint.Graphics, useDlss ? dlssCompositePipeline : compositePipeline);
            cb.SetViewport(firstViewport: 0, [new VkViewport { Width = extent.Width, Height = extent.Height, MinDepth = 0f, MaxDepth = 1f }]);
            cb.SetScissor(firstScissor: 0, [new VkRect2D { Extent = extent }]);
            cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, compositeLayout, 0, [hdrSet], []);
            cb.PushConstants(compositeLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, offset: 0, new ReadOnlySpan<byte>(&skyPush, sizeof(Push)));
            cb.Draw(3, 1, 0, 0);
            renderer.RenderDrawData(ImGui.GetDrawData(), cb);
            cb.EndRenderPass();
            var capturing = recording || (nextCapture < captures.Length && now >= captures[nextCapture]);
            if (capturing)
            {
                cb.PipelineBarrier(VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], 0, 1, VkImageLayout.PresentSrcKHR, VkImageLayout.TransferSrcOptimal, VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.TransferReadBit)]);
                cb.CopyImageToBuffer(images[imageIndex], VkImageLayout.TransferSrcOptimal, readback,
                    [new VkBufferImageCopy { ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 }, ImageExtent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 } }]);
                cb.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.BottomOfPipeBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], 0, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.PresentSrcKHR, default, default)]);
            }

            cb.WriteTimestamp(VkPipelineStageFlags.BottomOfPipeBit, queryPool, (slot * 4) + 3);
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
            Lap(6);
            var presentIndex = imageIndex;
            // With Streamline, through its proxy, which its common plugin needs once a frame.
            result = dlss is not null
                ? (VkResult)dlss.Present(queue.VkHandle, (ulong)signalSemaphore.VkHandle, (ulong)swapchain.VkHandle, (uint)presentIndex)
                : queue.PresentKHR(new VkPresentInfoKHR
                {
                    WaitSemaphoreCount = 1,
                    WaitSemaphores = &signalSemaphore,
                    SwapchainCount = 1,
                    Swapchains = &swapchain,
                    ImageIndices = &presentIndex,
                });
            Lap(7);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                reason = "swapchain out of date";
                break;
            }

            if (capturing)
            {
                device.WaitIdle();
                var image = new ReadOnlySpan<byte>((byte*)readbackMapped, (int)(extent.Width * extent.Height * 4));
                if (recording)
                {
                    encoder!.StandardInput.BaseStream.Write(image);
                    if (sound is not null)
                    {
                        sound.Render(audioFrame, audioFrame.Length / 2);
                    }

                    audioOut!.Write(MemoryMarshal.AsBytes(audioFrame.AsSpan()));
                }
                else
                {
                    var file = $"colossus-{captures[nextCapture]:0.#}s.png";
                    SavePng(file, image, (int)extent.Width, (int)extent.Height);
                    Console.WriteLine($"SWARM captured {file} at {now:F1} s ({(assembled ? "face" : "cloud")})");
                    nextCapture++;
                    last = clock.Elapsed.TotalSeconds; // the capture's wait is not a frame time
                }
            }

            if (frame > 0 && !capturing)
            {
                frameTimes.Add(frameMs);
            }

            frame++;
            if (now >= nextLog)
            {
                Console.WriteLine($"SWARM t={now:F0} s, {frame - lastLogFrame} frames, frame {frameMs:F2} ms, Jolt {physicsMs:F2} ms, {_ids.Length} bodies, {drawn} instances, {(assembled ? "face" : "cloud")}");
                if (now >= 3)
                {
                    worstSecond = Math.Min(worstSecond, frame - lastLogFrame);
                }

                lastLogFrame = frame;
                nextLog += 1;
            }
        }

        var seconds = clock.Elapsed.TotalSeconds;
        stepping?.Wait();
        if (encoder is not null)
        {
            encoder.StandardInput.Close();
            encoder.WaitForExit();
            audioOut!.Dispose();
            var muxed = Finish(ffmpeg, recordFile + ".video.mp4", recordFile + ".pcm", recordFile!, frame / 60.0, Arg(args, "--record-mb", 8));
            Console.WriteLine(muxed ? $"SWARM recorded {recordFile}: {frame} frames, {new FileInfo(recordFile!).Length / 1024} KB" : $"SWARM recording failed: see ffmpeg above");
            if (muxed)
            {
                File.Delete(recordFile + ".video.mp4");
                File.Delete(recordFile + ".pcm");
            }
        }
        sound?.Dispose();
        device.WaitIdle();
        dlss?.Shutdown();
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

        device.DestroyQueryPool(queryPool, null);
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

        if (dlss is not null)
        {
            device.DestroyPipeline(dlssPipeline, null);
            device.DestroyPipeline(skyMotionPipeline, null);
            device.DestroyPipeline(dlssCompositePipeline, null);
            if (ray)
            {
                device.DestroyPipeline(rayPipeline, null);
                device.DestroyPipeline(raySkyPipeline, null);
                device.DestroyShaderModule(rayFragmentModule, null);
                device.DestroyShaderModule(raySkyFragment, null);
            }

            device.DestroyPipelineLayout(dlssLayout, null);
            foreach (var module in new[] { dlssVertexModule, dlssFragmentModule, skyMotionVertex, skyMotionFragment, dlssCompositeFragment })
            {
                device.DestroyShaderModule(module, null);
            }

            device.DestroyFramebuffer(dlssFramebuffer, null);
            device.DestroyRenderPass(dlssRenderPass, null);
            foreach (var (image, memory, view) in new[] { (dlssColor, dlssColorMemory, dlssColorView), (dlssMotion, dlssMotionMemory, dlssMotionView), (dlssDepth, dlssDepthMemory, dlssDepthView) }.Concat(guideImages))
            {
                device.DestroyImageView(view, null);
                device.DestroyImage(image, null);
                device.FreeMemory(memory, null);
            }

            for (var i = 0; i < FramesInFlight; i++)
            {
                device.UnmapMemory(previousMemory[i]);
                device.DestroyBuffer(previousBuffers[i], null);
                device.FreeMemory(previousMemory[i], null);
            }
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
        foreach (var times in phaseTimes.Concat(stepTimes).Concat(gpuTimes))
        {
            times.Sort();
        }
        static double P(List<double> xs, double q) => xs.Count == 0 ? double.NaN : xs[Math.Min(xs.Count - 1, (int)(q * xs.Count))];
        Console.WriteLine($"SWARM summary: {frame} frames in {seconds:F1} s; frame ms p50 {P(frameTimes, 0.5):F2} p95 {P(frameTimes, 0.95):F2} p99 {P(frameTimes, 0.99):F2}, worst second {(worstSecond == int.MaxValue ? 0 : worstSecond)} frames; Jolt step ms p50 {P(physicsTimes, 0.5):F2} p95 {P(physicsTimes, 0.95):F2} p99 {P(physicsTimes, 0.99):F2} max {(physicsTimes.Count > 0 ? physicsTimes[^1] : double.NaN):F2}; Jolt errors: {errors}");
        Console.WriteLine("SWARM phases ms p50/p95: " + string.Join(", ", phases.Select((n, k) => $"{n} {P(phaseTimes[k], 0.5):F2}/{P(phaseTimes[k], 0.95):F2}")));
        Console.WriteLine("SWARM GPU ms p50/p95: " + string.Join(", ", gpuParts.Select((n, k) => $"{n} {P(gpuTimes[k], 0.5):F2}/{P(gpuTimes[k], 0.95):F2}")));
        Console.WriteLine("SWARM Jolt step parts ms p50/p95: " + string.Join(", ", stepParts.Select((n, k) => $"{n} {P(stepTimes[k], 0.5):F2}/{P(stepTimes[k], 0.95):F2}")));
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
        _statesBack = new BodyState[n];
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

        ReadStates(bodies, _states);
    }

    // Every body's pose and velocity, one call each. The bulk read, BodyLockInterface.GetBodyStates, is declared by the
    // binding but its entry point is missing from the package's jolt.dll (2026.10.2, win-x64).
    private static void ReadStates(BodyInterface bodies, BodyState[] into) => ForBodies(_ids.Length, (start, end) =>
    {
        for (var i = start; i < end; i++)
        {
            bodies.GetPositionAndRotation(_ids[i], out into[i].Position, out into[i].Rotation);
            into[i].LinearVelocity = bodies.GetLinearVelocity(_ids[i]);
        }
    });

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

    // The panel's language (--lang en|fr, or the system's; L switches it), through BRUTAL's localisation: each string's
    // id is its English text, and the provider answers with its French text while the panel is in French.
    private static bool _french;

    private static readonly Dictionary<string, string> French = new()
    {
        ["BRUTAL showcase"] = "Vitrine BRUTAL",
        ["{0:F0} fps ({1}), frame {2:F2} ms"] = "{0:F0} i/s ({1}), image {2:F2} ms",
        ["vsync"] = "vsync",
        ["no vsync"] = "sans vsync",
        ["Jolt: {0} bodies, {1:F2} ms a step, {2} threads"] = "Jolt : {0} corps, {1:F2} ms par pas, {2} fils",
        ["Vulkan: {0} instances in {1} draw calls, HDR, {2}, bloom"] = "Vulkan : {0} instances en {1} appels de dessin, HDR, {2}, halo",
        ["Swarm: {0}"] = "Essaim : {0}",
        ["face"] = "visage",
        ["cloud"] = "nuée",
        ["Disperse"] = "Disperser",
        ["Assemble"] = "Assembler",
        ["Auto cycle"] = "Cycle auto",
        ["Speaking"] = "Parole",
        ["Cinematic"] = "Cinématique",
        ["Say"] = "Dire",
        ["Voice: {0}"] = "Voix : {0}",
        ["Swarm speed"] = "Vitesse de l'essaim",
        ["Cohesion"] = "Cohésion",
        ["Exposure"] = "Exposition",
        ["Bloom"] = "Halo",
        ["Swarm size"] = "Taille de l'essaim",
        ["Rebuild"] = "Reconstruire",
        ["Shoulder"] = "Épaule",
        ["Wide"] = "Large",
        ["Face"] = "Visage",
        ["Drift"] = "Dérive",
        ["RTX: traced sun shadows and reflections"] = "RTX : ombres du soleil et reflets tracés",
        ["RTX: no ray queries on this GPU, or --no-rtx"] = "RTX : pas de ray queries sur ce GPU, ou --no-rtx",
        ["Sound"] = "Son",
        ["Volume"] = "Volume",
        ["Sound: {0}"] = "Son : {0}",
        ["Mirror the voice plugins"] = "Refléter les plugins de voix",
        ["not running"] = "absents",
        ["a voice speaks"] = "une voix parle",
        ["hearing you"] = "il vous entend",
        ["quiet"] = "silence",
        ["idle"] = "au repos",
        ["working"] = "au travail",
        ["waiting"] = "attend",
        ["done"] = "terminé",
        ["failed"] = "échec",
        ["frame"] = "image",
        ["Space assemble   C camera   H panel   drag turn   wheel zoom   L language   Esc quit"] = "Espace assembler   C caméra   H panneau   glisser tourner   molette zoom   L langue   Échap quitter",
        ["Built with BRUTAL (Ahwoo), Jolt Physics, Dear ImGui, ImPlot and OpenAL Soft"] = "Fait avec BRUTAL (Ahwoo), Jolt Physics, Dear ImGui, ImPlot et OpenAL Soft",
    };

    // A stream's cue for a line the colossus starts saying (--cues FILE): one JSON line with the wall-clock time it starts,
    // how long it lasts, and its French and English text, for a player to show in its own viewer's language.
    private static void Cue(string? file, string text, Dictionary<string, string> english, double seconds)
    {
        if (file is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("at"u8, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            json.WriteNumber("ms"u8, (int)(seconds * 1000));
            json.WriteString("fr"u8, text);
            json.WriteString("en"u8, english.GetValueOrDefault(text, text));
            json.WriteEndObject();
        }

        try
        {
            using var output = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            output.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
            output.WriteByte((byte)'\n');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"CUES failed: {e.Message}");
        }
    }

    private static string L(string english)
    {
        var s = LStrings.GetOrCreate(english, english);
        return string.IsNullOrEmpty(s.Localized) ? s.Template : s.Localized;
    }

    private static bool Translate(LString s, [NotNullWhen(true)] out string? text)
    {
        text = null;
        return _french && French.TryGetValue(s.Template, out text);
    }

    // How open the mouth is while the face speaks, from 0 to 1: syllables, in phrases.
    private static float Speech(float t) => MathF.Max(0f, MathF.Sin(t * 7f)) * (0.55f + (0.45f * MathF.Sin(t * 1.3f)));

    // The swarm's centre and mean speed, for the sound.
    private static (SN.Vector3 Centre, float Speed) Motion(BodyState[] states)
    {
        var sum = SN.Vector3.Zero;
        var speed = 0f;
        for (var i = 0; i < _ids.Length; i++)
        {
            sum += V(states[i].Position);
            speed += V(states[i].LinearVelocity).Length();
        }

        var n = Math.Max(1, _ids.Length);
        return (sum / n, speed / n);
    }

    // Each body's new velocity: toward its target, capped, with a little turbulence so the swarm never looks rigid.
    // When the face speaks, its jaw drops with each syllable, as far as mouth (0 to 1) says.
    private static void Steer(BodyInterface bodies, BodyState[] from, float t, bool assembled, float mouth, float cohesion, float speedScale)
    {
        var max = 28f * speedScale;
        var stir = assembled ? 0.25f : 2f;
        ForBodies(_ids.Length, (start, end) =>
        {
            for (var i = start; i < end; i++)
            {
                var p = V(from[i].Position);
                var v = V(from[i].LinearVelocity);
                SN.Vector3 target;
                if (assembled)
                {
                    target = _faceP[i] + (new SN.Vector3(0f, -1.6f, -0.4f) * (mouth * _jaw[i]));
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
        });
    }

    // Runs range over [0, n) in slices of 2,048 bodies, on up to _bodyThreads tasks. Each body is touched by one task
    // only, and Jolt locks its list of active bodies itself, so the interface without locks stays safe.
    private static void ForBodies(int n, Action<int, int> range)
    {
        const int Slice = 2048;
        if (_bodyThreads <= 1 || n <= Slice)
        {
            range(0, n);
            return;
        }

        Parallel.For(0, (n + Slice - 1) / Slice, new ParallelOptions { MaxDegreeOfParallelism = _bodyThreads }, k => range(k * Slice, Math.Min(n, (k + 1) * Slice)));
    }

    // The swarm's instances: each machine a long box, pointing where it flies, or out of the face once it has settled;
    // the face's eyes glow as it forms, brighter as it hears the user, and its mouth with each syllable. Each machine is
    // drawn ahead of Jolt's last step by its velocity, for the time since that step. Each machine turns toward where it
    // heads by a share of the way that depends on the frame's time, so it turns as fast at 60 frames a second as at 700.
    // Written in parallel, a thousand machines a task, the frames got slower and their tail longer: the tasks contend
    // with Jolt's worker and its jobs for the cores. One loop it stays.
    private static int WriteSwarm(Span<Drawn> o, float glow, float speech, float hearing, float ahead, float frameDt, RayScene? rt, int slot, int first)
    {
        var n = Math.Min(_ids.Length, o.Length);
        var scale = SN.Matrix4x4.CreateScale(_memberRadius * 1.7f, _memberRadius * 0.9f, _memberRadius * 3.8f);
        var flat = SN.Matrix4x4.CreateScale(_memberRadius * 2.4f, _memberRadius * 0.7f, _memberRadius * 4.2f);
        var turn = 1f - MathF.Pow(0.8f, frameDt * 60f);
        for (var i = 0; i < n; i++)
        {
            var v = V(_states[i].LinearVelocity);
            var p = V(_states[i].Position) + (v * ahead);
            // Settled on the face, a machine lies flat like a scale: its length down the slope, its thin side out.
            var normal = _faceN[i];
            var settled = glow > 0.5f && v.LengthSquared() <= 4f;
            var slope = MathF.Abs(normal.Y) > 0.95f ? SN.Vector3.UnitZ - (normal * normal.Z) : (normal * normal.Y) - SN.Vector3.UnitY;
            var want = v.LengthSquared() > 4f ? SN.Vector3.Normalize(v) : settled ? SN.Vector3.Normalize(slope) : _dirs[i];
            var d = SN.Vector3.Normalize(SN.Vector3.Lerp(_dirs[i], want, turn));
            _dirs[i] = d;
            var up = settled && MathF.Abs(SN.Vector3.Dot(d, normal)) < 0.9f ? normal : MathF.Abs(d.Y) > 0.95f ? SN.Vector3.UnitX : SN.Vector3.UnitY;
            var emission = ((_eye[i] * 5f * (1f + (1.5f * hearing))) + (_mouth[i] * 2.5f * speech)) * glow;
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
        private readonly uint[] _firstVertex;
        private readonly VkDeviceAddress[] _scratch;
        private readonly VkDeviceAddress[] _instances;
        private readonly nint[] _mapped;

        public RayScene(Device device, PhysicalDevice physical, Queue queue, CommandBuffer cb, VkBuffer mesh, int cubeCount, (int First, int Count, float Width)[] variants, int frames, int maxInstances)
        {
            _device = device;
            var meshAddress = device.GetBufferDeviceAddress(mesh);
            var ranges = new List<(int First, int Count)> { (0, cubeCount) };
            ranges.AddRange(variants.Select(v => (v.First, v.Count)));
            _firstVertex = [.. ranges.Select(r => (uint)r.First)];
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
        // structure of mesh m (0 the cube, 1 + v tower variant v). Its custom index is the mesh's first vertex, from which
        // the shader finds the triangle a ray hits.
        // Written field by field into the mapped buffer, in VkAccelerationStructureInstanceKHR's layout: the binding's
        // bit-field setters cost about 2 µs an instance, too much for 10,000 a frame.
        public void Write(int f, int k, in SN.Matrix4x4 m, int mesh, bool lamp, bool swarm = false)
        {
            var p = (RawInstance*)_mapped[f] + k;
            p->X = new SN.Vector4(m.M11, m.M21, m.M31, m.M41);
            p->Y = new SN.Vector4(m.M12, m.M22, m.M32, m.M42);
            p->Z = new SN.Vector4(m.M13, m.M23, m.M33, m.M43);
            p->CustomAndMask = _firstVertex[mesh] | ((lamp ? 0x02u : swarm ? 0x04u : 0x01u) << 24);
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
    // Under DLSS, the scene's has no MSAA, a second colour target for the motion and the previous frame's instances as a
    // third vertex input; the other is the sky's motion. guides: Ray Reconstruction's targets in the pass, which only
    // its own pipelines write (writeGuides).
    private static VkPipeline Pipeline(Device device, VkPipelineLayout layout, VkRenderPass renderPass, VkShaderModule vertex, VkShaderModule fragment, bool scene, bool dlss = false, int guides = 0, bool writeGuides = false)
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
                new() { Binding = 2, Stride = ByteSize.Of<Drawn>(), InputRate = VkVertexInputRate.Instance },
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
                new() { Location = 9, Binding = 2, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Zero },
                new() { Location = 10, Binding = 2, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(4) },
                new() { Location = 11, Binding = 2, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(8) },
                new() { Location = 12, Binding = 2, Format = VkFormat.R32G32B32A32SFloat, Offset = ByteSize.Of<float>(12) },
            };
            var vertexInput = scene
                ? new VkPipelineVertexInputStateCreateInfo
                {
                    VertexBindingDescriptionCount = dlss ? 3 : 2,
                    VertexBindingDescriptions = bindings,
                    VertexAttributeDescriptionCount = dlss ? 13 : 9,
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
            var multisample = new VkPipelineMultisampleStateCreateInfo { RasterizationSamples = scene && !dlss ? Msaa : VkSampleCountFlags._1Bit };
            // The sky's motion under DLSS: only where the depth is still the clear's, and without writing it.
            var depth = new VkPipelineDepthStencilStateCreateInfo { DepthTestEnable = scene || dlss, DepthWriteEnable = scene, DepthCompareOp = scene ? VkCompareOp.Greater : VkCompareOp.GreaterOrEqual };
            const VkColorComponentFlags rgba = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit;
            var blendAttachments = stackalloc VkPipelineColorBlendAttachmentState[]
            {
                new() { ColorWriteMask = rgba },
                new() { ColorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit },
                new() { ColorWriteMask = writeGuides ? rgba : 0 },
                new() { ColorWriteMask = writeGuides ? rgba : 0 },
                new() { ColorWriteMask = writeGuides ? rgba : 0 },
                new() { ColorWriteMask = writeGuides ? VkColorComponentFlags.RBit : 0 },
            };
            var blend = new VkPipelineColorBlendStateCreateInfo { AttachmentCount = dlss ? 2 + guides : 1, Attachments = blendAttachments };
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

    // Turns on, in one of Vulkan's feature structures, the features Streamline names (Vulkan's own camelCase names, which
    // BRUTAL's fields spell in PascalCase). A feature the GPU lacks, as has reports it, goes to missing instead.
    private static void EnableFeatures<T>(T* features, T* has, string[] names, List<string> missing) where T : unmanaged
    {
        foreach (var name in names)
        {
            var field = typeof(T).GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var offset = field is null ? -1 : (int)Marshal.OffsetOf<T>(field.Name);
            if (offset < 0 || *(uint*)((byte*)has + offset) == 0)
            {
                missing.Add(name);
                continue;
            }

            *(uint*)((byte*)features + offset) = 1;
        }
    }
}
