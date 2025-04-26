namespace Application.Utils;

public class ResettableCts : IDisposable
{
    private CancellationTokenSource cts = new();
    private bool disposed;
    
    public CancellationToken Token => cts.Token;
    
    public void Reset()
    {
        EnsureNotDisposed();
        
        cts.Cancel();
        cts.Dispose();
        cts = new CancellationTokenSource();
    }
    
    public void Cancel()
    {
        EnsureNotDisposed();
        cts.Cancel();
    }
    
    public void Dispose()
    {
        if (disposed) return;

        cts.Cancel();
        cts.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ResettableCts));
    }
}