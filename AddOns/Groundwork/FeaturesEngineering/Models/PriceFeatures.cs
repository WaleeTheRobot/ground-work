namespace Groundwork.FeaturesEngineering.Features.Models;

public readonly struct PriceFeatures(
    double closeOpenRelationship,
    double marketState)
{
    // Normalized relationship between close and open
    // Positive = bullish bar, negative = bearish bar
    public readonly double CloseOpenRelationship = closeOpenRelationship;

    public readonly double MarketState = marketState;

    public static PriceFeatures Empty => new PriceFeatures(0, 0);
}
