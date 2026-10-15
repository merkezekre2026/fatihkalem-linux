using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace FatihKalem.App;

public sealed record StartupOptions(bool PreLoad);

public static class AppLifetime
{
    public static StartupOptions ParseArgs(string[] args) =>
        new(args.Any(a => a.Equals("/PreLoad", StringComparison.OrdinalIgnoreCase)
                          || a.Equals("--preload", StringComparison.OrdinalIgnoreCase)));

    /// <summary>One lock per user: the runtime directory when there is one, else the temp directory.</summary>
    public static string DefaultLockPath => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime ? runtime : Path.GetTempPath(),
        $"fatih-kalem-{Environment.UserName}.lock");
}

/// <summary>
/// Only one copy of the board runs. The first instance holds an exclusive file lock and listens on a
/// named pipe; a later instance finds the lock taken, pokes the pipe so the first comes forward, and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly FileStream _lock;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(string lockPath, FileStream file)
    {
        _lock = file;
        _pipeName = PipeName(lockPath);
        _ = Task.Run(ListenAsync);
    }

    /// <summary>Raised on a background thread when another instance asks this one to come forward.</summary>
    public event Action? ActivateRequested;

    public static SingleInstance? TryAcquire(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockPath))!);
        try
        {
            var file = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new SingleInstance(lockPath, file);
        }
        catch (IOException)
        {
            return null; // held by the running instance
        }
    }

    /// <returns><c>true</c> if a running instance received the request.</returns>
    public static bool SignalExisting(string lockPath, int timeoutMs = 1000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName(lockPath), PipeDirection.Out);
            client.Connect(timeoutMs);
            client.WriteByte(1);
            client.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _lock.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_stop.Token);
                if (await server.ReadAsync(new byte[1], _stop.Token) > 0) ActivateRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // a client went away mid-handshake; keep listening
            }
        }
    }

    private static string PipeName(string lockPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(lockPath)));
        return "fatih-kalem-" + Convert.ToHexString(hash)[..16];
    }
}
