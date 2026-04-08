#pragma warning disable CS0436 // Program type conflict with referenced assembly (BlockController)
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
