using Ripperdoc.Core;
using Ripperdoc.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The script layer of a real install, resolved end to end.
/// </summary>
/// <remarks>
/// <para>
/// Tier (ii): this reads a real script directory, which no runner has and which
/// is other people's mod content that this project does not carry. The gate runs
/// it when the environment names a directory and announces it as skipped, by
/// name, when nothing does. Run outside the gate with nothing named, it fails
/// rather than passing quietly.
/// </para>
/// <para>
/// What is asserted is what holds of any script layer. A real layer changes
/// whenever its owner installs a mod, so counts taken from one install would
/// turn an ordinary install into a red run - the numbers are reported instead,
/// and the invariants are what fails.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledScriptLayer)]
public class InstalledScriptLayerTests
{
    private readonly ITestOutputHelper _output;

    public InstalledScriptLayerTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The variable naming the script directory, derived from the brand rather
    /// than spelled out, so a rebrand cannot leave a stale name here.
    /// </summary>
    internal static string VariableName => Branding.Name.ToUpperInvariant() + "_SCRIPTS_PATH";

    private static string ScriptDirectory
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(VariableName);

            return string.IsNullOrWhiteSpace(path)
                ? throw new InvalidOperationException(
                    $"These checks read a real install's script layer, which no runner has. Set "
                    + $"{VariableName} to a script directory to run them. The gate script announces "
                    + "them as skipped, by name, when it cannot run them - an absent input is never "
                    + "reported as a pass.")
                : path;
        }
    }

    [Fact]
    public void TheLayerResolvesAndItsShapeIsReported()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        _output.WriteLine(Report(state));

        Assert.NotEmpty(state.Enumeration.Sources);
    }

    [Fact]
    public void EverySourceHasItsOwnRankAndTheRanksAreTheOrderReported()
    {
        var sources = ScriptLayer.Read(ScriptDirectory).Enumeration.Sources;

        // The compile order is what decides winners, so a duplicated or
        // out-of-sequence rank is a defect rather than a curiosity.
        Assert.Equal(Enumerable.Range(0, sources.Count), sources.Select(source => source.Rank));
    }

    [Fact]
    public void EveryContestNamesAWinnerAndEveryLoserItHas()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        _output.WriteLine(
            $"contested methods: {state.Contested.Count}; "
            + $"silently overridden replacements: {state.SilentlyOverriddenReplacements.Count}");

        Assert.All(state.Contested, contest =>
        {
            Assert.NotNull(contest.Winner);
            Assert.Equal(contest.Replacements.Count - 1, contest.Overridden.Count);
            Assert.NotNull(contest.LoserNoWarningNames);
            Assert.DoesNotContain(contest.Winner, contest.Overridden);
        });
    }

    [Fact]
    public void AWinnerIsAlwaysTheHighestRankedReplacementOfItsMethod()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        Assert.All(
            state.Methods.Where(contest => contest.Winner is not null),
            contest => Assert.Equal(
                contest.Replacements.Max(annotation => annotation.Source.Rank),
                contest.Winner!.Source.Rank));
    }

    [Fact]
    public void ReadWithoutPluginSourcesEveryWinnerSaysItCouldBeDisplaced()
    {
        // The posture is the point. This lane is read from the script directory
        // alone, which is what a caller can find unaided - and every winner it
        // names can be taken by a plugin-contributed source that compiles after
        // all of them.
        var state = ScriptLayer.Read(ScriptDirectory);

        Assert.Equal(PluginScriptPosture.NotSupplied, state.Enumeration.PluginPosture);

        // Every result, not only the ones naming a winner. A method reported as
        // unreplaced rests on the same unread input as one reported as replaced.
        Assert.All(state.Methods, contest =>
        {
            Assert.True(contest.ResultIsProvisional);
            Assert.Contains(ScriptResolutionLimit.PluginScriptsNotSupplied, contest.Limits);
            Assert.Contains(
                "would take it from whatever this result names",
                contest.Describe(),
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void NoGatedAnnotationIsCountedIntoAContestAndEachIsNamed()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        _output.WriteLine($"undetermined annotations: {state.UndeterminedAnnotations.Count}");

        // A gate decides whether its declaration is compiled at all, so nothing
        // gated may appear among the annotations a result is resolved from.
        Assert.All(state.UndeterminedAnnotations, annotation => Assert.True(annotation.IsGated));
        Assert.All(state.Methods, contest =>
        {
            Assert.DoesNotContain(contest.Replacements, annotation => annotation.IsGated);
            Assert.DoesNotContain(contest.Wraps, annotation => annotation.IsGated);

            if (contest.Undetermined.Count > 0)
            {
                Assert.Contains(ScriptResolutionLimit.GatedAnnotationPresent, contest.Limits);
            }
        });
    }

    [Fact]
    public void AWrapIsEitherReadOrReportedUnreadAndNeverBoth()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        _output.WriteLine(
            $"wraps that drop the chain: {state.WrapsThatDropTheChain.Count}; "
            + $"wraps whose body could not be read: {state.WrapsWhoseBodyCouldNotBeRead.Count}");

        // Naming a mod as ending the chain is the strongest thing this lane says
        // about a wrap, and it may rest only on a body that was actually read.
        Assert.Empty(state.WrapsThatDropTheChain.Intersect(state.WrapsWhoseBodyCouldNotBeRead));
        Assert.All(
            state.WrapsThatDropTheChain,
            annotation => Assert.Equal(WrappedCallReading.DoesNotCall, annotation.WrappedCall));
    }

    [Fact]
    public void NoReportedSentenceClaimsAnExecutionNesting()
    {
        var state = ScriptLayer.Read(ScriptDirectory);

        // Every method's sentence, against the one list the tier (i) check reads.
        // Two copies of the vocabulary drift, and the copy that drifts is the one
        // over the layer nobody re-reads.
        foreach (var contest in state.Methods)
        {
            var sentence = contest.Describe();
            foreach (var forbidden in NestingVocabulary.Forbidden)
            {
                Assert.DoesNotContain(forbidden, sentence, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string Report(ScriptLayer state)
    {
        var lines = new List<string>
        {
            $"sources                          {state.Enumeration.Sources.Count}",
            $"annotated methods                {state.Methods.Count}",
            $"contested methods                {state.Contested.Count}",
            $"silently overridden replacements {state.SilentlyOverriddenReplacements.Count}",
            $"wrapped methods                  {state.Wrapped.Count}",
            $"wraps that drop the chain        {state.WrapsThatDropTheChain.Count}",
            $"wraps whose body was not read    {state.WrapsWhoseBodyCouldNotBeRead.Count}",
            $"undetermined (gated) annotations {state.UndeterminedAnnotations.Count}",
            $"methods with a gated annotation  {state.Methods.Count(c => c.Undetermined.Count > 0)}",
            $"annotations with no declaration  {state.Readings.Sum(r => r.AnnotationsWithNoDeclaration.Count)}",
            $"sources not spelled .reds        {state.Enumeration.SourcesNotSpelledInLowerCase.Count}",
        };

        return string.Join(Environment.NewLine, lines);
    }
}
