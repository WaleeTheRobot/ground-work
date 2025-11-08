using Groundwork.FeaturesEngineering.Core;
using Groundwork.Utils;
using System;

namespace Groundwork.Battleplan;

/// <summary>
/// Maintains a rolling buffer of bars and computes running levels:
///   - Bullish bar (Close > Open): update BullishLow  = bar.Low
///   - Bearish bar (Close < Open): update BearishHigh = bar.High
/// Neutral bars do not modify levels.
/// 
/// Provides fast checks to see if a spot price breaks above the
/// most recent BearishHigh or below the most recent BullishLow.
/// </summary>
public sealed class Breakout
{
    private const double LevelEps = 1e-6;
    private readonly object _sync = new();

    private CircularBuffer<BaseBar> _bars;
    private int _capacity;

    private bool _hasBullishLow;
    private bool _hasBearishHigh;
    private double _lastBullishLow;
    private double _lastBearishHigh;

    /// <summary>Raised whenever either level changes.</summary>
    public event Action LevelsChanged;

    public Breakout(int capacity = 20)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _bars = new CircularBuffer<BaseBar>(capacity);
    }

    // -------------------------
    // Capacity / counts
    // -------------------------
    public int Capacity { get { lock (_sync) return _capacity; } }
    public int Count { get { lock (_sync) return _bars.Count; } }

    /// <summary>
    /// Reinitialize the rolling buffer to a new capacity.
    /// Preserves the most recent bars that fit.
    /// </summary>
    public void SetCapacity(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        lock (_sync)
        {
            if (capacity == _capacity) return;

            var newBuf = new CircularBuffer<BaseBar>(capacity);
            int toCopy = Math.Min(_bars.Count, capacity);
            for (int i = _bars.Count - toCopy; i < _bars.Count; i++)
                newBuf.Add(_bars[i]);

            _bars = newBuf;
            _capacity = capacity;
            // Levels remain as-is; call RecomputeLevelsFromHistory() if you want a full recompute.
        }
    }

    // -------------------------
    // Read-only level access
    // -------------------------
    public bool HasBullishLow { get { lock (_sync) return _hasBullishLow; } }
    public bool HasBearishHigh { get { lock (_sync) return _hasBearishHigh; } }
    public double LastBullishLow { get { lock (_sync) return _lastBullishLow; } }
    public double LastBearishHigh { get { lock (_sync) return _lastBearishHigh; } }

    public readonly struct LevelsSnapshot
    {
        public readonly bool HasBullishLow, HasBearishHigh;
        public readonly double LastBullishLow, LastBearishHigh;
        public LevelsSnapshot(bool hasBL, bool hasBH, double lastBL, double lastBH)
        { HasBullishLow = hasBL; HasBearishHigh = hasBH; LastBullishLow = lastBL; LastBearishHigh = lastBH; }
    }

    public LevelsSnapshot GetSnapshot()
    {
        lock (_sync) return new LevelsSnapshot(_hasBullishLow, _hasBearishHigh, _lastBullishLow, _lastBearishHigh);
    }

    // -------------------------
    // Ingestion
    // -------------------------
    /// <summary>
    /// Append a CLOSED bar and update running levels:
    ///   bullish → BullishLow = bar.Low
    ///   bearish → BearishHigh = bar.High
    /// </summary>
    public void AddBar(BaseBar bar)
    {
        bool changed = false;

        lock (_sync)
        {
            _bars.Add(bar);

            if (bar.Close > bar.Open)
            {
                if (!_hasBullishLow || Math.Abs(bar.Low - _lastBullishLow) > LevelEps)
                {
                    _lastBullishLow = bar.Low;
                    _hasBullishLow = true;
                    changed = true;
                }
            }
            else if (bar.Close < bar.Open)
            {
                if (!_hasBearishHigh || Math.Abs(bar.High - _lastBearishHigh) > LevelEps)
                {
                    _lastBearishHigh = bar.High;
                    _hasBearishHigh = true;
                    changed = true;
                }
            }
        }

        if (changed) SafeInvoke(LevelsChanged);
    }

    /// <summary>
    /// If you bulk-load history or change rules, recompute levels from buffer (oldest → newest).
    /// </summary>
    public void RecomputeLevelsFromHistory()
    {
        bool changed = false;
        lock (_sync)
        {
            _hasBullishLow = _hasBearishHigh = false;
            _lastBullishLow = _lastBearishHigh = 0.0;

            for (int i = 0; i < _bars.Count; i++)
            {
                var b = _bars[i];
                if (b.Close > b.Open)
                {
                    _lastBullishLow = b.Low;
                    _hasBullishLow = true;
                    changed = true;
                }
                else if (b.Close < b.Open)
                {
                    _lastBearishHigh = b.High;
                    _hasBearishHigh = true;
                    changed = true;
                }
            }
        }
        if (changed) SafeInvoke(LevelsChanged);
    }

    public void Reset()
    {
        lock (_sync)
        {
            _bars.Clear();
            _hasBullishLow = _hasBearishHigh = false;
            _lastBullishLow = _lastBearishHigh = 0.0;
        }
        SafeInvoke(LevelsChanged);
    }

    // -------------------------
    // Breakout checks
    // -------------------------
    /// <summary>
    /// Returns (brokeAboveBearishHigh, brokeBelowBullishLow) for a given spot price.
    /// </summary>
    public (bool brokeAboveBearishHigh, bool brokeBelowBullishLow) CheckBreakout(double spot)
    {
        lock (_sync)
        {
            bool up = _hasBearishHigh && (spot > _lastBearishHigh + LevelEps);
            bool down = _hasBullishLow && (spot < _lastBullishLow - LevelEps);
            return (up, down);
        }
    }

    public bool IsBreakAboveBearishHigh(double spot)
    {
        lock (_sync) return _hasBearishHigh && (spot > _lastBearishHigh + LevelEps);
    }

    public bool IsBreakBelowBullishLow(double spot)
    {
        lock (_sync) return _hasBullishLow && (spot < _lastBullishLow - LevelEps);
    }

    // -------------------------
    // Internals
    // -------------------------
    private static void SafeInvoke(Action a)
    {
        try { a?.Invoke(); } catch { /* ignore */ }
    }
}
