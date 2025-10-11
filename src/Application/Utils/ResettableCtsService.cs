namespace Application.Utils;

public class ResettableCts : IDisposable
{
    private readonly Lock lockObj = new();
    private CancellationTokenSource cts = new();
    private bool disposed;

    public CancellationToken Token
    {
        get
        {
            lock (lockObj)
            {
                EnsureNotDisposed();
                return cts.Token;
            }
        }
    }
    
    public void Reset()
    {
        lock (lockObj)
        {
            EnsureNotDisposed();
            
            cts.Cancel();
            cts.Dispose();
            cts = new CancellationTokenSource();
        }
    }
    
    public void Cancel()
    {
        lock (lockObj)
        {
            EnsureNotDisposed();
            cts.Cancel();
        }
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