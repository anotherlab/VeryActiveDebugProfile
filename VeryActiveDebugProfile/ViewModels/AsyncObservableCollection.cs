using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace VeryActiveDebugProfile.ViewModels;

public class AsyncObservableCollection<T> : ObservableCollection<T>
{
    private readonly SynchronizationContext _syncContext;

    public AsyncObservableCollection()
    {
        _syncContext = SynchronizationContext.Current
                       ?? throw new InvalidOperationException(
                            "AsyncObservableCollection must be created on the UI thread.");
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (SynchronizationContext.Current == _syncContext)
        {
            // UI thread, invoke normally
            base.OnCollectionChanged(e);
        }
        else
        {
            // Marshal to UI thread
            _syncContext.Post(_ => base.OnCollectionChanged(e), null);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (SynchronizationContext.Current == _syncContext)
        {
            base.OnPropertyChanged(e);
        }
        else
        {
            _syncContext.Post(_ => base.OnPropertyChanged(e), null);
        }
    }
}