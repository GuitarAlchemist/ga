namespace GA.Core.Numerics;

using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
///     Small SIMD-friendly helpers built on System.Numerics.Vector for portable vectorization.
///     These avoid external dependencies and work on all .NET targets that support Vector.
/// </summary>
[PublicAPI]
public static class SimdOps
{
    // ---- double ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var len = a.Length;
        var vsz = Vector<double>.Count;
        int i = 0;
        var acc = 0.0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<double>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var va = new Vector<double>(a.Slice(i, vsz));
                var vb = new Vector<double>(b.Slice(i, vsz));
                vacc += va * vb;
            }
            acc += Vector.Dot(vacc, Vector<double>.One);
        }

        for (; i < len; i++) acc += a[i] * b[i];
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Norm(ReadOnlySpan<double> a)
    {
        return Math.Sqrt(Dot(a, a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double L1Distance(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var len = a.Length;
        var vsz = Vector<double>.Count;
        int i = 0;
        double sum = 0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<double>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var v = new Vector<double>(a.Slice(i, vsz)) - new Vector<double>(b.Slice(i, vsz));
                // abs via max(x, -x)
                var neg = -v;
                var abs = Vector.Max(v, neg);
                vacc += abs;
            }
            sum += Vector.Dot(vacc, Vector<double>.One);
        }

        for (; i < len; i++) sum += Math.Abs(a[i] - b[i]);
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double L2Distance(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var len = a.Length;
        var vsz = Vector<double>.Count;
        int i = 0;
        double sumSq = 0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<double>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var v = new Vector<double>(a.Slice(i, vsz)) - new Vector<double>(b.Slice(i, vsz));
                vacc += v * v;
            }
            sumSq += Vector.Dot(vacc, Vector<double>.One);
        }

        for (; i < len; i++)
        {
            var d = a[i] - b[i];
            sumSq += d * d;
        }

        return Math.Sqrt(sumSq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CosineSimilarity(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var denom = Norm(a) * Norm(b);
        if (denom <= 0) return 0.0;
        return Dot(a, b) / denom;
    }

    // ---- float ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var len = a.Length;
        var vsz = Vector<float>.Count;
        int i = 0;
        float acc = 0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<float>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var va = new Vector<float>(a.Slice(i, vsz));
                var vb = new Vector<float>(b.Slice(i, vsz));
                vacc += va * vb;
            }
            acc += Vector.Dot(vacc, Vector<float>.One);
        }

        for (; i < len; i++) acc += a[i] * b[i];
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Norm(ReadOnlySpan<float> a)
    {
        return MathF.Sqrt(Dot(a, a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float L1Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var len = a.Length;
        var vsz = Vector<float>.Count;
        int i = 0;
        float sum = 0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<float>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var v = new Vector<float>(a.Slice(i, vsz)) - new Vector<float>(b.Slice(i, vsz));
                var neg = -v;
                var abs = Vector.Max(v, neg);
                vacc += abs;
            }
            sum += Vector.Dot(vacc, Vector<float>.One);
        }

        for (; i < len; i++) sum += MathF.Abs(a[i] - b[i]);
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float L2Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var len = a.Length;
        var vsz = Vector<float>.Count;
        int i = 0;
        float sumSq = 0;

        if (Vector.IsHardwareAccelerated && len >= vsz)
        {
            var vacc = Vector<float>.Zero;
            int last = len - (len % vsz);
            for (; i < last; i += vsz)
            {
                var v = new Vector<float>(a.Slice(i, vsz)) - new Vector<float>(b.Slice(i, vsz));
                vacc += v * v;
            }
            sumSq += Vector.Dot(vacc, Vector<float>.One);
        }

        for (; i < len; i++)
        {
            var d = a[i] - b[i];
            sumSq += d * d;
        }

        return MathF.Sqrt(sumSq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var denom = Norm(a) * Norm(b);
        if (denom <= 0) return 0f;
        return Dot(a, b) / denom;
    }
}
