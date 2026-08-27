using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The compile order, against the rows of the published measurement.
/// </summary>
/// <remarks>
/// The table in the enumeration section of the script-annotation finding is
/// reproduced here as a fixture: same nine names, same nesting, same expected
/// sequence. If the compiler's order is ever re-measured differently, this is
/// the check that has to change with it.
/// </remarks>
public class ScriptSourceOrderTests
{
    /// <summary>
    /// The nine sources of the finding's enumeration table, in the order the
    /// compiler printed them.
    /// </summary>
    private static readonly string[] MeasuredOrder =
    [
        "1digit.reds",
        Path.Combine("Alpha", "inner.reds"),
        Path.Combine("Alpha", "Nested", "deep.reds"),
        "A_first.reds",
        Path.Combine("beta", "inner.reds"),
        "m_root.reds",
        Path.Combine("zeta", "inner.reds"),
        "z_last.reds",
        "_underscore.reds",
    ];

    private static SyntheticScriptLayer TheMeasuredTree() =>
        SyntheticScriptLayer.Of(MeasuredOrder.Select(path => (path, "public class C {}\n")).ToArray());

    [Fact]
    public void TheOrderIsTheOneTheCompilerPrinted()
    {
        using var layer = TheMeasuredTree();

        var enumeration = ScriptSourceOrder.Of(layer.Root);

        Assert.Equal(MeasuredOrder, enumeration.Sources.Select(source => source.Path));
    }

    [Fact]
    public void ASubdirectoryIsWalkedWhereItsOwnNameSitsAmongItsSiblings()
    {
        using var layer = TheMeasuredTree();

        var order = ScriptSourceOrder.Of(layer.Root).Sources.Select(source => source.Path).ToList();

        // The two rows that decide the rule, stated as the relations they are
        // rather than as positions, so the check keeps its meaning if the
        // fixture grows.
        Assert.True(
            order.IndexOf(Path.Combine("Alpha", "inner.reds")) < order.IndexOf("A_first.reds"),
            "a directory's contents come before a sibling file whose uppercased name sorts after the "
            + "directory's - the walk enters it in place");
        Assert.True(
            order.IndexOf("_underscore.reds") > order.IndexOf("z_last.reds"),
            "a leading underscore sorts after the letters, which is what an uppercased comparison does "
            + "and a lowercased one does not");
    }

    [Fact]
    public void OrderingIsNotPlainOrdinalAndNotLowercased()
    {
        using var layer = TheMeasuredTree();

        var measured = ScriptSourceOrder.Of(layer.Root).Sources.Select(source => source.Path).ToList();

        // Both of these were run against the compiler and both disagreed with
        // it. Naming them here keeps the two refuted hypotheses attached to the
        // rule that replaced them.
        var ordinal = measured.OrderBy(path => path, StringComparer.Ordinal).ToList();
        var lowered = measured.OrderBy(path => path.ToLowerInvariant(), StringComparer.Ordinal).ToList();

        Assert.NotEqual(ordinal, measured);
        Assert.NotEqual(lowered, measured);
    }

    [Fact]
    public void RanksAreUniqueAndAscendInTheOrderReported()
    {
        using var layer = TheMeasuredTree();

        var sources = ScriptSourceOrder.Of(layer.Root).Sources;

        Assert.Equal(Enumerable.Range(0, sources.Count), sources.Select(source => source.Rank));
    }

    [Fact]
    public void PluginSourcesComeAfterEverySourceInTheDirectory()
    {
        using var layer = TheMeasuredTree();

        var enumeration = ScriptSourceOrder.Of(layer.Root, ["AAA_plugin.reds"]);

        var plugin = Assert.Single(
            enumeration.Sources.Where(source => source.Origin == ScriptSourceOrigin.RuntimeExtensionPlugin));

        // The name is deliberately one that sorts first: were plugin sources
        // merged into the walk's ordering, this one would lead it.
        Assert.Equal(enumeration.Sources.Count - 1, plugin.Rank);
        Assert.All(
            enumeration.Sources.Where(source => source.Origin == ScriptSourceOrigin.ScriptDirectory),
            source => Assert.True(source.Rank < plugin.Rank));
    }

    [Fact]
    public void PluginSourcesKeepTheOrderTheyWereGivenRatherThanTheirNameOrder()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", "public class C {}\n"));

        // The order measured on a real install: within one plugin, a name
        // beginning "p" preceded one beginning "m". Sorting them would impose an
        // order the compiler does not use.
        var enumeration = ScriptSourceOrder.Of(layer.Root, ["packed.reds", "module.reds"]);

        Assert.Equal(
            ["packed.reds", "module.reds"],
            enumeration.Sources
                .Where(source => source.Origin == ScriptSourceOrigin.RuntimeExtensionPlugin)
                .Select(source => source.Path));
    }

    [Fact]
    public void ThePostureSaysWhetherPluginSourcesWereSupplied()
    {
        using var layer = SyntheticScriptLayer.Of(("a.reds", "public class C {}\n"));

        Assert.Equal(PluginScriptPosture.NotSupplied, ScriptSourceOrder.Of(layer.Root).PluginPosture);
        Assert.True(ScriptSourceOrder.Of(layer.Root).WinnersCanBeDisplacedByUnseenSources);

        var supplied = ScriptSourceOrder.Of(layer.Root, ["p.reds"]);
        Assert.Equal(PluginScriptPosture.Supplied, supplied.PluginPosture);
        Assert.False(supplied.WinnersCanBeDisplacedByUnseenSources);
    }

    [Fact]
    public void ASourceWhoseExtensionCarriesACapitalIsTakenAndReported()
    {
        using var layer = SyntheticScriptLayer.Of(
            ("plain.reds", "public class C {}\n"),
            ("shouty.REDS", "public class D {}\n"));

        var enumeration = ScriptSourceOrder.Of(layer.Root);

        // Whether the compiler reads such a file was never observed. This engine
        // takes it, and says which sources that decision covers, so the choice
        // is visible rather than buried in a match.
        Assert.Equal(2, enumeration.Sources.Count);
        Assert.Equal(["shouty.REDS"], enumeration.SourcesNotSpelledInLowerCase);
    }

    [Fact]
    public void ADirectoryThatIsNotThereIsRefusedRatherThanReadAsAnEmptyLayer()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ripperdoc-no-such-" + Guid.NewGuid().ToString("N"));

        var failure = Assert.Throws<DirectoryNotFoundException>(() => ScriptSourceOrder.Of(missing));

        Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
    }
}
