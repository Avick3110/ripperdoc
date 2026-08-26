using System.Globalization;

namespace Ripperdoc.Core.Archive;

/// <summary>
/// How a resource is written when it is reported.
/// </summary>
/// <remarks>
/// One home, because more than one artifact prints a resource and "report by
/// hash, never omit" has to mean the same string in every one of them. Two
/// sites formatting this independently would drift the first time one of them
/// learned to say something the other did not.
/// </remarks>
internal static class ResourceDisplay
{
    /// <summary>
    /// The resource's name, or its hash when nothing could name it.
    /// </summary>
    internal static string Of(ulong hash, string? name) =>
        string.IsNullOrEmpty(name) ? hash.ToString(CultureInfo.InvariantCulture) : name;
}
