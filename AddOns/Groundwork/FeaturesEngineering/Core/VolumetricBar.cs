using Groundwork.Utils;
using System.Collections.Generic;

namespace Groundwork.FeaturesEngineering.Core;

public readonly struct VolumetricBar(
    long totalVolume,
    long totalBuyingVolume,
    long totalSellingVolume,
    long barDelta,
    long maxDelta,
    long minDelta,
    long cumulativeDelta,
    double deltaPercentage,
    double valueAreaHigh,
    double valueAreaLow,
    double pointOfControl,
    int totalBidImbalances,
    int totalAskImbalances,
    int totalBidStackedImbalances,
    int totalAskStackedImbalances,
    List<BidAskVolume> bidAskVolumes
)
{
    public long TotalVolume { get; } = totalVolume;
    public long TotalBuyingVolume { get; } = totalBuyingVolume;
    public long TotalSellingVolume { get; } = totalSellingVolume;
    public long BarDelta { get; } = barDelta;
    public long MaxDelta { get; } = maxDelta;
    public long MinDelta { get; } = minDelta;
    public long CumulativeDelta { get; } = cumulativeDelta;
    public double DeltaPercentage { get; } = deltaPercentage;
    public double ValueAreaHigh { get; } = valueAreaHigh;
    public double ValueAreaLow { get; } = valueAreaLow;
    public double PointOfControl { get; } = pointOfControl;
    public int TotalBidImbalances { get; } = totalBidImbalances;
    public int TotalAskImbalances { get; } = totalAskImbalances;
    public int TotalBidStackedImbalances { get; } = totalBidStackedImbalances;
    public int TotalAskStackedImbalances { get; } = totalAskStackedImbalances;
    public List<BidAskVolume> BidAskVolumes { get; } = bidAskVolumes;
}
