namespace Groundwork.FeaturesEngineering.Core;

public struct BaseBar
{
    public int Time { get; set; }
    public int Day { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
    public double MovingAverage { get; set; }
    public double SlowMovingAverage { get; set; }
    public double ATR { get; set; }
    public double DonchianChannelUpper { get; set; }
    public double DonchianChannelLower { get; set; }
    public double ValueAreaHigh { get; set; }
    public double ValueAreaLow { get; set; }
}
