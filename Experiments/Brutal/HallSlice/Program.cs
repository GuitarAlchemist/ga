// M2 of the BRUTAL evaluation (GuitarAlchemist/gaia#257): a small slice of the Observatory's hall, drawn with Vulkan
// through BRUTAL's bindings: the ring of 16 piers with their lintels, the floor and the central pedestal and statue, as
// flat-shaded boxes with a depth buffer. The camera orbits the axis by itself; the arrow keys turn and tilt it, W and S
// move it in and out. It closes on Esc or after a time limit (seconds, first argument, default 10) and prints its
// frame times. Present mode: immediate (no vsync) when available, unless --vsync; --cap N then limits it to N frames a
// second (M3). A fixed-size window: a swapchain
// that goes out of date ends the run rather than being rebuilt.
//
// Lego (gaia's "fractal Lego"): --lego <block[@version]> [--seed N] [--arg name=value]... draws a Lego scene instead of
// the hall, framed by its bounds; a scene Lego refuses ends the run with code 2 before any window opens. --hidden keeps
// the window off screen, --capture FILE.png saves frame 120, and --cycles N then describes and materializes the scene
// N more times, dropping each, and prints what was left behind. --tour SECONDS shows the blocks one after another, from
// one column to a sanctuary of twenty-one temples, each built and framed in turn, then again with the next seed.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using LegoScene = Lego.Scene;
using SN = System.Numerics;

return HallSlice.Run(args);

[StructLayout(LayoutKind.Sequential)]
internal struct Vertex(SN.Vector3 pos, SN.Vector3 normal, SN.Vector3 color)
{
    public SN.Vector3 Pos = pos;
    public SN.Vector3 Normal = normal;
    public SN.Vector3 Color = color;
}

internal static unsafe class HallSlice
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int FramesInFlight = 2;
    private const int ApiVersion13 = (1 << 22) | (3 << 12);

    // The hall as observatory.gd lays it out: 16 bays, piers 1.1 x 1.4 on a 12.3 m ring.
    private const int Bays = 16;
    private const float Radius = 12.3f;

    private const string VertexShader = """
        #version 450
        layout(location = 0) in vec3 inPos;
        layout(location = 1) in vec3 inNormal;
        layout(location = 2) in vec3 inColor;
        layout(push_constant) uniform Push { mat4 viewProj; } pc;
        layout(location = 0) out vec3 vNormal;
        layout(location = 1) out vec3 vColor;
        void main()
        {
            gl_Position = pc.viewProj * vec4(inPos, 1.0);
            vNormal = inNormal;
            vColor = inColor;
        }
        """;

    private const string FragmentShader = """
        #version 450
        layout(location = 0) in vec3 vNormal;
        layout(location = 1) in vec3 vColor;
        layout(location = 0) out vec4 outColor;
        void main()
        {
            vec3 n = normalize(vNormal);
            float sun = max(dot(n, normalize(vec3(0.35, 0.85, 0.4))), 0.0);
            float sky = 0.5 + 0.5 * n.y;
            outColor = vec4(vColor * (0.15 + 0.25 * sky + 0.7 * sun), 1.0);
        }
        """;

    public static int Run(string[] args)
    {
        var limit = TimeSpan.FromSeconds(args.Length > 0 && double.TryParse(args[0], out var s) ? s : 10.0);
        var vsync = args.Contains("--vsync");
        var capAt = Array.IndexOf(args, "--cap");
        var cap = capAt >= 0 && capAt + 1 < args.Length && int.TryParse(args[capAt + 1], out var capValue) ? capValue : 0;
        var clock = Stopwatch.StartNew();
        var hidden = args.Contains("--hidden");
        var capture = Option(args, "--capture");
        var cycles = int.TryParse(Option(args, "--cycles"), out var cycleCount) ? cycleCount : 0;
        var legoSpec = Option(args, "--lego");
        var legoArgs = args.Where((a, i) => i > 0 && args[i - 1] == "--arg").ToArray();
        var tour = double.TryParse(Option(args, "--tour"), out var tourSeconds) ? tourSeconds : 0.0;
        if (tour > 0)
        {
            (legoSpec, legoArgs) = TourSteps[0];
        }

        var seedText = Option(args, "--seed");
        var seed = 0UL;
        if (seedText is not null && !ulong.TryParse(seedText, out seed))
        {
            Console.Error.WriteLine($"LEGO refused: the seed '{seedText}' is not a whole number from 0 to {ulong.MaxValue}");
            return 2;
        }

        LegoScene? scene = null;
        if (legoSpec is not null)
        {
            scene = LegoAdapter.Validated(legoSpec, seed, legoArgs);
            if (scene is null)
            {
                return 2;
            }

            Console.WriteLine($"LEGO {scene.Draft.Root.Block} seed {seed}: {scene.Draft.Instances} instances, {scene.Boxes.Count} boxes, {Lego.Describe.hash(scene)}, generated and validated in {clock.Elapsed.TotalMilliseconds:F0} ms");
        }

        if (!Glfw.Init())
        {
            Console.Error.WriteLine("M2 FAIL: Glfw.Init returned false");
            return 1;
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
        Glfw.WindowHint(GlfwWindowHint.Resizable, 0);
        if (hidden)
        {
            Glfw.WindowHint(GlfwWindowHint.Visible, 0);
        }

        var title = scene is null ? "Hall slice (BRUTAL)" : tour > 0 ? "Lego tour (BRUTAL)" : $"Lego {scene.Draft.Root.Block} (BRUTAL)";
        var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = title, Size = new int2(Width, Height) });

        // Instance with the extensions GLFW needs to present, and the window's surface.
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
            Console.Error.WriteLine("M2 FAIL: no window surface");
            return 1;
        }

        var surface = new VkSurfaceKHR((nint)rawSurface);

        // The first discrete GPU with a queue family that draws and presents (any GPU if none is discrete).
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
            Console.Error.WriteLine("M2 FAIL: no GPU can draw and present to this window");
            return 1;
        }

        var priority = 1.0f;
        var queueInfo = new VkDeviceQueueCreateInfo { QueueFamilyIndex = family, QueueCount = 1, QueuePriorities = &priority };
        var device = physical.CreateDevice(new Device.CreateInfo
        {
            Extensions = ["VK_KHR_swapchain"],
            QueueCreateInfos = new ReadOnlySpan<VkDeviceQueueCreateInfo>(&queueInfo, 1),
        });
        var queue = device.GetQueue(family, 0);

        // Swapchain: sRGB BGRA if offered, immediate presentation unless --vsync.
        var formatCount = physical.GetSurfaceFormatsKHR_Count(surface);
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[formatCount];
        physical.GetSurfaceFormatsKHR_Write(surface, formats);
        var surfaceFormat = formats[0];
        foreach (var f in formats)
        {
            if (f.Format == VkFormat.B8G8R8A8SRGB && f.ColorSpace == VkColorSpaceKHR.SRGBNonlinearKHR)
            {
                surfaceFormat = f;
            }
        }

        var modeCount = physical.GetSurfacePresentModesKHR_Count(surface);
        Span<VkPresentModeKHR> modes = stackalloc VkPresentModeKHR[modeCount];
        physical.GetSurfacePresentModesKHR_Write(surface, modes);
        var presentMode = VkPresentModeKHR.FifoKHR;
        if (!vsync)
        {
            foreach (var wanted in new[] { VkPresentModeKHR.ImmediateKHR, VkPresentModeKHR.MailboxKHR })
            {
                if (presentMode == VkPresentModeKHR.FifoKHR && modes.Contains(wanted))
                {
                    presentMode = wanted;
                }
            }
        }

        var caps = physical.GetSurfaceCapabilitiesKHR(surface);
        var extent = caps.CurrentExtent.Width == -1 ? new VkExtent2D { Width = Width, Height = Height } : caps.CurrentExtent;
        var imageCount = Math.Max(caps.MinImageCount, 3);
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
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | (capture is null ? default : VkImageUsageFlags.TransferSrcBit),
            ImageSharingMode = VkSharingMode.Exclusive,
            PreTransform = caps.CurrentTransform,
            CompositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
            PresentMode = presentMode,
            Clipped = true,
        }, null);
        var images = new VkImage[device.GetSwapchainImagesKHR_Count(swapchain)];
        device.GetSwapchainImagesKHR_Write(swapchain, images);

        // Depth buffer.
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

        // One subpass: colour to present, depth cleared and discarded.
        Span<VkAttachmentDescription> attachments =
        [
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
        ];
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
            AttachmentCount = attachments.Length,
            Attachments = (VkAttachmentDescription*)Unsafe.AsPointer(ref attachments[0]),
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

        // Shaders, compiled from GLSL at start-up.
        var compileClock = Stopwatch.StartNew();
        var vertexModule = Shader(device, VertexShader, ShaderKind.VertexShader, "hall.vert");
        var fragmentModule = Shader(device, FragmentShader, ShaderKind.FragmentShader, "hall.frag");
        Console.WriteLine($"M2 shaders compiled in {compileClock.Elapsed.TotalMilliseconds:F0} ms");

        var pushRange = new VkPushConstantRange { StageFlags = VkShaderStageFlags.VertexBit, Offset = ByteSize.Zero, Size = ByteSize.Of<SN.Matrix4x4>() };
        var layout = device.CreatePipelineLayout([], [pushRange], null);
        var pipeline = Pipeline(device, layout, renderPass, vertexModule, fragmentModule);

        // The geometry, the hall's or the Lego scene's, in a host-visible vertex buffer.
        var vertices = scene is null ? Hall() : LegoAdapter.Vertices(scene);
        var mesh = new SliceMesh(device, physical, vertices);
        Console.WriteLine($"M2 geometry: {vertices.Length / 36} boxes, {vertices.Length} vertices");

        // --capture: frame 120's pixels, copied from the swapchain image into a host-visible buffer.
        const int CaptureFrame = 120;
        var readbackSize = new ByteSize((nuint)(extent.Width * extent.Height * 4));
        var readback = default(VkBuffer);
        var readbackMemory = default(VkDeviceMemory);
        var readbackMapped = nint.Zero;
        if (capture is not null)
        {
            readback = device.CreateBuffer(new VkBufferCreateInfo { Size = readbackSize, Usage = VkBufferUsageFlags.TransferDstBit, SharingMode = VkSharingMode.Exclusive }, null);
            readbackMemory = device.AllocateMemory(physical, readback, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(readback, readbackMemory, memoryOffset: ByteSize.Zero);
            readbackMapped = (nint)(byte*)device.MapMemory<byte>(new MapInfo(readbackMemory) { Region = new MapRegion { Size = readbackSize } });
        }

        // Per frame in flight: a command pool and buffer, a fence and an image-acquired semaphore.
        var pools = new VkCommandPool[FramesInFlight];
        var commands = new CommandBuffer[FramesInFlight];
        var fences = new VkFence[FramesInFlight];
        var acquired = new VkSemaphore[FramesInFlight];
        for (var i = 0; i < FramesInFlight; i++)
        {
            pools[i] = device.CreateCommandPool(new VkCommandPoolCreateInfo { QueueFamilyIndex = family }, null);
            commands[i] = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo { CommandPool = pools[i], Level = VkCommandBufferLevel.Primary });
            fences[i] = device.CreateFence(new VkFenceCreateInfo { Flags = VkFenceCreateFlags.SignaledBit }, null);
            acquired[i] = device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }

        // Camera: orbits the axis by itself; arrows turn and tilt, W and S move in and out.
        float yaw = 0.6f, pitch = 0.32f, distance = 27f, closest = 6f, farthest = 60f, radius = 20f;
        var target = new SN.Vector3(0f, 2.5f, 0f);
        var (targetGoal, distanceGoal) = (target, distance);
        void Frame(LegoScene framed)
        {
            // A Lego scene: the camera aims at the middle of its bounds, far enough back to keep them in frame.
            radius = Math.Max(SN.Vector3.Distance(framed.Min, framed.Max) / 2f, 1f);
            targetGoal = (framed.Min + framed.Max) / 2f;
            pitch = 0.45f;
            distanceGoal = LegoAdapter.Distance(framed, targetGoal, pitch, extent.Width / (float)extent.Height);
            (closest, farthest) = (0.25f * distanceGoal, 2.5f * distanceGoal);
        }

        if (scene is not null)
        {
            Frame(scene);
            (target, distance) = (targetGoal, distanceGoal);
        }

        var keys = 0;
        var reason = "window closed";
        window.OnKey += (w, key, scancode, action, mods) =>
        {
            if (action == GlfwKeyAction.Release)
            {
                return;
            }

            switch (key)
            {
                case GlfwKey.Left: yaw -= 0.15f; break;
                case GlfwKey.Right: yaw += 0.15f; break;
                case GlfwKey.Up: pitch = Math.Min(pitch + 0.08f, 1.3f); break;
                case GlfwKey.Down: pitch = Math.Max(pitch - 0.08f, 0.05f); break;
                case GlfwKey.W: distanceGoal = Math.Max(distanceGoal - (closest / 4f), closest); break;
                case GlfwKey.S: distanceGoal = Math.Min(distanceGoal + (closest / 4f), farthest); break;
                case GlfwKey.Escape:
                    reason = "Esc";
                    w.ShouldClose = true;
                    break;
                default:
                    // Pause is left out: this host's focused window receives Pause presses nobody typed.
                    if (key != GlfwKey.Pause)
                    {
                        Console.WriteLine($"M2 key {key}: no camera action, at {clock.Elapsed.TotalSeconds:F2} s");
                    }

                    return;
            }

            keys++;
            Console.WriteLine($"M2 key {key}: yaw {yaw:F2} pitch {pitch:F2} distance {distance:F1} at {clock.Elapsed.TotalSeconds:F2} s");
        };

        Console.WriteLine($"M2 present mode {presentMode}, {images.Length} swapchain images, {extent.Width}x{extent.Height}");
        if (cap > 0)
        {
            Console.WriteLine($"M2 frame cap {cap} fps");
            if (OperatingSystem.IsWindows())
            {
                _ = TimeBeginPeriod(1); // 1 ms sleeps, so the cap paces frames without spinning the whole time
            }
        }

        var period = cap > 0 ? 1.0 / cap : 0.0;
        var nextFrame = 0.0;
        var lastPresent = 0.0;
        var frameTimes = new List<double>(100_000);
        var frame = 0;
        var last = clock.Elapsed.TotalSeconds;
        var firstFrame = -1.0;
        var step = 0;
        var nextStep = last + tour;
        var clears = stackalloc VkClearValue[2];
        var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        var loopStart = clock.Elapsed;
        while (!window.ShouldClose)
        {
            Glfw.PollEvents();
            if (clock.Elapsed >= limit)
            {
                reason = "time limit";
                break;
            }

            var now = clock.Elapsed.TotalSeconds;
            var dt = (float)(now - last);
            yaw += dt * 0.2f;
            last = now;
            if (tour > 0 && now >= nextStep)
            {
                // The tour's next step: the old mesh dropped once the GPU is done with it, the next one made and framed.
                step++;
                nextStep = now + tour;
                var (spec, stepArgs) = TourSteps[step % TourSteps.Length];
                var stepSeed = seed + (ulong)(step / TourSteps.Length);
                var next = LegoAdapter.Validated(spec, stepSeed, stepArgs);
                if (next is not null)
                {
                    device.WaitIdle();
                    mesh.Dispose();
                    mesh = new SliceMesh(device, physical, LegoAdapter.Vertices(next));
                    scene = next;
                    Frame(next);
                    Console.WriteLine($"LEGO tour {step}: {next.Draft.Root.Block} {string.Join(' ', stepArgs)} seed {stepSeed}: {next.Draft.Instances} instances, {next.Boxes.Count} boxes");
                }
            }

            // The hall's camera jumps with its keys; a Lego scene's glides to its new framing.
            var follow = scene is null ? 1f : 1f - MathF.Exp(-2.5f * dt);
            target = SN.Vector3.Lerp(target, targetGoal, follow);
            distance += (distanceGoal - distance) * follow;
            var far = scene is null ? 200f : (3f * distance) + (2f * radius) + 10f;

            var slot = frame % FramesInFlight;
            device.WaitForFences([fences[slot]], true, -1);
            var result = device.AcquireNextImageKHR(swapchain, -1, acquired[slot], default, out var imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                reason = "swapchain out of date (the window is fixed-size; rebuilding is out of scope)";
                break;
            }

            device.ResetFences([fences[slot]]);
            device.ResetCommandPool(pools[slot], VkCommandPoolResetFlags.None);

            var eye = target + (distance * new SN.Vector3(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw)));
            var projection = SN.Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, extent.Width / (float)extent.Height, 0.1f, far);
            projection.M22 = -projection.M22; // Vulkan's clip space has y pointing down
            var viewProj = SN.Matrix4x4.CreateLookAt(eye, target, SN.Vector3.UnitY) * projection;

            var cb = commands[slot];
            cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
            clears[0] = new VkClearValue { Color = new VkClearColorValue { Float32 = new float4(0.015f, 0.018f, 0.03f, 1f) } };
            clears[1] = new VkClearValue { DepthStencil = new VkClearDepthStencilValue { Depth = 1f } };
            cb.BeginRenderPass(new VkRenderPassBeginInfo
            {
                RenderPass = renderPass,
                Framebuffer = framebuffers[imageIndex],
                RenderArea = new VkRect2D { Extent = extent },
                ClearValueCount = 2,
                ClearValues = clears,
            }, VkSubpassContents.Inline);
            cb.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            cb.SetViewport(firstViewport: 0, [new VkViewport { Width = extent.Width, Height = extent.Height, MinDepth = 0f, MaxDepth = 1f }]);
            cb.SetScissor(firstScissor: 0, [new VkRect2D { Extent = extent }]);
            cb.PushConstants(layout, VkShaderStageFlags.VertexBit, offset: 0, new ReadOnlySpan<byte>(&viewProj, sizeof(SN.Matrix4x4)));
            cb.BindVertexBuffer(0, mesh.Buffer);
            cb.Draw(mesh.Count, 1, 0, 0);
            cb.EndRenderPass();
            var capturing = capture is not null && frame == CaptureFrame;
            if (capturing)
            {
                cb.PipelineBarrier(VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], VkImageLayout.PresentSrcKHR, VkImageLayout.TransferSrcOptimal, VkAccessFlags.ColorAttachmentWriteBit, VkAccessFlags.TransferReadBit)]);
                cb.CopyImageToBuffer(images[imageIndex], VkImageLayout.TransferSrcOptimal, readback,
                    [new VkBufferImageCopy { ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 }, ImageExtent = new VkExtent3D { Width = extent.Width, Height = extent.Height, Depth = 1 } }]);
                cb.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.BottomOfPipeBit, VkDependencyFlags.None, [], [],
                    [Barrier(images[imageIndex], VkImageLayout.TransferSrcOptimal, VkImageLayout.PresentSrcKHR, default, default)]);
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
                reason = "swapchain out of date (the window is fixed-size; rebuilding is out of scope)";
                break;
            }

            var t = clock.Elapsed.TotalSeconds;
            if (firstFrame < 0)
            {
                firstFrame = t;
                nextFrame = t;
                Console.WriteLine($"M2 first frame presented after {t * 1000:F0} ms");
            }
            else
            {
                frameTimes.Add((t - lastPresent) * 1000.0); // present to present
            }

            lastPresent = t;
            frame++;
            if (capturing)
            {
                device.WaitIdle();
                LegoAdapter.SavePng(capture!, new ReadOnlySpan<byte>((byte*)readbackMapped, (int)(extent.Width * extent.Height * 4)), extent.Width, extent.Height);
                Console.WriteLine($"LEGO captured {capture} at frame {CaptureFrame}");
                lastPresent = clock.Elapsed.TotalSeconds; // the capture's wait is not a frame time
            }

            if (cap > 0)
            {
                // Pace to the cap: sleep while more than 1.5 ms is left, then spin. A frame that falls more than one
                // period behind resets the schedule instead of bursting to catch up.
                nextFrame += period;
                if (clock.Elapsed.TotalSeconds - nextFrame > period)
                {
                    nextFrame = clock.Elapsed.TotalSeconds;
                }

                while (nextFrame - clock.Elapsed.TotalSeconds > 0.0015)
                {
                    Thread.Sleep(1);
                }

                while (clock.Elapsed.TotalSeconds < nextFrame)
                {
                    Thread.SpinWait(20);
                }
            }
        }

        var loopSeconds = (clock.Elapsed - loopStart).TotalSeconds;
        var cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuStart;
        if (cap > 0 && OperatingSystem.IsWindows())
        {
            _ = TimeEndPeriod(1);
        }

        if (capture is not null && frame <= CaptureFrame)
        {
            Console.WriteLine($"LEGO no capture: the run ended after {frame} frames, before frame {CaptureFrame}");
        }

        // --cycles: the scene described and materialized again and again, each mesh dropped; the scene must come out the
        // same each time and leave nothing behind.
        if (cycles > 0 && scene is not null && tour == 0)
        {
            device.WaitIdle();
            var hash = Lego.Describe.hash(scene);
            var mismatches = 0;
            using var process = Process.GetCurrentProcess();
            double Private()
            {
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                process.Refresh();
                return process.PrivateMemorySize64 / (1024.0 * 1024.0);
            }

            void Cycle(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    var again = LegoAdapter.Validated(legoSpec!, seed, legoArgs)!;
                    mismatches += Lego.Describe.hash(again) == hash ? 0 : 1;
                    using var dropped = new SliceMesh(device, physical, LegoAdapter.Vertices(again));
                }
            }

            // The first cycles warm the allocators up (the GC's and the driver's); what grows after them accumulates.
            var warm = Math.Min(10, cycles);
            var (liveBefore, start) = (SliceMesh.Live, Private());
            var cycleClock = Stopwatch.StartNew();
            Cycle(warm);
            var warmed = Private();
            Cycle(cycles - warm);
            var end = Private();
            Console.WriteLine($"LEGO cycles {cycles} in {cycleClock.Elapsed.TotalSeconds:F1} s: {mismatches} hash mismatches; meshes live {liveBefore} before, {SliceMesh.Live} after; private memory {warmed - start:+0.0;-0.0} MB over the first {warm}, {end - warmed:+0.0;-0.0} MB over the next {cycles - warm}");
        }

        // Clean shutdown, in reverse order of creation.
        device.WaitIdle();
        for (var i = 0; i < FramesInFlight; i++)
        {
            device.DestroySemaphore(acquired[i], null);
            device.DestroyFence(fences[i], null);
            device.DestroyCommandPool(pools[i], null);
        }

        if (capture is not null)
        {
            device.UnmapMemory(readbackMemory);
            device.DestroyBuffer(readback, null);
            device.FreeMemory(readbackMemory, null);
        }

        mesh.Dispose();
        if (scene is not null)
        {
            Console.WriteLine($"LEGO meshes live at exit: {SliceMesh.Live}");
        }

        device.DestroyPipeline(pipeline, null);
        device.DestroyPipelineLayout(layout, null);
        device.DestroyShaderModule(vertexModule, null);
        device.DestroyShaderModule(fragmentModule, null);
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
        double P(double q) => frameTimes.Count == 0 ? double.NaN : frameTimes[Math.Min(frameTimes.Count - 1, (int)(q * frameTimes.Count))];
        Console.WriteLine($"M2 frames {frame} in {loopSeconds:F2} s ({frame / loopSeconds:F0} fps); frame ms p50 {P(0.5):F3} p95 {P(0.95):F3} p99 {P(0.99):F3} max {(frameTimes.Count > 0 ? frameTimes[^1] : double.NaN):F3}");
        Console.WriteLine($"M2 CPU {cpu.TotalSeconds:F2} s over {loopSeconds:F2} s ({100 * cpu.TotalSeconds / loopSeconds / Environment.ProcessorCount:F1} % of {Environment.ProcessorCount} logical CPUs), peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine($"M2 closed by {reason} after {clock.Elapsed.TotalSeconds:F2} s, {keys} camera keys");
        return 0;
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint milliseconds);

    // --tour: from one column to a sanctuary of twenty-one temples, then from the richer temple to a walled city: the
    // same blocks composed again and again.
    private static readonly (string Spec, string[] Args)[] TourSteps =
    [
        ("column@1", []),
        ("column@2", []),
        ("colonnade", []),
        ("temple@1", []),
        ("temple@1", ["column=2"]),
        ("sanctuary", ["level=1"]),
        ("sanctuary", ["level=2"]),
        ("temple@2", []),
        ("stoa", []),
        ("tholos", []),
        ("agora", []),
        ("acropolis", []),
        ("polis", []),
    ];

    // The value after the option name, if both are there.
    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    // A colour image's layout change, as Colossus writes it.
    private static VkImageMemoryBarrier Barrier(VkImage image, VkImageLayout from, VkImageLayout to, VkAccessFlags src, VkAccessFlags dst) => new()
    {
        SrcAccessMask = src,
        DstAccessMask = dst,
        OldLayout = from,
        NewLayout = to,
        SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
        DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
        Image = image,
        SubresourceRange = new VkImageSubresourceRange { AspectMask = VkImageAspectFlags.ColorBit, LevelCount = 1, LayerCount = 1 },
    };

    private static VkImageViewCreateInfo View(VkImage image, VkFormat format, VkImageAspectFlags aspect) => new()
    {
        Image = image,
        ViewType = VkImageViewType._2D,
        Format = format,
        SubresourceRange = new VkImageSubresourceRange { AspectMask = aspect, LevelCount = 1, LayerCount = 1 },
    };

    private static VkShaderModule Shader(Device device, string source, ShaderKind kind, string name)
    {
        var compiler = ShaderC.CreateCompiler();
        var options = ShaderC.CreateCompilerOptions();
        using var result = compiler.Compile(Encoding.UTF8.GetBytes(source), kind, Utf8.GetBytes(name + "\0"), Utf8.GetBytes("main\0"), options);
        var module = device.CreateShaderModule(new VkShaderModuleCreateInfo { CodeSize = result.GetLength(), Code = (int*)result.GetBytes() }, null);
        options.Dispose();
        compiler.Dispose();
        return module;
    }

    private static VkPipeline Pipeline(Device device, VkPipelineLayout layout, VkRenderPass renderPass, VkShaderModule vertex, VkShaderModule fragment)
    {
        var entry = "main"u8;
        fixed (byte* entryName = entry)
        {
            var stages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                new() { Stage = VkShaderStageFlags.VertexBit, Module = vertex, Name = entryName },
                new() { Stage = VkShaderStageFlags.FragmentBit, Module = fragment, Name = entryName },
            };
            var binding = new VkVertexInputBindingDescription { Binding = 0, Stride = ByteSize.Of<Vertex>(), InputRate = VkVertexInputRate.Vertex };
            var attributes = stackalloc VkVertexInputAttributeDescription[]
            {
                new() { Location = 0, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = ByteSize.Zero },
                new() { Location = 1, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = ByteSize.Of<float>(3) },
                new() { Location = 2, Binding = 0, Format = VkFormat.R32G32B32SFloat, Offset = ByteSize.Of<float>(6) },
            };
            var vertexInput = new VkPipelineVertexInputStateCreateInfo
            {
                VertexBindingDescriptionCount = 1,
                VertexBindingDescriptions = &binding,
                VertexAttributeDescriptionCount = 3,
                VertexAttributeDescriptions = attributes,
            };
            var assembly = new VkPipelineInputAssemblyStateCreateInfo { Topology = VkPrimitiveTopology.TriangleList };
            var viewport = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
            var raster = new VkPipelineRasterizationStateCreateInfo
            {
                PolygonMode = VkPolygonMode.Fill,
                CullMode = VkCullModeFlags.None,
                FrontFace = VkFrontFace.CounterClockwise,
                LineWidth = 1f,
            };
            var multisample = new VkPipelineMultisampleStateCreateInfo { RasterizationSamples = VkSampleCountFlags._1Bit };
            var depth = new VkPipelineDepthStencilStateCreateInfo { DepthTestEnable = true, DepthWriteEnable = true, DepthCompareOp = VkCompareOp.Less };
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

    // The hall: a floor slab, 16 piers on the ring with a lintel between each pair, the pedestal and the statue on it.
    private static Vertex[] Hall()
    {
        var stone = new SN.Vector3(0.62f, 0.58f, 0.52f);
        var lintel = new SN.Vector3(0.52f, 0.48f, 0.42f);
        var boxes = new List<Vertex>();
        Box(boxes, new SN.Vector3(0f, -0.05f, 0f), new SN.Vector3(32f, 0.1f, 32f), 0f, new SN.Vector3(0.16f, 0.15f, 0.14f));
        for (var i = 0; i < Bays; i++)
        {
            var a = MathF.Tau * i / Bays;
            Box(boxes, new SN.Vector3(MathF.Cos(a) * Radius, 3.5f, MathF.Sin(a) * Radius), new SN.Vector3(1.4f, 7f, 1.1f), a, stone);
            var m = a + (MathF.PI / Bays);
            var chord = 2f * Radius * MathF.Sin(MathF.PI / Bays);
            Box(boxes, new SN.Vector3(MathF.Cos(m) * Radius, 7.25f, MathF.Sin(m) * Radius), new SN.Vector3(0.9f, 0.5f, chord), m, lintel);
        }

        Box(boxes, new SN.Vector3(0f, 0.6f, 0f), new SN.Vector3(1.8f, 1.2f, 1.8f), 0f, new SN.Vector3(0.85f, 0.66f, 0.3f));
        Box(boxes, new SN.Vector3(0f, 2.2f, 0f), new SN.Vector3(0.6f, 2f, 0.6f), 0f, new SN.Vector3(0.45f, 0.32f, 0.2f));
        return [.. boxes];
    }

    // A box of the given size centred on c, turned by yaw about the vertical axis (its local x points away from the
    // hall's axis when yaw is a ring angle).
    private static void Box(List<Vertex> o, SN.Vector3 c, SN.Vector3 size, float yaw, SN.Vector3 color) =>
        Box(o, SN.Matrix4x4.CreateRotationY(-yaw) * SN.Matrix4x4.CreateTranslation(c - new SN.Vector3(0f, size.Y / 2f, 0f)), size, color);

    // A box standing on its frame's floor and centred on its vertical axis, as Lego's are, placed by its world matrix,
    // as 12 triangles with one normal per face (turned by the matrix's inverse transpose, so a scaled box keeps them).
    internal static void Box(List<Vertex> o, SN.Matrix4x4 world, SN.Vector3 size, SN.Vector3 color)
    {
        _ = SN.Matrix4x4.Invert(world, out var inverse);
        var normals = SN.Matrix4x4.Transpose(inverse);
        var h = size / 2f;
        var c = new SN.Vector3(0f, h.Y, 0f);
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
            SN.Vector3 Corner(float su, float sv) => SN.Vector3.Transform(c + (h * (n + (su * u) + (sv * v))), world);
            var normal = SN.Vector3.Normalize(SN.Vector3.TransformNormal(n, normals));
            var p00 = Corner(-1, -1);
            var p10 = Corner(1, -1);
            var p11 = Corner(1, 1);
            var p01 = Corner(-1, 1);
            o.AddRange([new(p00, normal, color), new(p10, normal, color), new(p11, normal, color), new(p00, normal, color), new(p11, normal, color), new(p01, normal, color)]);
        }
    }
}
