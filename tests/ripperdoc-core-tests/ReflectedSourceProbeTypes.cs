// Types written here so the no-setup source can be exercised on shapes the
// pinned type model does not happen to contain - an annotation with no name, a
// property that is not a field, a base class that is not a record type, and two
// types that share one name. Nothing here is game-derived.
using System.Text;
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

/// <summary>A base whose simple name is deliberately taken twice.</summary>
public class ProbeSharedBase : RedBaseClass
{
    /// <summary>A field only this side of the clash declares.</summary>
    [RED("fieldFromHere")]
    public CName FieldFromHere { get; set; } = new();
}

/// <summary>A record type whose base is one half of the clashing pair.</summary>
public class gamedataProbeInheritsHere_Record : ProbeSharedBase
{
}

/// <summary>A record type carrying a property the type model cannot map.</summary>
public class gamedataProbeUnmappable_Record : RedBaseClass
{
    /// <summary>A field whose property type names no storage type.</summary>
    [RED("unmappable")]
    public StringBuilder Unmappable { get; set; } = new();

    /// <summary>An ordinary field beside it.</summary>
    [RED("ordinary")]
    public CFloat Ordinary { get; set; } = new();
}

/// <summary>A record-named type that is not part of any public type surface.</summary>
internal class gamedataProbeInternal_Record : RedBaseClass
{
    /// <summary>A field nothing should ever see.</summary>
    [RED("hidden")]
    public CFloat Hidden { get; set; } = new();
}

/// <summary>A record type carrying a container of a type the model cannot map.</summary>
public class gamedataProbeNestedUnmappable_Record : RedBaseClass
{
    /// <summary>A field whose storage type resolves to a container of nothing.</summary>
    [RED("nested")]
    public CArray<ProbeUnmappableElement> Nested { get; set; } = new();

    /// <summary>An ordinary field beside it.</summary>
    [RED("ordinary")]
    public CFloat Ordinary { get; set; } = new();
}

/// <summary>An element type the type model has no name for.</summary>
public sealed class ProbeUnmappableElement : IRedType
{
}
