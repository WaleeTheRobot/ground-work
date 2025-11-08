using Groundwork.FeaturesEngineering;
using System;
using System.Text;

namespace Groundwork.Utils;

/// <summary>
/// Platform-agnostic feature-based signal evaluator.
/// Generates entry/exit signals based purely on derived features, not bars.
/// </summary>
public class FeatureSignalEvaluator
{
    private readonly FeatureSignalConfig _config;

    public FeatureSignalEvaluator(FeatureSignalConfig config = null)
    {
        _config = config ?? new FeatureSignalConfig();
    }

    /// <summary>
    /// Evaluate a single bar for entry signals.
    /// </summary>
    public FeatureSignal EvaluateEntry(FeaturesEngineeringBar bar)
    {
        var contributions = new FeatureContributions();
        var reasons = new StringBuilder();
        int confirmedCategories = 0;

        // === 1. Temporal Analysis (MA Distance + Slope) ===
        bool temporalBullish = bar.F_MovingAverageFastSlowDistance > _config.MinMADistance &&
                               bar.F_MovingAverageSlope > _config.MinSlope;

        bool temporalBearish = bar.F_MovingAverageFastSlowDistance < -_config.MinMADistance &&
                               bar.F_MovingAverageSlope < -_config.MinSlope;

        if (temporalBullish || temporalBearish)
        {
            contributions = contributions with { TemporalAlignment = true };
            confirmedCategories++;
            reasons.Append($"Temporal({(temporalBullish ? "Bullish" : "Bearish")}:Dist={bar.F_MovingAverageFastSlowDistance:F2},Slope={bar.F_MovingAverageSlope:F2}) ");
        }

        // === 2. Order Flow (Delta + Volume Dominance) ===
        bool orderFlowBullish = bar.F_DeltaPressure > _config.MinDeltaPressure &&
                                bar.F_VolumeDominance > _config.MinVolumeDominance;

        bool orderFlowBearish = bar.F_DeltaPressure < -_config.MinDeltaPressure &&
                                bar.F_VolumeDominance < -_config.MinVolumeDominance;

        if (orderFlowBullish || orderFlowBearish)
        {
            contributions = contributions with { OrderFlowAlignment = true };
            confirmedCategories++;
            reasons.Append($"OrderFlow({(orderFlowBullish ? "Bullish" : "Bearish")}:Delta={bar.F_DeltaPressure:F2},VolDom={bar.F_VolumeDominance:F2}) ");
        }

        // === 3. Market Efficiency (Trend vs Chop) ===
        bool inTrend = Math.Abs(bar.F_MarketState) > _config.MinMarketStateTrend;
        bool marketStateBullish = bar.F_MarketState > _config.MinMarketStateTrend;
        bool marketStateBearish = bar.F_MarketState < -_config.MinMarketStateTrend;

        if (inTrend && (marketStateBullish || marketStateBearish))
        {
            contributions = contributions with { MarketEfficiency = true };
            confirmedCategories++;
            reasons.Append($"MarketState({(marketStateBullish ? "Bullish" : "Bearish")}:{bar.F_MarketState:F2}) ");
        }

        // === 4. Price Action ===
        bool priceActionBullish = bar.F_CloseOpenRelationship > _config.MinCloseOpenBullish;
        bool priceActionBearish = bar.F_CloseOpenRelationship < _config.MaxCloseOpenBearish;

        if (priceActionBullish || priceActionBearish)
        {
            contributions = contributions with { PriceAction = true };
            confirmedCategories++;
            reasons.Append($"PriceAction({(priceActionBullish ? "Bullish" : "Bearish")}:{bar.F_CloseOpenRelationship:F2}) ");
        }

        // === 5. Volume Surge ===
        bool volumeSurge = bar.F_VolumeSurge > _config.MinVolumeSurge;
        if (volumeSurge)
        {
            contributions = contributions with { VolumeSurge = true };
            confirmedCategories++;
            reasons.Append($"VolumeSurge({bar.F_VolumeSurge:F2}) ");
        }

        // === 6. Secondary Timeframe Alignment (if required) ===
        bool secondaryAligned = true;
        if (_config.RequireSecondaryAlignment)
        {
            secondaryAligned = Math.Abs(bar.F_MarketStateSecondary) > _config.MinMarketStateTrend;
            if (!secondaryAligned)
            {
                return FeatureSignal.NoSignal($"Secondary timeframe not trending (MarketStateSecondary={bar.F_MarketStateSecondary:F2})");
            }
        }

        // === Determine Signal Direction ===
        bool allBullish = temporalBullish && orderFlowBullish && marketStateBullish;
        bool allBearish = temporalBearish && orderFlowBearish && marketStateBearish;

        // Require minimum confirming categories
        if (confirmedCategories < _config.MinConfirmingCategories)
        {
            return FeatureSignal.NoSignal($"Insufficient confirmations ({confirmedCategories}/{_config.MinConfirmingCategories})");
        }

        // Determine direction
        int direction = 0;
        if (allBullish) direction = 1;
        else if (allBearish) direction = -1;
        else
        {
            // Mixed signals - use majority vote
            int bullishVotes = (temporalBullish ? 1 : 0) + (orderFlowBullish ? 1 : 0) + (marketStateBullish ? 1 : 0) + (priceActionBullish ? 1 : 0);
            int bearishVotes = (temporalBearish ? 1 : 0) + (orderFlowBearish ? 1 : 0) + (marketStateBearish ? 1 : 0) + (priceActionBearish ? 1 : 0);

            if (bullishVotes > bearishVotes && bullishVotes >= 2) direction = 1;
            else if (bearishVotes > bullishVotes && bearishVotes >= 2) direction = -1;
            else return FeatureSignal.NoSignal($"Mixed signals (Bull={bullishVotes}, Bear={bearishVotes})");
        }

        // Calculate strength and confidence
        double strength = confirmedCategories / 6.0; // 6 total categories
        double confidence = allBullish || allBearish ? 1.0 : 0.7; // Full confirmation vs partial

        return new FeatureSignal(
            direction,
            strength,
            confidence,
            reasons.ToString().Trim(),
            contributions
        );
    }

    /// <summary>
    /// Evaluate exit conditions for an existing position.
    /// </summary>
    /// <param name="bar">Current bar</param>
    /// <param name="positionDirection">+1 for long, -1 for short</param>
    public FeatureSignal EvaluateExit(FeaturesEngineeringBar bar, int positionDirection)
    {
        if (positionDirection == 0)
            return FeatureSignal.NoSignal("No position");

        var reasons = new StringBuilder();

        // === Exit Condition 1: Momentum Reversal ===
        bool slopeReversal = (positionDirection > 0 && bar.F_MovingAverageSlope < _config.ExitSlopeReversal) ||
                             (positionDirection < 0 && bar.F_MovingAverageSlope > -_config.ExitSlopeReversal);

        if (slopeReversal)
        {
            reasons.Append($"SlopeReversal({bar.F_MovingAverageSlope:F2}) ");
        }

        // === Exit Condition 2: Order Flow Reversal ===
        bool deltaReversal = (positionDirection > 0 && bar.F_DeltaPressure < _config.ExitDeltaPressureReversal) ||
                             (positionDirection < 0 && bar.F_DeltaPressure > -_config.ExitDeltaPressureReversal);

        if (deltaReversal)
        {
            reasons.Append($"DeltaReversal({bar.F_DeltaPressure:F2}) ");
        }

        // === Exit Condition 3: Cumulative Delta Momentum ===
        bool cdmReversal = (positionDirection > 0 && bar.F_CumulativeDeltaMomentum < _config.ExitCumulativeDeltaMomentum) ||
                           (positionDirection < 0 && bar.F_CumulativeDeltaMomentum > -_config.ExitCumulativeDeltaMomentum);

        if (cdmReversal)
        {
            reasons.Append($"CDM_Reversal({bar.F_CumulativeDeltaMomentum:F2}) ");
        }

        // === Exit Condition 4: Entering Chop ===
        bool enteringChop = Math.Abs(bar.F_MarketState) < _config.MaxMarketStateChop;

        if (enteringChop)
        {
            reasons.Append($"EnteringChop({bar.F_MarketState:F2}) ");
        }

        // === Exit Condition 5: POC Rejection ===
        bool pocRejection = (positionDirection > 0 && bar.F_POCDisplacement < _config.ExitPOCDisplacement) ||
                            (positionDirection < 0 && bar.F_POCDisplacement > -_config.ExitPOCDisplacement);

        if (pocRejection)
        {
            reasons.Append($"POC_Rejection({bar.F_POCDisplacement:F2}) ");
        }

        // Exit if any condition is met
        if (slopeReversal || deltaReversal || cdmReversal || enteringChop || pocRejection)
        {
            return new FeatureSignal(
                -positionDirection, // Opposite direction to exit
                1.0,
                0.8,
                $"EXIT: {reasons}",
                new FeatureContributions()
            );
        }

        return FeatureSignal.NoSignal("No exit conditions met");
    }
}
