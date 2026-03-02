using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GA.Domain.Core.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Extensions;
using GA.Domain.Core.Theory.Tonal.Scales;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

[MemoryDiagnoser]
[SimpleJob]
public class DomainCoreBenchmarks
{
    private const int Iterations = 100_000;
    private const int WarmupIterations = 1_000;

    [Benchmark(Baseline = true)]
    public void PitchClass_Creation()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var pitchClass = PitchClass.FromValue(i % 12);
        }
    }

    [Benchmark]
    public void Str_Creation()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var str = GA.Domain.Core.Instruments.Primitives.Str.FromValue((i % 26) + 1);
        }
    }

    [Benchmark]
    public void PitchClass_ToIntervalStructure()
    {
        var pitchClasses = Enumerable.Range(0, 7).Select(i => PitchClass.FromValue(i % 12)).ToList();
        
        for (int i = 0; i < Iterations; i++)
        {
            var structure = pitchClasses.ToIntervalStructure();
        }
    }

    [Benchmark]
    public void Fret_Creation()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var fret = GA.Domain.Core.Instruments.Primitives.Fret.FromValue(i % 24);
        }
    }

    [Benchmark]
    public void PitchClassSet_Creation()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var pitchClasses = Enumerable.Range(0, 4).Select(j => PitchClass.FromValue(j % 12));
            var set = new GA.Domain.Core.Theory.Atonal.PitchClassSet(pitchClasses);
        }
    }

    [Benchmark]
    public void Scale_Creation()
    {
        for (int i = 0; i < Iterations; i++)
        {
            var scale = i switch
            {
                0 => GA.Domain.Core.Theory.Tonal.Scales.Scale.Major,
                1 => GA.Domain.Core.Theory.Tonal.Scales.Scale.NaturalMinor,
                2 => GA.Domain.Core.Theory.Tonal.Scales.Scale.HarmonicMinor,
                3 => GA.Domain.Core.Theory.Tonal.Scales.Scale.MelodicMinor,
                4 => GA.Domain.Core.Theory.Tonal.Scales.Scale.MajorPentatonic,
                5 => GA.Domain.Core.Theory.Tonal.Scales.Scale.Blues,
                _ => GA.Domain.Core.Theory.Tonal.Scales.Scale.Major
            };
        }
    }

    [Benchmark]
    public void PitchClass_Subtraction()
    {
        var pc1 = PitchClass.FromValue(9);
        var pc2 = PitchClass.FromValue(4);
        
        for (int i = 0; i < Iterations; i++)
        {
            var result = pc1 - pc2;
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        // Warmup all domain types
        for (int i = 0; i < WarmupIterations; i++)
        {
            var pc = PitchClass.FromValue(i % 12);
            var str = GA.Domain.Core.Instruments.Primitives.Str.FromValue((i % 26) + 1);
            var fret = GA.Domain.Core.Instruments.Primitives.Fret.FromValue(i % 24);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Ensure all domain types are JIT-compiled
        Setup();
    }
}
