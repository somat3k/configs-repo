using FluentAssertions;
using Xunit;

namespace MLS.DeFi.Tests;

/// <summary>
/// Tests for <see cref="DeFiUtils"/> log-sanitisation helpers.
/// </summary>
public sealed class DeFiUtilsTests
{
    [Theory]
    [InlineData(null,            "")]
    [InlineData("",              "")]
    [InlineData("BTC-USDT",     "BTC-USDT")]
    [InlineData("cloid_abc123", "cloid_abc123")]
    [InlineData("0xdeadbeef",   "0xdeadbeef")]
    public void SafeLog_AllowsExpectedCharacters(string? input, string expected)
        => DeFiUtils.SafeLog(input).Should().Be(expected);

    [Fact]
    public void SafeLog_StripsNewlineCharacters()
    {
        var result = DeFiUtils.SafeLog("malicious\nlog");
        result.Should().NotContain("\n");
    }

    [Fact]
    public void SafeLog_StripsCarriageReturn()
    {
        var result = DeFiUtils.SafeLog("bad\rvalue");
        result.Should().NotContain("\r");
    }

    [Fact]
    public void SafeLog_TruncatesLongValues()
    {
        var longValue = new string('a', 200);
        var result    = DeFiUtils.SafeLog(longValue);
        result.Length.Should().BeLessOrEqualTo(128);
    }

    [Fact]
    public void SafeLog_StripsAngleBrackets()
    {
        var result = DeFiUtils.SafeLog("<script>alert(1)</script>");
        result.Should().NotContain("<").And.NotContain(">");
    }
}
