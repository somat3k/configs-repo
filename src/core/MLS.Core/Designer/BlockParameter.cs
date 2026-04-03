namespace MLS.Core.Designer;

/// <summary>
/// Non-generic base record for all block configuration parameters.
/// Parameters are introspectable at runtime and optionally optimisable
/// by <c>HyperparamSearchBlock</c>.
/// </summary>
/// <param name="Name">Programmatic key, e.g. <c>Period</c>.</param>
/// <param name="DisplayName">Human-readable label for the Designer UI.</param>
/// <param name="Description">Tooltip / help text shown in the Designer UI.</param>
public abstract record BlockParameter(string Name, string DisplayName, string Description);

/// <summary>
/// Strongly-typed block configuration parameter.
/// Equivalent to StockSharp's <c>StrategyParam&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">Parameter value type (e.g. <see cref="int"/>, <see cref="float"/>, <see cref="string"/>).</typeparam>
/// <param name="Name">Programmatic key.</param>
/// <param name="DisplayName">Human-readable label.</param>
/// <param name="Description">Help text.</param>
/// <param name="DefaultValue">Value applied when no override is provided.</param>
/// <param name="MinValue">Optional lower bound (used by optimisation search).</param>
/// <param name="MaxValue">Optional upper bound (used by optimisation search).</param>
/// <param name="IsOptimizable">
/// When <see langword="true"/>, this parameter is included in hyperparameter searches
/// performed by <c>HyperparamSearchBlock</c>.
/// </param>
public sealed record BlockParameter<T>(
    string Name,
    string DisplayName,
    string Description,
    T DefaultValue,
    T? MinValue = default,
    T? MaxValue = default,
    bool IsOptimizable = false
) : BlockParameter(Name, DisplayName, Description);
