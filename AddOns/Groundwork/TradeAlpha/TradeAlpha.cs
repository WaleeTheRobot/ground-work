using AddOns.Groundwork.Utils;
using Groundwork.Battleplan;
using Groundwork.FeaturesEngineering;
using Groundwork.FeaturesEngineering.Core;
using Groundwork.Utils;
using NinjaTrader.Cbi;
using NinjaTrader.Custom.AddOns.TradeAlpha.Net;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using System;
using System.ComponentModel.DataAnnotations;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class TradeAlpha : Strategy
    {
        public const int PRIMARY_SERIES = 1;
        public const int SECONDARY_SERIES = 2;

        private ApiClient _apiClient;
        private FeaturesEngineeringService _FeaturesEngineeringService;
        private bool _tradingEnabled = false;

        private string _atmStrategyId = "";
        private bool _isAtmStrategyCreated = false;

        private CircularBuffer<FeaturesEngineeringBar?> _bars;
        private EMA _movingAverage;
        private EMA _slowMovingAverage;
        private ATR _ATR;
        private DonchianChannel _donchianChannel;

        private EMA _movingAverageSecondary;
        private EMA _slowMovingAverageSecondary;
        private ATR _ATRSecondary;
        private DonchianChannel _donchianChannelSecondary;

        private VolumetricBarsType _volumetricBarsPrimary;
        private VolumetricBarsType _volumetricBarsSecondary;

        // ---- API snapshot (overwritten every bar; contains both BUY and SELL) ----
        private PredictResponse _apiSnapshot;

        // --- Per-bar guards to avoid duplicate entries ---
        private int _lastTradeBarUp = -1;
        private int _lastTradeBarDn = -1;

        // --- Simple diagnostics/counters ---
        private int _diagModelFavoredBuys;   // times model favored BUY over SELL
        private int _diagModelFavoredSells;  // times model favored SELL over BUY


        public const string GROUPNAME = "1. TradeAlpha";

        [NinjaScriptProperty]
        [Display(Name = "API Base URL", Order = 0, GroupName = GROUPNAME)]
        public string ApiBaseUrl { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "Volumetric Primary Period", Description = "Volumetric data series primary period", Order = 1, GroupName = GROUPNAME)]
        public int VolumetricPrimaryPeriod { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "Volumetric Secondary Period", Description = "Volumetric data series secondary period", Order = 2, GroupName = GROUPNAME)]
        public int VolumetricSecondaryPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Volumetric Bars Type", Description = "The type of bars for the volumetric data series", Order = 3, GroupName = GROUPNAME)]
        public BarTypeOptions VolumetricBarsType { get; set; }

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "Ticks Per Level", Description = "Ticks per level for volumetric bars", Order = 4, GroupName = GROUPNAME)]
        public int TicksPerLevel { get; set; }


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Scalp Master";
                Name = "_TradeAlpha";
                Calculate = Calculate.OnEachTick;

                BarsRequiredToTrade = 14;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                IsInstantiatedOnEachOptimizationIteration = false;

                ApiBaseUrl = "http://127.0.0.1:8000";
                VolumetricPrimaryPeriod = 500;
                VolumetricSecondaryPeriod = 1000;
                VolumetricBarsType = BarTypeOptions.Tick;
                TicksPerLevel = 5;
            }
            else if (State == State.Configure)
            {
                BarsPeriodType barsPeriodType;

                switch (VolumetricBarsType)
                {
                    case BarTypeOptions.Minute:
                        barsPeriodType = BarsPeriodType.Minute;
                        break;
                    case BarTypeOptions.Range:
                        barsPeriodType = BarsPeriodType.Range;
                        break;
                    case BarTypeOptions.Second:
                        barsPeriodType = BarsPeriodType.Second;
                        break;
                    case BarTypeOptions.Tick:
                        barsPeriodType = BarsPeriodType.Tick;
                        break;
                    case BarTypeOptions.Volume:
                        barsPeriodType = BarsPeriodType.Volume;
                        break;
                    default:
                        barsPeriodType = BarsPeriodType.Minute;
                        break;
                }

                AddVolumetric(Instrument.FullName, barsPeriodType, VolumetricPrimaryPeriod, VolumetricDeltaType.BidAsk, TicksPerLevel);
                AddVolumetric(Instrument.FullName, barsPeriodType, VolumetricSecondaryPeriod, VolumetricDeltaType.BidAsk, TicksPerLevel);
            }
            else if (State == State.DataLoaded)
            {
                var FeaturesEngineeringConfig = new FeaturesEngineeringConfig
                {
                    TickSize = TickSize,
                    BarsRequiredToTrade = BarsRequiredToTrade,
                };

                _FeaturesEngineeringService = new FeaturesEngineeringService(FeaturesEngineeringConfig);
                _apiClient = new ApiClient(ApiBaseUrl);

                _bars = new CircularBuffer<FeaturesEngineeringBar?>(BarsRequiredToTrade);

                _volumetricBarsPrimary = BarsArray[PRIMARY_SERIES].BarsType as VolumetricBarsType;
                _volumetricBarsSecondary = BarsArray[SECONDARY_SERIES].BarsType as VolumetricBarsType;

                // Indicators
                _movingAverage = EMA(BarsArray[PRIMARY_SERIES], 9);
                _slowMovingAverage = EMA(BarsArray[PRIMARY_SERIES], 14);
                _ATR = ATR(BarsArray[PRIMARY_SERIES], 9);
                _donchianChannel = DonchianChannel(BarsArray[PRIMARY_SERIES], 14);

                _movingAverageSecondary = EMA(BarsArray[SECONDARY_SERIES], 9);
                _slowMovingAverageSecondary = EMA(BarsArray[SECONDARY_SERIES], 14);
                _ATRSecondary = ATR(BarsArray[SECONDARY_SERIES], 9);
                _donchianChannelSecondary = DonchianChannel(BarsArray[SECONDARY_SERIES], 14);
            }
            else if (State == State.Historical)
            {
                if (_tradingEnabled != false) _tradingEnabled = false;

                if (ChartControl != null)
                    InitializeUIManager();
            }
            else if (State == State.Realtime)
            {
                ReadyControlPanel();
                RefreshToggleUI();

                // Verify API connection
                try
                {
                    bool isHealthy = _apiClient.HealthCheck();
                    Print(isHealthy
                        ? "[API] Health check passed - server is ready."
                        : "[API] Health check failed - check server status.");
                }
                catch (Exception ex)
                {
                    Print($"[API] Health check error: {ex.Message}");
                }
            }
            else if (State == State.Terminated)
            {
                _tradingEnabled = false;

                // Close any active ATM
                CloseActiveAtm();

                // Dispose API client
                _apiClient?.Dispose();
                _apiClient = null;

                // Remove UI control panel
                UnloadControlPanel();

                PrintDiagnosticsSummary();
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                if (State == State.Realtime && _isAtmStrategyCreated)
                    CheckAtmPosition();

                // Handle primary volumetric bars (matching StrategyAnalyzerExporter pattern)
                if (BarsInProgress == PRIMARY_SERIES)
                {
                    HandlePrimaryBar();
                }
                // Handle secondary volumetric bars
                else if (BarsInProgress == SECONDARY_SERIES)
                {
                    HandleSecondaryBar();
                }
            }
            catch (Exception ex)
            {
                Print($"[{Time[0]}] Unexpected error: {ex}");
            }
        }

        private void HandlePrimaryBar()
        {
            if (CurrentBars[PRIMARY_SERIES] < BarsRequiredToTrade)
                return;

            bool isFirstOfBar = (State == State.Realtime ? IsFirstTickOfBar : true);

            if (isFirstOfBar)
            {
                // Create volumetric bar for primary series
                var volumetricBarParams = new VolumetricBarParams(
                     TicksPerLevel,
                     TickSize,
                     _volumetricBarsPrimary.Volumes[CurrentBars[PRIMARY_SERIES] - 1],
                     Highs[PRIMARY_SERIES][1],
                     Lows[PRIMARY_SERIES][1]
                 );

                var volumetricBar = VolumetricBarCreator.GetVolumetricBar(volumetricBarParams);

                _FeaturesEngineeringService.SetVolmetricBarPrimary(volumetricBar);


                FeaturesEngineeringBar? bar = _FeaturesEngineeringService.GetFeaturesEngineeringBar(
                    new BaseBar
                    {
                        Time = ToTime(Times[PRIMARY_SERIES][1]),
                        Day = ToDay(Times[PRIMARY_SERIES][1]),
                        Open = Opens[PRIMARY_SERIES][1],
                        High = Highs[PRIMARY_SERIES][1],
                        Low = Lows[PRIMARY_SERIES][1],
                        Close = Closes[PRIMARY_SERIES][1],
                        Volume = Volumes[PRIMARY_SERIES][1],
                        MovingAverage = _movingAverage[1],
                        SlowMovingAverage = _slowMovingAverage[1],
                        ATR = _ATR[1],
                        DonchianChannelUpper = _donchianChannel.Upper[1],
                        DonchianChannelLower = _donchianChannel.Lower[1],
                        ValueAreaHigh = volumetricBar.ValueAreaHigh,
                        ValueAreaLow = volumetricBar.ValueAreaLow,
                    }
                );
                _bars.Add(bar);

                // Call API ONLY when in real-time
                if (State == State.Realtime && bar.HasValue)
                {
                    try
                    {
                        // Send only the current (most recent closed) bar
                        // API server will build lag features from its internal buffer
                        _apiSnapshot = _apiClient.Predict(new[] { bar.Value });

                        if (_apiSnapshot == null)
                        {
                            Print("[API] Null response (check server logs).");
                        }
                        else
                        {
                            Print($"[API] BUY={_apiSnapshot.Buy.Signal} (p={_apiSnapshot.Buy.Prediction:F4}, conf={_apiSnapshot.Buy.Confidence}) | SELL={_apiSnapshot.Sell.Signal} (p={_apiSnapshot.Sell.Prediction:F4}, conf={_apiSnapshot.Sell.Confidence})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Print($"[API] Predict error: {ex.Message}");
                        _apiSnapshot = null;
                    }
                }

                // API-driven trading on first tick of bar
                MaybeTradeOnApiSnapshot();
            }
        }

        private void HandleSecondaryBar()
        {
            if (CurrentBars[SECONDARY_SERIES] < BarsRequiredToTrade)
                return;

            var volumetricBarParams = new VolumetricBarParams(
               TicksPerLevel,
               TickSize,
               _volumetricBarsSecondary.Volumes[CurrentBars[SECONDARY_SERIES] - 1],
               Highs[SECONDARY_SERIES][1],
               Lows[SECONDARY_SERIES][1]
             );

            var volumetricBar = VolumetricBarCreator.GetVolumetricBar(volumetricBarParams);

            _FeaturesEngineeringService.SetVolmetricBarSecondary(volumetricBar);

            FeaturesEngineeringBar? bar = _FeaturesEngineeringService.GetFeaturesEngineeringBarSecondary(
                new BaseBar
                {
                    Time = ToTime(Times[SECONDARY_SERIES][1]),
                    Day = ToDay(Times[SECONDARY_SERIES][1]),
                    Open = Opens[SECONDARY_SERIES][1],
                    High = Highs[SECONDARY_SERIES][1],
                    Low = Lows[SECONDARY_SERIES][1],
                    Close = Closes[SECONDARY_SERIES][1],
                    Volume = Volumes[SECONDARY_SERIES][1],
                    MovingAverage = _movingAverageSecondary[1],
                    SlowMovingAverage = _slowMovingAverageSecondary[1],
                    ATR = _ATRSecondary[1],
                    DonchianChannelUpper = _donchianChannelSecondary.Upper[1],
                    DonchianChannelLower = _donchianChannelSecondary.Lower[1],
                    ValueAreaHigh = volumetricBar.ValueAreaHigh,
                    ValueAreaLow = volumetricBar.ValueAreaLow,
                }
            );
        }

        // === API-only decision - enter immediately when API says TAKE ===
        private void MaybeTradeOnApiSnapshot()
        {
            // Gate: only place orders if realtime, enabled, and no active ATM
            if (State != State.Realtime || !_tradingEnabled || _isAtmStrategyCreated)
                return;

            // Only check API signals if we have a valid snapshot
            if (_apiSnapshot == null)
                return;

            // Only enter on first tick of bar (after API was called)
            bool isFirstOfBar = IsFirstTickOfBar;
            if (!isFirstOfBar)
                return;

            bool buyTake = string.Equals(_apiSnapshot.Buy?.Signal, "TAKE", StringComparison.OrdinalIgnoreCase);
            bool sellTake = string.Equals(_apiSnapshot.Sell?.Signal, "TAKE", StringComparison.OrdinalIgnoreCase);

            // Determine direction: +1 buy, -1 sell, 0 none
            int dir = 0;
            if (buyTake && !sellTake)
                dir = +1;
            else if (sellTake && !buyTake)
                dir = -1;
            else if (buyTake && sellTake)
            {
                // Both signals - use higher probability
                if (_apiSnapshot.Buy.Prediction > _apiSnapshot.Sell.Prediction)
                    dir = +1;
                else if (_apiSnapshot.Sell.Prediction > _apiSnapshot.Buy.Prediction)
                    dir = -1;
            }

            // Enter immediately if signal says TAKE
            if (dir > 0 && CurrentBar != _lastTradeBarUp)
            {
                Print($"[Entry] LONG - API signal TAKE (prob={_apiSnapshot.Buy.Prediction:F4}, conf={_apiSnapshot.Buy.Confidence})");
                EnterAtmPosition(isLong: true);
                _lastTradeBarUp = CurrentBar;
            }
            else if (dir < 0 && CurrentBar != _lastTradeBarDn)
            {
                Print($"[Entry] SHORT - API signal TAKE (prob={_apiSnapshot.Sell.Prediction:F4}, conf={_apiSnapshot.Sell.Confidence})");
                EnterAtmPosition(isLong: false);
                _lastTradeBarDn = CurrentBar;
            }
        }


        // === Manual entry with dynamic target/stop based on ATR ===
        private void EnterAtmPosition(bool isLong)
        {
            if (State < State.Realtime)
            {
                Print("[ATM] Skipping (historical).");
                return;
            }

            if (_isAtmStrategyCreated)
            {
                Print("[ATM] An ATM is already active; waiting until it flattens.");
                return;
            }

            // Get current ATR
            double atr = _ATR[0];
            if (atr <= 0)
            {
                Print($"[ATM] Invalid ATR ({atr:F2}), skipping entry.");
                return;
            }

            // Round ATR down to be divisible by TickSize
            // Example: ATR=10.59, TickSize=0.25 -> 10.50
            double roundedATR = Math.Floor(atr / TickSize) * TickSize;

            // Calculate target and stop in points (matching backtest)
            // Target: 1.3x ATR, Stop: 0.7x ATR
            double targetPoints = Math.Floor(roundedATR * 1.3 / TickSize) * TickSize;
            double stopPoints = Math.Floor(roundedATR * 0.7 / TickSize) * TickSize;

            // Ensure minimum values
            if (targetPoints < TickSize * 4) targetPoints = TickSize * 4;
            if (stopPoints < TickSize * 2) stopPoints = TickSize * 2;

            Print($"[Entry] Creating {(isLong ? "LONG" : "SHORT")} | ATR={atr:F2} -> Rounded={roundedATR:F2} | Target={targetPoints:F2}pts | Stop={stopPoints:F2}pts");

            // Enter with market order and set protective orders
            if (isLong)
            {
                EnterLong(1, "ScalpEntry");
            }
            else
            {
                EnterShort(1, "ScalpEntry");
            }

            _isAtmStrategyCreated = true;
            _atmStrategyId = CurrentBar.ToString();

            // Set profit target and stop loss
            SetProfitTarget("ScalpEntry", CalculationMode.Ticks, targetPoints / TickSize);
            SetStopLoss("ScalpEntry", CalculationMode.Ticks, stopPoints / TickSize, false);
        }

        private void CheckAtmPosition()
        {
            try
            {
                // Check if position is flat
                if (_isAtmStrategyCreated && Position.MarketPosition == MarketPosition.Flat)
                {
                    ResetAtmState();
                }
            }
            catch { /* ignore transient errors */ }
        }

        private void ResetAtmState()
        {
            Print($"[Position] Exited. Resetting (id={_atmStrategyId}).");
            _atmStrategyId = "";
            _isAtmStrategyCreated = false;
        }

        private void CloseActiveAtm()
        {
            if (_isAtmStrategyCreated && State == State.Realtime)
            {
                try
                {
                    if (Position.MarketPosition == MarketPosition.Long)
                        ExitLong("ScalpEntry");
                    else if (Position.MarketPosition == MarketPosition.Short)
                        ExitShort("ScalpEntry");
                }
                catch { /* no-op */ }
                ResetAtmState();
            }
        }

        private void HandleEnabledDisabled(bool isEnabled)
        {
            _tradingEnabled = isEnabled;
            RefreshToggleUI();

            if (!_tradingEnabled && _isAtmStrategyCreated)
                CloseActiveAtm();

            Print($"[TradeAlpha:{Instrument?.FullName}] TradingEnabled = {_tradingEnabled}");
        }

        private void PrintDiagnosticsSummary()
        {
            Print($"[Diag] ModelFavoredBuys={_diagModelFavoredBuys} ModelFavoredSells={_diagModelFavoredSells}");
        }
    }
}
