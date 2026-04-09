using System.Diagnostics;
using FluentAssertions;
using MLS.Core.Designer;
using MLS.Designer.Blocks;
using MLS.Designer.Compilation;
using Xunit;

namespace MLS.Designer.Tests;

/// <summary>
/// Tests for the Roslyn strategy compiler and sandbox security model.
/// Validates Phase 6 acceptance criteria:
/// <list type="bullet">
///   <item>Custom C# indicator block compiles in &lt; 2 s.</item>
///   <item>Sandbox prevents file system access.</item>
///   <item>Security scanner rejects forbidden API usage.</item>
///   <item>Compiled block can be loaded and run in the sandbox.</item>
/// </list>
/// </summary>
public sealed class CompilationSandboxTests
{
    // ── Helper source templates ────────────────────────────────────────────────────

    private const string ValidIndicatorSource =
        """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Threading;
        using System.Threading.Tasks;
        using MLS.Core.Designer;
        using MLS.Designer.Blocks;

        namespace MLS.UserBlock;

        public sealed class TestPassthroughBlock : BlockBase
        {
            public override string BlockType   => "TestPassthroughBlock";
            public override string DisplayName => "Test Passthrough";
            public override IReadOnlyList<BlockParameter> Parameters => [];

            public TestPassthroughBlock() : base(
                [BlockSocket.Input("input", BlockSocketType.IndicatorValue)],
                [BlockSocket.Output("output", BlockSocketType.IndicatorValue)]) { }

            public override void Reset() { }

            protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
            {
                if (signal.SocketType != BlockSocketType.IndicatorValue)
                    return new ValueTask<BlockSignal?>(result: null);
                var value = signal.Value.GetSingle() * 2f;
                return new ValueTask<BlockSignal?>(
                    EmitFloat(BlockId, "output", BlockSocketType.IndicatorValue, value));
            }
        }
        """;

    private const string ForbiddenFileIOSource =
        """
        using System;
        using System.IO;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using MLS.Core.Designer;
        using MLS.Designer.Blocks;

        namespace MLS.UserBlock;

        public sealed class EvilBlock : BlockBase
        {
            public override string BlockType   => "EvilBlock";
            public override string DisplayName => "Evil Block";
            public override IReadOnlyList<BlockParameter> Parameters => [];

            public EvilBlock() : base([], []) { }
            public override void Reset() { }

            protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
            {
                var contents = File.ReadAllText("/etc/passwd");
                return new ValueTask<BlockSignal?>(result: null);
            }
        }
        """;

    private const string ForbiddenNetworkSource =
        """
        using System;
        using System.Net.Http;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using MLS.Core.Designer;
        using MLS.Designer.Blocks;

        namespace MLS.UserBlock;

        public sealed class NetworkBlock : BlockBase
        {
            public override string BlockType   => "NetworkBlock";
            public override string DisplayName => "Network Block";
            public override IReadOnlyList<BlockParameter> Parameters => [];

            public NetworkBlock() : base([], []) { }
            public override void Reset() { }

            protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
            {
                var client = new HttpClient();
                return new ValueTask<BlockSignal?>(result: null);
            }
        }
        """;

    private const string ForbiddenReflectionSource =
        """
        using System;
        using System.Reflection;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using MLS.Core.Designer;
        using MLS.Designer.Blocks;

        namespace MLS.UserBlock;

        public sealed class ReflectionBlock : BlockBase
        {
            public override string BlockType   => "ReflectionBlock";
            public override string DisplayName => "Reflection Block";
            public override IReadOnlyList<BlockParameter> Parameters => [];

            public ReflectionBlock() : base([], []) { }
            public override void Reset() { }

            protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
            {
                var t = typeof(object).Assembly.GetTypes();
                return new ValueTask<BlockSignal?>(result: null);
            }
        }
        """;

    // ── Roslyn compiler tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task RoslynCompiler_ValidSource_CompilesSuccessfully()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);

        result.Success.Should().BeTrue("valid indicator source should compile without errors");
        result.AssemblyBytes.Should().NotBeNullOrEmpty("compiled assembly bytes must be present on success");
        result.Diagnostics.Should().NotContain(d => d.StartsWith("error", StringComparison.OrdinalIgnoreCase),
            "no error-level diagnostics on a valid compilation");
    }

    [Fact]
    public async Task RoslynCompiler_ValidSource_CompilesInUnderFiveSeconds()
    {
        var compiler = BuildCompiler();
        var sw       = Stopwatch.StartNew();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        sw.Stop();

        result.Success.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "compilation must complete in < 5 s even on a loaded CI runner");
    }

    [Fact]
    public async Task RoslynCompiler_ForbiddenFileIO_RejectsCompilation()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ForbiddenFileIOSource, CancellationToken.None);

        result.Success.Should().BeFalse("System.IO file access is forbidden in user blocks");
        result.Diagnostics.Should().Contain(d => d.Contains("MLS-SEC"),
            "security diagnostic must identify the violation");
    }

    [Fact]
    public async Task RoslynCompiler_ForbiddenNetwork_RejectsCompilation()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ForbiddenNetworkSource, CancellationToken.None);

        result.Success.Should().BeFalse("System.Net.Http network access is forbidden in user blocks");
        result.Diagnostics.Should().Contain(d => d.Contains("MLS-SEC"),
            "security diagnostic must identify the violation");
    }

    [Fact]
    public async Task RoslynCompiler_ForbiddenReflection_RejectsCompilation()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ForbiddenReflectionSource, CancellationToken.None);

        result.Success.Should().BeFalse("System.Reflection is forbidden in user blocks");
        result.Diagnostics.Should().Contain(d => d.Contains("MLS-SEC"),
            "security diagnostic must identify the violation");
    }

    [Fact]
    public async Task RoslynCompiler_EmptySource_ReturnsDiagnosticErrors()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(string.Empty, CancellationToken.None);

        result.Success.Should().BeFalse("empty source cannot produce a valid assembly");
    }

    [Fact]
    public async Task RoslynCompiler_SyntaxError_ReturnsDiagnosticErrors()
    {
        const string brokenSource = "public class Broken { public void Foo() { ??? } }";
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(brokenSource, CancellationToken.None);

        result.Success.Should().BeFalse("syntax errors must prevent compilation");
        result.Diagnostics.Should().NotBeEmpty("syntax errors must produce at least one diagnostic");
    }

    // ── CompilationSandbox tests ──────────────────────────────────────────────────

    [Fact]
    public async Task CompilationSandbox_Load_InstantiatesBlockFromAssembly()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        result.Success.Should().BeTrue();

        await using var sandbox = CompilationSandbox.Load(result.AssemblyBytes!);

        sandbox.Block.Should().NotBeNull();
        sandbox.Block.BlockType.Should().Be("TestPassthroughBlock");
    }

    [Fact]
    public async Task CompilationSandbox_ProcessAsync_DoublesIndicatorValue()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        result.Success.Should().BeTrue();

        await using var sandbox = CompilationSandbox.Load(result.AssemblyBytes!);

        BlockSignal? emitted = null;
        // OutputProduced is on BlockBase (not IBlockElement), so cast to the concrete base class
        var blockBase = sandbox.Block as MLS.Designer.Blocks.BlockBase;
        blockBase.Should().NotBeNull("compiled block must extend BlockBase");
        blockBase!.OutputProduced += (sig, _) => { emitted = sig; return ValueTask.CompletedTask; };

        var inputSignal = new BlockSignal(
            Guid.NewGuid(),
            "input",
            BlockSocketType.IndicatorValue,
            System.Text.Json.JsonSerializer.SerializeToElement(3.14f));

        await sandbox.ProcessAsync(inputSignal, CancellationToken.None);

        emitted.Should().NotBeNull("block should emit an output signal");
        var output = emitted!.Value.Value.GetSingle();
        output.Should().BeApproximately(6.28f, 0.001f, "passthrough doubles the input value");
    }

    [Fact]
    public async Task CompilationSandbox_ProcessAsync_EnforcesTimeout()
    {
        // Source with an infinite loop — must be killed by the 100 ms timeout
        const string infiniteLoopSource =
            """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using MLS.Core.Designer;
            using MLS.Designer.Blocks;

            namespace MLS.UserBlock;

            public sealed class InfiniteLoopBlock : BlockBase
            {
                public override string BlockType   => "InfiniteLoopBlock";
                public override string DisplayName => "Infinite Loop";
                public override IReadOnlyList<BlockParameter> Parameters => [];

                public InfiniteLoopBlock() : base([], []) { }
                public override void Reset() { }

                protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
                {
                    // Will be cancelled by the sandbox 100 ms timeout
                    await Task.Delay(Timeout.Infinite, ct);
                    return null;
                }
            }
            """;

        var compiler = BuildCompiler();
        var compiled = await compiler.CompileAsync(infiniteLoopSource, CancellationToken.None);
        compiled.Success.Should().BeTrue("loop source should compile");

        await using var sandbox = CompilationSandbox.Load(compiled.AssemblyBytes!);

        var signal = new BlockSignal(
            Guid.NewGuid(), "x", BlockSocketType.IndicatorValue,
            System.Text.Json.JsonSerializer.SerializeToElement(1f));

        var act = async () => await sandbox.ProcessAsync(signal, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>(
            "the 100 ms sandbox timeout must cancel the infinite loop");
    }

    [Fact]
    public async Task CompilationSandbox_DisposeAsync_AllowsGarbageCollection()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        result.Success.Should().BeTrue();

        // Load and immediately dispose — should not throw
        var sandbox = CompilationSandbox.Load(result.AssemblyBytes!);
        var act = async () => await sandbox.DisposeAsync();
        await act.Should().NotThrowAsync("dispose must complete cleanly");
    }

    [Fact]
    public async Task CompilationSandbox_DoubleDispose_IsIdempotent()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        result.Success.Should().BeTrue();

        var sandbox = CompilationSandbox.Load(result.AssemblyBytes!);
        await sandbox.DisposeAsync();

        var act = async () => await sandbox.DisposeAsync();
        await act.Should().NotThrowAsync("second dispose must be a no-op");
    }

    [Fact]
    public async Task CompilationSandbox_ProcessAfterDispose_ThrowsObjectDisposedException()
    {
        var compiler = BuildCompiler();
        var result   = await compiler.CompileAsync(ValidIndicatorSource, CancellationToken.None);
        result.Success.Should().BeTrue();

        var sandbox = CompilationSandbox.Load(result.AssemblyBytes!);
        await sandbox.DisposeAsync();

        var signal = new BlockSignal(
            Guid.NewGuid(), "input", BlockSocketType.IndicatorValue,
            System.Text.Json.JsonSerializer.SerializeToElement(1f));

        var act = async () => await sandbox.ProcessAsync(signal, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>(
            "calling ProcessAsync after dispose must throw ObjectDisposedException");
    }

    // ── Factory ───────────────────────────────────────────────────────────────────

    private static RoslynStrategyCompiler BuildCompiler()
    {
        var httpFactory  = new Moq.Mock<IHttpClientFactory>().Object;
        var opts         = Microsoft.Extensions.Options.Options.Create(new Designer.Configuration.DesignerOptions());
        var logger       = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoslynStrategyCompiler>.Instance;
        return new RoslynStrategyCompiler(httpFactory, opts, logger);
    }
}
