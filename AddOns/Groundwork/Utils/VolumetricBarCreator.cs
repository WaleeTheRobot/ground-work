using Groundwork.FeaturesEngineering.Core;
using NinjaTrader.NinjaScript.BarsTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Groundwork.Utils;

public readonly struct BidAskVolume(double price, long bidVolume, long askVolume, bool hasBidImbalance = false, bool hasAskImbalance = false, bool isBidStacked = false, bool isAskStacked = false)
{
    public double Price { get; } = price;
    public long BidVolume { get; } = bidVolume;
    public long AskVolume { get; } = askVolume;
    public bool HasBidImbalance { get; } = hasBidImbalance;
    public bool HasAskImbalance { get; } = hasAskImbalance;
    public bool IsBidStacked { get; } = isBidStacked;
    public bool IsAskStacked { get; } = isAskStacked;
}

public readonly struct VolumetricBarParams(
    int ticksPerLevel,
    double tickSize,
    VolumetricData data,
    double high,
    double low,
    double valueAreaPercent = 0.70,
    double imbalanceRatio = 1.5,
    long imbalanceMinDelta = 10,
    int stackedImbalanceCount = 3
)
{
    public int TicksPerLevel { get; } = ticksPerLevel;
    public double TickSize { get; } = tickSize;
    public VolumetricData Data { get; } = data;
    public double High { get; } = high;
    public double Low { get; } = low;
    public double ValueAreaPercent { get; } = valueAreaPercent;

    public double ImbalanceRatio { get; } = imbalanceRatio;
    public long ImbalanceMinDelta { get; } = imbalanceMinDelta;
    public int StackedImbalanceCount { get; } = stackedImbalanceCount;
}

public static class VolumetricBarCreator
{
    public static VolumetricBar GetVolumetricBar(VolumetricBarParams values)
    {
        IReadOnlyList<BidAskVolume> bidAskVolumes = MapBidAsk(values);

        VolumetricData data = values.Data;

        long totalVolume = data.TotalVolume;
        long totalBuyingVolume = data.TotalBuyingVolume;
        long totalSellingVolume = data.TotalSellingVolume;

        long barDelta = data.BarDelta;
        long maxDelta = data.MaxSeenDelta;
        long minDelta = data.MinSeenDelta;
        long cumulativeDelta = data.CumulativeDelta;
        double deltaPercentage = data.GetDeltaPercent();

        // Value Area
        double valueAreaHigh = 0.0;
        double valueAreaLow = 0.0;

        if (bidAskVolumes.Count > 0 && values.ValueAreaPercent > 0 && values.ValueAreaPercent <= 1)
        {
            var (vah, val, _) = GetValueArea(bidAskVolumes, values.ValueAreaPercent);
            valueAreaHigh = vah;
            valueAreaLow = val;
        }

        // Use NinjaTrader's built-in POC - get price at highest combined volume
        data.GetMaximumVolume(null, out double pointOfControl);

        // Imbalances - returns updated bidAskVolumes with imbalance flags set
        var (totalBidImbalances,
             totalAskImbalances,
             totalBidStackedImbalances,
             totalAskStackedImbalances,
             bidAskVolumesWithFlags) = GetImbalances(
            bidAskVolumes,
            levelStep: values.TickSize * values.TicksPerLevel,
            ratio: values.ImbalanceRatio,
            minDelta: values.ImbalanceMinDelta,
            stackedThreshold: values.StackedImbalanceCount
         );

        return new VolumetricBar(
            totalVolume,
            totalBuyingVolume,
            totalSellingVolume,
            barDelta,
            maxDelta,
            minDelta,
            cumulativeDelta,
            deltaPercentage,
            valueAreaHigh,
            valueAreaLow,
            pointOfControl,
            totalBidImbalances,
            totalAskImbalances,
            totalBidStackedImbalances,
            totalAskStackedImbalances,
            bidAskVolumesWithFlags
        );
    }

    private static IReadOnlyList<BidAskVolume> MapBidAsk(VolumetricBarParams values)
    {
        int ticksPerLevel = values.TicksPerLevel;
        double tickSize = values.TickSize;
        VolumetricData data = values.Data;
        double high = values.High;
        double low = values.Low;

        var list = new List<BidAskVolume>();
        int totalLevels = 0;
        int counter = 0;

        while (high >= low)
        {
            if (counter == 0)
            {
                list.Add(new BidAskVolume(
                    high,
                    data.GetBidVolumeForPrice(high),
                    data.GetAskVolumeForPrice(high)
                ));
            }

            counter = (counter == ticksPerLevel - 1) ? 0 : counter + 1;

            totalLevels++;
            high -= tickSize;
        }

        if (totalLevels % ticksPerLevel > 0 && list.Count > 4)
            list.RemoveAt(0);

        return list;
    }

    public static (double ValueAreaHigh, double ValueAreaLow, double PointOfControl)
        GetValueArea(IReadOnlyList<BidAskVolume> bidAskVolumeList, double valueAreaPercent)
    {
        if (bidAskVolumeList == null || bidAskVolumeList.Count == 0 || valueAreaPercent <= 0)
            return (0.0, 0.0, 0.0);

        var sorted = bidAskVolumeList.OrderBy(v => v.Price).ToList();

        var poc = sorted.OrderByDescending(v => v.BidVolume + v.AskVolume).First();
        double pocPrice = poc.Price;

        long totalVolume = sorted.Sum(v => v.BidVolume + v.AskVolume);
        long targetVolume = (long)Math.Round(totalVolume * valueAreaPercent, MidpointRounding.AwayFromZero);

        long currentVolume = poc.BidVolume + poc.AskVolume;
        int lowerIndex = sorted.IndexOf(poc);
        int upperIndex = lowerIndex;

        while (currentVolume < targetVolume && (lowerIndex > 0 || upperIndex < sorted.Count - 1))
        {
            long lowerVolume = (lowerIndex > 0) ? sorted[lowerIndex - 1].BidVolume + sorted[lowerIndex - 1].AskVolume : long.MinValue;
            long upperVolume = (upperIndex < sorted.Count - 1) ? sorted[upperIndex + 1].BidVolume + sorted[upperIndex + 1].AskVolume : long.MinValue;

            if (lowerVolume == long.MinValue && upperVolume == long.MinValue) break;

            if (lowerVolume >= upperVolume && lowerIndex > 0)
            {
                currentVolume += lowerVolume;
                lowerIndex--;
            }
            else if (upperIndex < sorted.Count - 1)
            {
                currentVolume += upperVolume;
                upperIndex++;
            }
            else break;
        }

        double val = (lowerIndex >= 0 && lowerIndex < sorted.Count) ? sorted[lowerIndex].Price : pocPrice;
        double vah = (upperIndex >= 0 && upperIndex < sorted.Count) ? sorted[upperIndex].Price : pocPrice;

        return (vah, val, pocPrice);
    }

    public static (
        int BidImbalances,
        int AskImbalances,
        int BidStackedImbalances,
        int AskStackedImbalances,
        List<BidAskVolume> UpdatedLevels
    ) GetImbalances(
        IReadOnlyList<BidAskVolume> levels,
        double levelStep,
        double ratio,
        long minDelta,
        int stackedThreshold
    )
    {
        if (levels == null || levels.Count == 0)
            return (0, 0, 0, 0, new List<BidAskVolume>());

        int n = levels.Count;

        // First pass: mark ask/bid imbalances and capture volumes/prices
        var askFlags = new bool[n];
        var bidFlags = new bool[n];
        var prices = new double[n];

        for (int i = 0; i < n; i++)
        {
            prices[i] = levels[i].Price;

            // Ask imbalance at i vs diagonal Bid at i+1 (skip if at bottom boundary)
            long ask = levels[i].AskVolume;
            bool hasAskImbalance = false;

            if (i < n - 1) // Only check if there's a diagonal level below
            {
                long diagBid = levels[i + 1].BidVolume;
                bool askPassDelta = (ask - diagBid) >= minDelta;
                bool askPassRatio = diagBid == 0 ? ask >= (long)Math.Ceiling(ratio) : (double)ask / diagBid >= ratio;
                hasAskImbalance = askPassDelta && askPassRatio;
            }

            // Bid imbalance at i vs diagonal Ask at i-1 (skip if at top boundary)
            long bid = levels[i].BidVolume;
            bool hasBidImbalance = false;

            if (i > 0) // Only check if there's a diagonal level above
            {
                long diagAsk = levels[i - 1].AskVolume;
                bool bidPassDelta = (bid - diagAsk) >= minDelta;
                bool bidPassRatio = diagAsk == 0 ? bid >= (long)Math.Ceiling(ratio) : (double)bid / diagAsk >= ratio;
                hasBidImbalance = bidPassDelta && bidPassRatio;
            }

            // If both pass, choose the stronger imbalance (higher delta)
            if (hasAskImbalance && hasBidImbalance)
            {
                long diagBidForCompare = (i < n - 1) ? levels[i + 1].BidVolume : 0;
                long diagAskForCompare = (i > 0) ? levels[i - 1].AskVolume : 0;
                long askDelta = ask - diagBidForCompare;
                long bidDelta = bid - diagAskForCompare;

                if (askDelta > bidDelta)
                {
                    hasBidImbalance = false; // Ask wins
                }
                else
                {
                    hasAskImbalance = false; // Bid wins
                }
            }

            if (hasAskImbalance)
            {
                askFlags[i] = true;
            }

            if (hasBidImbalance)
            {
                bidFlags[i] = true;
            }
        }

        int bidCount = bidFlags.Count(b => b);
        int askCount = askFlags.Count(a => a);

        // Second pass: compute stacked runs and mark stacked flags
        var bidStackedFlags = new bool[n];
        var askStackedFlags = new bool[n];
        int bidStackCount = MarkStacks(bidFlags, bidStackedFlags, prices, levelStep, stackedThreshold);
        int askStackCount = MarkStacks(askFlags, askStackedFlags, prices, levelStep, stackedThreshold);

        // Third pass: create updated BidAskVolume list with flags
        var updatedLevels = new List<BidAskVolume>(n);
        for (int i = 0; i < n; i++)
        {
            updatedLevels.Add(new BidAskVolume(
                levels[i].Price,
                levels[i].BidVolume,
                levels[i].AskVolume,
                bidFlags[i],
                askFlags[i],
                bidStackedFlags[i],
                askStackedFlags[i]
            ));
        }

        return (bidCount, askCount, bidStackCount, askStackCount, updatedLevels);
    }

    private static int MarkStacks(
        bool[] flags,
        bool[] stackedFlags,
        double[] prices,
        double levelStep,
        int threshold
    )
    {
        if (flags.Length == 0) return 0;

        double eps = levelStep * 1e-6;
        int n = flags.Length;

        int stackedCount = 0;   // Number of flagged levels that belong to runs meeting the threshold

        int runStart = -1;
        int runLen = 0;
        double lastPrice = double.NaN;

        for (int i = 0; i < n; i++)
        {
            if (!flags[i])
            {
                // End of run - mark if it meets threshold
                if (runLen >= threshold)
                {
                    for (int j = runStart; j < runStart + runLen; j++)
                        stackedFlags[j] = true;
                    stackedCount += runLen;
                }
                runStart = -1;
                runLen = 0;
                lastPrice = double.NaN;
                continue;
            }

            bool contiguous = double.IsNaN(lastPrice) || Math.Abs(prices[i] - lastPrice) <= (levelStep + eps);
            if (!contiguous)
            {
                // End of previous run - mark if it meets threshold
                if (runLen >= threshold)
                {
                    for (int j = runStart; j < runStart + runLen; j++)
                        stackedFlags[j] = true;
                    stackedCount += runLen;
                }
                runStart = i;
                runLen = 0;
            }

            if (runStart == -1) runStart = i;
            runLen++;
            lastPrice = prices[i];
        }

        // Close tail run - mark if it meets threshold
        if (runLen >= threshold)
        {
            for (int j = runStart; j < runStart + runLen; j++)
                stackedFlags[j] = true;
            stackedCount += runLen;
        }

        return stackedCount;
    }
}
