using Groundwork.FeaturesEngineering.Core;
using Groundwork.FeaturesEngineering.Features.Models;
using Groundwork.Utils;
using System;
using System.Linq;

namespace Groundwork.FeaturesEngineering.Extractors;

public static class VolumetricExtractor
{
    public static VolumetricFeatures Extract(
        FeaturesEngineeringConfig config,
        BaseBar currentBar,
        CircularBuffer<VolumetricBar> volumetricSeries)
    {
        if (volumetricSeries?.Count == 0)
            return VolumetricFeatures.Empty;

        // volumetricSeries[Count-1] = Most recent CLOSED bar (previous bar, not current forming bar)
        // volumetricSeries[0] = Oldest bar in buffer
        var latestClosedBar = volumetricSeries[volumetricSeries.Count - 1];
        double atr = currentBar.ATR;

        // Prevent division by zero
        if (atr <= 0)
            atr = 0.0001;

        long totalVolume = latestClosedBar.TotalVolume;
        if (totalVolume == 0)
            totalVolume = 1; // Prevent division by zero

        // Imbalance features removed - too sparse and not providing good signals

        // 3. Delta Pressure - normalized delta
        double deltaPressure = latestClosedBar.BarDelta / (double)totalVolume;

        // 4. Cumulative Delta Momentum - rate of delta change normalized by ATR and capped
        double cumulativeDeltaMomentum = 0.0;
        if (volumetricSeries.Count >= 2)
        {
            var previousBar = volumetricSeries[volumetricSeries.Count - 2]; // Second to last
            double deltaChange = latestClosedBar.CumulativeDelta - previousBar.CumulativeDelta;
            cumulativeDeltaMomentum = deltaChange / atr;

            // Cap to [-3, 3] to prevent extreme values from dominating
            cumulativeDeltaMomentum = Math.Max(-3.0, Math.Min(3.0, cumulativeDeltaMomentum));
        }

        // 6. POC Displacement - price acceptance relative to high volume node
        double pocDisplacement = (currentBar.Close - latestClosedBar.PointOfControl) / atr;

        // 7. Volume Dominance - buying vs selling pressure
        double volumeDominance = (latestClosedBar.TotalBuyingVolume - latestClosedBar.TotalSellingVolume) / (double)totalVolume;

        // 9. Delta Percentage - use DeltaPressure instead (already calculated, same as #3)
        // DeltaPercentage from VolumetricBar can have extreme values, DeltaPressure is better normalized
        double deltaPercentage = deltaPressure; // Already in [-1, 1] range

        // 10. Value Area Width - compression indicator (normalized by ATR)
        double valueAreaWidth = (latestClosedBar.ValueAreaHigh - latestClosedBar.ValueAreaLow) / atr;

        // 11. Volume Surge - compare to recent average
        double avgVolume = CalculateAverageVolume(volumetricSeries, config.LookbackPeriod);
        double volumeSurge = avgVolume > 0
            ? (latestClosedBar.TotalVolume - avgVolume) / avgVolume
            : 0.0;

        return new VolumetricFeatures(
            deltaPressure,
            cumulativeDeltaMomentum,
            pocDisplacement,
            volumeDominance,
            deltaPercentage,
            valueAreaWidth,
            volumeSurge
        );
    }

    private static double CalculateAverageVolume(CircularBuffer<VolumetricBar> volumetricSeries, int lookback)
    {
        int count = Math.Min(lookback, volumetricSeries.Count);
        if (count == 0) return 0.0;

        long sum = 0;
        for (int i = 0; i < count; i++)
        {
            sum += volumetricSeries[i].TotalVolume;
        }

        return (double)sum / count;
    }
}
