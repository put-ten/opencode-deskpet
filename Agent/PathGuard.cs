using System.IO;

namespace DeskPet.Agent;

internal static class PathGuard
{
    public static readonly string[] AllowedRoots;

    static PathGuard()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        AllowedRoots = new[] { home, desktop, docs };
    }

    public static bool IsAllowed(string fullPath)
    {
        return AllowedRoots.Any(r => fullPath.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }
}
