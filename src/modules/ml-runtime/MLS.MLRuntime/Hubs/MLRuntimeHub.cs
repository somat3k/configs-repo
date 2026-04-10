using Microsoft.AspNetCore.SignalR;
using MLS.Core.Contracts;
using MLS.MLRuntime.Inference;

namespace MLS.MLRuntime.Hubs;

/// <summary>
/// SignalR hub for the ML Runtime module.
/// Connects on <c>/hubs/ml-runtime</c>.
/// Clients join a group per their <c>moduleId</c> or <c>clientId</c> query parameter
/// and all connections join the <c>broadcast</c> group.
/// </summary>
public sealed class MLRuntimeHub(
    IInferenceEngine _engine,
    ILogger<MLRuntimeHub> _logger) : Hub
{
    /// <summary>Hub method — client sends an envelope to ml-runtime (relayed to broadcast group).</summary>
    public Task SendEnvelope(EnvelopePayload envelope) =>
        Clients.Group("broadcast").SendAsync("ReceiveEnvelope", envelope);

    /// <summary>
    /// Hub method — run a single inference request and return the result directly.
    /// </summary>
    /// <param name="request">Inference request including model key and feature vector.</param>
    /// <param name="ct">Cancellation token provided by the SignalR framework.</param>
    /// <returns>The <see cref="InferenceResultPayload"/> produced by the ONNX session.</returns>
    public async Task<InferenceResultPayload> RunInference(
        InferenceRequestPayload request,
        CancellationToken ct = default)
        => await _engine.RunAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Hub method — streams inference results for a sequence of feature vectors.
    /// Iterates <paramref name="featureStream"/> and yields one result per input.
    /// Stops the stream on any inference error.
    /// </summary>
    /// <param name="modelKey">Model registry key to use for all inferences in the stream.</param>
    /// <param name="featureStream">Async stream of feature vectors from the client.</param>
    /// <param name="ct">Cancellation token provided by the SignalR framework.</param>
    /// <returns>An async stream of <see cref="InferenceResultPayload"/> values.</returns>
    public async IAsyncEnumerable<InferenceResultPayload> StreamInferences(
        string modelKey,
        IAsyncEnumerable<float[]> featureStream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var features in featureStream.WithCancellation(ct).ConfigureAwait(false))
        {
            var request = new InferenceRequestPayload(
                RequestId:         Guid.NewGuid(),
                ModelKey:          modelKey,
                Features:          features,
                RequesterModuleId: "hub-stream");

            InferenceResultPayload result;
            try
            {
                result = await _engine.RunAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StreamInferences: inference failed for modelKey={Key} — stopping stream.", modelKey);
                yield break;
            }

            yield return result;
        }
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var http     = Context.GetHttpContext();
        var moduleId = http?.Request.Query["moduleId"].ToString();
        var clientId = http?.Request.Query["clientId"].ToString();

        var peerId = !string.IsNullOrWhiteSpace(moduleId) ? moduleId
                   : !string.IsNullOrWhiteSpace(clientId) ? clientId
                   : null;

        if (peerId is not null)
        {
            var safeGroup = SanitisePeerId(peerId);
            await Groups.AddToGroupAsync(Context.ConnectionId, safeGroup).ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private static string SanitisePeerId(string id)
    {
        var truncated = id.Length > 64 ? id[..64] : id;
        return System.Text.RegularExpressions.Regex.Replace(truncated, @"[\r\n]", "_");
    }
}
