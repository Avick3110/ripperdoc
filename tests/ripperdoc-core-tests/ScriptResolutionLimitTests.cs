using System.Runtime.CompilerServices;
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
        // One assertion, not two: comparing the set against itself cannot fail
        // under any defect, because both readings project one cached list.
        Assert.Equal(
            ScriptResolutionLimit.All.Select(limit => limit.Name).ToList(),
            ScriptResolutionLimit.All.Select(limit => limit.Name).OrderBy(
                name => name, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void NoLimitsConsequenceCarriesACount()
    {
        // One half of the invariant the consequence declares - no mod, no
        // method, no count - is mechanically checkable, and this is that half.
        // A count is exactly what the deleted sentence interpolated, so a digit
        // reappearing here is that sentence returning one limit at a time.
        //
        // The other two halves are not checked and are not claimed to be: no
        // list of mod or method names exists to check against. What stands
        // behind them instead is that a limit is built with no access to a
        // result, so there is nothing for it to name.
        Assert.All(
            ScriptResolutionLimit.All,
            limit => Assert.False(
                limit.Consequence.Any(char.IsDigit),
                limit.Name + " carries a digit in its consequence, which is a count: "
                + limit.Consequence));
    }

    [Fact]
    public void AWitnessWithNoSourceIsRefused()
    {
        // The guard says a witness provoking nothing is the state the
        // completeness check exists to name. Every witness in the tree supplies
        // sources, so without this the refusal is a sentence rather than a
        // guard: the constructor is the one gate, and a gate nothing has been
        // seen to close is not known to close.
        var refusal = Assert.Throws<ArgumentException>(() => new ScriptLayerWitness());

        Assert.Contains("provokes nothing", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALimitTheDeclarationsDoNotHoldIsRefusedRatherThanNamed()
    {
        // The Name guard, reached the only way it can be reached: an instance
        // that never came from a declaration. What it refuses is a limit a
        // result could carry while no check walking the set can find it, which
        // would leave the completeness check passing over it while results went
        // on reporting it.
        var stranger = (ScriptResolutionLimit)RuntimeHelpers.GetUninitializedObject(
            typeof(ScriptResolutionLimit));

        var refusal = Assert.Throws<InvalidOperationException>(() => stranger.Name);

        Assert.Contains(
            "not among the ones read back", refusal.Message, StringComparison.Ordinal);
    }
}
