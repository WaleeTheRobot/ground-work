using Groundwork.FeaturesEngineering.Core;
using Groundwork.FeaturesEngineering.Features.Models;
using Groundwork.Utils;

namespace Groundwork.FeaturesEngineering.Extractors;

public static class PriceExtractor
{
    public static PriceFeatures Extract(
        FeaturesEngineeringConfig config,
        BaseBar last,
        CircularBuffer<double> openSeries,
        CircularBuffer<double> highSeries,
        CircularBuffer<double> lowSeries,
        CircularBuffer<double> closeSeries,
        CircularBuffer<double> atrSeries)
    {
        if (openSeries?.Count == 0 ||
            highSeries?.Count == 0 ||
            lowSeries?.Count == 0 ||
            closeSeries?.Count == 0 ||
            atrSeries?.Count == 0)
            return new PriceFeatures();

        var closeOpenRelationship = Price.GetCloseOpenRelationship(last);
        var marketState = Price.GetMarketState(openSeries, closeSeries, atrSeries);

        return new PriceFeatures(closeOpenRelationship, marketState);
    }
}
