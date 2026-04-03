namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>TRAINING_JOB_PROGRESS</c> — streamed per epoch from Shell VM to Designer.
/// </summary>
/// <param name="JobId">Training job this progress update belongs to.</param>
/// <param name="Epoch">Current epoch number (1-based).</param>
/// <param name="TotalEpochs">Total number of epochs scheduled.</param>
/// <param name="TrainLoss">Training loss for this epoch.</param>
/// <param name="ValLoss">Validation loss for this epoch.</param>
/// <param name="Accuracy">Validation accuracy for this epoch.</param>
/// <param name="ElapsedMs">Wall-clock milliseconds elapsed since job start.</param>
/// <param name="EtaMs">Estimated remaining milliseconds.</param>
public sealed record TrainingJobProgressPayload(
    Guid JobId,
    int Epoch,
    int TotalEpochs,
    float TrainLoss,
    float ValLoss,
    float Accuracy,
    long ElapsedMs,
    long EtaMs);
