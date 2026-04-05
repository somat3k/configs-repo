using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MLS.AIHub.Canvas;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes ML Runtime operations to the AI:
/// model training, metrics retrieval, and model deployment.
/// </summary>
public sealed class MLRuntimePlugin(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ICanvasActionDispatcher _canvasDispatcher,
    ILogger<MLRuntimePlugin> _logger)
{
    /// <summary>
    /// List all registered ML models with their current state and latest accuracy metrics.
    /// </summary>
    [KernelFunction, Description("List all registered ML models (model-t/a/d) with their training state and accuracy metrics")]
    public async Task<string> ListModels(CancellationToken ct = default)
    {
        try
        {
            using var client = CreateMlRuntimeClient();
            var models = await client.GetFromJsonAsync<List<ModelSummaryDto>>("/api/models", ct)
                                     .ConfigureAwait(false) ?? [];

            if (models.Count == 0)
                return "No ML models registered.";

            var lines = models.Select(m =>
                $"  {m.ModelId,-10} | Type: {m.ModelType,-12} | State: {m.State,-12} | Accuracy: {m.Accuracy * 100:F1}% | Last trained: {m.LastTrained?.ToString("yyyy-MM-dd") ?? "never"}");

            return $"Registered models ({models.Count}):\n" + string.Join("\n", lines);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MLRuntimePlugin.ListModels failed");
            return "Unable to retrieve model list — ML Runtime may be unavailable.";
        }
    }

    /// <summary>
    /// Get detailed training metrics for a specific model, including per-class metrics and training history.
    /// </summary>
    [KernelFunction, Description("Get detailed training metrics for a specific ML model, including accuracy, F1, precision, recall, and training history")]
    public async Task<string> GetModelMetrics(
        [Description("Authenticated user identifier used to route the metrics panel to the correct canvas")] Guid userId,
        [Description("Model identifier: 'model-t' (trading), 'model-a' (arbitrage), 'model-d' (defi), or a full GUID")] string modelId,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return "A valid userId is required to open the metrics panel on the canvas.";
        try
        {
            using var client = CreateMlRuntimeClient();
            var metrics = await client
                .GetFromJsonAsync<ModelMetricsDto>(
                    $"/api/models/{Uri.EscapeDataString(modelId)}/metrics", ct)
                .ConfigureAwait(false);

            if (metrics is null)
                return $"No metrics found for model '{modelId}'.";

            // Open metrics panel on canvas
            var panelData = JsonSerializer.SerializeToElement(metrics);
            await _canvasDispatcher.DispatchAsync(
                new OpenPanelAction("ModelMetrics", panelData, $"Metrics: {modelId}"),
                userId, ct).ConfigureAwait(false);

            return $"Metrics for '{modelId}':\n" +
                   $"  Accuracy:  {metrics.Accuracy:P2}\n" +
                   $"  F1 macro:  {metrics.F1Macro:F4}\n" +
                   $"  Precision: {metrics.Precision:F4}\n" +
                   $"  Recall:    {metrics.Recall:F4}\n" +
                   $"  Classes:   {metrics.NumClasses}\n" +
                   $"  Epochs:    {metrics.EpochsCompleted}\n" +
                   $"  State:     {metrics.State}\n" +
                   "Full metrics panel opened on canvas.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Model '{modelId}' not found in ML Runtime.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MLRuntimePlugin.GetModelMetrics failed for {Id}", modelId);
            return $"Failed to retrieve metrics for model '{modelId}'.";
        }
    }

    /// <summary>
    /// Start a training job for a model by dispatching a TRAINING_JOB_START envelope via Designer.
    /// Training runs asynchronously on the Shell VM.
    /// </summary>
    [KernelFunction, Description("Start a new training job for an ML model. Training runs asynchronously on the Shell VM. Use GetModelMetrics to check progress.")]
    public async Task<string> TrainModel(
        [Description("Model type to train: 'trading' (model-t), 'arbitrage' (model-a), or 'defi' (model-d)")] string modelType,
        [Description("Number of training epochs (default 50, max 500)")] int epochs = 50,
        [Description("Number of classes for the model output (2 for binary, 3 for null-zone)")] int numClasses = 2,
        [Description("Set to true only after the user has confirmed they want to start training.")] bool confirmed = false,
        CancellationToken ct = default)
    {
        if (!confirmed)
            return $"Please confirm: start {modelType} model training ({epochs} epochs, {numClasses} classes). Reply 'confirm training' to proceed.";

        epochs    = Math.Clamp(epochs, 1, 500);
        numClasses = Math.Clamp(numClasses, 2, 3);

        try
        {
            using var client = CreateDesignerClient();
            var request = new { model_type = modelType.ToLowerInvariant(), epochs, num_classes = numClasses };
            var response = await client.PostAsJsonAsync("/api/training/start", request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var job = await response.Content.ReadFromJsonAsync<TrainingJobDto>(ct).ConfigureAwait(false);
            return job is null
                ? $"Training job started for {modelType} model."
                : $"Training job started:\n" +
                  $"  Job ID:  {job.JobId}\n" +
                  $"  Model:   {job.ModelType}\n" +
                  $"  Epochs:  {job.Epochs}\n" +
                  $"  Classes: {job.NumClasses}\n" +
                  "Training progress will stream via TRAINING_JOB_PROGRESS envelopes.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MLRuntimePlugin.TrainModel failed for {Type}", modelType);
            return $"Failed to start training for {modelType} model.";
        }
    }

    /// <summary>
    /// Deploy a trained model to the ML Runtime so it begins serving live inference requests.
    /// </summary>
    [KernelFunction, Description("Deploy a trained ML model to live inference. The model must have state 'Complete' or 'ExportReady'.")]
    public async Task<string> DeployModel(
        [Description("Model identifier to deploy: 'model-t', 'model-a', 'model-d', or a full GUID")] string modelId,
        [Description("Set to true only after the user has confirmed the deployment.")] bool confirmed = false,
        CancellationToken ct = default)
    {
        if (!confirmed)
            return $"Please confirm: deploy model '{modelId}' to live inference. Reply 'confirm deploy' to proceed.";

        try
        {
            using var client = CreateMlRuntimeClient();
            var response = await client
                .PostAsJsonAsync($"/api/models/{Uri.EscapeDataString(modelId)}/deploy", new { }, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DeployResultDto>(ct).ConfigureAwait(false);
            return result is null
                ? $"Model '{modelId}' deployed successfully."
                : $"Model '{modelId}' deployed.\n" +
                  $"  State:    {result.State}\n" +
                  $"  Endpoint: {result.InferenceEndpoint}";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Model '{modelId}' not found.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return $"Model '{modelId}' is not in a deployable state (must be Complete or ExportReady).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "MLRuntimePlugin.DeployModel failed for {Id}", modelId);
            return $"Failed to deploy model '{modelId}'.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateMlRuntimeClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.MlRuntimeUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    private HttpClient CreateDesignerClient()
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_options.Value.DesignerUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record ModelSummaryDto(
        [property: JsonPropertyName("model_id")]   string ModelId,
        [property: JsonPropertyName("model_type")] string ModelType,
        [property: JsonPropertyName("state")]      string State,
        [property: JsonPropertyName("accuracy")]   float Accuracy,
        [property: JsonPropertyName("last_trained")] DateTimeOffset? LastTrained);

    private sealed record ModelMetricsDto(
        [property: JsonPropertyName("model_id")]        string ModelId,
        [property: JsonPropertyName("state")]           string State,
        [property: JsonPropertyName("accuracy")]        float Accuracy,
        [property: JsonPropertyName("f1_macro")]        float F1Macro,
        [property: JsonPropertyName("precision")]       float Precision,
        [property: JsonPropertyName("recall")]          float Recall,
        [property: JsonPropertyName("num_classes")]     int NumClasses,
        [property: JsonPropertyName("epochs_completed")] int EpochsCompleted);

    private sealed record TrainingJobDto(
        [property: JsonPropertyName("job_id")]     Guid JobId,
        [property: JsonPropertyName("model_type")] string ModelType,
        [property: JsonPropertyName("epochs")]     int Epochs,
        [property: JsonPropertyName("num_classes")] int NumClasses);

    private sealed record DeployResultDto(
        [property: JsonPropertyName("state")]             string State,
        [property: JsonPropertyName("inference_endpoint")] string InferenceEndpoint);
}
