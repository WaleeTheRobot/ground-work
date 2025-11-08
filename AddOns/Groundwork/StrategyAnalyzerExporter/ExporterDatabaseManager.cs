using Groundwork.FeaturesEngineering;
using Groundwork.DataBars;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Groundwork.StrategyAnalyzerExporter;

/// <summary>
/// Database manager with batching and reduced contention.
/// </summary>
public class ExporterDatabaseManager : IDisposable
{
    private readonly StrategyAnalyzerExporterConfig _config;
    private DatabaseWriter _dbWriter;
    private readonly EventManager _eventManager;
    private readonly StrategyType _strategyType;

    // Lock-free queue with better batching strategy - using object to support both bar types
    private readonly ConcurrentQueue<object> _pending = new ConcurrentQueue<object>();
    private volatile int _pendingCount;
    private readonly object _flushLock = new object();
    private volatile bool _disposed;
    private volatile bool _tableEnsured;

    // More intelligent flush triggers
    private readonly int _flushSize;
    private readonly TimeSpan _flushInterval;
    private DateTime _lastFlushUtc = DateTime.UtcNow;

    // Telemetry
    private long _barsEnqueued;
    private long _rowsWritten;
    private int _enqueueChecks;

    // Pre-allocated batch lists to reduce allocations
    private readonly List<BreakoutBar> _reusableBreakoutBatch;
    private readonly List<RegimeBar> _reusableRegimeBatch;

    public ExporterDatabaseManager(StrategyAnalyzerExporterConfig config, EventManager eventManager, StrategyType strategyType)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
        _strategyType = strategyType;

        _flushSize = Math.Max(1, _config.FlushSize);
        _flushInterval = TimeSpan.FromSeconds(Math.Max(1, _config.FlushIntervalSeconds));

        // Pre-allocate batch lists with reasonable capacity
        _reusableBreakoutBatch = new List<BreakoutBar>(_flushSize);
        _reusableRegimeBatch = new List<RegimeBar>(_flushSize);

        InitializeDatabase();
    }

    public void OnNewBarAvailable(FeaturesEngineeringBar bar)
    {
        if (!_config.EnableWriteToDatabase || _disposed) return;

        // Map to strategy-specific bar type
        object mappedBar = _strategyType switch
        {
            StrategyType.Breakout => BarMapper.ToBreakoutBar(bar),
            StrategyType.Regime => BarMapper.ToRegimeBar(bar),
            _ => throw new System.NotSupportedException($"StrategyType {_strategyType} is not supported.")
        };

        _pending.Enqueue(mappedBar);
        System.Threading.Interlocked.Increment(ref _pendingCount);
        System.Threading.Interlocked.Increment(ref _barsEnqueued);

        // Adaptive check frequency - more checks when queue is filling up
        int checkMask = _pendingCount > (_flushSize / 2) ? 0x3F : 0x1FF; // Check every 64 vs 512 items
        if ((System.Threading.Interlocked.Increment(ref _enqueueChecks) & checkMask) != 0)
            return;

        var now = DateTime.UtcNow;
        if (_pendingCount >= _flushSize || (now - _lastFlushUtc) >= _flushInterval)
            FlushToDatabase();
    }

    public void FlushPending() => FlushToDatabase();
    public void FinalizeAndClose() => FinalizeInternal();
    public void Dispose() => FinalizeInternal();

    private void InitializeDatabase()
    {
        if (!_config.EnableWriteToDatabase || string.IsNullOrEmpty(_config.DatabasePath))
            return;

        var tableName = string.IsNullOrWhiteSpace(_config.TableName) ? "Features" : _config.TableName;

        _dbWriter = new DatabaseWriter(_config, tableName);

        _dbWriter.OnCommitted += rows =>
        {
            _eventManager.PrintMessage($"Committed {rows:N0} rows to '{tableName}'.");
            System.Threading.Interlocked.Add(ref _rowsWritten, rows);
        };

        // Note: We'll ensure the table exists with the first bar that comes through
        // since we need to know the strategy type to determine which properties to include
        _tableEnsured = false;
    }

    private void FlushToDatabase()
    {
        if (_disposed || _dbWriter == null || _pendingCount == 0) return;

        // Use double-checked locking pattern for better performance
        if (_pendingCount == 0) return;

        lock (_flushLock)
        {
            if (_pendingCount == 0) return;

            try
            {
                if (!_tableEnsured)
                {
                    // Ensure table exists based on strategy type
                    if (_strategyType == StrategyType.Breakout)
                        _dbWriter.EnsureTableExists<BreakoutBar>();
                    else
                        _dbWriter.EnsureTableExists<RegimeBar>();

                    _tableEnsured = true;
                }

                // More efficient batch draining
                DrainPendingBatch();

                _lastFlushUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _eventManager.PrintMessage($"Database flush error: {ex.Message}", true);
            }
        }
    }

    /// <summary>
    /// Efficiently drain the pending queue in optimally-sized batches
    /// </summary>
    private void DrainPendingBatch()
    {
        if (_strategyType == StrategyType.Breakout)
            DrainPendingBatch<BreakoutBar>(_reusableBreakoutBatch);
        else
            DrainPendingBatch<RegimeBar>(_reusableRegimeBatch);
    }

    private void DrainPendingBatch<T>(List<T> reusableBatch)
    {
        while (_pendingCount > 0)
        {
            // Clear reusable batch list (capacity remains allocated)
            reusableBatch.Clear();

            // Determine optimal batch size (don't exceed our flush size)
            int targetBatchSize = Math.Min(_flushSize, _pendingCount);
            int actualBatchSize = 0;

            // Drain up to targetBatchSize items
            for (int i = 0; i < targetBatchSize; i++)
            {
                if (_pending.TryDequeue(out var item))
                {
                    reusableBatch.Add((T)item);
                    actualBatchSize++;
                    System.Threading.Interlocked.Decrement(ref _pendingCount);
                }
                else
                {
                    break; // Queue is empty
                }
            }

            if (actualBatchSize > 0)
            {
                _dbWriter.InsertBatch(reusableBatch);
            }

            // If we got fewer items than expected, queue is probably empty
            if (actualBatchSize < targetBatchSize)
                break;
        }
    }

    private void FinalizeInternal()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            FlushToDatabase(); // Drain any remaining
            try { _dbWriter?.ForceCommitAndCheckpoint(true); } catch { }
        }
        catch (Exception ex)
        {
            _eventManager.PrintMessage($"Cleanup error: {ex.Message}", true);
        }
        finally
        {
            try { _dbWriter?.Dispose(); } catch { }
            _dbWriter = null;

            _eventManager.PrintMessage(
                $"Export summary => Enqueued={_barsEnqueued:N0}, Written={_rowsWritten:N0}", true);
        }
    }
}
