namespace Groundwork.FeaturesEngineering.Features.Models;

public readonly struct VolumetricFeatures(
    double deltaPressure,
    double cumulativeDeltaMomentum,
    double pocDisplacement,
    double volumeDominance,
    double deltaPercentage,
    double valueAreaWidth,
    double volumeSurge)
{
    // Volume-normalized delta (positive = buying, negative = selling)
    public readonly double DeltaPressure = deltaPressure;

    // Rate of cumulative delta change normalized by ATR
    public readonly double CumulativeDeltaMomentum = cumulativeDeltaMomentum;

    // Distance from close to POC normalized by ATR (positive = accepting higher, negative = lower)
    public readonly double POCDisplacement = pocDisplacement;

    // Buying vs selling volume pressure (positive = buying, negative = selling)
    public readonly double VolumeDominance = volumeDominance;

    // Delta as percentage of total volume
    public readonly double DeltaPercentage = deltaPercentage;

    // Width of value area normalized by ATR (narrow = potential coiling)
    public readonly double ValueAreaWidth = valueAreaWidth;

    // Current volume compared to recent average (positive = surge, negative = low)
    public readonly double VolumeSurge = volumeSurge;

    public static VolumetricFeatures Empty => new(0, 0, 0, 0, 0, 0, 0);
}
