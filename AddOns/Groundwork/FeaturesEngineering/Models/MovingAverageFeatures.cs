namespace Groundwork.FeaturesEngineering.Features.Models;

public readonly struct MovingAverageFeatures(
    double fastSlowDistance,
    double fastSlope)
{
    // Normalized distance between fast and slow MA
    // Positive when fast > slow, negative when fast < slow
    public readonly double FastSlowDistance = fastSlowDistance;
    // Normalized slope of fast MA (positive = uptrend, negative = downtrend)
    public readonly double Slope = fastSlope;

    public static MovingAverageFeatures Empty => new(0, 0);
}
