using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Services;

/// <summary>
/// Background service that drives the BFS scan on a fixed periodic interval.
/// Decouples the price-feed hot path (which only updates the price dictionary) from
/// the heavier BFS traversal — keeping <see cref="IOpportunityScanner.PublishPrice"/>
/// allocation-free.
/// </summary>
public sealed class ScannerWorker(
    IOpportunityScanner _scanner,
    IOptions<ArbitragerOptions> _options,
    ILogger<ScannerWorker> _logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("ScannerWorker: started — BFS scan every {Interval}ms.",
            _options.Value.ScanIntervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.Value.ScanIntervalMs));

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                // Cast is safe: OpportunityScanner is the only concrete implementation.
                ((OpportunityScanner)_scanner).RunScan();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScannerWorker: BFS scan threw unexpectedly.");
            }
        }

        _logger.LogInformation("ScannerWorker: stopped.");
    }
}
