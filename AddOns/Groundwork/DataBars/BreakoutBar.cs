namespace Groundwork.DataBars;

public class BreakoutBar
{
    public int Time { get; set; }
    public int Day { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
    public double ATR { get; set; }
    public double DonchianChannelUpper { get; set; }
    public double DonchianChannelLower { get; set; }
    public double ValueAreaHigh { get; set; }
    public double ValueAreaLow { get; set; }

    // Moving Average Features
    public double F_MovingAverageFastSlowDistance { get; set; }
    public double F_MovingAverageSlope { get; set; }

    // Price Features
    public double F_CloseOpenRelationship { get; set; }
    public double F_MarketState { get; set; }
    public double F_MarketStateSecondary { get; set; }

    // Volumetric Features
    public double F_DeltaPressure { get; set; }
    public double F_CumulativeDeltaMomentum { get; set; }
    public double F_POCDisplacement { get; set; }
    public double F_VolumeDominance { get; set; }
    public double F_DeltaPercentage { get; set; }
    public double F_ValueAreaWidth { get; set; }
    public double F_VolumeSurge { get; set; }
}
