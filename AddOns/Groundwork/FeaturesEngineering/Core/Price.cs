using Groundwork.Utils;
using System;

namespace Groundwork.FeaturesEngineering.Core;

public static class Price
{
    /// <summary>
    /// Returns the normalized relationship between close and open.
    /// Positive when close > open (bullish), negative when close < open (bearish).
    /// Normalized by the bar's range (high - low).
    /// Returns value in range [-1, 1].
    /// </summary>
    public static double GetCloseOpenRelationship(in BaseBar bar, double tolerance = 1e-6)
    {
        double high = bar.High, low = bar.Low, open = bar.Open, close = bar.Close;
        double range = high - low;

        if (range < tolerance) return 0.0;

        // Normalize the close-open difference by the bar's range
        // Result: +1 when close = high and open = low (strong bullish bar)
        //         -1 when close = low and open = high (strong bearish bar)
        //          0 when close = open (doji)
        return (close - open) / range;
    }

    /// <summary>
    /// Market state index in [-1, +1] using Kaufman's Efficiency Ratio (ER).
    /// +1 = strong uptrend, 0 = range/chop, -1 = strong downtrend.
    /// No weights, no thresholds, unitless, robust across symbols/timeframes.
    /// Strong trend: |index| ≈ 0.6 – 0.95
    /// Transition: |index| ≈ 0.25 – 0.6
    /// Range/chop: |index| ≈ 0.0 – 0.25
    /// </summary>
    public static double GetMarketState(
        CircularBuffer<double> openSeries,
        CircularBuffer<double> closeSeries,
        CircularBuffer<double> atrSeries,
        double overlapWeight = 0.6,
        double tolerance = 1e-6)
    {
        if (openSeries == null || closeSeries == null)
            return 0.0;

        int n = Math.Min(openSeries.Count, closeSeries.Count);
        if (n < 2) return 0.0;

        // Use body midpoints to dampen tick noise
        double first = 0.5 * (openSeries[0] + closeSeries[0]);
        double prev = first;
        double last = prev;
        double path = 0.0;

        for (int i = 1; i < n; i++)
        {
            double mid = 0.5 * (openSeries[i] + closeSeries[i]);
            path += Math.Abs(mid - prev);  // total path length
            prev = mid;
            last = mid;
        }

        double net = last - first;                // net displacement
        if (path <= tolerance) return 0.0;        // flat window

        double er = Math.Abs(net) / path;         // 0..1
        double sign = (net >= 0.0) ? 1.0 : -1.0;  // direction
        double index = sign * er;                 // -1..+1

        // Clamp numerically
        if (double.IsNaN(index) || double.IsInfinity(index)) return 0.0;
        if (index > 1.0) index = 1.0; else if (index < -1.0) index = -1.0;
        return index;
    }
}
