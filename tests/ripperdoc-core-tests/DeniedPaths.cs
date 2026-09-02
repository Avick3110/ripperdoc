using System.Diagnostics;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Refuses the running user paths for the life of one check, and takes the
/// refusals off again.
/// </summary>
/// <remarks>
/// <para>
/// The denials come off before the tree does, or the tree stays. Disposing this
/// is what removes them, so it is held by the fixture that owns the tree and
/// disposed before the tree is deleted.
/// </para>
/// <para>
/// Whether a denial actually took is the caller's to measure, because what a
/// refusal should stop differs by tier - a read, a write, a listing. A process
/// holding a privilege that walks through the refusal would leave a check
/// asserting nothing, so no site here assumes it.
/// </para>
/// </remarks>
internal sealed class DeniedPaths : IDisposable
{
    private readonly List<string> denied = [];

    /// <summary>Refuses the running user the named rights to a path.</summary>
    /// <param name="path">The file or directory.</param>
    /// <param name="rights">The rights, in icacls' own spelling - "(R)", "(RX)", "(W)".</param>
    internal void Deny(string path, string rights)
    {
        Icacls(path, "/deny", $"{Environment.UserName}:{rights}");
        denied.Add(path);
    }

    public void Dispose()
    {
        foreach (var path in denied)
        {
            Icacls(path, "/remove:d", Environment.UserName);
        }

        denied.Clear();
    }

    private static void Icacls(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo("icacls")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add(path);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
        process?.WaitForExit();
    }
}
