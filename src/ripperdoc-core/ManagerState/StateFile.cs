namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The one place this reader opens a file in the manager's state directory.
/// </summary>
/// <remarks>
/// <para>
/// <strong>One site, so that "this cannot write" is a property of the code
/// rather than a habit.</strong> Every read goes through here, opened for
/// reading only and never creating anything, so a write path would have to be
/// added deliberately rather than arrived at by one call opening a file the
/// ordinary way.
/// </para>
/// <para>
/// The share is the one the sibling record reader takes: the owner of these
/// files is a manager that may be running, and a reader stricter than the
/// writer refuses files it could have read.
/// </para>
/// </remarks>
internal static class StateFile
{
    /// <summary>
    /// Reads a file whole.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>Its bytes.</returns>
    /// <exception cref="FileNotFoundException">There is no file at the path.</exception>
    /// <exception cref="DirectoryNotFoundException">There is no directory to look in.</exception>
    internal static byte[] ReadAllBytes(string path)
    {
        using var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();

        file.CopyTo(memory);

        return memory.ToArray();
    }
}
