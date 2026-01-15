using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace Q2Connect.Wpf.ViewModels;

public class ThrottledObservableCollection<T> : ObservableCollection<T>, IDisposable
{
    private readonly DispatcherTimer _updateTimer;
    private readonly Queue<T> _pendingItems = new();
    private readonly object _lockObject = new();
    private bool _disposed;

    public ThrottledObservableCollection(int updateIntervalMs = 150)
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(updateIntervalMs)
        };
        _updateTimer.Tick += OnTimerTick;
        _updateTimer.Start();
    }

    public void AddThrottled(T item)
    {
        lock (_lockObject)
        {
            if (_disposed) return;
            _pendingItems.Enqueue(item);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        List<T> itemsToAdd;
        lock (_lockObject)
        {
            if (_disposed || _pendingItems.Count == 0) return;

            itemsToAdd = new List<T>();
            while (_pendingItems.Count > 0)
            {
                itemsToAdd.Add(_pendingItems.Dequeue());
            }
        }

        // Add items outside the lock to avoid holding lock during UI operations
        // DispatcherTimer already runs on UI thread, but be explicit for safety
        if (itemsToAdd != null && itemsToAdd.Count > 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in itemsToAdd)
                {
                    Add(item);
                }
            });
        }
    }

    // CollectionChanged is already raised on the UI thread via Dispatcher.Invoke in OnTimerTick

    public void Stop()
    {
        lock (_lockObject)
        {
            if (!_disposed)
            {
                _updateTimer.Stop();
                _updateTimer.Tick -= OnTimerTick;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}

