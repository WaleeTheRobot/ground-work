namespace Groundwork.Utils;

/// <summary>
/// Configuration for feature-based signal thresholds.
/// All thresholds are based on normalized feature values [-1, 1].
/// </summary>
public class FeatureSignalConfig
{
    // === Temporal (Moving Average) Thresholds ===

    /// <summary>
    /// Minimum MA distance for trend confirmation (default: 0.3)
    /// Positive = fast above slow, Negative = fast below slow
    /// </summary>
    public double MinMADistance { get; set; } = 0.3;

    /// <summary>
    /// Minimum slope for momentum confirmation (default: 0.2)
    /// Positive = upward momentum, Negative = downward momentum
    /// </summary>
    public double MinSlope { get; set; } = 0.2;

    // === Order Flow (Volumetric) Thresholds ===

    /// <summary>
    /// Minimum delta pressure for order flow confirmation (default: 0.4)
    /// Positive = buying pressure, Negative = selling pressure
    /// </summary>
    public double MinDeltaPressure { get; set; } = 0.4;

    /// <summary>
    /// Minimum volume dominance (default: 0.3)
    /// </summary>
    public double MinVolumeDominance { get; set; } = 0.3;

    /// <summary>
    /// Minimum volume surge for entry (default: 0.5)
    /// </summary>
    public double MinVolumeSurge { get; set; } = 0.5;

    /// <summary>
    /// Cumulative delta momentum threshold for exit (default: -1.5)
    /// </summary>
    public double ExitCumulativeDeltaMomentum { get; set; } = -1.5;

    // === Market State (Efficiency) Thresholds ===

    /// <summary>
    /// Minimum market state for strong trend (default: 0.6)
    /// Values closer to 1.0 = strong trend, closer to 0.0 = chop
    /// </summary>
    public double MinMarketStateTrend { get; set; } = 0.6;

    /// <summary>
    /// Maximum market state for chop/range (default: 0.25)
    /// Below this = choppy market, exit or avoid entry
    /// </summary>
    public double MaxMarketStateChop { get; set; } = 0.25;

    // === Price Action Thresholds ===

    /// <summary>
    /// Minimum close/open relationship for bullish bar (default: 0.5)
    /// </summary>
    public double MinCloseOpenBullish { get; set; } = 0.5;

    /// <summary>
    /// Maximum close/open relationship for bearish bar (default: -0.5)
    /// </summary>
    public double MaxCloseOpenBearish { get; set; } = -0.5;

    // === Value Area Thresholds ===

    /// <summary>
    /// POC displacement for rejection/exit (default: -1.5 ATR)
    /// </summary>
    public double ExitPOCDisplacement { get; set; } = -1.5;

    // === Exit Thresholds ===

    /// <summary>
    /// Slope reversal threshold for exit (default: -0.3)
    /// </summary>
    public double ExitSlopeReversal { get; set; } = -0.3;

    /// <summary>
    /// Delta pressure reversal for exit (default: -0.4)
    /// </summary>
    public double ExitDeltaPressureReversal { get; set; } = -0.4;

    // === Confirmation Requirements ===

    /// <summary>
    /// Minimum number of feature categories that must align for entry (default: 3)
    /// Categories: Temporal, OrderFlow, MarketEfficiency, PriceAction, VolumeSurge, ValueArea
    /// </summary>
    public int MinConfirmingCategories { get; set; } = 3;

    /// <summary>
    /// Require secondary timeframe alignment (default: true)
    /// </summary>
    public bool RequireSecondaryAlignment { get; set; } = true;
}
