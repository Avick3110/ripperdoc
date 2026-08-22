using System.Diagnostics;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// A link to a directory, built for a check.
/// </summary>
/// <remarks>
/// Two mechanisms because no one of them works everywhere: a symbolic link is
/// the portable API and needs a privilege an ordinary Windows session does not
/// hold, and a junction needs none and exists only on Windows. Both are a
/// directory the walk will descend into and both resolve to their target, which
/// is all the checks here need of them.
/// </remarks>
internal static class DirectoryLink
{
    /// <summary>
    /// Link a directory, by whichever mechanism this machine allows.
    /// </summary>
    /// <param name="linkPath">Where the link goes.</param>
    /// <param name="targetPath">The directory it points at.</param>
    internal static void Create(string linkPath, string targetPath)
    {
        string symbolic;
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            symbolic = exception.Message;
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Fail(
                "This check needs a directory link and the only mechanism this platform offers was "
                + $"refused: {symbolic}");
            return;
        }

        var junction = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        junction?.WaitForExit();

        if (!Directory.Exists(linkPath))
        {
            Assert.Fail(
                "This check needs a directory link and neither mechanism was available on this machine. "
                + $"A symbolic link was refused: {symbolic}. A junction did not appear either.");
        }
    }

    /// <summary>Remove a link without following it.</summary>
    /// <param name="linkPath">The link.</param>
    /// <remarks>
    /// Deleting the tree that holds it would otherwise be a walk through the
    /// link, which is exactly what these checks build it to prevent.
    /// </remarks>
    internal static void Remove(string linkPath)
    {
        try
        {
            Directory.Delete(linkPath);
        }
        catch (IOException)
        {
            // A link outliving a check is untidy and nothing more.
        }
    }
}
