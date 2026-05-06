using CommunityToolkit.Mvvm.ComponentModel;

namespace MyProjectBase.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationTokenSource.Cancel();
        Dispose(true);
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
