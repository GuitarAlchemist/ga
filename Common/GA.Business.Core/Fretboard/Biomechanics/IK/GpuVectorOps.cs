using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace GA.Business.Core.Fretboard.Biomechanics.IK;

internal static class GpuVectorOps
{
    private static readonly object Sync = new();
    private static Context? _context;
    private static Accelerator? _accelerator;
    private static Action<AcceleratorStream, Index1D, ArrayView<Float3>, ArrayView<Float3>, ArrayView<Float3>>? _subtractKernel;

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
        using var bufferActual = accelerator.Allocate<Float3>(length);
        using var bufferTarget = accelerator.Allocate<Float3>(length);
        using var bufferOutput = accelerator.Allocate<Float3>(length);

        bufferActual.CopyFrom(actual, 0, 0, length);
        bufferTarget.CopyFrom(target, 0, 0, length);

        var kernel = _subtractKernel!;
        kernel(accelerator.DefaultStream, new Index1D(length), bufferActual.View, bufferTarget.View, bufferOutput.View);
        accelerator.Synchronize();

        bufferOutput.CopyTo(output, 0, 0, length);
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
                    if (_context.GetCudaDevices().Length > 0)
                    {
                        accelerator = _context.CreateCudaAccelerator(0);
                    }
                    else if (_context.GetCLDevices().Length > 0)
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
                _subtractKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Float3>, ArrayView<Float3>, ArrayView<Float3>>(SubtractKernel);
            }
            catch
            {
                Dispose();
            }
        }
    }

    private static void SubtractKernel(Index1D index, ArrayView<Float3> actual, ArrayView<Float3> target, ArrayView<Float3> output)
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

    internal readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 FromVector(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

        public Vector3 ToVector3() => new(X, Y, Z);

        public static Float3 operator -(Float3 left, Float3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}