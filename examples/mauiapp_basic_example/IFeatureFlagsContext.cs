namespace MauiApp_basic;

public interface IFeatureFlagsContext : IDisposable
{
    public bool IsFlagEnabled();
    public bool HasFailed(out Exception ex);
}
