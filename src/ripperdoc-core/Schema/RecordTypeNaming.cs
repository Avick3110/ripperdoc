namespace Ripperdoc.Core.Schema;

/// <summary>
/// How a record type is told apart from every other type in the game's type
/// model.
/// </summary>
/// <remarks>
/// The game's type model is one flat namespace holding tens of thousands of
/// classes, of which the record types are a named family. The naming is the
/// only marker they carry, so it is the whole membership test - and because
/// both possible sources of type information describe the same model, the test
/// lives here rather than being spelled out again in each of them.
/// </remarks>
public static class RecordTypeNaming
{
    private const string Prefix = "gamedata";
    private const string Suffix = "_Record";

    /// <summary>
    /// Whether a type of this name is a record type.
    /// </summary>
    /// <param name="typeName">The type name to test.</param>
    /// <returns>True if the name marks a record type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
    public static bool IsRecordTypeName(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return typeName.StartsWith(Prefix, StringComparison.Ordinal)
            && typeName.EndsWith(Suffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// The canonical spelling of a record type name.
    /// </summary>
    /// <param name="typeName">The name as it was written.</param>
    /// <returns>The name with the prefix and suffix present exactly once.</returns>
    /// <remarks>
    /// <para>
    /// The same type is spelled four ways in the wild - with the prefix and the
    /// suffix, with one or the other, and with neither - and all four are
    /// accepted by the framework that reads them. A layer read without
    /// canonicalising reports one type as several, and a schema lookup against
    /// the uncanonical spelling misses a type it knows perfectly well.
    /// </para>
    /// <para>
    /// This is a spelling change and never a validity test. A well-formed name
    /// can still be a type that does not exist, and a name that is not a type at
    /// all comes back from here dressed as one - so the answer is resolved
    /// against the type set afterwards, and an unresolved one is labelled rather
    /// than assumed away.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
    public static string Canonicalise(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        var bare = typeName;

        if (bare.StartsWith(Prefix, StringComparison.Ordinal))
        {
            bare = bare[Prefix.Length..];
        }

        if (bare.EndsWith(Suffix, StringComparison.Ordinal))
        {
            bare = bare[..^Suffix.Length];
        }

        return bare.Length == 0 ? typeName : Prefix + bare + Suffix;
    }
}
