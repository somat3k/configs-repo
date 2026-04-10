using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.Arbitrager.Addresses;
using MLS.Arbitrager.Execution;
using MLS.Arbitrager.Scanning;
using MLS.Core.Constants;
using Xunit;

namespace MLS.Arbitrager.Tests;

/// <summary>
/// Unit tests for <see cref="ArrayBuilder"/> — transaction array construction and address resolution.
/// </summary>
public sealed class ArrayBuilderTests
{
    private static ArbitrageOpportunity MakeOpportunity(int hopCount = 2)
    {
        var hops = new List<ArbHopDetail>
        {
            new("WETH", "USDC", "camelot",  2_000m, 0.003m, 0.03m),
            new("USDC", "WETH", "dfyn",     0.0005m, 0.003m, 0.03m),
        };

        if (hopCount > 2)
            hops.Add(new ArbHopDetail("WETH", "ARB", "balancer", 1_500m, 0.002m, 0.05m));

        var now = DateTimeOffset.UtcNow;
        return new ArbitrageOpportunity(
            OpportunityId:      Guid.NewGuid(),
            Hops:               hops[..hopCount].AsReadOnly(),
            InputAmountUsd:     1_000m,
            EstimatedOutputUsd: 1_010m,
            GasEstimateUsd:     0.1m,
            NetProfitUsd:       9.9m,
            ProfitRatio:        0.0099m,
            DetectedAt:         now,
            ExpiresAt:          now.AddSeconds(2));
    }

    private static Mock<IArbitragerAddressBook> AddressBookMock(string address = "0xCAFECAFE")
    {
        var mock = new Mock<IArbitragerAddressBook>();
        mock.Setup(m => m.GetRouterAddressAsync(It.IsAny<BlockchainAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        return mock;
    }

    [Fact]
    public async Task BuildAsync_ReturnsArrayWithCorrectHopCount()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity(2);

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        array.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildAsync_SetsOpportunityId()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity();

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        array.OpportunityId.Should().Be(opp.OpportunityId);
    }

    [Fact]
    public async Task BuildAsync_AssignsSequenceIndices()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity(2);

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        array.Steps[0].SequenceIndex.Should().Be(0);
        array.Steps[1].SequenceIndex.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_ResolvesRouterAddressFromAddressBook()
    {
        const string expected = "0xABCDEF1234567890";
        var builder = new ArrayBuilder(AddressBookMock(expected).Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity();

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        array.Steps.All(s => s.RouterAddress == expected).Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_MinAmountOutRespectsSlippageTolerance()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity();

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        // MinAmountOut should be estimatedOut * (1 - 0.005) = estimatedOut * 0.995
        foreach (var step in array.Steps)
        {
            var estimatedOut = step.AmountIn * opp.Hops[step.SequenceIndex].Price
                             * (1m - opp.Hops[step.SequenceIndex].Fee);
            var expectedMin  = estimatedOut * 0.995m;
            step.MinAmountOut.Should().BeApproximately(expectedMin, 0.001m);
        }
    }

    [Fact]
    public async Task BuildAsync_ThrowsInvalidOperationWhenRouterAddressMissing()
    {
        var mock = new Mock<IArbitragerAddressBook>();
        mock.Setup(m => m.GetRouterAddressAsync(It.IsAny<BlockchainAddress>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("not found"));

        var builder = new ArrayBuilder(mock.Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity();

        // Missing router address should throw InvalidOperationException (not silently use zero address)
        await builder.Invoking(b => b.BuildAsync(opp, CancellationToken.None).AsTask())
                     .Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("*Missing router address*");
    }

    [Fact]
    public async Task BuildAsync_ExpiresAtMatchesOpportunity()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity();

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        array.ExpiresAt.Should().Be(opp.ExpiresAt);
    }

    [Fact]
    public async Task BuildAsync_AssignsGasLimitsPerExchange()
    {
        var builder = new ArrayBuilder(AddressBookMock().Object, NullLogger<ArrayBuilder>.Instance);
        var opp     = MakeOpportunity(2);

        var array = await builder.BuildAsync(opp, CancellationToken.None);

        // Camelot = 300_000, DFYN = 300_000
        array.Steps[0].GasLimit.Should().Be(300_000L);
        array.Steps[1].GasLimit.Should().Be(300_000L);
    }
}
