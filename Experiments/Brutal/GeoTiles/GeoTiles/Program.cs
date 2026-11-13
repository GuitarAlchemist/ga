// The window of the 3D Tiles demo: HallSlice's Vulkan renderer (M2 of the BRUTAL evaluation), given a triangle soup
// instead of the hall. The camera orbits the place looked at; the arrow keys turn and tilt it, W and S move it in and
// out. It closes on Esc or after a time limit and prints its frame times. What it draws is loaded by Demo.cs.


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
using SN = System.Numerics;

return await Demo.Main(args);

[StructLayout(LayoutKind.Sequential)]
internal struct Vertex(SN.Vector3 pos, SN.Vector3 normal, SN.Vector3 color, SN.Vector2 uv = default)
{
    public SN.Vector3 Pos = pos;
    public SN.Vector3 Normal = normal;
    public SN.Vector3 Color = color;
    public SN.Vector2 Uv = uv;
}

internal static unsafe class TileView
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int FramesInFlight = 2;
    private const int ApiVersion13 = (1 << 22) | (3 << 12);


    private const string VertexShader = """
        #version 450
        layout(location = 0) in vec3 inPos;
        layout(location = 1) in vec3 inNormal;
        layout(location = 2) in vec3 inColor;
        layout(location = 3) in vec2 inUv;
        layout(push_constant) uniform Push { mat4 viewProj; } pc;
        layout(location = 0) out vec3 vNormal;
        layout(location = 1) out vec3 vColor;
        layout(location = 2) out vec2 vUv;
        void main()
        {
            gl_Position = pc.viewProj * vec4(inPos, 1.0);
            vNormal = inNormal;
            vColor = inColor;
            vUv = inUv;
        }
        """;

    // A vertex with a zero normal is textured and unlit: a photograph carries its own light.
    private const string FragmentShader = """
        #version 450
        layout(location = 0) in vec3 vNormal;
        layout(location = 1) in vec3 vColor;
        layout(location = 2) in vec2 vUv;
        layout(set = 0, binding = 0) uniform sampler2D image;
        layout(location = 0) out vec4 outColor;
        void main()
        {
            float light = 1.0;
            if (dot(vNormal, vNormal) > 0.25)
            {
                vec3 n = normalize(vNormal);
                float sun = max(dot(n, normalize(vec3(0.35, 0.85, 0.4))), 0.0);
                float sky = 0.5 + 0.5 * n.y;
                light = 0.15 + 0.25 * sky + 0.7 * sun;
            }

            outColor = vec4(texture(image, vUv).rgb * vColor * light, 1.0);
        }
        """;

    public static int Run(string[] args, Vertex[] vertices, IReadOnlyList<GeoTiles.Batch> batches, string title,
        string caption, float targetY, float startDistance)
    {
        var limit = TimeSpan.FromSeconds(args.Length > 0 && double.TryParse(args[0], out var s) ? s : 10.0);
        var vsync = args.Contains("--vsync");
        var capAt = Array.IndexOf(args, "--cap");
        var cap = capAt >= 0 && capAt + 1 < args.Length && int.TryParse(args[capAt + 1], out var capValue) ? capValue : 0;
        var clock = Stopwatch.StartNew();

        if (!Glfw.Init())
        {
            Console.Error.WriteLine("GeoTiles FAIL: Glfw.Init returned false");
            return 1;
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
        Glfw.WindowHint(GlfwWindowHint.Resizable, 0);
        // --fullscreen: the primary monitor, at the mode it is already in.
        var fullscreen = args.Contains("--fullscreen");
        var drone = args.Contains("--drone");
        const float droneHeight = 150f; // metres above the place: over Ottawa's towers
        var monitor = fullscreen ? Glfw.PrimaryMonitor : GlfwMonitor.Null;
        var size = !fullscreen ? new int2(Width, Height) : new int2(monitor.VideoMode.Width, monitor.VideoMode.Height);
        var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = title, Size = size, Monitor = monitor });

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
            Console.Error.WriteLine("GeoTiles FAIL: no window surface");
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
            Console.Error.WriteLine("GeoTiles FAIL: no GPU can draw and present to this window");
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
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
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
        var vertexModule = Shader(device, VertexShader, ShaderKind.VertexShader, "tiles.vert");
        var fragmentModule = Shader(device, FragmentShader, ShaderKind.FragmentShader, "tiles.frag");
        Console.WriteLine($"GeoTiles shaders compiled in {compileClock.Elapsed.TotalMilliseconds:F0} ms");

        var pushRange = new VkPushConstantRange { StageFlags = VkShaderStageFlags.VertexBit, Offset = ByteSize.Zero, Size = ByteSize.Of<SN.Matrix4x4>() };
        var samplerBinding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit,
        };
        var setLayout = device.CreateDescriptorSetLayout(
            new VkDescriptorSetLayoutCreateInfo { BindingCount = 1, Bindings = &samplerBinding }, null);
        var layout = device.CreatePipelineLayout([setLayout], [pushRange], null);
        var pipeline = Pipeline(device, layout, renderPass, vertexModule, fragmentModule);

        // Textures: one image per run of vertices that has one, and a white pixel for the runs drawn by colour.
        // Trilinear sampling over a full mip chain, so a distant photograph does not shimmer.
        var uploadPool = device.CreateCommandPool(new VkCommandPoolCreateInfo { QueueFamilyIndex = family }, null);
        var upload = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo { CommandPool = uploadPool, Level = VkCommandBufferLevel.Primary });
        var sampler = device.CreateSampler(new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.ClampToEdge,
            AddressModeV = VkSamplerAddressMode.ClampToEdge,
            AddressModeW = VkSamplerAddressMode.ClampToEdge,
            MaxLod = 16f,
        }, null);
        var poolSize = new VkDescriptorPoolSize { Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = batches.Count + 2 };
        var descriptorPool = device.CreateDescriptorPool(
            new VkDescriptorPoolCreateInfo { MaxSets = batches.Count + 2, PoolSizeCount = 1, PoolSizes = &poolSize }, null);
        var textures = new List<(VkImage Image, VkDeviceMemory Memory, VkImageView View)>();
        var stagings = new List<(VkBuffer Buffer, VkDeviceMemory Memory)>();
        var textureBytes = 0L;
        upload.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });

        VkDescriptorSet Texture(int width, int height, int channels, byte[] data)
        {
            // Every mip level, each a box filter of the one above, in one staging buffer.
            var levels = new List<(int W, int H, byte[] Rgba)>();
            var rgba = new byte[width * height * 4];
            for (var i = 0; i < width * height; i++)
            {
                rgba[i * 4] = data[i * channels];
                rgba[i * 4 + 1] = data[i * channels + 1];
                rgba[i * 4 + 2] = data[i * channels + 2];
                rgba[i * 4 + 3] = 255;
            }

            levels.Add((width, height, rgba));
            while (levels[^1].W > 1 || levels[^1].H > 1)
            {
                var (pw, ph, parent) = levels[^1];
                int w = Math.Max(pw / 2, 1), h = Math.Max(ph / 2, 1);
                var level = new byte[w * h * 4];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        int x0 = Math.Min(x * 2, pw - 1), x1 = Math.Min(x * 2 + 1, pw - 1);
                        int y0 = Math.Min(y * 2, ph - 1), y1 = Math.Min(y * 2 + 1, ph - 1);
                        for (var c = 0; c < 4; c++)
                        {
                            level[(y * w + x) * 4 + c] = (byte)((parent[(y0 * pw + x0) * 4 + c] + parent[(y0 * pw + x1) * 4 + c] +
                                                                parent[(y1 * pw + x0) * 4 + c] + parent[(y1 * pw + x1) * 4 + c] + 2) / 4);
                        }
                    }
                }

                levels.Add((w, h, level));
            }

            var total = levels.Sum(l => l.Rgba.Length);
            textureBytes += total;
            var stagingSize = new ByteSize((nuint)total);
            var staging = device.CreateBuffer(new VkBufferCreateInfo
            {
                Size = stagingSize,
                Usage = VkBufferUsageFlags.TransferSrcBit,
                SharingMode = VkSharingMode.Exclusive,
            }, null);
            var stagingMemory = device.AllocateMemory(physical, staging, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
            device.BindBufferMemory(staging, stagingMemory, memoryOffset: ByteSize.Zero);
            var to = new Span<byte>((byte*)device.MapMemory<byte>(new MapInfo(stagingMemory) { Region = new MapRegion { Size = stagingSize } }), total);
            var regions = new VkBufferImageCopy[levels.Count];
            var at = 0;
            for (var i = 0; i < levels.Count; i++)
            {
                levels[i].Rgba.CopyTo(to[at..]);
                regions[i] = new VkBufferImageCopy
                {
                    BufferOffset = new ByteSize64((ulong)at),
                    ImageSubresource = new VkImageSubresourceLayers { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = i, LayerCount = 1 },
                    ImageExtent = new VkExtent3D { Width = levels[i].W, Height = levels[i].H, Depth = 1 },
                };
                at += levels[i].Rgba.Length;
            }

            device.UnmapMemory(stagingMemory);
            stagings.Add((staging, stagingMemory));

            const VkFormat format = VkFormat.R8G8B8A8SRGB;
            var image = device.CreateImage(new VkImageCreateInfo
            {
                ImageType = VkImageType._2D,
                Format = format,
                Extent = new VkExtent3D { Width = width, Height = height, Depth = 1 },
                MipLevels = levels.Count,
                ArrayLayers = 1,
                Samples = VkSampleCountFlags._1Bit,
                Tiling = VkImageTiling.Optimal,
                Usage = VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
                SharingMode = VkSharingMode.Exclusive,
                InitialLayout = VkImageLayout.Undefined,
            }, null);
            var memory = device.AllocateMemory(physical, image, VkMemoryPropertyFlags.DeviceLocalBit, null);
            device.BindImageMemory(image, memory, ByteSize.Zero);
            var range = new VkImageSubresourceRange { AspectMask = VkImageAspectFlags.ColorBit, LevelCount = levels.Count, LayerCount = 1 };
            upload.PipelineBarrier(VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, [], [],
            [
                new VkImageMemoryBarrier
                {
                    DstAccessMask = VkAccessFlags.TransferWriteBit,
                    OldLayout = VkImageLayout.Undefined,
                    NewLayout = VkImageLayout.TransferDstOptimal,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = image,
                    SubresourceRange = range,
                },
            ]);
            upload.CopyBufferToImage(staging, image, VkImageLayout.TransferDstOptimal, regions);
            upload.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.FragmentShaderBit, VkDependencyFlags.None, [], [],
            [
                new VkImageMemoryBarrier
                {
                    SrcAccessMask = VkAccessFlags.TransferWriteBit,
                    DstAccessMask = VkAccessFlags.ShaderReadBit,
                    OldLayout = VkImageLayout.TransferDstOptimal,
                    NewLayout = VkImageLayout.ShaderReadOnlyOptimal,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = image,
                    SubresourceRange = range,
                },
            ]);
            var view = device.CreateImageView(new VkImageViewCreateInfo { Image = image, ViewType = VkImageViewType._2D, Format = format, SubresourceRange = range }, null);
            textures.Add((image, memory, view));

            VkDescriptorSet set;
            var oneLayout = setLayout;
            device.AllocateDescriptorSets(
                new VkDescriptorSetAllocateInfo { DescriptorPool = descriptorPool, DescriptorSetCount = 1, SetLayouts = &oneLayout },
                new ReadOnlySpan<VkDescriptorSet>(&set, 1));
            var imageInfo = new VkDescriptorImageInfo { Sampler = sampler, ImageView = view, ImageLayout = VkImageLayout.ShaderReadOnlyOptimal };
            device.UpdateDescriptorSets(
            [
                new VkWriteDescriptorSet
                {
                    DstSet = set,
                    DstBinding = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.CombinedImageSampler,
                    ImageInfo = &imageInfo,
                },
            ], []);
            return set;
        }

        var white = Texture(1, 1, 4, [255, 255, 255, 255]);

        // The attribution, inside the picture: a band at the bottom left, drawn last in clip space.
        var band = GeoTiles.Caption.Render(caption, Math.Max(2, (int)extent.Height / 360));
        var bandSet = Texture(band.Width, band.Height, band.Channels, band.Data);
        var bandFirst = vertices.Length;
        {
            float margin = 12f * extent.Height / 720f;
            float x0 = -1f + (2f * margin / extent.Width), x1 = x0 + (2f * band.Width / extent.Width);
            float y1 = 1f - (2f * margin / extent.Height), y0 = y1 - (2f * band.Height / extent.Height);
            Vertex Corner(float x, float y, float u, float v) => new(new SN.Vector3(x, y, 0f), default, SN.Vector3.One, new SN.Vector2(u, v));
            vertices =
            [
                .. vertices,
                Corner(x0, y0, 0f, 0f), Corner(x1, y0, 1f, 0f), Corner(x1, y1, 1f, 1f),
                Corner(x0, y0, 0f, 0f), Corner(x1, y1, 1f, 1f), Corner(x0, y1, 0f, 1f),
            ];
        }
        var sets = new VkDescriptorSet[batches.Count];
        for (var i = 0; i < batches.Count; i++)
        {
            sets[i] = batches[i].Image is { } pixels ? Texture(pixels.Width, pixels.Height, pixels.Channels, pixels.Data) : white;
        }

        upload.End();
        var uploadHandle = upload.Handle;
        queue.Submit([new VkSubmitInfo { CommandBufferCount = 1, CommandBuffers = &uploadHandle }], default);
        device.WaitIdle();
        foreach (var (buffer, memory) in stagings)
        {
            device.DestroyBuffer(buffer, null);
            device.FreeMemory(memory, null);
        }

        Console.WriteLine($"GeoTiles textures: {textures.Count - 2} images with their mip levels, {textureBytes / (1024.0 * 1024.0):F0} MB");

        // The tiles, in a host-visible vertex buffer.
        var bufferSize = new ByteSize((nuint)(vertices.Length * sizeof(Vertex)));
        var vertexBuffer = device.CreateBuffer(new VkBufferCreateInfo
        {
            Size = bufferSize,
            Usage = VkBufferUsageFlags.VertexBufferBit,
            SharingMode = VkSharingMode.Exclusive,
        }, null);
        var vertexMemory = device.AllocateMemory(physical, vertexBuffer, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit, null);
        device.BindBufferMemory(vertexBuffer, vertexMemory, memoryOffset: ByteSize.Zero);
        var mapped = device.MapMemory<Vertex>(new MapInfo(vertexMemory) { Region = new MapRegion { Size = bufferSize } });
        vertices.AsSpan().CopyTo(new Span<Vertex>((Vertex*)mapped, vertices.Length));
        device.UnmapMemory(vertexMemory);
        Console.WriteLine($"GeoTiles geometry: {vertices.Length / 3} triangles in a {vertices.Length * (long)sizeof(Vertex) / (1024.0 * 1024.0):F1} MB vertex buffer");

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
        float yaw = 0.6f, pitch = 0.45f, distance = startDistance;
        // Taking the controls, as in the Observatory's fly camera: a movement key or a click hands the camera over;
        // the mouse looks, W A S D (or Z Q S D, or the arrows) move, E or Space climbs, C sinks, Shift is three
        // times faster, the wheel changes the speed, Tab gives the camera back.
        const int CursorNormal = 0x00034001, CursorHidden = 0x00034002, CursorDisabled = 0x00034003;
        bool manual = false, takeOver = false;
        if (fullscreen)
        {
            window.SetInputMode(GlfwInputMode.Cursor, CursorHidden); // no pointer over the picture
        }

        var flyPos = SN.Vector3.Zero;
        float flyYaw = 0f, flyPitch = 0f, flySpeed = 40f;
        double lastX = 0, lastY = 0;
        window.OnScroll += (w, offset) => flySpeed = Math.Clamp(flySpeed * (offset.Y > 0 ? 1.2f : 1f / 1.2f), 2f, 400f);
        var keys = 0;
        var reason = "window closed";
        window.OnKey += (w, key, scancode, action, mods) =>
        {
            if (action == GlfwKeyAction.Release)
            {
                return;
            }

            if (key == GlfwKey.Tab)
            {
                // Back to the camera that moves by itself.
                manual = false;
                w.SetInputMode(GlfwInputMode.Cursor, fullscreen ? CursorHidden : CursorNormal);
                return;
            }

            if (!manual && key is GlfwKey.W or GlfwKey.A or GlfwKey.S or GlfwKey.D or GlfwKey.Z or GlfwKey.Q or GlfwKey.E
                    or GlfwKey.C or GlfwKey.Space)
            {
                takeOver = true; // the loop hands the camera over from where it is
                return;
            }

            if (manual && key != GlfwKey.Escape)
            {
                return; // held keys are read every frame
            }

            switch (key)
            {
                case GlfwKey.Left: yaw -= 0.15f; break;
                case GlfwKey.Right: yaw += 0.15f; break;
                case GlfwKey.Up: pitch = Math.Min(pitch + 0.08f, 1.3f); break;
                case GlfwKey.Down: pitch = Math.Max(pitch - 0.08f, 0.05f); break;
                case GlfwKey.Escape:
                    reason = "Esc";
                    w.ShouldClose = true;
                    break;
                default:
                    // Pause is left out: this host's focused window receives Pause presses nobody typed.
                    if (key != GlfwKey.Pause)
                    {
                        Console.WriteLine($"GeoTiles key {key}: no camera action, at {clock.Elapsed.TotalSeconds:F2} s");
                    }

                    return;
            }

            keys++;
            Console.WriteLine($"GeoTiles key {key}: yaw {yaw:F2} pitch {pitch:F2} distance {distance:F1} at {clock.Elapsed.TotalSeconds:F2} s");
        };

        Console.WriteLine($"GeoTiles present mode {presentMode}, {images.Length} swapchain images, {extent.Width}x{extent.Height}");
        if (cap > 0)
        {
            Console.WriteLine($"GeoTiles frame cap {cap} fps");
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

            var target = new SN.Vector3(0f, targetY, 0f);
            var eye = target + (distance * new SN.Vector3(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw)));
            if (drone)
            {
                // A drone's ride: an oval above the place, rising and sinking, looking ahead along its path and a
                // little toward the place. W and S change its height.
                var reach = startDistance / 1.6f;
                SN.Vector3 At(float u) => new(reach * 0.85f * MathF.Cos(u), 0f, reach * 0.55f * MathF.Sin(u));
                var along = (float)now * 0.11f;
                var height = droneHeight * distance / startDistance;
                eye = At(along) + new SN.Vector3(0f, height + (0.2f * height * MathF.Sin(along * 1.7f)), 0f);
                target = SN.Vector3.Lerp(At(along + 0.7f) + new SN.Vector3(0f, height * 0.25f, 0f), new SN.Vector3(0f, targetY, 0f), 0.3f);
            }

            bool Held(GlfwKey key) => window.GetKey(key) != GlfwKeyAction.Release;
            if (!manual && (takeOver || window.GetMouseButton(GlfwMouseButton.Left) != GlfwButtonAction.Release))
            {
                manual = true;
                flyPos = eye;
                var look = SN.Vector3.Normalize(target - eye);
                flyYaw = MathF.Atan2(look.Z, look.X);
                flyPitch = MathF.Asin(Math.Clamp(look.Y, -1f, 1f));
                window.SetInputMode(GlfwInputMode.Cursor, CursorDisabled);
                (lastX, lastY) = (window.CursorX, window.CursorY);
                Console.WriteLine($"GeoTiles controls taken at {now:F2} s");
            }

            takeOver = false;
            if (manual)
            {
                double cursorX = window.CursorX, cursorY = window.CursorY;
                flyYaw += (float)(cursorX - lastX) * 0.0025f;
                flyPitch = Math.Clamp(flyPitch - ((float)(cursorY - lastY) * 0.0025f), -1.5f, 1.5f);
                (lastX, lastY) = (cursorX, cursorY);
                var forward = new SN.Vector3(MathF.Cos(flyPitch) * MathF.Cos(flyYaw), MathF.Sin(flyPitch), MathF.Cos(flyPitch) * MathF.Sin(flyYaw));
                var right = SN.Vector3.Normalize(SN.Vector3.Cross(forward, SN.Vector3.UnitY));
                var move = SN.Vector3.Zero;
                if (Held(GlfwKey.W) || Held(GlfwKey.Z) || Held(GlfwKey.Up)) { move += forward; }
                if (Held(GlfwKey.S) || Held(GlfwKey.Down)) { move -= forward; }
                if (Held(GlfwKey.A) || Held(GlfwKey.Q) || Held(GlfwKey.Left)) { move -= right; }
                if (Held(GlfwKey.D) || Held(GlfwKey.Right)) { move += right; }
                if (Held(GlfwKey.E) || Held(GlfwKey.Space)) { move += SN.Vector3.UnitY; }
                if (Held(GlfwKey.C)) { move -= SN.Vector3.UnitY; }
                if (move.LengthSquared() > 0f)
                {
                    flyPos += SN.Vector3.Normalize(move) * flySpeed * (Held(GlfwKey.LeftShift) ? 3f : 1f) * dt;
                }

                eye = flyPos;
                target = flyPos + forward;
            }
            var projection = SN.Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, extent.Width / (float)extent.Height, 2f, 60000f);
            projection.M22 = -projection.M22; // Vulkan's clip space has y pointing down
            var viewProj = SN.Matrix4x4.CreateLookAt(eye, target, SN.Vector3.UnitY) * projection;

            var cb = commands[slot];
            cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
            clears[0] = new VkClearValue { Color = new VkClearColorValue { Float32 = new float4(0.45f, 0.62f, 0.82f, 1f) } };
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
            cb.BindVertexBuffer(0, vertexBuffer);
            for (var i = 0; i < batches.Count; i++)
            {
                cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, layout, 0, [sets[i]], []);
                cb.Draw(batches[i].Count, 1, batches[i].First, 0);
            }
            var identity = SN.Matrix4x4.Identity;
            cb.PushConstants(layout, VkShaderStageFlags.VertexBit, offset: 0, new ReadOnlySpan<byte>(&identity, sizeof(SN.Matrix4x4)));
            cb.BindDescriptorSets(VkPipelineBindPoint.Graphics, layout, 0, [bandSet], []);
            cb.Draw(6, 1, bandFirst, 0);
            cb.EndRenderPass();
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
                Console.WriteLine($"GeoTiles first frame presented after {t * 1000:F0} ms");
            }
            else
            {
                frameTimes.Add((t - lastPresent) * 1000.0); // present to present
            }

            lastPresent = t;
            frame++;
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

        // Clean shutdown, in reverse order of creation.
        device.WaitIdle();
        for (var i = 0; i < FramesInFlight; i++)
        {
            device.DestroySemaphore(acquired[i], null);
            device.DestroyFence(fences[i], null);
            device.DestroyCommandPool(pools[i], null);
        }

        foreach (var (image, memory, view) in textures)
        {
            device.DestroyImageView(view, null);
            device.DestroyImage(image, null);
            device.FreeMemory(memory, null);
        }

        device.DestroyDescriptorPool(descriptorPool, null);
        device.DestroySampler(sampler, null);
        device.DestroyDescriptorSetLayout(setLayout, null);
        device.DestroyCommandPool(uploadPool, null);
        device.DestroyBuffer(vertexBuffer, null);
        device.FreeMemory(vertexMemory, null);
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
        Console.WriteLine($"GeoTiles frames {frame} in {loopSeconds:F2} s ({frame / loopSeconds:F0} fps); frame ms p50 {P(0.5):F3} p95 {P(0.95):F3} p99 {P(0.99):F3} max {(frameTimes.Count > 0 ? frameTimes[^1] : double.NaN):F3}");
        Console.WriteLine($"GeoTiles CPU {cpu.TotalSeconds:F2} s over {loopSeconds:F2} s ({100 * cpu.TotalSeconds / loopSeconds / Environment.ProcessorCount:F1} % of {Environment.ProcessorCount} logical CPUs), peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine($"GeoTiles closed by {reason} after {clock.Elapsed.TotalSeconds:F2} s, {keys} camera keys");
        return 0;
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint milliseconds);

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
                new() { Location = 3, Binding = 0, Format = VkFormat.R32G32SFloat, Offset = ByteSize.Of<float>(9) },
            };
            var vertexInput = new VkPipelineVertexInputStateCreateInfo
            {
                VertexBindingDescriptionCount = 1,
                VertexBindingDescriptions = &binding,
                VertexAttributeDescriptionCount = 4,
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

}
