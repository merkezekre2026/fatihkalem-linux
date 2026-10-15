namespace FatihKalem.App.Tests;

public sealed class AppLifetimeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk life Ş-" + Guid.NewGuid().ToString("N"));
    private string LockPath => Path.Combine(_dir, "sub dir", "instance.lock");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("/PreLoad")]
    [InlineData("/preload")]
    [InlineData("--preload")]
    public void ParseArgs_PreLoad_Detected(string arg) =>
        Assert.True(AppLifetime.ParseArgs(["other", arg]).PreLoad);

    [Fact]
    public void ParseArgs_Empty_NoPreload()
    {
        Assert.False(AppLifetime.ParseArgs([]).PreLoad);
        Assert.False(AppLifetime.ParseArgs(["--something"]).PreLoad);
    }

    [Fact]
    public void SecondInstance_CannotAcquire()
    {
        using var first = SingleInstance.TryAcquire(LockPath);
        Assert.NotNull(first);
        Assert.Null(SingleInstance.TryAcquire(LockPath));
    }

    [Fact]
    public void Lock_ReleasedOnDispose()
    {
        SingleInstance.TryAcquire(LockPath)!.Dispose();
        using var again = SingleInstance.TryAcquire(LockPath);
        Assert.NotNull(again);
    }

    [Fact]
    public void LockPath_WithSpacesAndTurkishChars_Works()
    {
        using var instance = SingleInstance.TryAcquire(LockPath);
        Assert.NotNull(instance);
        Assert.True(File.Exists(LockPath));
    }

    [Fact]
    public void SecondInstance_SignalsFirst()
    {
        using var first = SingleInstance.TryAcquire(LockPath)!;
        using var activated = new ManualResetEventSlim();
        first.ActivateRequested += activated.Set;

        Assert.True(SingleInstance.SignalExisting(LockPath));
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)), "first instance was not told to come forward");
    }

    [Fact]
    public void SignalExisting_WithoutRunningInstance_ReturnsFalse() =>
        Assert.False(SingleInstance.SignalExisting(LockPath, timeoutMs: 200));

    [Fact]
    public void DefaultLockPath_IsPerUserAndInTempOrRuntimeDir()
    {
        var path = AppLifetime.DefaultLockPath;
        Assert.EndsWith(".lock", path);
        Assert.Contains(Environment.UserName, path);
    }
}
