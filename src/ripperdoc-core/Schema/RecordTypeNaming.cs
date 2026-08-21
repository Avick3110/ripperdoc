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
}
