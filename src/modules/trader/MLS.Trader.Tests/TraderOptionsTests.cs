using FluentAssertions;
using MLS.Trader.Configuration;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Tests for <see cref="TraderOptions"/>.</summary>
public sealed class TraderOptionsTests
{
    [Fact]
    public void TraderOptions_DefaultValues_AreCorrect()
    {
        var opts = new TraderOptions();

        opts.HttpEndpoint.Should().Be("http://trader:5300");
        opts.WsEndpoint.Should().Be("ws://trader:6300");
        opts.BlockControllerUrl.Should().Be("http://block-controller:5100");
        opts.ModelPath.Should().BeEmpty();
        opts.PaperTrading.Should().BeFalse();
        opts.MinSignalConfidence.Should().BeApproximately(0.65f, 0.001f);
        opts.MaxPositionSizeUsd.Should().Be(10_000m);
        opts.RiskRewardRatio.Should().BeApproximately(2.0, 0.001);
        opts.AtrMultiplier.Should().BeApproximately(2.0, 0.001);
        opts.StopLossPercent.Should().BeApproximately(0.02, 0.001);
        opts.AccountEquityUsd.Should().Be(100_000m);
        opts.SignalChannelCapacity.Should().Be(512);
    }

    [Fact]
    public void TraderOptions_CanSetCustomValues()
    {
        var opts = new TraderOptions
        {
            PaperTrading       = true,
            MinSignalConfidence = 0.75f,
            MaxPositionSizeUsd = 5_000m,
            RiskRewardRatio    = 3.0,
            AccountEquityUsd   = 50_000m,
        };

        opts.PaperTrading.Should().BeTrue();
        opts.MinSignalConfidence.Should().BeApproximately(0.75f, 0.001f);
        opts.MaxPositionSizeUsd.Should().Be(5_000m);
        opts.RiskRewardRatio.Should().BeApproximately(3.0, 0.001);
        opts.AccountEquityUsd.Should().Be(50_000m);
    }

    [Fact]
    public void TraderOptions_PostgresConnectionString_HasDefault()
    {
        var opts = new TraderOptions();
        opts.PostgresConnectionString.Should().Contain("mls_db");
    }

    [Fact]
    public void TraderOptions_RedisConnectionString_HasDefault()
    {
        var opts = new TraderOptions();
        opts.RedisConnectionString.Should().Contain("redis");
    }
}
