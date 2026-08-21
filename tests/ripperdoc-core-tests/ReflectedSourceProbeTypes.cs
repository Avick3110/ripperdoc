// Types written here so the no-setup source can be exercised on shapes the
// pinned type model does not happen to contain - an annotation with no name, a
// property that is not a field, a base class that is not a record type, and two
// types that share one name. Nothing here is game-derived.
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Tests;

/// <summary>A record type written here, to exercise annotation handling.</summary>
public class gamedataProbeAnnotated_Record : RedBaseClass
{
    /// <summary>A field the annotation names differently from the property.</summary>
    [RED("annotatedName")]
    public CFloat DifferentPropertyName { get; set; } = new();

    /// <summary>A field the annotation does not name.</summary>
    [RED]
    public CFloat Unnamed { get; set; } = new();

    /// <summary>A property that is not a field at all.</summary>
    public CFloat NotAField { get; set; } = new();
}

/// <summary>A base class that is not itself a record type.</summary>
public class ProbeBaseClass : RedBaseClass
{
    /// <summary>A field every type below this one carries.</summary>
    [RED("inherited")]
    public CName Inherited { get; set; } = new();
}

/// <summary>A record type whose fields are all inherited.</summary>
public class gamedataProbeDerived_Record : ProbeBaseClass
{
}

/// <summary>One half of a deliberate name clash.</summary>
public class gamedataProbeClash_Record : RedBaseClass
{
}
