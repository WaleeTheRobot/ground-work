using AddOns.Groundwork.Utils;
using Groundwork.FeaturesEngineering;
using Groundwork.FeaturesEngineering.Core;
using Groundwork.StrategyAnalyzerExporter;
using Groundwork.Utils;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.Indicators;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace NinjaTrader.NinjaScript.Strategies;

public class StrategyAnalyzerExporter : Strategy
{
    public const int PRIMARY_SERIES = 1;
    public const int SECONDARY_SERIES = 2;

    private int _parsedTimeStart;
    private int _parsedTimeEnd;
    private FeaturesEngineeringService _FeaturesEngineeringService;
    private EventManager _eventManager;
    private ExporterDatabaseManager _databaseManager;

    private EMA _movingAverage;
    private EMA _slowMovingAverage;
    private ATR _ATR;
    private DonchianChannel _donchianChannel;

    private EMA _movingAverageSecondary;
    private EMA _slowMovingAverageSecondary;
    private ATR _ATRSecondary;
    private DonchianChannel _donchianChannelSecondary;

    private Stopwatch _histStopwatch;
    private bool _histStarted;
    private bool _histEnded;
    private long _histProcessedBars;

    private VolumetricBarsType _volumetricBarsPrimary;
    private VolumetricBarsType _volumetricBarsSecondary;

    #region Properties

    public const string GROUP_NAME_DEFAULT = "1. Strategy Analyzer Exporter";

    [NinjaScriptProperty]
    [Display(Name = "Enable Write to Database", Description = "Enable to write to database.", Order = 0, GroupName = GROUP_NAME_DEFAULT)]
    public bool EnableWriteToDatabase { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Print Data Bar in Output", Description = "Enable to the feature bar in the output.", Order = 1, GroupName = GROUP_NAME_DEFAULT)]
    public bool EnablePrintDataBar { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Database Path", Description = "The database path.", Order = 2, GroupName = GROUP_NAME_DEFAULT)]
    public string DatabasePath { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Use Float32", Description = "Use float32 instead of double when writing to the database. Less precision and size reduces approximately 50%.", Order = 3, GroupName = GROUP_NAME_DEFAULT)]
    public bool UseFloat32 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Table Name", Description = "The table name.", Order = 4, GroupName = GROUP_NAME_DEFAULT)]
    public string TableName { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Time Enabled", Description = "Enable this to enable time start/end.", Order = 5, GroupName = GROUP_NAME_DEFAULT)]
    public bool TimeEnabled { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Time Start", Description = "The allowed time to enable.", Order = 6, GroupName = GROUP_NAME_DEFAULT)]
    public string TimeStart { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Time End", Description = "The allowed time to disable and close positions.", Order = 7, GroupName = GROUP_NAME_DEFAULT)]
    public string TimeEnd { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Strategy Type", Description = "The strategy type to filter features for (Breakout or Regime).", Order = 8, GroupName = GROUP_NAME_DEFAULT)]
    public StrategyType StrategyType { get; set; }

    [Range(1, int.MaxValue), NinjaScriptProperty]
    [Display(Name = "Volumetric Primary Period", Description = "Volumetric data series primary period", Order = 9, GroupName = GROUP_NAME_DEFAULT)]
    public int VolumetricPrimaryPeriod { get; set; }

    [Range(1, int.MaxValue), NinjaScriptProperty]
    [Display(Name = "Volumetric Secondary Period", Description = "Volumetric data series secondary period", Order = 10, GroupName = GROUP_NAME_DEFAULT)]
    public int VolumetricSecondaryPeriod { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Volumetric Bars Type", Description = "The type of bars for the volumetric data series", Order = 11, GroupName = GROUP_NAME_DEFAULT)]
    public BarTypeOptions VolumetricBarsType { get; set; }

    [Range(1, int.MaxValue), NinjaScriptProperty]
    [Display(Name = "Ticks Per Level", Description = "The ticks per level", Order = 12, GroupName = GROUP_NAME_DEFAULT)]
    public int TicksPerLevel { get; set; }

    #endregion

    protected override void OnStateChange()
    {
        if (State == State.SetDefaults)
        {
            Description = @"Exports the historical bar data using the strategy analyzer with maximum performance.";
            Name = "_StrategyAnalyzerExporter";
            Calculate = Calculate.OnBarClose;
            EntriesPerDirection = 1;
            EntryHandling = EntryHandling.AllEntries;
            IsExitOnSessionCloseStrategy = true;
            ExitOnSessionCloseSeconds = 30;
            IsFillLimitOnTouch = false;
            MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
            OrderFillResolution = OrderFillResolution.Standard;
            Slippage = 0;
            StartBehavior = StartBehavior.WaitUntilFlat;
            TraceOrders = false;
            RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
            StopTargetHandling = StopTargetHandling.PerEntryExecution;
            BarsRequiredToTrade = 14;
            IsInstantiatedOnEachOptimizationIteration = true;

            // Properties (DB off by default for timing runs)
            EnableWriteToDatabase = false;   // << set false to measure calc-only
            EnablePrintDataBar = false;
            DatabasePath = @"C:\temp\features.duckdb";
            TableName = "Features";
            UseFloat32 = true;
            TimeEnabled = true;
            TimeStart = "083000";
            TimeEnd = "155500";
            StrategyType = StrategyType.Breakout;

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
            var config = new StrategyAnalyzerExporterConfig
            {
                EnableWriteToDatabase = EnableWriteToDatabase,
                EnablePrintDataBar = EnablePrintDataBar,
                DatabasePath = DatabasePath,
                UseFloat32 = UseFloat32,
                TableName = TableName,

                // Optimized batch settings for better performance
                FlushSize = 50_000,      // Larger batches for better throughput
                FlushIntervalSeconds = 60,

                // More aggressive commit settings for real-time performance
                CommitEveryRows = 25_000,  // Smaller commits for lower latency
                MaxTxDurationSeconds = 30,  // Prevent long-running transactions
                IdleTailCommitSeconds = 15, // Quick commits when idle
                CheckpointEveryCommits = 10, // More frequent checkpoints
            };

            var FeaturesEngineeringConfig = new FeaturesEngineeringConfig
            {
                TickSize = TickSize,
                BarsRequiredToTrade = BarsRequiredToTrade,
            };

            _parsedTimeStart = int.Parse(TimeStart);
            _parsedTimeEnd = int.Parse(TimeEnd);
            _FeaturesEngineeringService = new FeaturesEngineeringService(FeaturesEngineeringConfig);
            _eventManager = new EventManager();

            // Not using the dataseries on the chart as the primary
            _volumetricBarsPrimary = BarsArray[PRIMARY_SERIES].BarsType as VolumetricBarsType;
            _volumetricBarsSecondary = BarsArray[SECONDARY_SERIES].BarsType as VolumetricBarsType;

            // Only create DB manager if writing is enabled
            if (EnableWriteToDatabase)
                _databaseManager = new ExporterDatabaseManager(config, _eventManager, StrategyType);

            // Indicators
            _movingAverage = EMA(BarsArray[PRIMARY_SERIES], 9);
            _slowMovingAverage = EMA(BarsArray[PRIMARY_SERIES], 14);
            _ATR = ATR(BarsArray[PRIMARY_SERIES], 9);
            _donchianChannel = DonchianChannel(BarsArray[PRIMARY_SERIES], 14);

            _movingAverageSecondary = EMA(BarsArray[SECONDARY_SERIES], 9);
            _slowMovingAverageSecondary = EMA(BarsArray[SECONDARY_SERIES], 14);
            _ATRSecondary = ATR(BarsArray[SECONDARY_SERIES], 9);
            _donchianChannelSecondary = DonchianChannel(BarsArray[SECONDARY_SERIES], 14);

            // Timing init
            _histStopwatch = new Stopwatch();
            _histStarted = false;
            _histEnded = false;
            _histProcessedBars = 0;

            _eventManager.OnPrintMessage += HandlePrintMessage;
        }
        else if (State == State.Terminated)
        {
            try
            {
                // If DB was enabled, finish cleanly
                if (EnableWriteToDatabase)
                {
                    _databaseManager?.FlushPending();
                    _databaseManager?.FinalizeAndClose();
                }

                // If historical never transitioned (rare), print any timing we have
                if (_histStarted && !_histEnded)
                {
                    _histStopwatch.Stop();
                    var secs = System.Math.Max(0.0001, _histStopwatch.Elapsed.TotalSeconds);
                    var rate = _histProcessedBars / secs;
                    HandlePrintMessage(
                        $"Calculation finished (termination): {_histProcessedBars:N0} bars in {_histStopwatch.Elapsed.TotalSeconds:N1}s ({rate:N0} bars/s)."
                    );
                }
            }
            finally
            {
                _eventManager.OnPrintMessage -= HandlePrintMessage;
                _databaseManager = null;
            }
        }
    }

    protected override void OnBarUpdate()
    {
        // No need to process dataseries on chart
        if (BarsInProgress == 1) HandlePrimaryBar();
        if (BarsInProgress == 2) HandleSecondaryBar();
    }

    private void HandlePrimaryBar()
    {
        if (!IsValidBarsRequiredAndTimeRange(PRIMARY_SERIES)) return;

        // Historical timing start
        bool isHistorical = State == State.Historical;
        if (isHistorical && !_histStarted)
        {
            _histStarted = true;
            _histStopwatch.Start();
        }

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

        if (bar == null) return;

        if (EnablePrintDataBar)
        {
            PrintVolumetricBar(volumetricBar, "Primary");
            PrintBar(bar.Value, "Primary");
        }

        // Count processed historical bars
        if (isHistorical) _histProcessedBars++;

        // Skip DB writes entirely if disabled
        if (EnableWriteToDatabase)
            _databaseManager?.OnNewBarAvailable(bar.Value);

        // On first realtime bar after historical, print timing once
        if (!isHistorical && _histStarted && !_histEnded)
        {
            _histStopwatch.Stop();
            _histEnded = true;

            var secs = System.Math.Max(0.0001, _histStopwatch.Elapsed.TotalSeconds);
            var rate = _histProcessedBars / secs;

            HandlePrintMessage(
                $"Calculation finished: {_histProcessedBars:N0} bars in {_histStopwatch.Elapsed.TotalSeconds:N1}s ({rate:N0} bars/s)."
            );
        }
    }

    private void HandleSecondaryBar()
    {
        if (!IsValidBarsRequiredAndTimeRange(SECONDARY_SERIES)) return;

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

        if (bar == null) return;

        if (EnablePrintDataBar)
        {
            PrintVolumetricBar(volumetricBar, "Secondary");
            PrintBar(bar.Value, "Secondary");
        }
    }

    private bool IsValidBarsRequiredAndTimeRange(int barsInProgress)
    {
        if (CurrentBars[barsInProgress] < BarsRequiredToTrade) return false;

        bool shouldProcess = true;
        if (TimeEnabled)
        {
            int barTime = ToTime(Times[barsInProgress][1]);
            shouldProcess = barTime >= _parsedTimeStart && barTime <= _parsedTimeEnd;
        }

        return shouldProcess;
    }

    private void HandlePrintMessage(string message, bool addNewLine = false)
    {
        Print(message);
        if (addNewLine) Print("");
    }

    private void PrintBar(FeaturesEngineeringBar bar, string series)
    {
        Print($"=== {series} FeaturesEngineeringBar ===");
        Print($"Time: {bar.Time}");
        Print($"Day: {bar.Day}");
        Print($"Open: {bar.Open}");
        Print($"High: {bar.High}");
        Print($"Low: {bar.Low}");
        Print($"Close: {bar.Close}");
        Print($"Volume: {bar.Volume}");
        Print($"ATR: {bar.ATR}");
        Print($"VAH: {bar.ValueAreaHigh}");
        Print($"VAL: {bar.ValueAreaLow}");
        Print($"Donchian Channel Upper: {bar.DonchianChannelUpper}");
        Print($"Donchian Channel Lower: {bar.DonchianChannelLower}");
        Print($"F_MovingAverageFastSlowDistance: {bar.F_MovingAverageFastSlowDistance}");
        Print($"F_MovingAverageSlope: {bar.F_MovingAverageSlope}");
        Print($"F_CloseOpenRelationship: {bar.F_CloseOpenRelationship}");
        Print($"F_MarketState: {bar.F_MarketState}");
        Print("==============================");
        Print("");
    }

    private void PrintVolumetricBar(VolumetricBar bar, string series)
    {
        Print($"=== {series} VolumetricBar ===");
        Print($"TotalVolume: {bar.TotalVolume}");
        Print($"TotalBuyingVolume: {bar.TotalBuyingVolume}");
        Print($"TotalSellingVolume: {bar.TotalSellingVolume}");
        Print($"BarDelta: {bar.BarDelta}");
        Print($"MaxSeenDelta: {bar.MaxDelta}");
        Print($"MinSeenDelta: {bar.MinDelta}");
        Print($"CumulativeDelta: {bar.CumulativeDelta}");
        Print($"DeltaPercent: {bar.DeltaPercentage}");
        Print($"ValueAreaHigh: {bar.ValueAreaHigh}");
        Print($"ValueAreaLow: {bar.ValueAreaLow}");
        Print($"PointOfControl: {bar.PointOfControl}");
        Print($"TotalBidImbalances: {bar.TotalBidImbalances}");
        Print($"TotalAskImbalances: {bar.TotalAskImbalances}");
        Print($"TotalBidStackedImbalances: {bar.TotalBidStackedImbalances}");
        Print($"TotalAskStackedImbalances: {bar.TotalAskStackedImbalances}");

        // Print individual imbalances
        Print("--- Imbalances ---");
        if (bar.BidAskVolumes != null && bar.BidAskVolumes.Count > 0)
        {
            for (int i = 0; i < bar.BidAskVolumes.Count; i++)
            {
                var level = bar.BidAskVolumes[i];
                Print($"  Price: {level.Price:F2}, Bid: {level.BidVolume}, Ask: {level.AskVolume}");
            }
        }
        else
        {
            Print("  No imbalance data available");
        }

        Print("===========================");
        Print("");
    }
}
