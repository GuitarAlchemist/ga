namespace GaApi.Services;

using Path = System.IO.Path;

/// <summary>
///     Locates the Demerzel governance submodule (<c>governance/demerzel</c>) when
///     <c>Governance:DemerzelRoot</c> is not configured. This is a repo-layout lookup, not a
///     GA config file, so it does not go through <c>GA.Business.Config.ConfigFileLocator</c>
///     (which resolves names under <c>Common/GA.Business.Config</c>). It replaces four identical
///     private copies in GovernanceController, AlgedonicSignalService, BeliefStateService and
///     GovernanceWatcherService; the probe order is unchanged.
/// </summary>
internal static class DemerzelRootLocator
{
    public static string? Find()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "governance", "demerzel"),
            Path.Combine(Directory.GetCurrentDirectory(), "governance", "demerzel"),
            @"C:\Users\spare\source\repos\ga\governance\demerzel",
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}
