namespace MauiApp_basic;

public interface IFeatureFlagsContext : IDisposable
{
    public bool IsFlagEnabled();
    public bool InitFailed(out Exception ex);
}
