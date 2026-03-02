namespace GA.Domain.Services.Fretboard.Biomechanics.IK;

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

internal static class GpuVectorOps
{
    private static readonly object Sync = new();
    private static Context? _context;
    private static Accelerator? _accelerator;

    private static Action<AcceleratorStream, Index1D, ArrayView<Float3>, ArrayView<Float3>, ArrayView<Float3>>?
        _subtractKernel;

    static GpuVectorOps()
    {
        Initialize();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public static bool IsAvailable => _subtractKernel is not null && _accelerator is not null;

    public static void ComputeDifference(Float3[] actual, Float3[] target, Float3[] output, int length, bool useGpu)
    {
        if (!useGpu || !IsAvailable)
        {
            for (var i = 0; i < length; i++)
            {
                output[i] = target[i] - actual[i];
            }

            return;
        }

        var accelerator = _accelerator!;
        using var bufferActual = accelerator.Allocate1D<Float3>(length);
        using var bufferTarget = accelerator.Allocate1D<Float3>(length);
        using var bufferOutput = accelerator.Allocate1D<Float3>(length);

        bufferActual.CopyFromCPU(actual);
        bufferTarget.CopyFromCPU(target);

        var kernel = _subtractKernel!;
        kernel(accelerator.DefaultStream, new(length), bufferActual.View, bufferTarget.View, bufferOutput.View);
        accelerator.Synchronize();

        bufferOutput.CopyToCPU(output);
    }

    private static void Initialize()
    {
        lock (Sync)
        {
            if (_accelerator is not null)
            {
                return;
            }

            try
            {
                _context = Context.Create(builder => builder.AllAccelerators());
                Accelerator? accelerator = null;

                try
                {
                    if (_context.GetCudaDevices().Count > 0)
                    {
                        accelerator = _context.CreateCudaAccelerator(0);
                    }
                    else if (_context.GetCLDevices().Count > 0)
                    {
                        accelerator = _context.CreateCLAccelerator(0);
                    }
                }
                catch
                {
                    accelerator?.Dispose();
                    accelerator = null;
                }

                accelerator ??= _context.CreateCPUAccelerator(0);

                _accelerator = accelerator;
                _subtractKernel =
                    accelerator.LoadAutoGroupedKernel<Index1D, ArrayView<Float3>, ArrayView<Float3>, ArrayView<Float3>>(
                        SubtractKernel);
            }
            catch
            {
                Dispose();
            }
        }
    }

    private static void SubtractKernel(Index1D index, ArrayView<Float3> actual, ArrayView<Float3> target,
        ArrayView<Float3> output)
    {
        var diff = target[index] - actual[index];
        output[index] = diff;
    }

    private static void Dispose()
    {
        lock (Sync)
        {
            _subtractKernel = null;
            _accelerator?.Dispose();
            _accelerator = null;
            _context?.Dispose();
            _context = null;
        }
    }

    internal readonly struct Float3(float x, float y, float z)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly float Z = z;

        public static Float3 FromVector(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

        public Vector3 ToVector3() => new(X, Y, Z);

        public static Float3 operator -(Float3 left, Float3 right) =>
            new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}
