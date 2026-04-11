namespace MLS.Core.Transport;

/// <summary>
/// Result of envelope validation by <see cref="EnvelopeValidator"/>.
/// </summary>
/// <param name="IsValid">True when all mandatory fields pass validation.</param>
/// <param name="Errors">Empty when <see cref="IsValid"/> is true; contains one entry per validation failure otherwise.</param>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    /// <summary>A successful validation result with no errors.</summary>
    public static readonly ValidationResult Valid = new(true, Array.Empty<ValidationError>());

    /// <summary>Creates a failed result from a list of errors.</summary>
    public static ValidationResult Fail(IReadOnlyList<ValidationError> errors) =>
        new(false, errors);
}

/// <summary>
/// A single validation failure produced by <see cref="EnvelopeValidator"/>.
/// </summary>
/// <param name="Field">The envelope field that failed validation.</param>
/// <param name="Reason">Human-readable description of why validation failed.</param>
public sealed record ValidationError(string Field, string Reason);
