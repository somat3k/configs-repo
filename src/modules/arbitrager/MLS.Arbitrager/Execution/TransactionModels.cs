using System.Text.Json.Serialization;

namespace MLS.Arbitrager.Execution;

/// <summary>
/// A single on-chain swap step in a transaction array.
/// </summary>
/// <param name="SequenceIndex">Zero-based position in the execution sequence.</param>
/// <param name="FromToken">Input token symbol.</param>
/// <param name="ToToken">Output token symbol.</param>
/// <param name="AmountIn">Token amount to provide for this swap.</param>
/// <param name="MinAmountOut">Minimum acceptable output after slippage (0.5% tolerance).</param>
/// <param name="Exchange">DEX exchange identifier.</param>
/// <param name="RouterAddress">On-chain router contract address (loaded from address book).</param>
/// <param name="GasLimit">Estimated gas limit for this step.</param>
public sealed record TransactionStep(
    [property: JsonPropertyName("sequence_index")] int SequenceIndex,
    [property: JsonPropertyName("from_token")] string FromToken,
    [property: JsonPropertyName("to_token")] string ToToken,
    [property: JsonPropertyName("amount_in")] decimal AmountIn,
    [property: JsonPropertyName("min_amount_out")] decimal MinAmountOut,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("router_address")] string RouterAddress,
    [property: JsonPropertyName("gas_limit")] long GasLimit);

/// <summary>
/// An ordered sequence of swap transactions that together execute one arbitrage path.
/// Sent to the Transactions module for atomic on-chain execution.
/// </summary>
/// <param name="ArrayId">Unique transaction array identifier.</param>
/// <param name="OpportunityId">The source opportunity this array was built from.</param>
/// <param name="Steps">Ordered swap steps.</param>
/// <param name="InputAmountUsd">Notional input in USD.</param>
/// <param name="ExpectedOutputUsd">Expected total output in USD.</param>
/// <param name="ExpectedNetProfitUsd">Expected net profit after all fees and gas.</param>
/// <param name="CreatedAt">UTC time this array was constructed.</param>
/// <param name="ExpiresAt">UTC time after which the array should not be submitted on-chain.</param>
public sealed record TransactionArray(
    [property: JsonPropertyName("array_id")] Guid ArrayId,
    [property: JsonPropertyName("opportunity_id")] Guid OpportunityId,
    [property: JsonPropertyName("steps")] IReadOnlyList<TransactionStep> Steps,
    [property: JsonPropertyName("input_amount_usd")] decimal InputAmountUsd,
    [property: JsonPropertyName("expected_output_usd")] decimal ExpectedOutputUsd,
    [property: JsonPropertyName("expected_net_profit_usd")] decimal ExpectedNetProfitUsd,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
