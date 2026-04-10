using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.MLRuntime.Configuration;
using MLS.MLRuntime.Inference;
using MLS.MLRuntime.Models;

namespace MLS.MLRuntime.Services;

/// <summary>
/// Background service that subscribes to the Block Controller SignalR hub,
/// dispatches <c>INFERENCE_REQUEST</c> messages through the inference engine,
/// and triggers model hot-reload on <c>TRAINING_JOB_COMPLETE</c>.
/// </summary>
public sealed class InferenceWorker(
    IModelRegistry _registry,
    IInferenceEngine _engine,
    IEnvelopeSender _sender,
    ModuleIdentity _identity,
    IOptions<MLRuntimeOptions> _options,
    ILogger<InferenceWorker> _logger) : BackgroundService
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts   = _options.Value;
        var hubUrl = $"{opts.BlockControllerUrl}/hubs/block-controller?moduleId={_identity.Id}";

        while (!ct.IsCancellationRequested)
        {
            var hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(ReconnectDelays)
                .Build();

            hub.On<EnvelopePayload>("ReceiveEnvelope", async envelope =>
            {
                try
                {
                    await HandleEnvelopeAsync(envelope, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error handling envelope type={Type}", envelope.Type);
                }
            });

            hub.Closed += ex =>
            {
                if (ex is not null)
                    _logger.LogWarning(ex, "Hub connection closed unexpectedly.");
                return Task.CompletedTask;
            };

            try
            {
                await hub.StartAsync(ct).ConfigureAwait(false);

                // Subscribe to message types this worker handles.
                // The Block Controller routes envelopes by subscription table, so without
                // an explicit subscription these messages would never be delivered here.
                await hub.InvokeAsync("SubscribeToTopicAsync", MessageTypes.InferenceRequest, ct)
                         .ConfigureAwait(false);
                await hub.InvokeAsync("SubscribeToTopicAsync", MessageTypes.TrainingJobComplete, ct)
                         .ConfigureAwait(false);

                _logger.LogInformation(
                    "InferenceWorker connected and subscribed to topics as moduleId={Id}", _identity.Id);

                await WaitUntilCancelledAsync(hub, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hub connection failed — retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            finally
            {
                await hub.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // ── Envelope dispatch ─────────────────────────────────────────────────────

    private async Task HandleEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct)
    {
        if (envelope.Type == MessageTypes.InferenceRequest)
            await HandleInferenceRequestAsync(envelope, ct).ConfigureAwait(false);
        else if (envelope.Type == MessageTypes.TrainingJobComplete)
            await HandleTrainingJobCompleteAsync(envelope, ct).ConfigureAwait(false);
    }

    private async Task HandleInferenceRequestAsync(EnvelopePayload envelope, CancellationToken ct)
    {
        var request = envelope.Payload.Deserialize<InferenceRequestPayload>();
        if (request is null)
        {
            _logger.LogWarning("Received INFERENCE_REQUEST with null/invalid payload.");
            return;
        }

        _logger.LogDebug("Inference request id={Id} model={Model}", request.RequestId, request.ModelKey);

        try
        {
            var result   = await _engine.RunAsync(request, ct).ConfigureAwait(false);
            var response = EnvelopePayload.Create(
                MessageTypes.InferenceResult,
                _identity.Id.ToString(),
                result);

            await _sender.SendEnvelopeAsync(response, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Inference failed — model not loaded for key={Key}", request.ModelKey);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Inference timed out for key={Key}", request.ModelKey);
        }
    }

    private async Task HandleTrainingJobCompleteAsync(EnvelopePayload envelope, CancellationToken ct)
    {
        var payload = envelope.Payload.Deserialize<TrainingJobCompletePayload>();
        if (payload is null)
        {
            _logger.LogWarning("Received TRAINING_JOB_COMPLETE with null/invalid payload.");
            return;
        }

        _logger.LogInformation(
            "Hot-reload triggered: modelType={Type} modelId={Id} path={Path}",
            payload.ModelType, payload.ModelId, payload.OnnxPath);

        try
        {
            await _registry.LoadAsync(payload.ModelType, payload.OnnxPath, payload.ModelId, ct)
                            .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Hot-reload failed — ONNX file not found at {Path}", payload.OnnxPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hot-reload failed for modelType={Type}", payload.ModelType);
        }
    }

    private static async Task WaitUntilCancelledAsync(HubConnection hub, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        hub.Closed += _ =>
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        };

        using var reg = ct.Register(() => tcs.TrySetResult());
        await tcs.Task.ConfigureAwait(false);
    }
}
