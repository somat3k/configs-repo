using MLS.Arbitrager.Execution;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Services;

/// <summary>
/// Background service that drains the opportunity channel from <see cref="IOpportunityScanner"/>
/// and dispatches each opportunity through <see cref="IArbitrageExecutor"/>.
/// </summary>
public sealed class ExecutorPipeline(
    IOpportunityScanner _scanner,
    IArbitrageExecutor _executor,
    ILogger<ExecutorPipeline> _logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("ExecutorPipeline: started — draining opportunity channel.");

        await foreach (var opportunity in _scanner.Opportunities.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await _executor.ExecuteAsync(opportunity, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecutorPipeline: unhandled error processing opportunity {Id}.",
                    opportunity.OpportunityId);
            }
        }

        _logger.LogInformation("ExecutorPipeline: stopped.");
    }
}
