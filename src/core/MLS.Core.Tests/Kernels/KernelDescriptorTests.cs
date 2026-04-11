using FluentAssertions;
using MLS.Core.Kernels;
using Xunit;

namespace MLS.Core.Tests.Kernels;

/// <summary>
/// Unit tests for <see cref="KernelDescriptor"/>.
/// </summary>
public sealed class KernelDescriptorTests
{
    [Fact]
    public void Create_WithSameValues_AreEqual()
    {
        var budget = new KernelPerformanceBudget(TimeSpan.FromMilliseconds(10), null, false);

        var a = KernelDescriptor.Create(
            operationId: "op-a",
            inputContract: "input/v1",
            outputContract: "output/v1",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: budget);

        var b = KernelDescriptor.Create(
            operationId: "op-a",
            inputContract: "input/v1",
            outputContract: "output/v1",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: budget);

        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ProducesNewImmutableRecord()
    {
        var original = KernelDescriptor.Create(
            operationId: "op-a",
            inputContract: "input/v1",
            outputContract: "output/v1",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: new KernelPerformanceBudget(TimeSpan.FromMilliseconds(10), null, false));

        var updated = original with { Version = "2.0.0" };

        original.Version.Should().Be("1.0.0");
        updated.Version.Should().Be("2.0.0");
        updated.Should().NotBeSameAs(original);
    }
}
