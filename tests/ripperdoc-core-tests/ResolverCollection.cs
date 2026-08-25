using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Checks that touch the pinned library's process-wide resource resolver.
/// </summary>
/// <remarks>
/// The resolver is one piece of shared, additive, un-unloadable state for the
/// whole process, so checks that read or change it are not independent of each
/// other. Test classes run in parallel by default, which lets one class load a
/// dictionary in the gap between another class's read and its assertion about
/// that same read. Naming them into one collection serialises them.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ResolverCollection
{
    /// <summary>The collection's name, spelled once.</summary>
    public const string Name = "the pinned library's resource resolver";
}
