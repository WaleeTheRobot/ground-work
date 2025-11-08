namespace Groundwork.Utils;

/// <summary>
/// Represents a trading signal derived purely from features (not bars).
/// </summary>
public readonly struct FeatureSignal
{
    /// <summary>
    /// Signal direction: +1 = Long, -1 = Short, 0 = No signal
    /// </summary>
    public int Direction { get; init; }

    /// <summary>
    /// Signal strength (0.0 to 1.0)
    /// </summary>
    public double Strength { get; init; }

    /// <summary>
    /// Confidence level (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Human-readable reason for the signal
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Individual feature contributions to the signal
    /// </summary>
    public FeatureContributions Contributions { get; init; }

    public FeatureSignal(int direction, double strength, double confidence, string reason, FeatureContributions contributions)
    {
        Direction = direction;
        Strength = strength;
        Confidence = confidence;
        Reason = reason ?? string.Empty;
        Contributions = contributions;
    }

    public static FeatureSignal NoSignal(string reason = "No signal conditions met")
        => new FeatureSignal(0, 0.0, 0.0, reason, new FeatureContributions());

    public bool HasSignal => Direction != 0;
    public bool IsLong => Direction > 0;
    public bool IsShort => Direction < 0;
}

/// <summary>
/// Tracks which features contributed to the signal and how much
/// </summary>
public readonly struct FeatureContributions
{
    public bool TemporalAlignment { get; init; }      // MA distance + slope alignment
    public bool OrderFlowAlignment { get; init; }     // Delta + volume dominance alignment
    public bool MarketEfficiency { get; init; }       // Market state indicates trend vs chop
    public bool PriceAction { get; init; }            // Close/open relationship
    public bool VolumeSurge { get; init; }            // Volume spike present
    public bool ValueAreaPosition { get; init; }      // Price vs value area

    public int TotalContributors =>
        (TemporalAlignment ? 1 : 0) +
        (OrderFlowAlignment ? 1 : 0) +
        (MarketEfficiency ? 1 : 0) +
        (PriceAction ? 1 : 0) +
        (VolumeSurge ? 1 : 0) +
        (ValueAreaPosition ? 1 : 0);
}
