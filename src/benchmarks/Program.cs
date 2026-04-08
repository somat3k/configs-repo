using BenchmarkDotNet.Running;

namespace MLS.Benchmarks;

/// <summary>Entry point for the MLS benchmark suite.</summary>
public static class BenchmarkProgram
{
    /// <summary>Runs <see cref="BenchmarkSwitcher"/> for the current assembly.</summary>
    /// <param name="args">Command-line arguments forwarded to BenchmarkDotNet.</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(BenchmarkProgram).Assembly).Run(args);
}
