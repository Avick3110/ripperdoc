using Ripperdoc.Core.Reporting;
using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The reporting surface no longer depends on anyone remembering to wire a
/// member: a limit either reaches a result, or a check says it did not.
/// </summary>
public sealed class ScriptResolutionLimitTests
{
    [Fact]
    public void EveryDeclaredLimitReachesAResult()
    {
        var unreached = KindCompleteness.KindsReachingNoResult<ScriptResolutionLimit>();

        Assert.True(
            unreached.Count == 0,
            "These limits are declared and reach no result: " + string.Join(", ", unreached)
            + ". A limit nothing produces is a reason a result could be wrong that no result "
            + "carries, which reads as a result with nothing unresolved about it.");
    }

    [Fact]
    public void TheCheckSeesALimitThatReachesNoResult()
    {
        // The permanent known-RED. Run over a set holding one member whose
        // witness does not provoke it, the check has to name that member and
        // no other.
        var unreached = KindCompleteness.KindsReachingNoResult<UnwiredKindProbe>();

        Assert.Equal(new[] { nameof(UnwiredKindProbe.Unwired) }, unreached.ToList());
    }

    [Fact]
    public void TheProbeSetIsReadWholeBeforeItsVerdictIsBelieved()
    {
        // A derivation that came back short would leave the cell above green
        // by finding nothing wrong with members it never read. Both members
        // are accounted for, and the wired one is what makes the verdict a
        // discrimination rather than a blanket.
        Assert.Equal(2, KindCompleteness.DeclaredCount<UnwiredKindProbe>());
        Assert.NotEmpty(ScriptResolutionLimit.All);
    }

    [Fact]
    public void EveryLimitDeclaredIsOneTheSetReports()
    {
        // The two readings of the same declarations, compared by identity. A
        // limit written in a shape reflection does not reach would be produced
        // by results while being absent from every check that walks the set.
        var reflected = ScriptResolutionLimit.All;
        var constructed = DeclaredKinds.Constructed<ScriptResolutionLimit>();

        Assert.Equal(constructed.Count, reflected.Count);
        foreach (var limit in constructed)
        {
            Assert.Contains(limit, reflected, ReferenceEqualityComparer.Instance);
        }
    }

    [Fact]
    public void ALimitReportsTheNameItIsDeclaredUnder()
    {
        Assert.Equal(
            nameof(ScriptResolutionLimit.PluginScriptsNotSupplied),
            ScriptResolutionLimit.PluginScriptsNotSupplied.Name);
        Assert.Equal(
            nameof(ScriptResolutionLimit.WrapBodyNotResolved),
            ScriptResolutionLimit.WrapBodyNotResolved.ToString());
    }

    [Fact]
    public void TheReportedSetIsOrderedTheSameWayOnEveryReading()
    {
        Assert.Equal(
            ScriptResolutionLimit.All.Select(limit => limit.Name).ToList(),
            ScriptResolutionLimit.All.Select(limit => limit.Name).ToList());
        Assert.Equal(
            ScriptResolutionLimit.All.Select(limit => limit.Name).ToList(),
            ScriptResolutionLimit.All.Select(limit => limit.Name).OrderBy(
                name => name, StringComparer.Ordinal).ToList());
    }
}
