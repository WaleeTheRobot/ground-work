using Groundwork.FeaturesEngineering.Core;
using Groundwork.FeaturesEngineering.Extractors;
using Groundwork.Utils;
using System;

namespace Groundwork.FeaturesEngineering;

public class FeaturesEngineeringService
{
    private readonly FeaturesEngineeringConfig _config;

    private readonly BufferSet _primary;
    private readonly BufferSet _secondary;

    public FeaturesEngineeringService(FeaturesEngineeringConfig config, int? expectedBars = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        int capacity = _config.BarsRequiredToTrade + 1;

        _primary = new BufferSet(capacity);
        _secondary = new BufferSet(capacity);
    }

    public void SetVolmetricBarPrimary(VolumetricBar bar)
    {
        _primary.AddVolumetric(bar);
    }

    public void SetVolmetricBarSecondary(VolumetricBar bar)
    {
        _secondary.AddVolumetric(bar);
    }

    public FeaturesEngineeringBar? GetFeaturesEngineeringBar(BaseBar bar)
    {
        _primary.Add(bar);

        if (_primary.Count < _config.BarsRequiredToTrade)
            return null;

        // Get F_MarketStateSecondary from secondary bufferset
        double marketStateSecondary = GetMarketStateFromSecondary();

        return CreateFeaturesEngineeringBar(bar, _primary, marketStateSecondary);
    }

    public FeaturesEngineeringBar? GetFeaturesEngineeringBarSecondary(BaseBar bar)
    {
        _secondary.Add(bar);

        if (_secondary.Count < _config.BarsRequiredToTrade)
            return null;

        return CreateFeaturesEngineeringBar(bar, _secondary);
    }

    private double GetMarketStateFromSecondary()
    {
        if (_secondary.Count < _config.BarsRequiredToTrade)
            return 0.0;

        // Get the most recent (newest) bar from secondary buffer, not the oldest!
        var price = PriceExtractor.Extract(_config, _secondary.Bars[_secondary.Count - 1],
            _secondary.Open, _secondary.High, _secondary.Low, _secondary.Close, _secondary.ATR);

        return price.MarketState;
    }

    private FeaturesEngineeringBar CreateFeaturesEngineeringBar(BaseBar bar, BufferSet buffers, double marketStateSecondary = 0.0)
    {
        var ma = MovingAverageExtractor.Extract(_config, bar, buffers.MAFast);
        var price = PriceExtractor.Extract(_config, bar,
            buffers.Open, buffers.High, buffers.Low, buffers.Close, buffers.ATR);
        var volumetric = VolumetricExtractor.Extract(_config, bar, buffers.VolumetricBars);

        return FeaturesEngineeringBarCreator.Create(in bar, in ma, in price, in volumetric, marketStateSecondary);
    }

    private sealed class BufferSet
    {
        private int _lastDay = -1;

        public CircularBuffer<BaseBar> Bars { get; }
        public CircularBuffer<VolumetricBar> VolumetricBars { get; }
        public CircularBuffer<double> MAFast { get; }
        public CircularBuffer<double> MASlow { get; }
        public CircularBuffer<double> Open { get; }
        public CircularBuffer<double> High { get; }
        public CircularBuffer<double> Low { get; }
        public CircularBuffer<double> Close { get; }
        public CircularBuffer<double> ATR { get; }

        public int Count => Bars.Count;

        public BufferSet(int capacity)
        {
            Bars = new CircularBuffer<BaseBar>(capacity);
            VolumetricBars = new CircularBuffer<VolumetricBar>(capacity);
            MAFast = new CircularBuffer<double>(capacity);
            MASlow = new CircularBuffer<double>(capacity);
            Open = new CircularBuffer<double>(capacity);
            High = new CircularBuffer<double>(capacity);
            Low = new CircularBuffer<double>(capacity);
            Close = new CircularBuffer<double>(capacity);
            ATR = new CircularBuffer<double>(capacity);
        }

        public void Add(BaseBar bar)
        {
            // Reset on day change
            if (_lastDay != -1 && bar.Day != _lastDay)
                ClearAll();

            _lastDay = bar.Day;

            Bars.Add(bar);
            MAFast.Add(bar.MovingAverage);
            MASlow.Add(bar.SlowMovingAverage);
            Open.Add(bar.Open);
            High.Add(bar.High);
            Low.Add(bar.Low);
            Close.Add(bar.Close);
            ATR.Add(bar.ATR);
        }

        public void AddVolumetric(VolumetricBar volumetricBar)
        {
            VolumetricBars.Add(volumetricBar);
        }

        private void ClearAll()
        {
            Bars.Clear();
            VolumetricBars.Clear();
            MAFast.Clear();
            MASlow.Clear();
            Open.Clear();
            High.Clear();
            Low.Clear();
            Close.Clear();
            ATR.Clear();
        }
    }
}
