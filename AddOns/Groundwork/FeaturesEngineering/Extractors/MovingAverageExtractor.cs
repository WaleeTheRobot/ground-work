using Groundwork.FeaturesEngineering.Core;
using Groundwork.FeaturesEngineering.Features.Models;
using Groundwork.Utils;

namespace Groundwork.FeaturesEngineering.Extractors;

public static class MovingAverageExtractor
{
    public static MovingAverageFeatures Extract(
        FeaturesEngineeringConfig config,
        BaseBar last,
        CircularBuffer<double> maFastSeries)
    {
        if (maFastSeries?.Count == 0)
            return new MovingAverageFeatures();

        double fastSlowDistance = MovingAverage.GetDistance(last);
        // Calculate slope normalized by ATR using fast MA window
        double fastSlope = MovingAverage.GetSlope(maFastSeries, last, config.LookbackPeriod);

        return new MovingAverageFeatures(fastSlowDistance, fastSlope);
    }
}
