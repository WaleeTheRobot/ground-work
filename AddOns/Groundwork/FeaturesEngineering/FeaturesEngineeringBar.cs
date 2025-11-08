using Groundwork.FeaturesEngineering.Core;

namespace Groundwork.FeaturesEngineering;

public readonly struct FeaturesEngineeringBar(
       in BaseBar b,
       double f_MovingAverageFastSlowDistance,
       double f_MovingAverageSlope,
       double f_CloseOpenRelationship,
       double f_marketState,
       double f_marketStateSecondary,
       double f_DeltaPressure,
       double f_CumulativeDeltaMomentum,
       double f_POCDisplacement,
       double f_VolumeDominance,
       double f_DeltaPercentage,
       double f_ValueAreaWidth,
       double f_VolumeSurge)
{
    public int Time { get; } = b.Time;
    public int Day { get; } = b.Day;
    public double Open { get; } = b.Open;
    public double High { get; } = b.High;
    public double Low { get; } = b.Low;
    public double Close { get; } = b.Close;
    public double Volume { get; } = b.Volume;
    public double ATR { get; } = b.ATR;
    public double DonchianChannelUpper { get; } = b.DonchianChannelUpper;
    public double DonchianChannelLower { get; } = b.DonchianChannelLower;
    public double ValueAreaHigh { get; } = b.ValueAreaHigh;
    public double ValueAreaLow { get; } = b.ValueAreaLow;

    // Moving Averages
    public double F_MovingAverageFastSlowDistance { get; } = f_MovingAverageFastSlowDistance;
    public double F_MovingAverageSlope { get; } = f_MovingAverageSlope;

    // Price
    public double F_CloseOpenRelationship { get; } = f_CloseOpenRelationship;
    public double F_MarketState { get; } = f_marketState;
    public double F_MarketStateSecondary { get; } = f_marketStateSecondary;

    // Volumetric
    public double F_DeltaPressure { get; } = f_DeltaPressure;
    public double F_CumulativeDeltaMomentum { get; } = f_CumulativeDeltaMomentum;
    public double F_POCDisplacement { get; } = f_POCDisplacement;
    public double F_VolumeDominance { get; } = f_VolumeDominance;
    public double F_DeltaPercentage { get; } = f_DeltaPercentage;
    public double F_ValueAreaWidth { get; } = f_ValueAreaWidth;
    public double F_VolumeSurge { get; } = f_VolumeSurge;
}
