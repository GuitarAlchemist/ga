// Dummy Main method to satisfy compiler since Program.cs is excluded
// Program.cs references unimplemented GPU features (GpuGrothendieckService, GpuShapeGraphBuilder)

internal class DummyMain
{
    private static void Main(string[] args)
    {
        Console.WriteLine("GpuBenchmark is currently disabled - GPU features not yet implemented");
    }
}

