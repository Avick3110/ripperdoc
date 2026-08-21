namespace Ripperdoc.Core;

/// <summary>
/// The single source of truth for the brand string.
/// </summary>
/// <remarks>
/// Every user-visible occurrence of the name - the tool prefix, log banners,
/// the config directory - derives from here rather than appearing as a bare
/// literal. That is what makes a rebrand a one-line change instead of a
/// tree-wide audit, and it is why interior names (namespaces, types, files)
/// stay purpose-based and carry no brand at all.
/// </remarks>
public static class Branding
{
    /// <summary>The brand, lower-case, as it appears to users.</summary>
    public const string Name = "ripperdoc";

    /// <summary>
    /// The prefix every tool on the eventual MCP surface carries.
    /// A tool name that does not start with this is a hard failure, not a
    /// style preference: the prefix is how a caller tells this surface apart
    /// from every other one loaded beside it.
    /// </summary>
    public const string ToolPrefix = Name + "_";
}
