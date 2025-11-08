using Groundwork.FeaturesEngineering.Utils;
using Groundwork.Utils;
using System;

namespace Groundwork.FeaturesEngineering.Core;

public static class MovingAverage
{
    public static double GetSlope(CircularBuffer<double> maSeries, BaseBar bar, int window, double tolerance = 1e-6)
    {
        if (maSeries == null || maSeries.Count < 2 || bar.ATR < tolerance)
            return 0.0;

        int n = maSeries.Count;
        int actualWindow = Math.Min(window, n);

        // Calculate absolute change in MA over the window
        double maStart = maSeries[n - actualWindow];
        double maEnd = maSeries[n - 1];
        double maChange = maEnd - maStart;

        // Normalize by ATR and bars
        // Divide by window to get per-bar change, then normalize by ATR
        double normalizedSlope = (maChange / actualWindow) / bar.ATR;

        // Clamp to [-1, 1] range
        // Typical strong slopes are around 0.1-0.3 ATR per bar
        return Math.Max(-1.0, Math.Min(1.0, normalizedSlope * 5.0));
    }

    public static double GetDistance(in BaseBar bar, double tolerance = 1e-6)
    {
        double fastMA = bar.MovingAverage;
        double slowMA = bar.SlowMovingAverage;
        double atr = bar.ATR;

        if (!Common.IsValidDouble(fastMA) || !Common.IsValidDouble(slowMA) || atr < tolerance)
            return 0.0;

        // Returns normalized distance: positive when fast > slow, negative when fast < slow
        // Normalized by ATR to measure MA separation relative to market volatility
        double distance = (fastMA - slowMA) / atr;

        // Clamp to [-1, 1] range
        // For 9/14 EMA, typical separation is 0.2-0.5 ATR
        // Scale by 2.5 so that 0.4 ATR separation = 1.0
        return Math.Max(-1.0, Math.Min(1.0, distance * 2.5));
    }

    public static double GetRatio(in BaseBar bar, double tolerance = 1e-6)
    {
        double fastMA = bar.MovingAverage;
        double slowMA = bar.SlowMovingAverage;

        if (!Common.IsValidDouble(fastMA) || !Common.IsValidDouble(slowMA) || Math.Abs(slowMA) < tolerance)
            return 1.0;

        return fastMA / slowMA;
    }
}
