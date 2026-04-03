using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.Designer.Configuration;
using System.Text.Json;

namespace MLS.Designer.Services;

/// <summary>
/// Dispatches <c>TRAINING_JOB_START</c> envelopes to the Shell VM via the Block Controller hub
/// and raises <see cref="ProgressReceived"/> / <see cref="JobCompleted"/> events as the
/// Shell VM streams per-epoch progress and completion back.
/// </summary>
public interface ITrainingDispatcher
{
    /// <summary>
    /// Raised for every <c>TRAINING_JOB_PROGRESS</c> envelope received from the Shell VM.
    /// Subscribe and filter by <c>JobId</c> to handle only your own training job.
    /// </summary>
    event Func<TrainingJobProgressPayload, CancellationToken, ValueTask>? ProgressReceived;

    /// <summary>
    /// Raised when a <c>TRAINING_JOB_COMPLETE</c> envelope is received from the Shell VM.
    /// Subscribe and filter by <c>JobId</c> to handle only your own training job.
    /// </summary>
    event Func<TrainingJobCompletePayload, CancellationToken, ValueTask>? JobCompleted;

    /// <summary>
    /// Serialises <paramref name="payload"/> into a <c>TRAINING_JOB_START</c> envelope and
    /// sends it to the Block Controller hub which forwards it to the Shell VM.
    /// </summary>
    /// <returns>The <c>JobId</c> embedded in <paramref name="payload"/>.</returns>
    ValueTask<Guid> DispatchJobAsync(TrainingJobStartPayload payload, CancellationToken ct);
}

/// <inheritdoc cref="ITrainingDispatcher"/>
public sealed class TrainingDispatcher(
    IOptions<DesignerOptions> _options,
    ILogger<TrainingDispatcher> _logger) : ITrainingDispatcher, IAsyncDisposable
{
    // Stable client identifier used to join the Block Controller broadcast group
    private readonly string _clientId = Guid.NewGuid().ToString("N");

    private HubConnection? _hub;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    // ── ITrainingDispatcher ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public event Func<TrainingJobProgressPayload, CancellationToken, ValueTask>? ProgressReceived;

    /// <inheritdoc/>
    public event Func<TrainingJobCompletePayload, CancellationToken, ValueTask>? JobCompleted;

    /// <inheritdoc/>
    public async ValueTask<Guid> DispatchJobAsync(TrainingJobStartPayload payload, CancellationToken ct)
    {
        await EnsureConnectedAsync(ct).ConfigureAwait(false);

        var envelope = EnvelopePayload.Create(
            MessageTypes.TrainingJobStart,
            "designer",
            payload);

        await _hub!.InvokeAsync("SendEnvelope", envelope, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "TrainingDispatcher: dispatched job {JobId} (model={ModelType})",
            payload.JobId, payload.ModelType);

        return payload.JobId;
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync().ConfigureAwait(false);
            await _hub.DisposeAsync().ConfigureAwait(false);
        }

        _connectLock.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_hub is { State: HubConnectionState.Connected })
            return;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_hub is { State: HubConnectionState.Connected })
                return;

            var hubUrl = $"{_options.Value.BlockControllerHubUrl}?clientId={_clientId}";

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Receive inbound envelopes from Block Controller (Shell VM replies)
            _hub.On<EnvelopePayload>("ReceiveEnvelope", HandleInboundEnvelopeAsync);

            await _hub.StartAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "TrainingDispatcher connected to Block Controller hub as clientId={ClientId}",
                _clientId);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task HandleInboundEnvelopeAsync(EnvelopePayload envelope)
    {
        switch (envelope.Type)
        {
            case MessageTypes.TrainingJobProgress:
            {
                var progress = Deserialise<TrainingJobProgressPayload>(envelope.Payload);
                if (progress is not null && ProgressReceived is not null)
                    await ProgressReceived(progress, CancellationToken.None).ConfigureAwait(false);
                break;
            }
            case MessageTypes.TrainingJobComplete:
            {
                var complete = Deserialise<TrainingJobCompletePayload>(envelope.Payload);
                if (complete is not null && JobCompleted is not null)
                    await JobCompleted(complete, CancellationToken.None).ConfigureAwait(false);
                break;
            }
        }
    }

    private static T? Deserialise<T>(JsonElement element) where T : class
    {
        try { return element.Deserialize<T>(); }
        catch { return null; }
    }
}
