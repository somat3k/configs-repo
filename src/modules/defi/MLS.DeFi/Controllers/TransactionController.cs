using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;
using MLS.DeFi.Persistence;

namespace MLS.DeFi.Controllers;

/// <summary>
/// HTTP API for DeFi transaction history queries.
/// Base path: <c>/api/defi/transactions</c>.
/// </summary>
[ApiController]
[Route("api/defi/transactions")]
public sealed class TransactionController(
    IDbContextFactory<DeFiDbContext> _dbFactory,
    ILogger<TransactionController> _logger) : ControllerBase
{
    /// <summary>
    /// GET /api/defi/transactions/{clientOrderId} — returns a single transaction.
    /// </summary>
    [HttpGet("{clientOrderId}")]
    public async Task<IActionResult> GetTransaction(string clientOrderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
            return BadRequest(new { error = "clientOrderId is required" });

        await using var db   = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo             = new TransactionRepository(db);
        var entity           = await repo.GetByClientOrderIdAsync(clientOrderId, ct)
                                          .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogDebug("Transaction '{ClientOrderId}' not found", DeFiUtils.SafeLog(clientOrderId));
            return NotFound(new { error = $"Transaction '{DeFiUtils.SafeLog(clientOrderId)}' not found" });
        }

        return Ok(entity);
    }

    /// <summary>
    /// GET /api/defi/transactions/open — streams all open/pending transactions.
    /// </summary>
    [HttpGet("open")]
    public async IAsyncEnumerable<TransactionEntity> GetOpenTransactions(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo           = new TransactionRepository(db);

        await foreach (var tx in repo.GetOpenTransactionsAsync(ct).ConfigureAwait(false))
            yield return tx;
    }
}
