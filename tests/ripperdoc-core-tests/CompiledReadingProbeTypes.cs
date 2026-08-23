// Types written here so the reading of a compiled type model can be exercised
// on shapes the pinned one cannot be made to hold on demand - two properties
// stored under one name. Nothing here is game-derived.
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Tests;

/// <summary>A class carrying one property under a stored name.</summary>
public class ProbeStoredNameAlone : RedBaseClass
{
    /// <summary>The property, under the shared stored name.</summary>
    [RED("bar")]
    public CName Bar { get; set; } = new();
}

/// <summary>
/// A class carrying a different property under that same stored name, so the
/// two can be told apart by what they are stored as.
/// </summary>
public class ProbeStoredNameAloneOther : RedBaseClass
{
    /// <summary>The other property, under the shared stored name.</summary>
    [RED("bar")]
    public CFloat BarNumber { get; set; } = new();
}

/// <summary>A class whose two properties are stored under one name.</summary>
public class ProbeDuplicateStoredName : RedBaseClass
{
    /// <summary>One of the two properties sharing a stored name.</summary>
    [RED("bar")]
    public CName Bar { get; set; } = new();

    /// <summary>The other, which the reading cannot also carry under it.</summary>
    [RED("bar")]
    public CFloat BarNumber { get; set; } = new();
}
