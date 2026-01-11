using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace Q2Browser.Wpf.ViewModels;

public class ThrottledObservableCollection<T> : ObservableCollection<T>
{
    private readonly DispatcherTimer _updateTimer;
    private readonly Queue<T> _pendingItems = new();
    private readonly object _lockObject = new();

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
            _pendingItems.Enqueue(item);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        lock (_lockObject)
        {
            if (_pendingItems.Count == 0) return;

            var itemsToAdd = new List<T>();
            while (_pendingItems.Count > 0)
            {
                itemsToAdd.Add(_pendingItems.Dequeue());
            }

            if (itemsToAdd.Count > 0)
            {
                // DispatcherTimer already runs on UI thread
                foreach (var item in itemsToAdd)
                {
                    Add(item);
                }
            }
        }
    }

    // CollectionChanged is already raised on the UI thread via Dispatcher.Invoke in OnTimerTick

    public void Stop()
    {
        _updateTimer.Stop();
        _updateTimer.Tick -= OnTimerTick;
    }
}

