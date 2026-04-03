using System.Text.Json;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>TRAINING_JOB_START</c> — dispatched by <c>TrainModelBlock</c>
/// to the Shell VM which spawns the Python training process.
/// <para><b>Rule</b>: <c>TrainModelBlock</c> MUST emit this envelope; it MUST NOT call Python directly.</para>
/// </summary>
/// <param name="JobId">Unique identifier for this training run.</param>
/// <param name="ModelType">Model registry key: <c>model-t</c>, <c>model-a</c>, or <c>model-d</c>.</param>
/// <param name="FeatureSchemaVersion">Must match the deployed model's expected input dimension.</param>
/// <param name="Hyperparams">Training hyperparameters (JSON object).</param>
/// <param name="DataRange">Historical data window for training.</param>
public sealed record TrainingJobStartPayload(
    Guid JobId,
    string ModelType,
    int FeatureSchemaVersion,
    JsonElement Hyperparams,
    TrainingDataRange DataRange);

/// <summary>Historical data window for a training job.</summary>
/// <param name="From">Inclusive start of the training data range (UTC).</param>
/// <param name="To">Exclusive end of the training data range (UTC).</param>
public sealed record TrainingDataRange(DateTimeOffset From, DateTimeOffset To);
