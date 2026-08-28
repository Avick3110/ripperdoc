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

        Assert.Equal(["a.reds", "b.reds"], contest.OverriddenInCompileOrder.Select(a => a.Source.Path));
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
        Assert.Empty(contest.OverriddenInCompileOrder);
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
            Assert.Single(contest.WrapsInCompileOrder);
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
            contest.WrapsInCompileOrder.Select(a => a.Source.Path));
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
    public void APluginSourceTheWalkAlreadyFoundIsRefusedRatherThanCountedTwice()
    {
        // The same file at two ranks reads as two annotations, which reports one
        // replacement as a contest and names the file as a replacement that lost
        // and does nothing. That sentence names a mod and would be false.
        using var layer = SyntheticScriptLayer.Of(
            ("inside.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var inside = Path.Combine(layer.Root, "inside.reds");

        var failure = Assert.Throws<ScriptReadException>(() => ScriptLayer.Read(layer.Root, [inside]));

        Assert.Contains("also inside the script directory", failure.Message, StringComparison.Ordinal);
        Assert.Contains("inside.reds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingsShortOfTheEnumerationAreRefusedRatherThanResolved()
    {
        // Resolving over part of the order names whichever source is last among
        // the part, which is a different mod, with nothing said.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("c.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var enumeration = ScriptSourceOrder.Of(layer.Root);
        var partial = enumeration.Sources
            .Take(2)
            .Select(source => ScriptAnnotationReader.Read(
                source, File.ReadAllText(Path.Combine(layer.Root, source.Path))))
            .ToList();

        var failure = Assert.Throws<ScriptReadException>(() => ScriptLayer.Of(enumeration, partial));

        Assert.Contains("3 source(s) and 2 reading(s)", failure.Message, StringComparison.Ordinal);
        Assert.Contains("names whichever source is last among the part", failure.Message, StringComparison.Ordinal);
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
            ScriptResolutionLimit.PluginScriptsNotSupplied.Consequence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheDerivedMembersAgreeWithTheCollectionsTheyComeFrom()
    {
        // Each of these is a computed fact a caller reads, so a check reads the
        // same fact back out of the collection it is drawn from. They cannot
        // drift apart without one of them naming a different source.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("c.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;
        var replacements = contest.ReplacementsInCompileOrder;

        Assert.Equal(2, replacements.Count);
        Assert.Same(replacements[^1], contest.Winner);
        Assert.Same(replacements[0], contest.LoserNoWarningNames);
        Assert.Equal(replacements.Take(replacements.Count - 1), contest.OverriddenInCompileOrder);
        Assert.Single(contest.WrapsInCompileOrder);
    }

    [Fact]
    public void NoTextThisEngineEmitsClaimsAnExecutionNesting()
    {
        // Held against the declared set rather than against whatever one
        // fixture happens to produce. A guard whose population is a fixture
        // sees the sentences that fixture reaches and none of the others.
        var consequences = ScriptResolutionLimit.All
            .Select(limit => limit.Consequence)
            .ToList();

        Assert.NotEmpty(consequences);
        Assert.All(consequences, consequence =>
        {
            Assert.False(string.IsNullOrWhiteSpace(consequence));
            foreach (var forbidden in NestingVocabulary.Forbidden)
            {
                Assert.DoesNotContain(forbidden, consequence, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    [Fact]
    public void TheOrderTheAnnotationListsCarryIsStatedWhereACallerCannotMissIt()
    {
        // The order is the one thing about these lists a caller cannot recover
        // from the data, and the reading this project has not measured is the
        // one a reader would supply. So it is in the member's name, which a
        // call site has to spell, rather than in prose a caller may never
        // print. Read off the type, so the name and the claim cannot drift.
        var lists = typeof(MethodContest)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(IReadOnlyList<ScriptAnnotation>))
            .Select(property => property.Name)
            .ToList();

        Assert.NotEmpty(lists);
        Assert.All(lists, name => Assert.Contains("InCompileOrder", name, StringComparison.Ordinal));
        Assert.Contains(nameof(MethodContest.WrapsInCompileOrder), lists);
        Assert.Contains(nameof(MethodContest.ReplacementsInCompileOrder), lists);
    }

    [Fact]
    public void NothingOnTheResultAssemblesASentence()
    {
        // The class this closes is prose asserting more than its parts. With no
        // sentence there is nothing to assert with, and the check that says so
        // reads the type rather than trusting that nobody adds one back.
        var emitters = typeof(MethodContest)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(MethodContest)
                && method.ReturnType == typeof(string)
                && method.GetParameters().Length == 0)
            .Select(method => method.Name)
            .ToList();

        Assert.Empty(emitters);
    }

    [Fact]
    public void AStrayQuoteInOneSourceDoesNotHideTheReplacementBelowIt()
    {
        // The reported consequence of the bound, at the layer: the replacement
        // beneath the stray quote is a carrier of this contest, and losing it
        // turns a contest into an uncontested win naming the wrong source.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", "public func L() -> Void {\n  let s = \"oops;\n}\n"
                + SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.True(contest.IsContested);
        Assert.Equal("b.reds", contest.Winner!.Source.Path);
        Assert.Equal("a.reds", contest.LoserNoWarningNames!.Source.Path);
    }

    [Fact]
    public void AnOddlySpelledSourceMakesEveryResultOfTheReadingSaySo()
    {
        // The compile set decides every winner, so a source taken on this
        // engine's own choice rather than on a measured rule is a limit of the
        // whole reading and not of the contests it happens to touch.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("untouched.REDS", SyntheticScriptLayer.Replaces(Type, "Elsewhere")));

        var state = ScriptLayer.Read(layer.Root);

        Assert.All(state.Methods, contest =>
        {
            Assert.Contains(ScriptResolutionLimit.SourceTakenOnAnUnmeasuredRule, contest.Limits);
        });
        Assert.Contains(
            "spelled with a capital",
            ScriptResolutionLimit.SourceTakenOnAnUnmeasuredRule.Consequence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnattachedAnnotationMakesEveryResultOfTheReadingSaySo()
    {
        // It names no method, so which contest it would have joined is exactly
        // what is unknown. Every result says the reading holds one rather than
        // any result claiming to know which.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", "@replaceMethod(T)\n\n@addMethod(T)\npublic func BrandNew() -> Void {}\n"));

        var state = ScriptLayer.Read(layer.Root);

        Assert.Single(state.AnnotationsNotResolvedToAMethod);
        Assert.All(state.Methods, contest =>
        {
            Assert.Contains(ScriptResolutionLimit.AnnotationCouldNotBeAttached, contest.Limits);
        });
        Assert.Contains(
            "could not be resolved to a method",
            ScriptResolutionLimit.AnnotationCouldNotBeAttached.Consequence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AContendingAnnotationWithAnUnmodelledArgumentReachesTheResultToo()
    {
        // The second door into the same limit. Dropped instead, this source is
        // invisible: the contest reads as one uncontested replacement and no
        // limit says the reading missed a thing.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Replaces(Type, Method)),
            ("b.reds", "@replaceMethod(Mod.T)\npublic func M() -> Void {}\n"));

        var state = ScriptLayer.Read(layer.Root);

        Assert.Equal(["b.reds:1"], state.AnnotationsNotResolvedToAMethod);
        Assert.All(state.Methods, contest =>
        {
            Assert.Contains(ScriptResolutionLimit.AnnotationCouldNotBeAttached, contest.Limits);
        });
    }

    [Fact]
    public void AReadingWithNeitherCarriesNeitherLimit()
    {
        // The arm that keeps the two above honest: an ordinary layer must not
        // pick up either limit, or they would say nothing.
        using var layer = SyntheticScriptLayer.Of(("a.reds", SyntheticScriptLayer.Replaces(Type, Method)));

        var contest = ScriptLayer.Read(layer.Root).ContestFor(Target)!;

        Assert.DoesNotContain(ScriptResolutionLimit.SourceTakenOnAnUnmeasuredRule, contest.Limits);
        Assert.DoesNotContain(ScriptResolutionLimit.AnnotationCouldNotBeAttached, contest.Limits);
    }

    [Fact]
    public void AReadingWithNothingUnresolvedCarriesNoLimitAtAll()
    {
        // The negative arm over the whole declared set. The completeness check
        // asks only whether each limit reaches some result, and a limit that
        // fired on every reading would pass it while naming every result
        // provisional. Asking the opposite question of all of them at once is
        // what the per-limit arms cannot do as the set grows: a limit added
        // later is in this population the day it is declared.
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.Wraps(Type, Method)),
            ("b.reds", SyntheticScriptLayer.Wraps(Type, Method)));

        // Supplied plugin sources are what empties the limit list; the file is
        // inert so that it changes the posture and nothing else.
        using var elsewhere = SyntheticScriptLayer.Of(
            ("plugin-provided.reds", "public func NothingHere() -> Void {}\n"));

        var contest = ScriptLayer
            .Read(layer.Root, [Path.Combine(elsewhere.Root, "plugin-provided.reds")])
            .ContestFor(Target)!;

        Assert.False(
            contest.ResultIsProvisional,
            "These limits fired on a reading with nothing unresolved about it: "
            + string.Join(", ", contest.Limits.Select(limit => limit.Name))
            + ". A limit applying where nothing was left unread names a result "
            + "provisional that is not, which is the same false claim as a sentence "
            + "overstating what was read - and the completeness check cannot see it.");
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
        Assert.Empty(contest.OverriddenInCompileOrder);
        Assert.Equal("b_gated.reds", Assert.Single(contest.UndeterminedInCompileOrder).Source.Path);
        Assert.Contains(ScriptResolutionLimit.GatedAnnotationPresent, contest.Limits);
    }

    [Fact]
    public void AGatedWrapIsNotReportedAsWrappingTheMethod()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("a.reds", SyntheticScriptLayer.GatedWraps(Type, Method)));

        var state = ScriptLayer.Read(layer.Root);
        var contest = state.ContestFor(Target)!;

        Assert.Empty(contest.WrapsInCompileOrder);
        Assert.Empty(state.Wrapped);
        Assert.Single(state.UndeterminedAnnotations);
        Assert.Contains(ScriptResolutionLimit.GatedAnnotationPresent, contest.Limits);
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
        Assert.Single(contest.UndeterminedInCompileOrder);
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
            "could not read to the end",
            ScriptResolutionLimit.WrapBodyNotResolved.Consequence,
            StringComparison.Ordinal);
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
