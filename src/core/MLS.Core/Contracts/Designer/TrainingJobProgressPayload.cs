using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>TRAINING_JOB_PROGRESS</c> — streamed per epoch from Shell VM to Designer.
/// The optional Optuna fields (<see cref="TrialIndex"/>, <see cref="NTrials"/>,
/// <see cref="BestValue"/>, <see cref="IsPruned"/>, <see cref="IsHyperparamSearch"/>) are
/// populated only when <c>hyperparam_search.py</c> is running a hyperparameter study.
/// Standard <c>training_pipeline.py</c> runs leave these fields <see langword="null"/>.
/// </summary>
/// <param name="JobId">Training job this progress update belongs to.</param>
/// <param name="Epoch">Current epoch number (1-based).</param>
/// <param name="TotalEpochs">Total number of epochs scheduled.</param>
/// <param name="TrainLoss">Training loss for this epoch.</param>
/// <param name="ValLoss">Validation loss for this epoch.</param>
/// <param name="Accuracy">Validation accuracy for this epoch.</param>
/// <param name="ElapsedMs">Wall-clock milliseconds elapsed since job start.</param>
/// <param name="EtaMs">Estimated remaining milliseconds.</param>
/// <param name="TrialIndex">0-based Optuna trial index (hyperparameter search only).</param>
/// <param name="NTrials">Total number of Optuna trials (hyperparameter search only).</param>
/// <param name="BestValue">Best objective value seen so far across all trials.</param>
/// <param name="IsPruned">Whether this trial was pruned by the Optuna pruner.</param>
/// <param name="IsHyperparamSearch">
/// <see langword="true"/> when the progress message originates from a hyperparameter search run.
/// </param>
public sealed record TrainingJobProgressPayload(
    [property: JsonPropertyName("job_id")] Guid JobId,
    [property: JsonPropertyName("epoch")] int Epoch,
    [property: JsonPropertyName("total_epochs")] int TotalEpochs,
    [property: JsonPropertyName("train_loss")] float TrainLoss,
    [property: JsonPropertyName("val_loss")] float ValLoss,
    [property: JsonPropertyName("accuracy")] float Accuracy,
    [property: JsonPropertyName("elapsed_ms")] long ElapsedMs,
    [property: JsonPropertyName("eta_ms")] long EtaMs,
    [property: JsonPropertyName("trial_index")] int? TrialIndex = null,
    [property: JsonPropertyName("n_trials")] int? NTrials = null,
    [property: JsonPropertyName("best_value")] float? BestValue = null,
    [property: JsonPropertyName("is_pruned")] bool? IsPruned = null,
    [property: JsonPropertyName("is_hyperparam_search")] bool? IsHyperparamSearch = null);

