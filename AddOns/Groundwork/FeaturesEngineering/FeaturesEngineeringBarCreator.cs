using Groundwork.FeaturesEngineering.Core;
using Groundwork.FeaturesEngineering.Features.Models;

namespace Groundwork.FeaturesEngineering;

public static class FeaturesEngineeringBarCreator
{
    public static FeaturesEngineeringBar Create(
        in BaseBar bar,
        in MovingAverageFeatures ma,
        in PriceFeatures price,
        in VolumetricFeatures volumetric,
        double marketStateSecondary = 0.0)
        => new FeaturesEngineeringBar(
            in bar,
            ma.FastSlowDistance,
            ma.Slope,
            price.CloseOpenRelationship,
            price.MarketState,
            marketStateSecondary,
            volumetric.DeltaPressure,
            volumetric.CumulativeDeltaMomentum,
            volumetric.POCDisplacement,
            volumetric.VolumeDominance,
            volumetric.DeltaPercentage,
            volumetric.ValueAreaWidth,
            volumetric.VolumeSurge);
}
