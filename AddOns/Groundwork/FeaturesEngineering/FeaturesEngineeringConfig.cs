namespace Groundwork.FeaturesEngineering;

public class FeaturesEngineeringConfig
{
    public double TickSize { get; set; }
    public int BarsRequiredToTrade { get; set; } // Should be the longest period for lookback
    public int LookbackPeriod { get; set; } = 9;
    public int LookbackPeriodSlow { get; set; } = 14;
}
