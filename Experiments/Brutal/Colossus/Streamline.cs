// NVIDIA DLSS for the colossus (--dlss), through NVIDIA's Streamline 2.14.1, loaded at run time. Nothing of NVIDIA's is
// in this repository: the DLLs come from a folder on the user's machine (--sl-path; by default the one the Observatory's
// RTX build fetches from NVIDIA's own release), and each one loads only once Windows has verified its Authenticode
// signature and found NVIDIA Corporation's name on it. Streamline runs in its manual hooking mode: the renderer keeps its
// own Vulkan calls, tells Streamline its instance, device and queue, tags its images each frame, and presents through
// Streamline's proxy, which its common plugin needs once a frame. Over-the-air updates and downloaded plugins stay off.
// When anything is missing or fails, Colossus says why and renders with 4x MSAA, as without --dlss.
// Ray Reconstruction (--rr) is DLSS's denoiser for ray-traced light: its plugin and NGX's model load only when asked for,
// under the same checks, and when they fail DLSS goes on without it.
// Frame generation (--fg) is DLSS-G: it draws frames between the renderer's own. It needs Reflex and its latency markers,
// and its own swapchain: the window's surface, the swapchain, its images, acquiring, presenting and waiting for the
// device all go through Streamline's proxies. Its plugins load only when asked for, under the same checks.
// The structures mirror Streamline's headers (sl_core_types.h, sl_consts.h, sl_dlss.h, sl_dlss_d.h, sl_dlss_g.h,
// sl_reflex.h, sl_pcl.h, sl_helpers_vk.h)
// field by field; Load compares their sizes with the ones MSVC gives the headers, and stops if one differs.
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using SN = System.Numerics;

internal sealed unsafe class Dlss
{
    public const string DefaultFolder = @"C:\tmp\godot-sl214\bin";
    private const uint FeatureDlss = 0, FeatureRay = 1001, FeatureFrames = 1000, FeatureReflex = 3, FeaturePcl = 4;
    // Streamline 2.14.1 and its magic number, as sl_version.h builds kSDKVersion.
    private const ulong SdkVersion = (2ul << 48) | (14ul << 32) | (1ul << 16) | 0xfedc;
    private const uint BufferDepth = 0, BufferMotion = 1, BufferHudLess = 2, BufferInput = 3, BufferOutput = 4;
    private const uint BufferAlbedo = 7, BufferSpecularAlbedo = 8, BufferNormalRoughness = 14, BufferSpecularHitDistance = 42;
    private const int ValidUntilPresent = 1;
    // Streamline's files that this loads, each checked first: the interposer, the common plugin, DLSS's plugin, NGX's DLSS.
    private static readonly string[] Files = ["sl.interposer.dll", "sl.common.dll", "sl.dlss.dll", "nvngx_dlss.dll"];
    // And with --rr: Ray Reconstruction's plugin and NGX's model.
    private static readonly string[] RayFiles = ["sl.dlss_d.dll", "nvngx_dlssd.dll"];
    // And with --fg: frame generation's plugin and NGX's model, Reflex's plugin and its Vulkan driver, the latency markers.
    private static readonly string[] FrameFiles = ["sl.dlss_g.dll", "nvngx_dlssg.dll", "sl.reflex.dll", "NvLowLatencyVk.dll", "sl.pcl.dll"];

    // PCLMarker: where a frame is, for Reflex and frame generation.
    public const uint SimulationStart = 0, SimulationEnd = 1, RenderSubmitStart = 2, RenderSubmitEnd = 3, PresentStart = 4, PresentEnd = 5;
    private static bool _verbose;

    // DLSS's settings by name, as Streamline numbers them (DLSSMode).
    public static readonly Dictionary<string, uint> Modes = new()
    {
        ["dlaa"] = 6,
        ["quality"] = 3,
        ["balanced"] = 2,
        ["performance"] = 1,
        ["ultra"] = 4,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Base
    {
        public void* Next;
        public Guid Type;
        public nuint Version;

        public static Base Of(Guid type, int version) => new() { Type = type, Version = (nuint)version };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Preferences
    {
        public Base Base;
        public byte ShowConsole;
        public uint LogLevel;
        public char** PathsToPlugins;
        public uint NumPathsToPlugins;
        public char* PathToLogsAndData;
        public void* AllocateCallback;
        public void* ReleaseCallback;
        public delegate* unmanaged<uint, byte*, void> LogMessageCallback;
        public ulong Flags;
        public uint* FeaturesToLoad;
        public uint NumFeaturesToLoad;
        public uint ApplicationId;
        public uint Engine;
        public byte* EngineVersion;
        public byte* ProjectId;
        public uint RenderApi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FeatureRequirements
    {
        public Base Base;
        public uint Flags;
        public uint MaxNumCpuThreads;
        public uint MaxNumViewports;
        public uint NumRequiredTags;
        public uint* RequiredTags;
        public fixed uint Versions[12];
        public uint VkNumComputeQueuesRequired;
        public uint VkNumGraphicsQueuesRequired;
        public uint VkNumDeviceExtensions;
        public byte** VkDeviceExtensions;
        public uint VkNumInstanceExtensions;
        public byte** VkInstanceExtensions;
        public uint VkNumFeatures12;
        public byte** VkFeatures12;
        public uint VkNumFeatures13;
        public byte** VkFeatures13;
        public uint VkNumOpticalFlowQueuesRequired;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Resource
    {
        public Base Base;
        public byte Type;
        public void* Native;
        public void* Memory;
        public void* View;
        public uint State;
        public uint Width;
        public uint Height;
        public uint NativeFormat;
        public uint MipLevels;
        public uint ArrayLayers;
        public ulong GpuVirtualAddress;
        public uint Flags;
        public uint Usage;
        public ushort InternalFlags;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResourceTag
    {
        public Base Base;
        public Resource* Resource;
        public uint Type;
        public int Lifecycle;
        public uint Top;
        public uint Left;
        public uint Width;
        public uint Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewportHandle
    {
        public Base Base;
        public uint Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public Base Base;
        public byte* DeviceLuid;
        public uint DeviceLuidSize;
        public void* VkPhysicalDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Constants
    {
        public Base Base;
        public SN.Matrix4x4 CameraViewToClip;
        public SN.Matrix4x4 ClipToCameraView;
        public SN.Matrix4x4 ClipToLensClip;
        public SN.Matrix4x4 ClipToPrevClip;
        public SN.Matrix4x4 PrevClipToClip;
        public SN.Vector2 JitterOffset;
        public SN.Vector2 MvecScale;
        public SN.Vector2 CameraPinholeOffset;
        public SN.Vector3 CameraPos;
        public SN.Vector3 CameraUp;
        public SN.Vector3 CameraRight;
        public SN.Vector3 CameraFwd;
        public float CameraNear;
        public float CameraFar;
        public float CameraFov;
        public float CameraAspectRatio;
        public float MotionVectorsInvalidValue;
        public byte DepthInverted;
        public byte CameraMotionIncluded;
        public byte MotionVectors3D;
        public byte Reset;
        public byte OrthographicProjection;
        public byte MotionVectorsDilated;
        public byte MotionVectorsJittered;
        public float MinRelativeLinearDepthObjectSeparation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VulkanInfo
    {
        public Base Base;
        public nint Device;
        public nint Instance;
        public nint PhysicalDevice;
        public uint ComputeQueueIndex;
        public uint ComputeQueueFamily;
        public uint GraphicsQueueIndex;
        public uint GraphicsQueueFamily;
        public uint OpticalFlowQueueIndex;
        public uint OpticalFlowQueueFamily;
        public byte UseNativeOpticalFlowMode;
        public uint ComputeQueueCreateFlags;
        public uint GraphicsQueueCreateFlags;
        public uint OpticalFlowQueueCreateFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Options
    {
        public Base Base;
        public uint Mode;
        public uint OutputWidth;
        public uint OutputHeight;
        public float Sharpness;
        public float PreExposure;
        public float ExposureScale;
        public byte ColorBuffersHdr;
        public byte IndicatorInvertAxisX;
        public byte IndicatorInvertAxisY;
        public fixed uint Presets[6];
        public byte UseAutoExposure;
        public byte AlphaUpscalingEnabled;
    }

    // DLSSDOptions: Ray Reconstruction's, with the camera's view matrix and its inverse.
    [StructLayout(LayoutKind.Sequential)]
    private struct RayOptions
    {
        public Base Base;
        public uint Mode;
        public uint OutputWidth;
        public uint OutputHeight;
        public float Sharpness;
        public float PreExposure;
        public float ExposureScale;
        public byte ColorBuffersHdr;
        public byte IndicatorInvertAxisX;
        public byte IndicatorInvertAxisY;
        public uint NormalRoughnessMode;
        public SN.Matrix4x4 WorldToCameraView;
        public SN.Matrix4x4 CameraViewToWorld;
        public byte AlphaUpscalingEnabled;
        public fixed uint Presets[6];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OptimalSettings
    {
        public Base Base;
        public uint OptimalRenderWidth;
        public uint OptimalRenderHeight;
        public float OptimalSharpness;
        public uint RenderWidthMin;
        public uint RenderHeightMin;
        public uint RenderWidthMax;
        public uint RenderHeightMax;
    }

    // DLSSGOptions: on or off, how many frames to draw between two of the renderer's.
    [StructLayout(LayoutKind.Sequential)]
    private struct FrameOptions
    {
        public Base Base;
        public uint Mode;
        public uint NumFramesToGenerate;
        public uint Flags;
        public uint DynamicResWidth;
        public uint DynamicResHeight;
        public uint NumBackBuffers;
        public uint MvecDepthWidth;
        public uint MvecDepthHeight;
        public uint ColorWidth;
        public uint ColorHeight;
        public uint ColorBufferFormat;
        public uint MvecBufferFormat;
        public uint DepthBufferFormat;
        public uint HudLessBufferFormat;
        public uint UiBufferFormat;
        public void* OnErrorCallback;
        public byte Reserved15;
        public uint QueueParallelismMode;
        public byte EnableUserInterfaceRecomposition;
        public float DynamicTargetFrameRate;
    }

    // DLSSGState: what frame generation did at the last present.
    [StructLayout(LayoutKind.Sequential)]
    private struct FrameState
    {
        public Base Base;
        public ulong EstimatedVramUsageInBytes;
        public uint Status;
        public uint MinWidthOrHeight;
        public uint NumFramesActuallyPresented;
        public uint NumFramesToGenerateMax;
        public byte Reserved4;
        public byte IsVsyncSupportAvailable;
        public void* InputsProcessingCompletionFence;
        public ulong LastPresentInputsProcessingCompletionFenceValue;
        public byte IsDynamicMfgSupported;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReflexOptions
    {
        public Base Base;
        public uint Mode;
        public uint FrameLimitUs;
        public byte UseMarkersToOptimize;
        public ushort VirtualKey;
        public uint IdThread;
    }

    // Vulkan's own VkSwapchainCreateInfoKHR and VkWin32SurfaceCreateInfoKHR, for Streamline's proxies.
    [StructLayout(LayoutKind.Sequential)]
    private struct SwapchainInfo
    {
        public int SType;
        public void* Next;
        public uint Flags;
        public ulong Surface;
        public uint MinImageCount;
        public int ImageFormat;
        public int ImageColorSpace;
        public uint Width;
        public uint Height;
        public uint ImageArrayLayers;
        public uint ImageUsage;
        public int ImageSharingMode;
        public uint QueueFamilyIndexCount;
        public uint* QueueFamilyIndices;
        public int PreTransform;
        public int CompositeAlpha;
        public int PresentMode;
        public uint Clipped;
        public ulong OldSwapchain;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SurfaceInfo
    {
        public int SType;
        public void* Next;
        public uint Flags;
        public nint Instance;
        public nint Window;
    }

    // Vulkan's own VkPresentInfoKHR, for Streamline's present proxy.
    [StructLayout(LayoutKind.Sequential)]
    private struct PresentInfo
    {
        public int SType;
        public void* Next;
        public uint WaitSemaphoreCount;
        public ulong* WaitSemaphores;
        public uint SwapchainCount;
        public ulong* Swapchains;
        public uint* ImageIndices;
        public int* Results;
    }

    // An image DLSS reads or writes: its handles, size, format, usage and the layout it is in when DLSS runs.
    public readonly record struct Image(ulong Handle, ulong View, ulong Memory, int Width, int Height, uint Format, uint Usage, uint Layout);

    // What Ray Reconstruction reads besides DLSS's images, at the render size: the surfaces' diffuse and specular
    // reflectance, their normal in world space with the roughness in alpha, and how far each reflection ray went.
    public readonly record struct Guides(Image Diffuse, Image Specular, Image NormalRoughness, Image HitDistance);

    private static readonly Guid PreferencesType = new(0x1ca10965, 0xbf8e, 0x432b, 0x8d, 0xa1, 0x67, 0x16, 0xd8, 0x79, 0xfb, 0x14);
    private static readonly Guid FeatureRequirementsType = new(0x66714097, 0xac6d, 0x4bc6, 0x89, 0x15, 0x1e, 0x0f, 0x55, 0xa6, 0xb6, 0x1f);
    private static readonly Guid ResourceType = new(0x3a9d70cf, 0x2418, 0x4b72, 0x83, 0x91, 0x13, 0xf8, 0x72, 0x1c, 0x72, 0x61);
    private static readonly Guid ResourceTagType = new(0x4c6a5aad, 0xb445, 0x496c, 0x87, 0xff, 0x1a, 0xf3, 0x84, 0x5b, 0xe6, 0x53);
    private static readonly Guid ViewportHandleType = new(0x171b6435, 0x9b3c, 0x4fc8, 0x99, 0x94, 0xfb, 0xe5, 0x25, 0x69, 0xaa, 0xa4);
    private static readonly Guid AdapterInfoType = new(0x0677315f, 0xa746, 0x4492, 0x9f, 0x42, 0xcb, 0x61, 0x42, 0xc9, 0xc3, 0xd4);
    private static readonly Guid ConstantsType = new(0xdcd35ad7, 0x4e4a, 0x4bad, 0xa9, 0x0c, 0xe0, 0xc4, 0x9e, 0xb2, 0x3a, 0xfe);
    private static readonly Guid VulkanInfoType = new(0x0eed6fd5, 0x82cd, 0x43a9, 0xbd, 0xb5, 0x47, 0xa5, 0xba, 0x2f, 0x45, 0xd6);
    private static readonly Guid OptionsType = new(0x6ac826e4, 0x4c61, 0x4101, 0xa9, 0x2d, 0x63, 0x8d, 0x42, 0x10, 0x57, 0xb8);
    private static readonly Guid OptimalSettingsType = new(0xef1d0957, 0xfd58, 0x4df7, 0xb5, 0x04, 0x8b, 0x69, 0xd8, 0xaa, 0x6b, 0x76);
    private static readonly Guid RayOptionsType = new(0x0ad87504, 0x774e, 0x4bf3, 0x96, 0x33, 0xa4, 0x4d, 0x1f, 0x7f, 0x9c, 0xb8);
    private static readonly Guid FrameOptionsType = new(0xfac5f1cb, 0x2dfd, 0x4f36, 0xa1, 0xe6, 0x3a, 0x9e, 0x86, 0x52, 0x56, 0xc5);
    private static readonly Guid FrameStateType = new(0xcc8ac8e1, 0xa179, 0x44f5, 0x97, 0xfa, 0xe7, 0x41, 0x12, 0xf9, 0xbc, 0x61);
    private static readonly Guid ReflexOptionsType = new(0xf03af81a, 0x6d0b, 0x4902, 0xa6, 0x51, 0xc4, 0x96, 0x5e, 0x21, 0x54, 0x34);

    private readonly delegate* unmanaged<Preferences*, ulong, int> _init;
    private readonly delegate* unmanaged<int> _shutdown;
    private readonly delegate* unmanaged<uint, AdapterInfo*, int> _isFeatureSupported;
    private readonly delegate* unmanaged<nint, ViewportHandle*, ResourceTag*, uint, nint, int> _setTagForFrame;
    private readonly delegate* unmanaged<Constants*, nint, ViewportHandle*, int> _setConstants;
    private readonly delegate* unmanaged<uint, FeatureRequirements*, int> _getFeatureRequirements;
    private readonly delegate* unmanaged<uint, nint, Base**, uint, nint, int> _evaluateFeature;
    private readonly delegate* unmanaged<uint, byte*, void**, int> _getFeatureFunction;
    private readonly delegate* unmanaged<nint*, uint*, int> _getNewFrameToken;
    private readonly delegate* unmanaged<VulkanInfo*, int> _setVulkanInfo;
    private readonly delegate* unmanaged<nint, byte*, void*> _getDeviceProcAddr;
    private delegate* unmanaged<Options*, OptimalSettings*, int> _getOptimalSettings;
    private delegate* unmanaged<ViewportHandle*, Options*, int> _setOptions;
    private delegate* unmanaged<ViewportHandle*, RayOptions*, int> _setRayOptions;
    private delegate* unmanaged<nint, PresentInfo*, int> _present;
    private readonly delegate* unmanaged<nint, byte*, void*> _getInstanceProcAddr;
    private delegate* unmanaged<ViewportHandle*, FrameOptions*, int> _setFrameOptions;
    private delegate* unmanaged<ViewportHandle*, FrameState*, FrameOptions*, int> _getFrameState;
    private delegate* unmanaged<ReflexOptions*, int> _setReflexOptions;
    private delegate* unmanaged<nint, int> _reflexSleep;
    private delegate* unmanaged<uint, nint, int> _setMarker;
    private delegate* unmanaged<nint, SwapchainInfo*, void*, ulong*, int> _createSwapchain;
    private delegate* unmanaged<nint, ulong, void*, void> _destroySwapchain;
    private delegate* unmanaged<nint, ulong, uint*, ulong*, int> _getSwapchainImages;
    private delegate* unmanaged<nint, ulong, ulong, ulong, ulong, uint*, int> _acquire;
    private delegate* unmanaged<nint, int> _waitIdle;
    private nint _token;
    private uint _tokenFrame = uint.MaxValue;
    private uint _framesToGenerate;
    private bool _frameOptionsSet;
    private bool _initialised;
    private string? _failed;

    // Whether Ray Reconstruction loaded and runs on this GPU, and if not, why (empty when it was not asked for).
    public bool RayReconstruction { get; private set; }
    public string RayNote { get; private set; } = "";

    // What DLSS asks of the Vulkan instance and device, from slGetFeatureRequirements.
    public string[] InstanceExtensions { get; private set; } = [];
    public string[] DeviceExtensions { get; private set; } = [];
    public string[] Features12 { get; private set; } = [];
    public string[] Features13 { get; private set; } = [];
    public uint GraphicsQueues { get; private set; }
    public uint ComputeQueues { get; private set; }

    // Whether frame generation loaded and runs here, and if not, why (empty when it was not asked for).
    public bool FrameGeneration { get; private set; }
    public string FrameNote { get; private set; } = "";

    private Dlss(nint module)
    {
        void* Export(string name) => NativeLibrary.TryGetExport(module, name, out var p) ? (void*)p : throw new EntryPointNotFoundException(name);
        _init = (delegate* unmanaged<Preferences*, ulong, int>)Export("slInit");
        _shutdown = (delegate* unmanaged<int>)Export("slShutdown");
        _isFeatureSupported = (delegate* unmanaged<uint, AdapterInfo*, int>)Export("slIsFeatureSupported");
        _setTagForFrame = (delegate* unmanaged<nint, ViewportHandle*, ResourceTag*, uint, nint, int>)Export("slSetTagForFrame");
        _setConstants = (delegate* unmanaged<Constants*, nint, ViewportHandle*, int>)Export("slSetConstants");
        _getFeatureRequirements = (delegate* unmanaged<uint, FeatureRequirements*, int>)Export("slGetFeatureRequirements");
        _evaluateFeature = (delegate* unmanaged<uint, nint, Base**, uint, nint, int>)Export("slEvaluateFeature");
        _getFeatureFunction = (delegate* unmanaged<uint, byte*, void**, int>)Export("slGetFeatureFunction");
        _getNewFrameToken = (delegate* unmanaged<nint*, uint*, int>)Export("slGetNewFrameToken");
        _setVulkanInfo = (delegate* unmanaged<VulkanInfo*, int>)Export("slSetVulkanInfo");
        _getDeviceProcAddr = (delegate* unmanaged<nint, byte*, void*>)Export("vkGetDeviceProcAddr");
        _getInstanceProcAddr = (delegate* unmanaged<nint, byte*, void*>)Export("vkGetInstanceProcAddr");
    }

    // Checks and loads Streamline from the folder, starts it with DLSS (and Ray Reconstruction when asked for and its files
    // check out), and reads what they ask of Vulkan. Null, with the reason, when DLSS fails at any step.
    public static Dlss? Load(string folder, bool verbose, bool rayReconstruction, int frameGeneration, out string why)
    {
        _verbose = verbose;
        if (!OperatingSystem.IsWindows())
        {
            why = "Streamline runs on Windows only";
            return null;
        }

        // The sizes MSVC gives Streamline 2.14.1's headers for these structures.
        (string Name, int Size, int Expected)[] sizes =
        [
            ("Preferences", sizeof(Preferences), 144), ("FeatureRequirements", sizeof(FeatureRequirements), 184),
            ("Resource", sizeof(Resource), 112), ("ResourceTag", sizeof(ResourceTag), 64), ("ViewportHandle", sizeof(ViewportHandle), 40),
            ("AdapterInfo", sizeof(AdapterInfo), 56), ("Constants", sizeof(Constants), 456), ("VulkanInfo", sizeof(VulkanInfo), 96),
            ("DLSSOptions", sizeof(Options), 88), ("DLSSOptimalSettings", sizeof(OptimalSettings), 64), ("VkPresentInfoKHR", sizeof(PresentInfo), 64),
            ("DLSSDOptions", sizeof(RayOptions), 224), ("DLSSGOptions", sizeof(FrameOptions), 120), ("DLSSGState", sizeof(FrameState), 88),
            ("ReflexOptions", sizeof(ReflexOptions), 48), ("VkSwapchainCreateInfoKHR", sizeof(SwapchainInfo), 104), ("VkWin32SurfaceCreateInfoKHR", sizeof(SurfaceInfo), 40),
        ];
        foreach (var (name, size, expected) in sizes)
        {
            if (size != expected)
            {
                why = $"{name} is {size} bytes here, {expected} in Streamline's header";
                return null;
            }
        }

        foreach (var file in Files)
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                why = $"{path} is missing";
                return null;
            }

            if (!SignedByNvidia(path, out why))
            {
                return null;
            }
        }

        var rayNote = "";
        foreach (var file in rayReconstruction ? RayFiles : [])
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                rayNote = $"{path} is missing";
                break;
            }

            if (!SignedByNvidia(path, out rayNote))
            {
                break;
            }
        }

        rayReconstruction &= rayNote.Length == 0;
        var frameNote = "";
        foreach (var file in frameGeneration > 1 ? FrameFiles : [])
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                frameNote = $"{path} is missing";
                break;
            }

            if (!SignedByNvidia(path, out frameNote))
            {
                break;
            }
        }

        var frames = frameGeneration > 1 && frameNote.Length == 0;
        Dlss dlss;
        try
        {
            dlss = new Dlss(NativeLibrary.Load(Path.Combine(folder, "sl.interposer.dll")));
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            why = $"sl.interposer.dll: {e.Message}";
            return null;
        }

        // Kept for the process's life: Streamline may hold on to these.
        var pluginPath = (char*)Marshal.StringToHGlobalUni(folder);
        var paths = (char**)NativeMemory.Alloc((nuint)sizeof(char*));
        paths[0] = pluginPath;
        var features = (uint*)NativeMemory.Alloc(5 * sizeof(uint));
        var featureCount = 0u;
        features[featureCount++] = FeatureDlss;
        if (rayReconstruction)
        {
            features[featureCount++] = FeatureRay;
        }

        if (frames)
        {
            features[featureCount++] = FeatureFrames;
            features[featureCount++] = FeatureReflex;
            features[featureCount++] = FeaturePcl;
        }
        var preferences = new Preferences
        {
            Base = Base.Of(PreferencesType, 1),
            LogLevel = verbose ? 2u : 1u,
            PathsToPlugins = paths,
            NumPathsToPlugins = 1,
            LogMessageCallback = &Log,
            // eDisableCLStateTracking | eUseManualHooking | eUseFrameBasedResourceTagging; no eAllowOTA, no
            // eLoadDownloadedPlugins.
            Flags = 1ul | 4ul | 128ul,
            FeaturesToLoad = features,
            NumFeaturesToLoad = featureCount,
            Engine = 0,
            EngineVersion = (byte*)Marshal.StringToHGlobalAnsi("Colossus"),
            // NGX's identifier for an engine of its own: a random UUID made for Colossus.
            ProjectId = (byte*)Marshal.StringToHGlobalAnsi("d06320dd-22f7-4ae8-8138-667b8ad377f9"),
            RenderApi = 2,
        };
        var result = dlss._init(&preferences, SdkVersion);
        if (result != 0)
        {
            why = $"slInit failed ({result})";
            return null;
        }

        dlss._initialised = true;
        var requirements = new FeatureRequirements { Base = Base.Of(FeatureRequirementsType, 2) };
        result = dlss._getFeatureRequirements(FeatureDlss, &requirements);
        if (result != 0 || (requirements.Flags & 4) == 0)
        {
            why = result != 0 ? $"slGetFeatureRequirements failed ({result})" : "DLSS does not run on Vulkan here";
            dlss.Shutdown();
            return null;
        }

        static string[] Strings(byte** at, uint count) => [.. Enumerable.Range(0, (int)count).Select(i => Marshal.PtrToStringUTF8((nint)at[i])!)];
        dlss.InstanceExtensions = Strings(requirements.VkInstanceExtensions, requirements.VkNumInstanceExtensions);
        dlss.DeviceExtensions = Strings(requirements.VkDeviceExtensions, requirements.VkNumDeviceExtensions);
        dlss.Features12 = Strings(requirements.VkFeatures12, requirements.VkNumFeatures12);
        dlss.Features13 = Strings(requirements.VkFeatures13, requirements.VkNumFeatures13);
        dlss.GraphicsQueues = requirements.VkNumGraphicsQueuesRequired;
        dlss.ComputeQueues = requirements.VkNumComputeQueuesRequired;

        // Ray Reconstruction's own asks, added to DLSS's.
        if (rayReconstruction)
        {
            var ray = new FeatureRequirements { Base = Base.Of(FeatureRequirementsType, 2) };
            result = dlss._getFeatureRequirements(FeatureRay, &ray);
            if (result != 0 || (ray.Flags & 4) == 0)
            {
                rayNote = result != 0 ? $"slGetFeatureRequirements failed ({result})" : "Ray Reconstruction does not run on Vulkan here";
            }
            else
            {
                dlss.InstanceExtensions = [.. dlss.InstanceExtensions.Union(Strings(ray.VkInstanceExtensions, ray.VkNumInstanceExtensions))];
                dlss.DeviceExtensions = [.. dlss.DeviceExtensions.Union(Strings(ray.VkDeviceExtensions, ray.VkNumDeviceExtensions))];
                dlss.Features12 = [.. dlss.Features12.Union(Strings(ray.VkFeatures12, ray.VkNumFeatures12))];
                dlss.Features13 = [.. dlss.Features13.Union(Strings(ray.VkFeatures13, ray.VkNumFeatures13))];
                dlss.GraphicsQueues = Math.Max(dlss.GraphicsQueues, ray.VkNumGraphicsQueuesRequired);
                dlss.ComputeQueues = Math.Max(dlss.ComputeQueues, ray.VkNumComputeQueuesRequired);
                dlss.RayReconstruction = true;
            }
        }

        // Frame generation's, Reflex's and the markers' asks, the same way. Without them, no frame generation.
        foreach (var feature in frames ? new[] { FeatureFrames, FeatureReflex, FeaturePcl } : [])
        {
            var asks = new FeatureRequirements { Base = Base.Of(FeatureRequirementsType, 2) };
            result = dlss._getFeatureRequirements(feature, &asks);
            if (result != 0 || (asks.Flags & 4) == 0)
            {
                frameNote = result != 0 ? $"slGetFeatureRequirements({feature}) failed ({result})" : $"feature {feature} does not run on Vulkan here";
                frames = false;
                break;
            }

            dlss.InstanceExtensions = [.. dlss.InstanceExtensions.Union(Strings(asks.VkInstanceExtensions, asks.VkNumInstanceExtensions))];
            dlss.DeviceExtensions = [.. dlss.DeviceExtensions.Union(Strings(asks.VkDeviceExtensions, asks.VkNumDeviceExtensions))];
            dlss.Features12 = [.. dlss.Features12.Union(Strings(asks.VkFeatures12, asks.VkNumFeatures12))];
            dlss.Features13 = [.. dlss.Features13.Union(Strings(asks.VkFeatures13, asks.VkNumFeatures13))];
            dlss.GraphicsQueues = Math.Max(dlss.GraphicsQueues, asks.VkNumGraphicsQueuesRequired);
            dlss.ComputeQueues = Math.Max(dlss.ComputeQueues, asks.VkNumComputeQueuesRequired);
        }

        dlss.FrameGeneration = frames;
        dlss._framesToGenerate = (uint)Math.Max(1, frameGeneration - 1);
        dlss.FrameNote = frameNote;
        dlss.RayNote = rayNote;
        why = "";
        return dlss;
    }

    // Tells Streamline the instance, device and queues, checks that DLSS runs on this GPU, and fetches DLSS's functions and
    // the present proxy. Streamline's own queues, if it asks for any, follow the renderer's one in the same family.
    public bool Attach(nint instance, nint physical, nint device, uint family, out string why)
    {
        var info = new VulkanInfo
        {
            Base = Base.Of(VulkanInfoType, 3),
            Device = device,
            Instance = instance,
            PhysicalDevice = physical,
            GraphicsQueueFamily = family,
            GraphicsQueueIndex = 1,
            ComputeQueueFamily = family,
            ComputeQueueIndex = 1 + GraphicsQueues,
        };
        var result = _setVulkanInfo(&info);
        if (result != 0)
        {
            why = $"slSetVulkanInfo failed ({result})";
            return false;
        }

        var adapter = new AdapterInfo { Base = Base.Of(AdapterInfoType, 1), VkPhysicalDevice = (void*)physical };
        result = _isFeatureSupported(FeatureDlss, &adapter);
        if (result != 0)
        {
            why = $"DLSS is not supported on this GPU or driver ({result})";
            return false;
        }

        void* function = null;
        fixed (byte* name = "slDLSSGetOptimalSettings"u8)
        {
            result = _getFeatureFunction(FeatureDlss, name, &function);
        }

        _getOptimalSettings = (delegate* unmanaged<Options*, OptimalSettings*, int>)function;
        function = null;
        fixed (byte* name = "slDLSSSetOptions"u8)
        {
            result |= _getFeatureFunction(FeatureDlss, name, &function);
        }

        _setOptions = (delegate* unmanaged<ViewportHandle*, Options*, int>)function;
        fixed (byte* name = "vkQueuePresentKHR"u8)
        {
            _present = (delegate* unmanaged<nint, PresentInfo*, int>)_getDeviceProcAddr(device, name);
        }

        if (result != 0 || _getOptimalSettings is null || _setOptions is null || _present is null)
        {
            why = "DLSS's functions or Streamline's present proxy are missing";
            return false;
        }

        if (FrameGeneration)
        {
            void* Feature(uint feature, string name)
            {
                void* f = null;
                var bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
                fixed (byte* n = bytes)
                {
                    var gpu = new AdapterInfo { Base = Base.Of(AdapterInfoType, 1), VkPhysicalDevice = (void*)physical };
                    return _isFeatureSupported(feature, &gpu) == 0 && _getFeatureFunction(feature, n, &f) == 0 ? f : null;
                }
            }

            void* Proxy(string name)
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
                fixed (byte* n = bytes)
                {
                    return _getDeviceProcAddr(device, n);
                }
            }

            _setFrameOptions = (delegate* unmanaged<ViewportHandle*, FrameOptions*, int>)Feature(FeatureFrames, "slDLSSGSetOptions");
            _getFrameState = (delegate* unmanaged<ViewportHandle*, FrameState*, FrameOptions*, int>)Feature(FeatureFrames, "slDLSSGGetState");
            _setReflexOptions = (delegate* unmanaged<ReflexOptions*, int>)Feature(FeatureReflex, "slReflexSetOptions");
            _reflexSleep = (delegate* unmanaged<nint, int>)Feature(FeatureReflex, "slReflexSleep");
            _setMarker = (delegate* unmanaged<uint, nint, int>)Feature(FeaturePcl, "slPCLSetMarker");
            _createSwapchain = (delegate* unmanaged<nint, SwapchainInfo*, void*, ulong*, int>)Proxy("vkCreateSwapchainKHR");
            _destroySwapchain = (delegate* unmanaged<nint, ulong, void*, void>)Proxy("vkDestroySwapchainKHR");
            _getSwapchainImages = (delegate* unmanaged<nint, ulong, uint*, ulong*, int>)Proxy("vkGetSwapchainImagesKHR");
            _acquire = (delegate* unmanaged<nint, ulong, ulong, ulong, ulong, uint*, int>)Proxy("vkAcquireNextImageKHR");
            _waitIdle = (delegate* unmanaged<nint, int>)Proxy("vkDeviceWaitIdle");
            if (_setFrameOptions is null || _getFrameState is null || _setReflexOptions is null || _reflexSleep is null || _setMarker is null
                || _createSwapchain is null || _destroySwapchain is null || _getSwapchainImages is null || _acquire is null || _waitIdle is null)
            {
                FrameNote = "frame generation, Reflex or the markers are not supported on this GPU or driver, or a proxy is missing";
                FrameGeneration = false;
            }
            else
            {
                // Reflex in low-latency mode: frame generation needs it.
                var reflex = new ReflexOptions { Base = Base.Of(ReflexOptionsType, 1), Mode = 1 };
                result = _setReflexOptions(&reflex);
                if (result != 0)
                {
                    FrameNote = $"slReflexSetOptions failed ({result})";
                    FrameGeneration = false;
                }
            }
        }

        if (RayReconstruction)
        {
            function = null;
            fixed (byte* name = "slDLSSDSetOptions"u8)
            {
                result = _isFeatureSupported(FeatureRay, &adapter);
                if (result == 0)
                {
                    result = _getFeatureFunction(FeatureRay, name, &function);
                }
            }

            _setRayOptions = (delegate* unmanaged<ViewportHandle*, RayOptions*, int>)function;
            if (result != 0 || _setRayOptions is null)
            {
                RayNote = $"Ray Reconstruction is not supported on this GPU or driver ({result})";
                RayReconstruction = false;
            }
        }

        why = "";
        return true;
    }

    // The window's surface, made through Streamline's proxy so that frame generation knows its window. Zero on failure.
    public ulong CreateSurface(nint instance, nint window, nint module)
    {
        void* create;
        fixed (byte* name = "vkCreateWin32SurfaceKHR"u8)
        {
            create = _getInstanceProcAddr(instance, name);
        }

        var info = new SurfaceInfo { SType = 1000009000, Instance = module, Window = window };
        ulong surface = 0;
        return create is not null && ((delegate* unmanaged<nint, SurfaceInfo*, void*, ulong*, int>)create)(instance, &info, null, &surface) == 0 ? surface : 0;
    }

    // The swapchain and its images, through Streamline's proxies, for frame generation. Zero on failure.
    public ulong CreateSwapchain(nint device, ulong surface, int imageCount, int format, int colorSpace, int width, int height, uint usage, int transform, int presentMode)
    {
        var info = new SwapchainInfo
        {
            SType = 1000001000,
            Surface = surface,
            MinImageCount = (uint)imageCount,
            ImageFormat = format,
            ImageColorSpace = colorSpace,
            Width = (uint)width,
            Height = (uint)height,
            ImageArrayLayers = 1,
            ImageUsage = usage,
            PreTransform = transform,
            CompositeAlpha = 1,
            PresentMode = presentMode,
            Clipped = 1,
        };
        ulong swapchain = 0;
        var result = _createSwapchain(device, &info, null, &swapchain);
        if (result != 0)
        {
            Console.Error.WriteLine($"DLSS-G: vkCreateSwapchainKHR failed ({result})");
        }

        return result == 0 ? swapchain : 0;
    }

    public ulong[] SwapchainImages(nint device, ulong swapchain)
    {
        uint count = 0;
        _getSwapchainImages(device, swapchain, &count, null);
        var images = new ulong[count];
        fixed (ulong* at = images)
        {
            _getSwapchainImages(device, swapchain, &count, at);
        }

        return images;
    }

    // Acquires the next image through the proxy; Vulkan's result.
    public int Acquire(nint device, ulong swapchain, ulong semaphore, out uint index)
    {
        uint i = 0;
        var result = _acquire(device, swapchain, ulong.MaxValue, semaphore, 0, &i);
        index = i;
        return result;
    }

    public void WaitIdle(nint device) => _waitIdle(device);

    // When the surface or the swapchain could not go through the proxies: no frame generation.
    public void NoFrameGeneration(string why) => (FrameGeneration, FrameNote) = (false, why);

    public void DestroySwapchain(nint device, ulong swapchain) => _destroySwapchain(device, swapchain, null);

    // A frame starts: its token, and with frame generation Reflex's sleep and the simulation's start.
    public void BeginFrame(uint frame)
    {
        nint token = 0;
        if (_getNewFrameToken(&token, &frame) == 0)
        {
            (_token, _tokenFrame) = (token, frame);
        }

        if (FrameGeneration && _token != 0)
        {
            _reflexSleep(_token);
            _setMarker(SimulationStart, _token);
        }
    }

    // Where the frame is, for Reflex and frame generation.
    public void Mark(uint marker)
    {
        if (FrameGeneration && _token != 0)
        {
            _setMarker(marker, _token);
        }
    }

    // How many frames the last present showed, the renderer's and the generated ones, and frame generation's status.
    public (uint Presented, uint Status) FrameStatus()
    {
        if (!FrameGeneration)
        {
            return (1, 0);
        }

        var viewport = new ViewportHandle { Base = Base.Of(ViewportHandleType, 1) };
        var state = new FrameState { Base = Base.Of(FrameStateType, 4) };
        return _getFrameState(&viewport, &state, null) == 0 ? (state.NumFramesActuallyPresented, state.Status) : (0, uint.MaxValue);
    }

    // The size DLSS renders at for an output of width by height in this mode: the output itself for DLAA.
    public (int Width, int Height) RenderSize(uint mode, int width, int height)
    {
        var options = new Options { Base = Base.Of(OptionsType, 3), Mode = mode, OutputWidth = (uint)width, OutputHeight = (uint)height, PreExposure = 1f, ExposureScale = 1f, ColorBuffersHdr = 1 };
        var settings = new OptimalSettings { Base = Base.Of(OptimalSettingsType, 1) };
        return _getOptimalSettings(&options, &settings) == 0 && settings.OptimalRenderWidth > 0 && settings.OptimalRenderHeight > 0
            ? ((int)settings.OptimalRenderWidth, (int)settings.OptimalRenderHeight)
            : (width, height);
    }

    // How many jitter positions a cycle has: eight, and more as DLSS upscales further (eight times the scale squared).
    public static int Phases(int render, int output) => Math.Max(8, (int)MathF.Ceiling(8f * (output / (float)render) * (output / (float)render)));

    // The jitter of a frame in pixels, each axis in [-0.5, 0.5): Halton's sequence in bases 2 and 3.
    public static SN.Vector2 Jitter(long frame, int phases)
    {
        static float Halton(int index, int radix)
        {
            float f = 1f, r = 0f;
            for (var i = index; i > 0; i /= radix)
            {
                f /= radix;
                r += f * (i % radix);
            }

            return r;
        }

        var k = (int)(frame % phases) + 1;
        return new SN.Vector2(Halton(k, 2) - 0.5f, Halton(k, 3) - 0.5f);
    }

    // One frame of DLSS, recorded into the command buffer: the camera, the four images and their layouts, then DLSS
    // itself. The matrices carry no jitter; jitter is the one the scene was drawn with, in pixels, as it moved the image.
    // Motion is in UV units, from where a pixel is to where it was. With guides, Ray Reconstruction runs in DLSS's place,
    // in the same mode. False, the first time only, when Streamline refuses.
    public bool Evaluate(nint commandBuffer, uint frame, uint mode, SN.Matrix4x4 view, SN.Matrix4x4 projection, SN.Matrix4x4 previousViewProjection,
        SN.Vector2 jitter, bool reset, float fovY, float near, Image depth, Image motion, Image input, Image output, Guides? guides = null, Image? hudLess = null)
    {
        var ray = guides is not null && RayReconstruction;
        var viewProjection = view * projection;
        SN.Matrix4x4.Invert(viewProjection, out var clipToWorld);
        SN.Matrix4x4.Invert(projection, out var clipToView);
        SN.Matrix4x4.Invert(view, out var camera);
        var clipToPrevious = clipToWorld * previousViewProjection;
        SN.Matrix4x4.Invert(clipToPrevious, out var previousToClip);
        var constants = new Constants
        {
            Base = Base.Of(ConstantsType, 2),
            CameraViewToClip = projection,
            ClipToCameraView = clipToView,
            ClipToLensClip = SN.Matrix4x4.Identity,
            ClipToPrevClip = clipToPrevious,
            PrevClipToClip = previousToClip,
            JitterOffset = jitter,
            MvecScale = SN.Vector2.One,
            CameraPos = camera.Translation,
            CameraRight = new SN.Vector3(camera.M11, camera.M12, camera.M13),
            CameraUp = new SN.Vector3(camera.M21, camera.M22, camera.M23),
            CameraFwd = -new SN.Vector3(camera.M31, camera.M32, camera.M33),
            CameraNear = near,
            CameraFar = 100_000f,
            CameraFov = fovY,
            CameraAspectRatio = output.Width / (float)output.Height,
            MotionVectorsInvalidValue = float.MaxValue,
            DepthInverted = 1,
            CameraMotionIncluded = 1,
            MotionVectors3D = 0,
            Reset = reset ? (byte)1 : (byte)0,
            MinRelativeLinearDepthObjectSeparation = 40f,
        };
        var viewport = new ViewportHandle { Base = Base.Of(ViewportHandleType, 1) };
        var options = new Options
        {
            Base = Base.Of(OptionsType, 3),
            Mode = mode,
            OutputWidth = (uint)output.Width,
            OutputHeight = (uint)output.Height,
            PreExposure = 1f,
            ExposureScale = 1f,
            ColorBuffersHdr = 1,
            UseAutoExposure = 1,
        };
        // Ray Reconstruction's: the normal and roughness packed in one image, the camera's view both ways.
        var rayOptions = new RayOptions
        {
            Base = Base.Of(RayOptionsType, 3),
            Mode = mode,
            OutputWidth = (uint)output.Width,
            OutputHeight = (uint)output.Height,
            PreExposure = 1f,
            ExposureScale = 1f,
            ColorBuffersHdr = 1,
            NormalRoughnessMode = 1,
            WorldToCameraView = view,
            CameraViewToWorld = camera,
        };
        var resources = stackalloc Resource[9];
        var tags = stackalloc ResourceTag[9];
        static void Tag(Resource* resource, ResourceTag* tag, Image image, uint type)
        {
            *resource = new Resource
            {
                Base = Base.Of(ResourceType, 1),
                Native = (void*)image.Handle,
                Memory = (void*)image.Memory,
                View = (void*)image.View,
                State = image.Layout,
                Width = (uint)image.Width,
                Height = (uint)image.Height,
                NativeFormat = image.Format,
                MipLevels = 1,
                ArrayLayers = 1,
                Usage = image.Usage,
            };
            *tag = new ResourceTag { Base = Base.Of(ResourceTagType, 1), Resource = resource, Type = type, Lifecycle = ValidUntilPresent, Width = (uint)image.Width, Height = (uint)image.Height };
        }

        Tag(&resources[0], &tags[0], depth, BufferDepth);
        Tag(&resources[1], &tags[1], motion, BufferMotion);
        Tag(&resources[2], &tags[2], input, BufferInput);
        Tag(&resources[3], &tags[3], output, BufferOutput);
        if (ray)
        {
            Tag(&resources[4], &tags[4], guides!.Value.Diffuse, BufferAlbedo);
            Tag(&resources[5], &tags[5], guides.Value.Specular, BufferSpecularAlbedo);
            Tag(&resources[6], &tags[6], guides.Value.NormalRoughness, BufferNormalRoughness);
            Tag(&resources[7], &tags[7], guides.Value.HitDistance, BufferSpecularHitDistance);
        }

        // For frame generation, the final image without the panel and subtitles, so that it does not smear them.
        var count = ray ? 8u : 4u;
        if (hudLess is { } plain)
        {
            Tag(&resources[count], &tags[count], plain, BufferHudLess);
            count++;
        }

        // The frame's token from BeginFrame, or a new one.
        var token = _tokenFrame == frame ? _token : 0;
        var result = token != 0 ? 0 : _getNewFrameToken(&token, &frame);
        var step = "slGetNewFrameToken";
        if (result == 0)
        {
            step = "slSetConstants";
            result = _setConstants(&constants, token, &viewport);
        }

        if (result == 0)
        {
            step = ray ? "slDLSSDSetOptions" : "slDLSSSetOptions";
            result = ray ? _setRayOptions(&viewport, &rayOptions) : _setOptions(&viewport, &options);
        }

        // Frame generation on, once its viewport has its first constants: one frame or more between two of ours.
        if (result == 0 && FrameGeneration && !_frameOptionsSet)
        {
            step = "slDLSSGSetOptions";
            var frameOptions = new FrameOptions { Base = Base.Of(FrameOptionsType, 5), Mode = 1, NumFramesToGenerate = _framesToGenerate };
            result = _setFrameOptions(&viewport, &frameOptions);
            _frameOptionsSet = result == 0;
        }

        if (result == 0)
        {
            step = "slSetTagForFrame";
            result = _setTagForFrame(token, &viewport, tags, count, commandBuffer);
        }

        if (result == 0)
        {
            step = "slEvaluateFeature";
            var inputs = stackalloc Base*[1];
            inputs[0] = &viewport.Base;
            result = _evaluateFeature(ray ? FeatureRay : FeatureDlss, token, inputs, 1, commandBuffer);
        }

        if (result != 0 && _failed is null)
        {
            _failed = $"{step} failed ({result})";
            Console.Error.WriteLine($"DLSS: {_failed}");
        }

        return result == 0;
    }

    // Presents through Streamline's proxy for vkQueuePresentKHR. Vulkan's result.
    public int Present(nint queue, ulong waitSemaphore, ulong swapchain, uint imageIndex)
    {
        var info = new PresentInfo
        {
            SType = 1000001001,
            WaitSemaphoreCount = 1,
            WaitSemaphores = &waitSemaphore,
            SwapchainCount = 1,
            Swapchains = &swapchain,
            ImageIndices = &imageIndex,
        };
        return _present(queue, &info);
    }

    // Before the device and instance go.
    public void Shutdown()
    {
        if (_initialised)
        {
            _initialised = false;
            _shutdown();
        }
    }

    [UnmanagedCallersOnly]
    private static void Log(uint type, byte* message)
    {
        if (type > 0 || _verbose)
        {
            var kind = type == 2 ? "error" : type == 1 ? "warning" : "info";
            Console.Error.WriteLine($"STREAMLINE {kind}: {Marshal.PtrToStringUTF8((nint)message)?.TrimEnd()}");
        }
    }

    // WinVerifyTrust on the file, offline, then its signer's name: NVIDIA Corporation.
    private static bool SignedByNvidia(string path, out string why)
    {
        var action = new Guid(0x00aac56b, 0xcd44, 0x11d0, 0x8c, 0xc2, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee); // WINTRUST_ACTION_GENERIC_VERIFY_V2
        fixed (char* file = path)
        {
            var fileInfo = new WinTrustFileInfo { Size = (uint)sizeof(WinTrustFileInfo), FilePath = file };
            var data = new WinTrustData
            {
                Size = (uint)sizeof(WinTrustData),
                UiChoice = 2, // WTD_UI_NONE
                RevocationChecks = 0, // WTD_REVOKE_NONE
                UnionChoice = 1, // WTD_CHOICE_FILE
                File = &fileInfo,
                StateAction = 1, // WTD_STATEACTION_VERIFY
                ProvFlags = 0x10 | 0x1000, // WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL: nothing fetched
            };
            var status = WinVerifyTrust(-1, &action, &data);
            data.StateAction = 2; // WTD_STATEACTION_CLOSE
            WinVerifyTrust(-1, &action, &data);
            if (status != 0)
            {
                why = $"{Path.GetFileName(path)}: its signature does not verify (0x{status:x8})";
                return false;
            }
        }

        try
        {
#pragma warning disable SYSLIB0057
            using var signer = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            if (!signer.Subject.Contains("O=NVIDIA Corporation", StringComparison.Ordinal))
            {
                why = $"{Path.GetFileName(path)} is signed by {signer.Subject}, not NVIDIA";
                return false;
            }
        }
        catch (System.Security.Cryptography.CryptographicException e)
        {
            why = $"{Path.GetFileName(path)}: {e.Message}";
            return false;
        }

        why = "";
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        public char* FilePath;
        public nint File;
        public Guid* KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public void* PolicyCallbackData;
        public void* SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public WinTrustFileInfo* File;
        public uint StateAction;
        public nint StateData;
        public char* UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public void* SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(nint window, Guid* action, WinTrustData* data);
}
