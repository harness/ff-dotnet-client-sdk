namespace MauiApp_basic;

public interface IFeatureFlagsContext : IDisposable
{
    public bool IsTestFlagEnabled();
}
