using Groundwork.FeaturesEngineering;
using Groundwork.FeaturesEngineering.Core;
using Groundwork.Utils;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Feature Signal Dashboard - Visual display of feature-based market conditions
    /// Shows real-time feature alignment without relying on traditional bars
    /// </summary>
    public class FeatureSignalDashboard : Indicator
    {
        private FeaturesEngineeringService _featuresEngineeringService;
        private FeatureSignalEvaluator _signalEvaluator;
        private FeatureSignalConfig _signalConfig;

        private EMA _movingAverage;
        private EMA _slowMovingAverage;
        private ATR _atr;
        private DonchianChannel _donchianChannel;

        private VolumetricBarsType _volumetricBars;

        private FeatureSignal _lastEntrySignal;
        private FeaturesEngineeringBar? _lastBar;

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Show Dashboard Panel", Order = 0, GroupName = "Display")]
        public bool ShowDashboard { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Signal Arrows", Order = 1, GroupName = "Display")]
        public bool ShowSignalArrows { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Background Color", Order = 2, GroupName = "Display")]
        public bool ShowBackgroundColor { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "Ticks Per Level", Order = 3, GroupName = "Volumetric")]
        public int TicksPerLevel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min MA Distance", Order = 4, GroupName = "Thresholds")]
        public double MinMADistance { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Slope", Order = 5, GroupName = "Thresholds")]
        public double MinSlope { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Delta Pressure", Order = 6, GroupName = "Thresholds")]
        public double MinDeltaPressure { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Market State Trend", Order = 7, GroupName = "Thresholds")]
        public double MinMarketStateTrend { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Confirming Categories", Order = 8, GroupName = "Thresholds")]
        public int MinConfirmingCategories { get; set; }

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Feature Signal Dashboard - Visual display of feature-based market conditions";
                Name = "FeatureSignalDashboard";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel = true;

                // Display defaults
                ShowDashboard = true;
                ShowSignalArrows = true;
                ShowBackgroundColor = false;
                TicksPerLevel = 5;

                // Threshold defaults
                MinMADistance = 0.3;
                MinSlope = 0.2;
                MinDeltaPressure = 0.4;
                MinMarketStateTrend = 0.6;
                MinConfirmingCategories = 3;
            }
            else if (State == State.Configure)
            {
                // No additional data series needed - using chart's primary series
            }
            else if (State == State.DataLoaded)
            {
                // Initialize feature engineering
                var feConfig = new FeaturesEngineeringConfig
                {
                    TickSize = TickSize,
                    BarsRequiredToTrade = 14,
                };
                _featuresEngineeringService = new FeaturesEngineeringService(feConfig);

                // Initialize signal evaluator with custom config
                _signalConfig = new FeatureSignalConfig
                {
                    MinMADistance = MinMADistance,
                    MinSlope = MinSlope,
                    MinDeltaPressure = MinDeltaPressure,
                    MinMarketStateTrend = MinMarketStateTrend,
                    MinConfirmingCategories = MinConfirmingCategories,
                    RequireSecondaryAlignment = false, // Single timeframe for indicator
                };
                _signalEvaluator = new FeatureSignalEvaluator(_signalConfig);

                // Initialize indicators
                _movingAverage = EMA(9);
                _slowMovingAverage = EMA(14);
                _atr = ATR(9);
                _donchianChannel = DonchianChannel(14);

                // Get volumetric bars
                _volumetricBars = BarsArray[0].BarsType as VolumetricBarsType;

                if (_volumetricBars == null)
                    Print("WARNING: Chart is not using volumetric bars! Indicator will not function.");
                else
                    Print("FeatureSignalDashboard loaded successfully on volumetric bars.");

                _lastEntrySignal = FeatureSignal.NoSignal();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 14)
                return;

            // Skip if not volumetric
            if (_volumetricBars == null)
            {
                Print($"[{Time[0]}] Warning: Not using volumetric bars. This indicator requires volumetric bar types.");
                return;
            }

            // Create volumetric bar
            var volumetricBarParams = new VolumetricBarParams(
                TicksPerLevel,
                TickSize,
                _volumetricBars.Volumes[CurrentBar - 1],
                High[1],
                Low[1]
            );

            var volumetricBar = VolumetricBarCreator.GetVolumetricBar(volumetricBarParams);
            _featuresEngineeringService.SetVolmetricBarPrimary(volumetricBar);

            // Get features engineering bar
            var bar = _featuresEngineeringService.GetFeaturesEngineeringBar(new BaseBar
            {
                Time = ToTime(Time[1]),
                Day = ToDay(Time[1]),
                Open = Open[1],
                High = High[1],
                Low = Low[1],
                Close = Close[1],
                Volume = Volume[1],
                MovingAverage = _movingAverage[1],
                SlowMovingAverage = _slowMovingAverage[1],
                ATR = _atr[1],
                DonchianChannelUpper = _donchianChannel.Upper[1],
                DonchianChannelLower = _donchianChannel.Lower[1],
                ValueAreaHigh = volumetricBar.ValueAreaHigh,
                ValueAreaLow = volumetricBar.ValueAreaLow,
            });

            if (!bar.HasValue)
                return;

            _lastBar = bar;

            // Evaluate entry signal
            _lastEntrySignal = _signalEvaluator.EvaluateEntry(bar.Value);

            // Force chart refresh to trigger OnRender
            if (ChartControl != null)
                ChartControl.InvalidateVisual();

            // Draw signal arrows
            if (ShowSignalArrows && _lastEntrySignal.HasSignal)
            {
                if (_lastEntrySignal.IsLong)
                {
                    Draw.ArrowUp(this, "EntryLong" + CurrentBar, true, 0, Low[0] - 2 * TickSize,
                        Brushes.LimeGreen);
                }
                else if (_lastEntrySignal.IsShort)
                {
                    Draw.ArrowDown(this, "EntryShort" + CurrentBar, true, 0, High[0] + 2 * TickSize,
                        Brushes.Red);
                }
            }

            // Background color for strong signals
            if (ShowBackgroundColor && _lastEntrySignal.HasSignal && _lastEntrySignal.Confidence > 0.8)
            {
                var bgColor = _lastEntrySignal.IsLong
                    ? Color.FromArgb(20, 0, 255, 0)  // Faint green
                    : Color.FromArgb(20, 255, 0, 0); // Faint red

                BackBrushes[0] = new SolidColorBrush(bgColor);
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!ShowDashboard)
                return;

            if (!_lastBar.HasValue)
                return;

            // Ensure we're in the proper render state
            if (RenderTarget == null || RenderTarget.IsDisposed)
                return;

            var bar = _lastBar.Value;
            var signal = _lastEntrySignal;

            // Dashboard position (top-left corner)
            double x = 10;
            double y = 10;
            double width = 380;
            double lineHeight = 20;

            // Background panel - using SharpDX types
            var panelBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(30, 30, 30, 230));
            var borderColor = signal.HasSignal
                ? (signal.IsLong ? new SharpDX.Color(0, 255, 0, 255) : new SharpDX.Color(255, 0, 0, 255))
                : new SharpDX.Color(80, 80, 80, 255);
            var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, borderColor);

            var rect = new SharpDX.RectangleF((float)x, (float)y, (float)width, (float)(lineHeight * 11 + 20));
            RenderTarget.FillRectangle(rect, panelBrush);
            RenderTarget.DrawRectangle(rect, borderBrush, 2);

            // Text formatting
            var headerFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory,
                "Arial", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 14);
            var textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory,
                "Consolas", SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, 11);

            var whiteBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.White);
            var greenBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.LimeGreen);
            var redBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Red);
            var yellowBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Yellow);
            var grayBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Gray);

            double currentY = y + 10;

            // Header
            string header = signal.HasSignal
                ? $"SIGNAL: {(signal.IsLong ? "LONG ↑" : "SHORT ↓")} | Strength: {signal.Strength:P0} | Conf: {signal.Confidence:P0}"
                : "NO SIGNAL - Monitoring Features";

            var headerBrush = signal.HasSignal ? (signal.IsLong ? greenBrush : redBrush) : yellowBrush;
            RenderTarget.DrawText(header, headerFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                headerBrush);
            currentY += lineHeight + 5;

            // Separator
            RenderTarget.DrawLine(new SharpDX.Vector2((float)(x + 10), (float)currentY),
                new SharpDX.Vector2((float)(x + width - 10), (float)currentY),
                grayBrush, 1);
            currentY += 5;

            // Feature checks
            var contrib = signal.Contributions;

            // 1. Temporal
            bool temporalBullish = bar.F_MovingAverageFastSlowDistance > _signalConfig.MinMADistance &&
                                   bar.F_MovingAverageSlope > _signalConfig.MinSlope;
            bool temporalBearish = bar.F_MovingAverageFastSlowDistance < -_signalConfig.MinMADistance &&
                                   bar.F_MovingAverageSlope < -_signalConfig.MinSlope;

            string temporalText = $"{(contrib.TemporalAlignment ? "✓" : "✗")} Temporal: " +
                                  $"Dist={bar.F_MovingAverageFastSlowDistance:F2} Slope={bar.F_MovingAverageSlope:F2}";
            var temporalBrush = contrib.TemporalAlignment
                ? (temporalBullish ? greenBrush : redBrush)
                : grayBrush;
            RenderTarget.DrawText(temporalText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                temporalBrush);
            currentY += lineHeight;

            // 2. Order Flow
            bool orderFlowBullish = bar.F_DeltaPressure > _signalConfig.MinDeltaPressure;
            bool orderFlowBearish = bar.F_DeltaPressure < -_signalConfig.MinDeltaPressure;

            string orderFlowText = $"{(contrib.OrderFlowAlignment ? "✓" : "✗")} OrderFlow: " +
                                   $"Delta={bar.F_DeltaPressure:F2} VolDom={bar.F_VolumeDominance:F2}";
            var orderFlowBrush = contrib.OrderFlowAlignment
                ? (orderFlowBullish ? greenBrush : redBrush)
                : grayBrush;
            RenderTarget.DrawText(orderFlowText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                orderFlowBrush);
            currentY += lineHeight;

            // 3. Market State
            bool marketStateBullish = bar.F_MarketState > _signalConfig.MinMarketStateTrend;
            bool marketStateBearish = bar.F_MarketState < -_signalConfig.MinMarketStateTrend;
            string marketStateLabel = Math.Abs(bar.F_MarketState) < 0.25 ? "Chop" :
                                      Math.Abs(bar.F_MarketState) < 0.6 ? "Transition" : "Strong Trend";

            string marketStateText = $"{(contrib.MarketEfficiency ? "✓" : "✗")} MarketState: " +
                                     $"{bar.F_MarketState:F2} ({marketStateLabel})";
            var marketStateBrush = contrib.MarketEfficiency
                ? (marketStateBullish ? greenBrush : redBrush)
                : grayBrush;
            RenderTarget.DrawText(marketStateText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                marketStateBrush);
            currentY += lineHeight;

            // 4. Price Action
            bool priceActionBullish = bar.F_CloseOpenRelationship > 0.5;
            bool priceActionBearish = bar.F_CloseOpenRelationship < -0.5;

            string priceActionText = $"{(contrib.PriceAction ? "✓" : "✗")} PriceAction: " +
                                     $"{bar.F_CloseOpenRelationship:F2} {(priceActionBullish ? "(Bullish)" : priceActionBearish ? "(Bearish)" : "(Neutral)")}";
            var priceActionBrush = contrib.PriceAction
                ? (priceActionBullish ? greenBrush : redBrush)
                : grayBrush;
            RenderTarget.DrawText(priceActionText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                priceActionBrush);
            currentY += lineHeight;

            // 5. Volume Surge
            string volumeSurgeText = $"{(contrib.VolumeSurge ? "✓" : "✗")} VolumeSurge: {bar.F_VolumeSurge:F2}";
            var volumeSurgeBrush = contrib.VolumeSurge ? greenBrush : grayBrush;
            RenderTarget.DrawText(volumeSurgeText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                volumeSurgeBrush);
            currentY += lineHeight;

            // 6. Value Area
            string valueAreaText = $"{(contrib.ValueAreaPosition ? "✓" : "✗")} POC Displacement: {bar.F_POCDisplacement:F2}";
            var valueAreaBrush = contrib.ValueAreaPosition ? greenBrush : grayBrush;
            RenderTarget.DrawText(valueAreaText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                valueAreaBrush);
            currentY += lineHeight;

            // Separator
            currentY += 5;
            RenderTarget.DrawLine(new SharpDX.Vector2((float)(x + 10), (float)currentY),
                new SharpDX.Vector2((float)(x + width - 10), (float)currentY),
                grayBrush, 1);
            currentY += 5;

            // Summary
            int totalContributors = signal.Contributions.TotalContributors;
            string summaryText = $"Features Aligned: {totalContributors}/6 (Min Required: {MinConfirmingCategories})";
            RenderTarget.DrawText(summaryText, textFormat,
                new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight)),
                whiteBrush);
            currentY += lineHeight;

            // Reason
            if (!string.IsNullOrEmpty(signal.Reason))
            {
                string reasonText = $"Reason: {signal.Reason}";
                RenderTarget.DrawText(reasonText, textFormat,
                    new SharpDX.RectangleF((float)(x + 10), (float)currentY, (float)(x + width - 10), (float)(currentY + lineHeight + 10)),
                    yellowBrush);
            }

            // Cleanup
            headerFormat.Dispose();
            textFormat.Dispose();
            panelBrush.Dispose();
            borderBrush.Dispose();
            whiteBrush.Dispose();
            greenBrush.Dispose();
            redBrush.Dispose();
            yellowBrush.Dispose();
            grayBrush.Dispose();
        }

        private int ToTime(DateTime dt) => dt.Hour * 10000 + dt.Minute * 100 + dt.Second;
        private int ToDay(DateTime dt) => dt.Year * 10000 + dt.Month * 100 + dt.Day;
    }
}
