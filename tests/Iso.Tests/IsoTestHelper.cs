using System;
using System.IO;

namespace Iso.Tests;

public static class IsoTestHelper
{
    public static string? FindRepoFile(params string[] paths)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PipingAgent.sln")))
        {
            directory = directory.Parent;
        }

        if (directory == null) return null;
        var path = Path.Combine(directory.FullName, Path.Combine(paths));
        return File.Exists(path) ? path : null;
    }
}
