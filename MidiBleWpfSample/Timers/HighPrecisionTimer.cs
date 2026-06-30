using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MidiBleWpfSample.Timers
{
    public class HighPrecisionTimer : IDisposable
    {
        private readonly Action _callback;
        private int? _timerThreadId = null;
        private CancellationTokenSource? _cts;
        private Task? _task;
        private long _intervalMs;
        private bool _isDisposed;

        public HighPrecisionTimer(long intervalMs, Action callback)
        {
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _intervalMs = intervalMs;
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _task = Task.Run(() => TimerLoop(token), token);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _cts?.Cancel();

            _cts?.Dispose();
            _cts = null;
            _task = null;
        }

        public void SetInterval(long intervalMs)
        {
            if (intervalMs <= 0) return;
            // This will be picked up by the loop on its next iteration.
            Interlocked.Exchange(ref _intervalMs, intervalMs);
        }

        private void TimerLoop(CancellationToken token)
        {
            _timerThreadId = Thread.CurrentThread.ManagedThreadId;
            var stopwatch = Stopwatch.StartNew();
            long nextTick = stopwatch.ElapsedMilliseconds;

            while (!token.IsCancellationRequested)
            {
                long currentMs = stopwatch.ElapsedMilliseconds;
                if (currentMs >= nextTick)
                {
                    try
                    {
                        _callback();
                    }
                    catch (Exception ex)
                    {
                        // Log or handle exceptions from the callback
                        Debug.WriteLine($"[HighPrecisionTimer] Callback exception: {ex.Message}");
                    }

                    // Read the interval within the loop to allow for dynamic changes
                    long currentInterval = Interlocked.Read(ref _intervalMs);
                    nextTick += currentInterval;

                    // If the callback took too long and we're already behind for the next tick,
                    // jump ahead to prevent burst-firing ticks to "catch up".
                    if (stopwatch.ElapsedMilliseconds > nextTick)
                    {
                        nextTick = stopwatch.ElapsedMilliseconds + currentInterval;
                    }
                }

                // Use a small sleep to prevent pegging the CPU at 100%
                // Thread.Sleep(1) can sleep for more than 1ms (often 15ms on Windows).
                // A spin-wait is more accurate for short delays.
                int sleepTime = (int)(nextTick - stopwatch.ElapsedMilliseconds);
                if (sleepTime > 10)
                {
                    Thread.Sleep(5); // Coarse sleep if we have plenty of time
                }
                else
                {
                    Thread.SpinWait(1000); // Fine-grained wait
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
