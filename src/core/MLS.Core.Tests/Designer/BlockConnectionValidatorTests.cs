using FluentAssertions;
using MLS.Core.Designer;
using Xunit;

namespace MLS.Core.Tests.Designer;

/// <summary>
/// Verifies that <see cref="BlockConnectionValidator"/> correctly enforces
/// type-safe socket connections and rejects mismatched or invalid configurations.
/// </summary>
public sealed class BlockConnectionValidatorTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static FakeSocket Output(BlockSocketType type) =>
        new(Guid.NewGuid(), type, SocketDirection.Output);

    private static FakeSocket Input(BlockSocketType type) =>
        new(Guid.NewGuid(), type, SocketDirection.Input);

    private sealed record FakeSocket(
        Guid SocketId,
        BlockSocketType DataType,
        SocketDirection Direction,
        string Name = "socket",
        bool IsConnected = false,
        Guid? ConnectedToSocketId = null) : IBlockSocket;

    // ── Valid connections ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BlockSocketType.CandleStream)]
    [InlineData(BlockSocketType.IndicatorValue)]
    [InlineData(BlockSocketType.MLSignal)]
    [InlineData(BlockSocketType.TradeOrder)]
    [InlineData(BlockSocketType.ArbitrageOpportunity)]
    [InlineData(BlockSocketType.DeFiSignal)]
    [InlineData(BlockSocketType.FeatureVector)]
    [InlineData(BlockSocketType.TrainingStatus)]
    public void Validate_MatchingTypes_DoesNotThrow(BlockSocketType type)
    {
        var from = Output(type);
        var to   = Input(type);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should().NotThrow();
    }

    // ── Type mismatch ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_CandleStreamToIndicatorValueInput_ThrowsInvalidBlockConnectionException()
    {
        var from = Output(BlockSocketType.CandleStream);
        var to   = Input(BlockSocketType.IndicatorValue);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .WithMessage("*CandleStream*IndicatorValue*");
    }

    [Fact]
    public void Validate_MLSignalToTradeOrderInput_ThrowsInvalidBlockConnectionException()
    {
        var from = Output(BlockSocketType.MLSignal);
        var to   = Input(BlockSocketType.TradeOrder);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .WithMessage("*MLSignal*TradeOrder*");
    }

    [Fact]
    public void Validate_Exception_ExposesFromAndToTypes()
    {
        var from = Output(BlockSocketType.IndicatorValue);
        var to   = Input(BlockSocketType.CandleStream);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .Which.FromType.Should().Be(BlockSocketType.IndicatorValue);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .Which.ToType.Should().Be(BlockSocketType.CandleStream);
    }

    // ── Direction violations ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_InputToInput_ThrowsInvalidBlockConnectionException()
    {
        var from = Input(BlockSocketType.CandleStream);
        var to   = Input(BlockSocketType.CandleStream);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .WithMessage("*Output socket*");
    }

    [Fact]
    public void Validate_OutputToOutput_ThrowsInvalidBlockConnectionException()
    {
        var from = Output(BlockSocketType.CandleStream);
        var to   = Output(BlockSocketType.CandleStream);

        var act = () => BlockConnectionValidator.Validate(from, to);

        act.Should()
           .Throw<InvalidBlockConnectionException>()
           .WithMessage("*Input socket*");
    }

    // ── All BlockSocketType values have matching connections ──────────────────────

    [Fact]
    public void Validate_AllSocketTypes_CanConnectMatchingPairs()
    {
        var allTypes = Enum.GetValues<BlockSocketType>();

        foreach (var type in allTypes)
        {
            var from = Output(type);
            var to   = Input(type);

            var act = () => BlockConnectionValidator.Validate(from, to);

            act.Should().NotThrow($"all socket types must support same-type connections (failed for {type})");
        }
    }
}
