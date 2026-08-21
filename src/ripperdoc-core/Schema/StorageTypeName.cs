namespace Ripperdoc.Core.Schema;

/// <summary>
/// What counts as the name of a storage type.
/// </summary>
/// <remarks>
/// The type model answers a type it cannot map with an empty name rather than
/// by refusing, and a container of such a type with a name that trails off
/// after the container's own prefix. Both are answers in the shape of a
/// storage type and neither is one, so every place that asks the model for a
/// name checks the answer here - the derivation side, which would otherwise
/// carry a field matching nothing, and the arbitration side, which would
/// otherwise compare an unread type against a schema's and call the schema
/// wrong.
/// </remarks>
internal static class StorageTypeName
{
    /// <summary>
    /// Whether <paramref name="storageType"/> names a storage type at all.
    /// </summary>
    /// <param name="storageType">The name the type model answered with.</param>
    /// <returns>False for an empty name, or one with an empty part.</returns>
    internal static bool IsUsable(string storageType) =>
        storageType.Length > 0
        && !storageType.Split(':').Any(string.IsNullOrEmpty);
}
