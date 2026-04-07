using FluentAssertions;
using MLS.DataLayer.Hydra;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Tests for <see cref="CamelotFeedCollector"/> timeframe validation logic.
/// </summary>
public sealed class CamelotFeedCollectorTests
{
    [Theory]
    [InlineData("1h",  true)]
    [InlineData("1d",  true)]
    [InlineData("1H",  true)]  // case-insensitive
    [InlineData("1D",  true)]
    [InlineData("1m",  false)]
    [InlineData("5m",  false)]
    [InlineData("15m", false)]
    [InlineData("4h",  false)]
    [InlineData("1w",  false)]
    public void SupportedTimeframes_ContainsExpected(string tf, bool expected)
    {
        CamelotFeedCollector.SupportedTimeframes.Contains(tf).Should().Be(expected);
    }

    [Fact]
    public void SupportedTimeframes_HasExactlyTwoEntries()
    {
        CamelotFeedCollector.SupportedTimeframes.Should().HaveCount(2);
    }
}
