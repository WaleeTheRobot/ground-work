using Groundwork.FeaturesEngineering;

namespace Groundwork.DataBars;

public static class BarMapper
{
    public static BreakoutBar ToBreakoutBar(FeaturesEngineeringBar source)
    {
        return new BreakoutBar
        {
            Time = source.Time,
            Day = source.Day,
            Open = source.Open,
            High = source.High,
            Low = source.Low,
            Close = source.Close,
            Volume = source.Volume,
            ATR = source.ATR,
            DonchianChannelUpper = source.DonchianChannelUpper,
            DonchianChannelLower = source.DonchianChannelLower,
            ValueAreaHigh = source.ValueAreaHigh,
            ValueAreaLow = source.ValueAreaLow,
            F_MovingAverageFastSlowDistance = source.F_MovingAverageFastSlowDistance,
            F_MovingAverageSlope = source.F_MovingAverageSlope,
            F_CloseOpenRelationship = source.F_CloseOpenRelationship,
            F_MarketState = source.F_MarketState,
            F_MarketStateSecondary = source.F_MarketStateSecondary,
            F_DeltaPressure = source.F_DeltaPressure,
            F_CumulativeDeltaMomentum = source.F_CumulativeDeltaMomentum,
            F_POCDisplacement = source.F_POCDisplacement,
            F_VolumeDominance = source.F_VolumeDominance,
            F_DeltaPercentage = source.F_DeltaPercentage,
            F_ValueAreaWidth = source.F_ValueAreaWidth,
            F_VolumeSurge = source.F_VolumeSurge,
        };
    }

    public static RegimeBar ToRegimeBar(FeaturesEngineeringBar source)
    {
        return new RegimeBar
        {
            Time = source.Time,
            Day = source.Day,
            Open = source.Open,
            High = source.High,
            Low = source.Low,
            Close = source.Close,
            Volume = source.Volume,
            ATR = source.ATR,
        };
    }
}
