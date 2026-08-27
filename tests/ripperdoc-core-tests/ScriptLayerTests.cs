using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The resolved state of a script layer, against the rows of the published
/// measurement.
/// </summary>
/// <remarks>
/// The collision table is reproduced as its own fixture pair: the same two
/// bodies swapped between the same two file names, which is the design that
/// separated "the later source wins" from "one of these is special".
/// </remarks>
public class ScriptLayerTests
{
    private const string Type = "TargetType";
    private const string Method = "TargetMethod";

    private static MethodIdentity Target => new(Type, Method);

    [Fact]
    public void TheLastReplacementInCompileOrderWins()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a_one.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b_two.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target);

        Assert.NotNull(contest);
        Assert.True(contest.IsContested);
        Assert.Equal("b_two.reds", contest.Winner!.Source.Path);
    }

    [Fact]
    public void TheWinnerFollowsThePositionAndNotTheSource()
    {
        // The swap. Under "the later source wins" these two disagree; under any
        // rule that turns on the content of a source they would agree, and this
        // check is the one that tells them apart.
        using var first = SyntheticScriptLayer.Of(
            ("a_one.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b_two.reds", SyntheticScriptLayer.Wraps(Type, "Other")));
        using var second = SyntheticScriptLayer.Of(
            ("a_one.reds", SyntheticScriptLayer.Wraps(Type, "Other")),
            ("b_two.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        Assert.Equal("a_one.reds", ScriptLayer.Read(first.Root).ContestFor(Target)!.Winner!.Source.Path);
        Assert.Equal("b_two.reds", ScriptLayer.Read(second.Root).ContestFor(Target)!.Winner!.Source.Path);
    }

    [Fact]
    public void EveryReplacementButTheLastIsReportedAsOverridden()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("c.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);
        var contest = state.ContestFor(Target)!;

        Assert.Equal(["a.reds", "b.reds"], contest.Overridden.Select(a => a.Source.Path));
        Assert.Equal("c.reds", contest.Winner!.Source.Path);
        Assert.Equal(2, state.SilentlyOverriddenReplacements.Count);
    }

    [Fact]
    public void TheFirstReplacementIsTheLoserNoWarningNames()
    {
        // The compiler warns once per replacement after the first, on the one
        // doing the overwriting. So the first replacement is the loser that
        // reading the whole log never names, and it is the reason this report
        // exists at all.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("c.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.Equal("a.reds", contest.LoserNoWarningNames!.Source.Path);
    }

    [Fact]
    public void OneReplacementIsNotAContestAndHasNoUnnamedLoser()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.False(contest.IsContested);
        Assert.Empty(contest.Overridden);
        Assert.Null(contest.LoserNoWarningNames);
    }

    [Fact]
    public void AWrapIsKeptWhicheverSideOfTheReplacementItSitsOn()
    {
        // Measured: both orderings produce byte-identical output, so position
        // relative to a replacement changes nothing about a wrap.
        using var before = SyntheticScriptLayer.Of(
            ("a_wrap.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("b_replace.reds", SyntheticScriptLayer.Replaces(Type, Method)));
        using var after = SyntheticScriptLayer.Of(
            ("a_replace.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b_wrap.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        foreach (var root in new[] { before.Root, after.Root })
        {
            var contest = ScriptLayer.Read(root).ContestFor(Target)!;
            Assert.Single(contest.Wraps);
            Assert.NotNull(contest.Winner);
            Assert.False(contest.IsContested);
        }
    }

    [Fact]
    public void WrapsAreListedInCompileOrder()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a_w1.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("b_w2.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("c_w3.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.Equal(
            ["a_w1.reds", "b_w2.reds", "c_w3.reds"],
            contest.Wraps.Select(a => a.Source.Path));
    }

    [Fact]
    public void AWrapThatNeverCallsWhatItWrapsIsReported()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("b.reds", SyntheticScriptLayer.WrapsWithoutCalling(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);

        var dropped = Assert.Single(state.WrapsThatDropTheChain);
        Assert.Equal("b.reds", dropped.Source.Path);
    }

    [Fact]
    public void APluginSourceTakesTheMethodFromEveryModThatReplacedIt()
    {
        // The load-bearing consequence of the compile set having two sources: a
        // plugin script sits after the whole directory, so under last-wins it
        // beats every mod's replacement of the same method.
        using var layer = SyntheticScriptLayer.Of(
            ("zzz_last_in_the_directory.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        // The plugin's source lives outside the script directory, which is where
        // the one measured install carries them. Writing it inside would put the
        // same file at two ranks and make a contest out of one annotation.
        using var elsewhere = SyntheticScriptLayer.Of(
            ("plugin-provided.reds", SyntheticScriptLayer.Replaces(Type, Method)));
        var pluginPath = Path.Combine(elsewhere.Root, "plugin-provided.reds");

        var state = ScriptLayer.Read(layer.Root, [pluginPath]);
        var contest = state.ContestFor(Target)!;

        Assert.Equal(ScriptSourceOrigin.RuntimeExtensionPlugin, contest.Winner!.Source.Origin);
        Assert.DoesNotContain(
            ScriptResolutionLimit.PluginScriptsNotSupplied, contest.Limits);
    }

    [Fact]
    public void AWinnerFoundWithoutPluginSourcesSaysItCouldBeDisplaced()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.True(contest.ResultIsProvisional);
        Assert.Contains(ScriptResolutionLimit.PluginScriptsNotSupplied, contest.Limits);
        Assert.Contains(
            "would take it from whatever this result names",
            contest.Describe(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSentenceCarriesTheCountsItComputes()
    {
        // The description is a computed fact shown to a reader, so a check reads
        // the same facts back out of it: the counts and the winner cannot drift
        // from the collections they come from.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("c.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;
        var sentence = contest.Describe();

        Assert.Contains($"replaced by {contest.Replacements.Count} sources", sentence, StringComparison.Ordinal);
        Assert.Contains($"{contest.Winner!.Source.Display} wins", sentence, StringComparison.Ordinal);
        Assert.Contains($"{contest.Overridden.Count} replacement(s) are overridden", sentence, StringComparison.Ordinal);
        Assert.Contains($"{contest.Wraps.Count} wrap(s)", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSentenceNeverClaimsAnExecutionNesting()
    {
        // The wrap order this engine reports is a compile order. The words that
        // would turn it into a claim about run time are the ones a reader would
        // most naturally supply, so no output sentence may contain them.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        var sentence = ScriptLayer.Read(layer.Root).ContestFor(Target)!.Describe();

        Assert.Contains("compile order", sentence, StringComparison.Ordinal);
        foreach (var forbidden in NestingVocabulary.Forbidden)
        {
            Assert.DoesNotContain(forbidden, sentence, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AMethodOnlyWrappedHasNoWinnerAndIsNotAContest()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.Null(contest.Winner);
        Assert.False(contest.IsContested);

        // The negative claim is displaceable by exactly the input a positive one
        // is - a plugin source compiling after the whole walk - so it carries the
        // same limit rather than going out flat.
        Assert.True(contest.ResultIsProvisional);
        Assert.Contains(ScriptResolutionLimit.PluginScriptsNotSupplied, contest.Limits);

        var sentence = contest.Describe();
        Assert.Contains("is not replaced by any source this reading resolved", sentence, StringComparison.Ordinal);
        Assert.Contains("would take it from whatever this result names", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void AGatedReplacementIsHeldOutOfTheContestRatherThanWinningIt()
    {
        // The inversion this exists to stop: a gated replacement last in compile
        // order would otherwise be named the winner, and the replacement that
        // actually takes the method reported as doing nothing.
        using var layer = SyntheticScriptLayer.Of(
            ("a_real.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b_gated.reds", SyntheticScriptLayer.GatedReplaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.Equal("a_real.reds", contest.Winner!.Source.Path);
        Assert.False(contest.IsContested);
        Assert.Empty(contest.Overridden);
        Assert.Equal("b_gated.reds", Assert.Single(contest.Undetermined).Source.Path);
        Assert.Contains(ScriptResolutionLimit.GatedAnnotationPresent, contest.Limits);
    }

    [Fact]
    public void AGatedWrapIsNotReportedAsWrappingTheMethod()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.GatedWraps(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);
        var contest = state.ContestFor(Target)!;

        Assert.Empty(contest.Wraps);
        Assert.Empty(state.Wrapped);
        Assert.Single(state.UndeterminedAnnotations);
        Assert.Contains(
            "annotation(s) on this method are behind a conditional-compilation gate",
            contest.Describe(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AMethodOnlyGatedIsStillReportedRatherThanDisappearing()
    {
        // Dropping it would be the other silent answer: a reader asking about
        // this method would be told nothing touches it.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.GatedReplaces(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);
        var contest = state.ContestFor(Target);

        Assert.NotNull(contest);
        Assert.Null(contest.Winner);
        Assert.Single(contest.Undetermined);
        Assert.True(contest.ResultIsProvisional);
    }

    [Fact]
    public void AWrapWhoseBodyCouldNotBeReadIsNamedAsUnreadAndNotAsBroken()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.WrapWithAnUnclosedBody(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);
        var contest = state.ContestFor(Target)!;

        Assert.Empty(state.WrapsThatDropTheChain);
        Assert.Single(state.WrapsWhoseBodyCouldNotBeRead);
        Assert.Contains(ScriptResolutionLimit.WrapBodyNotResolved, contest.Limits);
        Assert.Contains(
            "could not read to the end", contest.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoMethodsOnOneTypeAreSeparateContests()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, "One")),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, "Two")));

        var state = ScriptLayer.Read(layer.Root);

        Assert.Empty(state.Contested);
        Assert.Equal(2, state.Methods.Count);
    }

    [Fact]
    public void ASourceThatCannotBeReadEndsTheReadRatherThanShrinkingIt()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        using var held = new FileStream(
            Path.Combine(layer.Root, "a.reds"), FileMode.Open, FileAccess.Read, FileShare.None);

        var failure = Assert.Throws<ScriptReadException>(() => ScriptLayer.Read(layer.Root));

        Assert.Contains("a.reds", failure.Message, StringComparison.Ordinal);
        Assert.Contains("may hold the replacement that wins", failure.Message, StringComparison.Ordinal);
    }
}
