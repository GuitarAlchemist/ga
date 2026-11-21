// A tour of BRUTAL's packages (GuitarAlchemist/gaia#257): one small check a package, each doing real work with it where
// it can run without a window or a user (a world stepped, an image written and read back, a mesh simplified, a shader
// compiled), and otherwise loading its native library and asking it its version. Each check is timed, and its failure
// is caught and reported, so one package cannot stop the tour. It prints a Markdown table, which the README keeps.
// Usage: dotnet run -c Release [-- <out.md>]
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Box2DApi;
using Brutal.GliApi;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.FreeType;
using Brutal.KtxApi;
using Brutal.KtxApi.Abstractions;
using Brutal.OpenGlApi;
using Brutal.RakNetApi;
using Brutal.SlangApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using Brutal.Concurrency.Jobs;
using Brutal.Localization;
using Brutal.Logging;
using Brutal.MeshOptimizer;
using Brutal.NativeFileDialogApi;
using Brutal.NotCursesApi;
using Brutal.Numerics;
using Brutal.Pointers;
using Brutal.RenderDocApi;
using Brutal.SdlApi;
using Brutal.StbApi;
using Brutal.TextureApi;
using Microsoft.Extensions.Logging;

var rows = new List<(string Package, bool Ok, string Detail, double Ms)>();
var work = Path.Combine(Path.GetTempPath(), "brutal-tour");
Directory.CreateDirectory(work);

void Check(string package, Func<string> run)
{
    var clock = Stopwatch.StartNew();
    try
    {
        var detail = run();
        rows.Add((package, true, detail, clock.Elapsed.TotalMilliseconds));
    }
    catch (Exception e)
    {
        rows.Add((package, false, $"{e.GetType().Name}: {e.Message.Split('\n')[0]}", clock.Elapsed.TotalMilliseconds));
    }

    var last = rows[^1];
    Console.WriteLine($"TOUR {(last.Ok ? "ok  " : "FAIL")} {package}: {last.Detail} ({last.Ms:F1} ms)");
}

// A native library shipped beside the app, opened by hand: for the packages that bind no C# API of their own.
nint Native(string file) =>
    NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier.StartsWith("win") ? "win-x64" : "linux-x64", "native", file));

Check("Box2D", () =>
{
    var version = Box2D.GetVersion();
    var world = Box2D.CreateWorld(new B2WorldDef { Gravity = new float2(0f, -10f) });
    var ground = world.CreateBody(new B2BodyDef());
    ground.CreatePolygonShape(new B2ShapeDef(), Box2D.MakeBox(20f, 0.5f));
    var crate = world.CreateBody(new B2BodyDef { Type = B2BodyType.Dynamic, Position = new float2(0f, 10f) });
    crate.CreatePolygonShape(new B2ShapeDef { Density = 1f }, Box2D.MakeBox(0.5f, 0.5f));
    for (var i = 0; i < 180; i++)
    {
        world.Step(1f / 60f, 4);
    }

    var y = crate.GetPosition().Y;
    world.Dispose();
    if (MathF.Abs(y - 1f) > 0.05f)
    {
        throw new InvalidOperationException($"the crate rests at y = {y:F3}, not 1");
    }

    return $"Box2D {version.Major}.{version.Minor}.{version.Revision}: a crate dropped from 10 m rests on the ground at y = {y:F3} after 3 s";
});

Check("Concurrency", () =>
{
    var scheduler = new JobScheduler(4, "tour");
    var jobs = Enumerable.Range(0, 64).Select(k => new SumJob(k * 100_000, 100_000)).ToArray();
    foreach (var job in jobs)
    {
        scheduler.Queue(job, 10);
    }

    scheduler.ExecuteJobs();
    scheduler.Wait();
    var total = jobs.Sum(j => j.Result);
    var expected = (long)6_400_000 * (6_400_000 - 1) / 2;
    scheduler.AbortJobs(true);
    return total == expected ? $"64 jobs on 4 runners summed 0..6.4M: {total}" : throw new InvalidOperationException($"sum {total}, expected {expected}");
});

Check("Logging", () =>
{
    var sink = new ListSink();
    LogSystem.Initialize(b => b.AddProvider(sink));
    DefaultCategory.Log.Info("The colossus wakes");
    return sink.Lines.Count > 0 ? $"a message reached a custom ILoggerProvider: \"{sink.Lines[0]}\"" : throw new InvalidOperationException("nothing logged");
});

Check("Localization", () =>
{
    var french = new Dictionary<string, string> { ["tour.title"] = "Le colosse parle", ["tour.swarm"] = "{0} machines" };
    LStrings.SetLocalizedStringProvider((LString s, [NotNullWhen(true)] out string? t) => french.TryGetValue(s.Id.ToString(), out t));
    var title = LStrings.GetOrCreate("tour.title", "The colossus speaks");
    return title.Localized == "Le colosse parle" ? $"\"{title.Template}\" became \"{title.Localized}\"" : throw new InvalidOperationException($"got \"{title.Localized}\"");
});

var png = Path.Combine(work, "gradient.png");
Check("Stb", () =>
{
    const int size = 64;
    var pixels = new byte[size * size * 4];
    for (var i = 0; i < size * size; i++)
    {
        pixels[(i * 4) + 0] = (byte)(i % size * 4);
        pixels[(i * 4) + 1] = (byte)(i / size * 4);
        pixels[(i * 4) + 2] = 128;
        pixels[(i * 4) + 3] = 255;
    }

    if (!Stb.WritePng(png, size, size, 4, pixels, size * 4))
    {
        throw new InvalidOperationException("WritePng failed");
    }

    using var image = Stb.Load(png, 4);
    bool same;
    unsafe
    {
        same = image.Width == size && image.Height == size && new ReadOnlySpan<byte>(image.Data, pixels.Length).SequenceEqual(pixels);
    }

    return same ? $"wrote a {size}x{size} PNG ({new FileInfo(png).Length} bytes) and read the same pixels back" : throw new InvalidOperationException("pixels differ");
});

Check("Texture", () =>
{
    using var texture = TextureLoader.LoadFromFile(png).Using();
    var extent = texture.Texture.Extent(0);
    return $"loaded the PNG as {texture.Texture.Format}, {extent.X}x{extent.Y}";
});

Check("MeshOptimizer", () =>
{
    // A UV sphere, 128 by 64 segments.
    const int across = 128, down = 64;
    var positions = new List<float>();
    for (var j = 0; j <= down; j++)
    {
        for (var i = 0; i <= across; i++)
        {
            var (theta, phi) = (MathF.PI * j / down, 2f * MathF.PI * i / across);
            positions.AddRange([MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi)]);
        }
    }

    var indices = new List<int>();
    for (var j = 0; j < down; j++)
    {
        for (var i = 0; i < across; i++)
        {
            int a = (j * (across + 1)) + i, b = a + across + 1;
            indices.AddRange([a, b, a + 1, a + 1, b, b + 1]);
        }
    }

    var vertex = positions.ToArray();
    var index = indices.ToArray();
    var cached = new int[index.Length];
    var lod = new int[index.Length];
    float error = 0f;
    long kept;
    unsafe
    {
        fixed (int* src = index, dst = cached, low = lod)
        fixed (float* v = vertex)
        {
            MeshOptimizer.OptimizeVertexCache(dst, src, index.Length, vertex.Length / 3);
            kept = (long)MeshOptimizer.Simplify(low, dst, index.Length, v, vertex.Length / 3, new ByteSize64(12), index.Length / 10, 0.01f, 0, &error).Value;
        }
    }

    return $"sphere of {index.Length / 3} triangles: vertex cache reordered, simplified to {kept / 3} triangles (target 10 %), error {error:E2}";
});

Check("Sdl", () =>
{
    static string V(int v) => $"{v / 1_000_000}.{v / 1000 % 1000}.{v % 1000}";
    return $"SDL {V(Sdl.PInvoke.GetVersion())}, SDL_image {V(Brutal.SdlApi.Image.SdlImage.Version())}, SDL_ttf {V(Brutal.SdlApi.Ttf.SdlTtf.Version())}, SDL_mixer {V(Brutal.SdlApi.Mixer.SdlMixer.Version())}";
});

Check("NotCurses", () => $"notcurses {NotCurses.Version} (version only: it takes over the terminal)");

Check("NativeFileDialog", () =>
{
    NativeFileDialog.Initialize();
    NativeFileDialog.Shutdown();
    return "nfd initialised and shut down (no dialog opened: it needs a user)";
});

Check("RenderDoc", () => RenderDoc.Initialize()
    ? $"RenderDoc in-app API {RenderDoc.Version} available"
    : "RenderDoc is not loaded in this process: the in-app API binds only when RenderDoc launches or injects the app");

Check("ZLib", () =>
{
    var lib = Native("zlib.dll");
    unsafe
    {
        var version = (delegate* unmanaged<sbyte*>)NativeLibrary.GetExport(lib, "zlibVersion");
        return $"zlib {new string(version())} (native only, for FreeType and libpng)";
    }
});

Check("FreeType", () =>
{
    var lib = Native("freetype.dll");
    unsafe
    {
        var init = (delegate* unmanaged<nint*, int>)NativeLibrary.GetExport(lib, "FT_Init_FreeType");
        var version = (delegate* unmanaged<nint, int*, int*, int*, void>)NativeLibrary.GetExport(lib, "FT_Library_Version");
        var done = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(lib, "FT_Done_FreeType");
        nint library;
        if (init(&library) != 0)
        {
            throw new InvalidOperationException("FT_Init_FreeType failed");
        }

        int major, minor, patch;
        version(library, &major, &minor, &patch);
        done(library);
        return $"FreeType {major}.{minor}.{patch} (native only, for ImGui.FreeType and SDL_ttf)";
    }
});

Check("Slang", () =>
{
    const string source = """
        RWStructuredBuffer<float> values;
        [shader("compute")]
        [numthreads(64, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID) { values[id.x] = sqrt(float(id.x)) * 2.0; }
        """;
    var global = Slang.CreateGlobalSession();
    unsafe
    {
        ProfileID profile;
        fixed (byte* name = "spirv_1_5\0"u8)
        {
            profile = global.FindProfile(name);
        }

        var target = new TargetDesc { StructureSize = new ByteSize64((ulong)sizeof(TargetDesc)), Format = CompileTarget.Spirv, Profile = profile };
        var desc = new SessionDesc { StructureSize = new ByteSize64((ulong)sizeof(SessionDesc)), Targets = &target, TargetCount = 1 };
        global.CreateSession(ref desc, out var session);
        var module = session.LoadModuleFromSourceString("tour", "tour.slang", source, out var diagnostics)
            ?? throw new InvalidOperationException(diagnostics is null ? "no module" : System.Text.Encoding.UTF8.GetString(diagnostics.AsSpan()));
        IEntryPoint entry;
        fixed (byte* main = "main\0"u8)
        {
            var found = module.FindEntryPointByName(main, out entry);
            if (entry is null)
            {
                throw new InvalidOperationException($"no entry point main: {found}");
            }
        }

        // The entry point links with the module it comes from. (Composing the two with CreateCompositeComponentType
        // crashed the process with an access violation in the binding's COM stub.)
        entry.Link(out var linked, out _);
        linked.GetEntryPointCode(0, 0, out var code, out _);
        var spirv = code.AsSpan();
        var magic = BitConverter.ToUInt32(spirv[..4]);
        return magic == 0x07230203 ? $"Slang {global.GetBuildTag()}: compiled a compute shader to {spirv.Length} bytes of SPIR-V" : throw new InvalidOperationException($"not SPIR-V: 0x{magic:x8}");
    }
});

Check("Ktx", () =>
{
    const int size = 256;
    var rgba = new byte[size * size * 4];
    for (var i = 0; i < size * size; i++)
    {
        var (x, y) = (i % size, i / size);
        var on = ((x / 32) + (y / 32)) % 2 == 0;
        rgba[(i * 4) + 0] = (byte)(on ? 200 : x);
        rgba[(i * 4) + 1] = (byte)(on ? 120 : y);
        rgba[(i * 4) + 2] = 40;
        rgba[(i * 4) + 3] = 255;
    }

    var info = new KtxTextureCreateInfo { VkFormat = 43 /* VK_FORMAT_R8G8B8A8_SRGB */, BaseWidth = size, BaseHeight = size, BaseDepth = 1, NumDimensions = 2, NumLevels = 1, NumLayers = 1, NumFaces = 1 };
    Expect(KtxTexture2.Create(in info, KtxTextureCreateStorageEnum.TextureCreateAllocStorage, out var texture));
    Expect(texture.SetImageFromMemory(0, 0, 0, rgba));
    Expect(texture.CompressBasis(128));
    var file = Path.Combine(work, "checker.ktx2");
    Expect(texture.WriteToNamedFile(file));
    texture.Destroy();
    Expect(KtxTexture2.CreateFromNamedFile(System.Text.Encoding.UTF8.GetBytes(file + "\0"), KtxTextureCreateFlagBits.LoadImageDataBit, out var loaded));
    Expect(loaded.TranscodeBasis(KtxTranscodeFmt.Bc7Rgba, 0));
    loaded.Destroy();
    return $"{size}x{size} RGBA ({rgba.Length} bytes) compressed to Basis Universal, written as KTX2 ({new FileInfo(file).Length} bytes), read back and transcoded to BC7";

    static void Expect(KtxErrorCode code)
    {
        if (code != KtxErrorCode.Success)
        {
            throw new InvalidOperationException($"libktx: {code}");
        }
    }
});

Check("Gli", () =>
{
    var file = Path.Combine(work, "levels.dds");
    using (var texture = new Gli.Texture(Gli.Target._2D, Gli.Format.RGBA8_UNorm_Pack8, new int3(128, 128, 1), 1, 1, 8, Gli.Swizzles.Identity))
    {
        unsafe
        {
            for (var level = 0; level < 8; level++)
            {
                new Span<byte>(texture.ImageData(level), (int)texture.ImageSize(level)).Fill((byte)(level * 30));
            }
        }

        if (!texture.Save(file))
        {
            throw new InvalidOperationException("Save failed");
        }
    }

    using var back = new Gli.Texture(file);
    var extent = back.ImageExtent(7);
    return $"a 128x128 texture with 8 mips saved as DDS ({new FileInfo(file).Length} bytes) and read back, last mip {extent.X}x{extent.Y}";
});

Check("RakNet", () =>
{
    const ushort port = 61234;
    using var server = RakNetLibrary.CreateInstance();
    using var client = RakNetLibrary.CreateInstance();
    Span<SocketDescriptor> serverSocket = [new SocketDescriptor("127.0.0.1", port)];
    Span<SocketDescriptor> clientSocket = [new SocketDescriptor(0)];
    if (server.Startup(4, serverSocket) != StartupResult.RaknetStarted || client.Startup(1, clientSocket) != StartupResult.RaknetStarted)
    {
        throw new InvalidOperationException("Startup failed");
    }

    server.SetMaximumIncomingConnections(4);
    unsafe
    {
        client.Connect("127.0.0.1"u8, port, default, null);
        var clock = Stopwatch.StartNew();
        string? received = null;
        var sent = false;
        while (received is null && clock.ElapsedMilliseconds < 3000)
        {
            for (var p = client.Receive(); p != null; client.DeallocatePacket(p), p = client.Receive())
            {
                if (p->Data[0] == (byte)DefaultMessageIDTypes.ConnectionRequestAccepted && !sent)
                {
                    byte[] message = [(byte)DefaultMessageIDTypes.UserPacketEnum, .. "the swarm gathers"u8];
                    client.Send(message, PacketPriority.HighPriority, PacketReliability.ReliableOrdered, 0, AddressOrGUID.From(p->SystemAddress), false);
                    sent = true;
                }
            }

            for (var p = server.Receive(); p != null; server.DeallocatePacket(p), p = server.Receive())
            {
                if (p->Data[0] == (byte)DefaultMessageIDTypes.UserPacketEnum)
                {
                    received = System.Text.Encoding.UTF8.GetString(p->Data + 1, (int)p->Length - 1);
                }
            }

            Thread.Sleep(5);
        }

        var ms = clock.ElapsedMilliseconds;
        client.Shutdown(100);
        server.Shutdown(100);
        return received is not null ? $"two peers on 127.0.0.1:{port}: connected and delivered \"{received}\" reliably in {ms} ms" : throw new InvalidOperationException("no message within 3 s");
    }
});

Check("OpenGl", () =>
{
    if (!Glfw.Init())
    {
        throw new InvalidOperationException("Glfw.Init failed");
    }

    Glfw.WindowHint(GlfwWindowHint.Visible, 0);
    Glfw.WindowHint(GlfwWindowHint.ClientApi, 0x00030001 /* GLFW_OPENGL_API */);
    var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = "tour", Size = new int2(64, 64) });
    try
    {
        Glfw.PInvoke.MakeContextCurrent(window);
        unsafe
        {
            if (!Glad.Initialize(name => Glfw.PInvoke.GetProcAddress(name)))
            {
                throw new InvalidOperationException("glad could not load OpenGL");
            }

            var renderer = new string((sbyte*)OpenGl.GetString((GlEnum)0x1F01));
            var version = new string((sbyte*)OpenGl.GetString((GlEnum)0x1F02));
            return $"OpenGL {version.Split(' ')[0]} on {renderer}, in a hidden GLFW window";
        }
    }
    finally
    {
        window.Dispose();
    }
});

Check("Vulkan.Vma", () =>
{
    var instance = Vulkan.CreateInstance(new Instance.CreateInfo { AppInfo = new VkApplicationInfo { ApiVersion = (1 << 22) | (3 << 12) }, EnabledExtensions = [], EnabledLayers = [] });
    var physical = instance.PhysicalDevices.OrderBy(p => p.GetProperties().DeviceType == VkPhysicalDeviceType.DiscreteGpu ? 0 : 1).First();
    unsafe
    {
        var priority = 1f;
        var queueInfo = new VkDeviceQueueCreateInfo { QueueFamilyIndex = 0, QueueCount = 1, QueuePriorities = &priority };
        var device = physical.CreateDevice(new DeviceEx.CreateInfo { Extensions = [], QueueCreateInfos = new ReadOnlySpan<VkDeviceQueueCreateInfo>(&queueInfo, 1) });
        var allocator = device.CreateVmaAllocator(new VmaAllocatorCreateInfo { PhysicalDevice = new VkPhysicalDevice(physical.VkHandle), Device = new VkDevice(device.VkHandle), Instance = new VkInstance(instance.VkHandle), VulkanApiVersion = (1 << 22) | (3 << 12) });
        var bufferInfo = new VkBufferCreateInfo { Size = new ByteSize64(256UL << 20), Usage = VkBufferUsageFlags.StorageBufferBit };
        var result = allocator.CreateBuffer(bufferInfo, new VmaAllocationCreateInfo { Usage = VmaMemoryUsage.AutoPreferDevice }, out var buffer, out var allocation, out var allocationInfo);
        allocator.DestroyBuffer(buffer, allocation);
        device.DestroyVmaAllocator(allocator);
        var name = physical.GetProperties().DeviceType;
        device.Dispose();
        instance.Dispose();
        return result == VkResult.Success ? $"Vulkan 1.3 on {name}, no window: VMA placed a 256 MB storage buffer in memory type {allocationInfo.MemoryType}" : throw new InvalidOperationException($"CreateBuffer: {result}");
    }
});

Check("ImGui.FreeType", () =>
{
    var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    ImGui.CreateContext();
    try
    {
        var fonts = ImGui.GetIO().Fonts;
        fonts.SetFontLoader(ImGuiFreeType.GetFontLoader());
        var face = fonts.AddFontFromFileTTF(font, 18f);
        var size = face.CalcTextSizeA(18f, float.MaxValue, 0f, "Le colosse parle");
        return $"Dear ImGui loaded {Path.GetFileName(font)} through FreeType: \"Le colosse parle\" at 18 px is {size.X:F1} x {size.Y:F1} px";
    }
    finally
    {
        ImGui.DestroyContext();
    }
});

// The managed packages with no native side and no single job to try: each is loaded, and its public surface counted.
foreach (var package in new[] { "Collections", "Memory", "Strings", "Maths", "Interop", "Render.Mesh", "Render.Common", "Render.Debug", "ImGui.Abstractions", "ImGui.Extensions", "Monitor.Common", "Monitor.Host" })
{
    Check(package, () =>
    {
        var assembly = System.Reflection.Assembly.Load($"Brutal.{package}");
        var types = assembly.GetExportedTypes();
        return $"loaded {assembly.GetName().Name} {assembly.GetName().Version}: {(types.Length == 0 ? "no public types" : $"{types.Length} public types, among them {string.Join(", ", types.Take(3).Select(t => t.Name))}")} (loaded only)";
    });
}

Console.WriteLine();
var table =new System.Text.StringBuilder("| Package | Result | What the check did | ms |\n|---|---|---|---|\n");
foreach (var (package, ok, detail, ms) in rows)
{
    table.Append($"| {package} | {(!ok ? "fails" : detail.EndsWith("(loaded only)") ? "loads" : "works")} | {detail.Replace("|", "\\|")} | {ms:F1} |\n");
}

Console.Write(table);
if (args.Length > 0)
{
    File.WriteAllText(args[0], table.ToString());
}

Console.WriteLine($"TOUR {rows.Count(r => r.Ok)} of {rows.Count} checks passed");
return rows.All(r => r.Ok) ? 0 : 1;

internal sealed class SumJob(long first, long count) : IJob
{
    public long Result { get; private set; }

    public void Run()
    {
        long sum = 0;
        for (var k = first; k < first + count; k++)
        {
            sum += k;
        }

        Result = sum;
    }
}

internal sealed class ListSink : ILoggerProvider, ILogger
{
    public List<string> Lines { get; } = [];

    public ILogger CreateLogger(string categoryName) => this;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Add($"{logLevel}: {formatter(state, exception)}");

    public void Dispose()
    {
    }
}
