// The other half of the deliberate name clash, in a second namespace because
// one file cannot hold two.
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Tests.Elsewhere;

/// <summary>A type whose simple name is already taken.</summary>
public class gamedataProbeClash_Record : RedBaseClass
{
}

/// <summary>The other type of that name, declaring a different field.</summary>
public class ProbeSharedBase : RedBaseClass
{
    /// <summary>A field only this side of the clash declares.</summary>
    [RED("fieldFromElsewhere")]
    public CName FieldFromElsewhere { get; set; } = new();
}

/// <summary>A record type whose base is the other half of the clashing pair.</summary>
public class gamedataProbeInheritsElsewhere_Record : ProbeSharedBase
{
}
