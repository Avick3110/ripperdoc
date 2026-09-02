namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// A file name the state itself named, asked about before anything joins it to
/// a path.
/// </summary>
/// <remarks>
/// <para>
/// The state names files this reader then opens - the manifest its pointer
/// names, the directory a mod is staged under - and a name that is not one
/// plain file name is a name that leaves the directory it is read under or one
/// the platform refuses when it is opened, in the platform's own words rather
/// than this reader's. The platform is asked what a file name is, rather than a
/// list of separators being kept here: a drive-relative name carries no
/// separator and still leaves the directory, because joining a rooted second
/// part returns that part verbatim.
/// </para>
/// <para>
/// One door, so that "the state's own names are asked about" is a property of
/// the code rather than a habit. The joining member takes one of these and not
/// a string, so a site that joins a name the state supplied has already asked;
/// a check holds the member to that.
/// </para>
/// </remarks>
internal readonly struct PlainFileName
{
    private readonly string? named;

    private PlainFileName(string named) => this.named = named;

    /// <summary>The name, as the state spelled it.</summary>
    /// <exception cref="InvalidOperationException">
    /// The value was never made by <see cref="Named" />.
    /// </exception>
    internal string Name =>
        named ?? throw new InvalidOperationException(
            $"a file name the state named was read before it was named. Every one is made by "
            + $"{nameof(PlainFileName)}.{nameof(Named)}, which is what asks the platform whether "
            + "the state's spelling is one plain file name.");

    /// <summary>
    /// A name the state supplied, held to being one plain file name.
    /// </summary>
    /// <param name="named">The name, as the state spelled it.</param>
    /// <param name="what">What named it, for a refusal.</param>
    /// <param name="of">What it names, for a refusal.</param>
    /// <returns>The name.</returns>
    /// <exception cref="StateReadException">
    /// The name is empty, carries a directory part, or holds a character no
    /// file name may hold.
    /// </exception>
    internal static PlainFileName Named(string? named, string what, string of)
    {
        if (named is not { Length: > 0 }
            || Path.GetFileName(named) != named
            || named.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new StateReadException(
                $"{what} names {of} '{named}', and this reader models {of} as one plain file name "
                + "in the directory it is read under. A name that is not one either leaves that "
                + "directory or is refused when it is opened, by the platform rather than by this "
                + "reader.");
        }

        return new PlainFileName(named);
    }

    /// <summary>
    /// The name, under a directory.
    /// </summary>
    /// <param name="directory">The directory it is read under.</param>
    /// <param name="named">The name.</param>
    /// <returns>The path.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="named" /> was never made by <see cref="Named" />.
    /// </exception>
    internal static string Under(string directory, PlainFileName named) =>
        Path.Combine(directory, named.Name);
}
