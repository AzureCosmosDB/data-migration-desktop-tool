using System.Reflection;

namespace Cosmos.DataTransfer.Ui.Common;

public class DmtUtility
{
    public static string GetDmtAppPath(string platformCoreAppName)
    {
        string? executionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string? searchDir = null;

#if DEBUG
        searchDir ??= FindParentWithContents(executionDir, "Core", ".git");
#endif

        searchDir ??= FindParentWithContents(executionDir, "Extensions");

        var dmtAppPath = FindPreferredCoreAppPath(searchDir, platformCoreAppName);
        return dmtAppPath;
    }

    private static string? FindParentWithContents(string? executionDir, params string[] markers)
    {
        string? searchDir = executionDir;
        while (searchDir != null && !markers.Any(m => Directory.Exists(Path.Combine(searchDir, m))))
        {
            searchDir = Path.GetDirectoryName(searchDir);
        }

        return searchDir;
    }

    private static string FindPreferredCoreAppPath(string? rootSearchFolder, string dmtFileName)
    {
        var dir = new DirectoryInfo(rootSearchFolder ?? Environment.CurrentDirectory);

        var fileList = dir.EnumerateFiles("*.*", SearchOption.AllDirectories);

        var candidates = fileList.Where(file => file.Name == dmtFileName).ToList();

        if (candidates.Count == 1)
            return candidates.Single().FullName;

        var preferred = candidates.Where(file => file.DirectoryName != null && new DirectoryInfo(file.DirectoryName).EnumerateDirectories().Any(d => d.Name == "Extensions")).ToList();
        if (preferred.Count == 1)
            return preferred.Single().FullName;

        preferred = (preferred.Any() ? preferred : candidates).Where(file => file.DirectoryName?.Contains("bin\\Debug\\net6.0") == true).ToList();
        if (preferred.Count == 1)
            return preferred.Single().FullName;

        return (preferred.FirstOrDefault() ?? candidates.FirstOrDefault())?.FullName ?? Path.Combine(dir.FullName, dmtFileName);
    }

}
