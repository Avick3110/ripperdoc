using Ripperdoc.Core.Diagnosis;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The cycle check, over rule sets this project wrote.
/// </summary>
/// <remarks>
/// The rules are synthetic because a cycle turns on the shape of the graph and
/// not on which mods it names - so no real mod's identity is needed to
/// reproduce a row, and none is here. The three acyclic rows of the finding's
/// ordering table are reproduced as shapes: a large rule set, a small one, and
/// one dominated by rules that are not precedence claims.
/// </remarks>
public sealed class OrderingGraphTests
{
    private const string Home = "a curated list";

    /// <summary>
    /// A rule set with no cycle in it reports none, and says what it read.
    /// </summary>
    [Fact]
    public void AnAcyclicRuleSetReportsNoCycle()
    {
        var graph = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("b", "c", OrderingRuleKind.Before),
            new OrderingRule("d", "c", OrderingRuleKind.After));

        Assert.Empty(graph.Cycles);
        Assert.Equal(4, graph.NodeCount);
        Assert.Equal(3, graph.EdgeCount);
        Assert.Equal([Home], graph.HomesRead);
    }

    /// <summary>
    /// Two rules that contradict each other are reported as the path that
    /// closes, with the first mod repeated as the last.
    /// </summary>
    [Fact]
    public void TwoContradictingRulesAreReportedAsAPath()
    {
        var graph = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("a", "b", OrderingRuleKind.After));

        var cycle = Assert.Single(graph.Cycles);
        Assert.Equal(["a", "b", "a"], cycle.Path);
    }

    /// <summary>
    /// A cycle running through more than two mods carries all of them.
    /// </summary>
    /// <remarks>
    /// The two-mod case can be satisfied by a check that only ever looks one
    /// edge back, so the longer path is the one that says the walk keeps a
    /// path rather than a predecessor.
    /// </remarks>
    [Fact]
    public void ALongerCycleCarriesEveryMod()
    {
        var graph = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("b", "c", OrderingRuleKind.Before),
            new OrderingRule("c", "d", OrderingRuleKind.Before),
            new OrderingRule("d", "a", OrderingRuleKind.Before));

        var cycle = Assert.Single(graph.Cycles);
        Assert.Equal(["a", "b", "c", "d", "a"], cycle.Path);
    }

    /// <summary>
    /// The two precedence kinds point in opposite directions.
    /// </summary>
    /// <remarks>
    /// The two graphs differ in one rule's kind and in nothing else, and one
    /// closes while the other does not. A two-node arrangement cannot say this:
    /// there, swapping both directions leaves every cycle exactly where it was,
    /// so a check built on one would pass against an implementation that had
    /// the two kinds the wrong way round.
    /// </remarks>
    [Fact]
    public void BeforeAndAfterPointOppositeWays()
    {
        var closes = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("b", "c", OrderingRuleKind.Before),
            new OrderingRule("c", "a", OrderingRuleKind.Before));

        var doesNot = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("b", "c", OrderingRuleKind.Before),
            new OrderingRule("c", "a", OrderingRuleKind.After));

        Assert.Equal(["a", "b", "c", "a"], Assert.Single(closes.Cycles).Path);
        Assert.Empty(doesNot.Cycles);
        Assert.Equal(3, closes.EdgeCount);
        Assert.Equal(3, doesNot.EdgeCount);
    }

    /// <summary>
    /// A rule that is not a precedence claim produces no edge and is counted.
    /// </summary>
    /// <remarks>
    /// The claim the type makes to a reader is that it read the rule and chose
    /// not to edge it. A rule dropped instead of counted would leave the same
    /// no-cycle verdict with nothing saying the graph was quieter than its
    /// input, which is the reading this check exists to keep honest.
    /// </remarks>
    [Fact]
    public void ARuleThatIsNotAPrecedenceClaimIsCountedRatherThanDropped()
    {
        var graph = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Requires),
            new OrderingRule("b", "a", OrderingRuleKind.Requires),
            new OrderingRule("c", "d", OrderingRuleKind.Unmodelled));

        Assert.Empty(graph.Cycles);
        Assert.Equal(0, graph.EdgeCount);
        Assert.Equal(4, graph.NodeCount);
        Assert.Equal(
            [
                new NonEdgeRules(OrderingRuleKind.Unmodelled, 1),
                new NonEdgeRules(OrderingRuleKind.Requires, 2),
            ],
            graph.RulesNotEdges);
    }

    /// <summary>
    /// Rules from more than one home go into one graph, and both homes are
    /// named.
    /// </summary>
    /// <remarks>
    /// The cycle here has one edge from each home, so a graph that read only
    /// one of them reports no cycle - which is the failure this arrangement is
    /// built to catch rather than describe.
    /// </remarks>
    [Fact]
    public void ACycleSpanningTwoHomesIsFoundAndBothHomesAreNamed()
    {
        var graph = OrderingGraph.Over(
            [
                new OrderingRuleSet("a curated list", [new OrderingRule("a", "b", OrderingRuleKind.Before)]),
                new OrderingRuleSet("the manager's own rules", [new OrderingRule("b", "a", OrderingRuleKind.Before)]),
            ],
            []);

        Assert.Single(graph.Cycles);
        Assert.Equal(["a curated list", "the manager's own rules"], graph.HomesRead);
    }

    /// <summary>
    /// A home that could not be read is named on the result with its reason.
    /// </summary>
    [Fact]
    public void AHomeThatWasNotReadIsNamedWithItsReason()
    {
        var graph = OrderingGraph.Over(
            [new OrderingRuleSet(Home, [])],
            [new UnreadRuleSet("the manager's own rules", "no manager instance was supplied")]);

        Assert.Empty(graph.Cycles);
        var unread = Assert.Single(graph.HomesNotRead);
        Assert.Equal("the manager's own rules", unread.Home);
        Assert.Equal("no manager instance was supplied", unread.Reason);
    }

    /// <summary>
    /// One cycle reachable from several starting mods is reported once.
    /// </summary>
    /// <remarks>
    /// Without this the reported count is a property of the order the walk
    /// happened to take rather than of the rules, and a caller counting cycles
    /// would report a number that changes when an unrelated mod is added.
    /// </remarks>
    [Fact]
    public void OneCycleReachableFromSeveralStartsIsReportedOnce()
    {
        var graph = Graph(
            new OrderingRule("a", "b", OrderingRuleKind.Before),
            new OrderingRule("b", "a", OrderingRuleKind.Before),
            new OrderingRule("x", "a", OrderingRuleKind.Before),
            new OrderingRule("y", "b", OrderingRuleKind.Before));

        Assert.Single(graph.Cycles);
    }

    /// <summary>
    /// The same rules give the same path however they are ordered on the way
    /// in.
    /// </summary>
    [Fact]
    public void TheReportedPathDoesNotDependOnTheOrderTheRulesArrive()
    {
        OrderingRule[] rules =
        [
            new("c", "a", OrderingRuleKind.Before),
            new("a", "b", OrderingRuleKind.Before),
            new("b", "c", OrderingRuleKind.Before),
        ];

        var forwards = Graph(rules);
        var backwards = Graph([.. rules.Reverse()]);

        Assert.Equal(forwards.Cycles[0].Path, backwards.Cycles[0].Path);
    }

    /// <summary>
    /// A rule set large enough that a recursive walk would be a real risk is
    /// still answered.
    /// </summary>
    /// <remarks>
    /// The chain is longer than a default stack survives under one frame per
    /// node, which is why the walk is iterative. The cycle at the far end is
    /// what makes the case a reading rather than a smoke test.
    /// </remarks>
    [Fact]
    public void ALongChainIsWalkedWithoutExhaustingTheStack()
    {
        const int Length = 50_000;

        var rules = new List<OrderingRule>(Length);
        for (var i = 0; i < Length - 1; i++)
        {
            rules.Add(new OrderingRule(Name(i), Name(i + 1), OrderingRuleKind.Before));
        }

        rules.Add(new OrderingRule(Name(Length - 1), Name(0), OrderingRuleKind.Before));

        var graph = Graph([.. rules]);

        Assert.Single(graph.Cycles);
        Assert.Equal(Length + 1, graph.Cycles[0].Path.Count);

        // Ordinal order over a fixed width, so the walk starts at node zero and
        // the reported path is the chain in its own order.
        static string Name(int index) => index.ToString("D6");
    }

    /// <summary>
    /// Neither argument may be null.
    /// </summary>
    [Fact]
    public void TheGraphRefusesAMissingArgument()
    {
        Assert.Throws<ArgumentNullException>(() => OrderingGraph.Over(null!, []));
        Assert.Throws<ArgumentNullException>(() => OrderingGraph.Over([], null!));
    }

    private static OrderingGraph Graph(params OrderingRule[] rules) =>
        OrderingGraph.Over([new OrderingRuleSet(Home, rules)], []);
}
