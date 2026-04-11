using FluentAssertions;
using MLS.Core.Kernels;
using Xunit;

namespace MLS.Core.Tests.Kernels;

/// <summary>
/// Unit tests for <see cref="KernelExecutionContext"/>.
/// </summary>
public sealed class KernelExecutionContextTests
{
    [Fact]
    public void CancellationToken_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var context = KernelExecutionContext.Create(cancellationToken: cts.Token);

        context.CancellationToken.IsCancellationRequested.Should().BeFalse();

        cts.Cancel();

        context.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }
}
