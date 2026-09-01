namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// The precedence graph the ordering rules describe, and the cycles in it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What this graph is a verdict about is its own inputs, and nothing
/// wider.</strong> The homes it read are named in <see cref="HomesRead" />, the
/// homes it did not in <see cref="HomesNotRead" />, and the rules it read
/// without edging in <see cref="RulesNotEdges" />. A caller reporting no cycle
/// without those three is reporting something this type did not compute.
/// </para>
/// <para>
/// It is silent about why any past deployment failed. A manager warning of
/// cycles in a rule set that no longer exists is not a claim this graph can
/// carry, and
/// <c>findings/2026-09-01-manager-state-and-partition.md</c> records the
/// measurement that keeps it out.
/// </para>
/// <para>
/// Nodes and neighbours are walked in ordinal order, so a cycle reported twice
/// over the same rules is reported as the same path. Nothing reads meaning from
/// the order itself.
/// </para>
/// </remarks>
public sealed class OrderingGraph
{
    private const int Open = 1;
    private const int Closed = 2;

    private OrderingGraph(
        IReadOnlyList<OrderingCycle> cycles,
        IReadOnlyList<string> homesRead,
        IReadOnlyList<UnreadRuleSet> homesNotRead,
        IReadOnlyList<NonEdgeRules> rulesNotEdges,
        int nodeCount,
        int edgeCount)
    {
        Cycles = cycles;
        HomesRead = homesRead;
        HomesNotRead = homesNotRead;
        RulesNotEdges = rulesNotEdges;
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
    }

    /// <summary>Every cycle found, each as the path that closes it.</summary>
    public IReadOnlyList<OrderingCycle> Cycles { get; }

    /// <summary>The homes whose rules are in this graph.</summary>
    public IReadOnlyList<string> HomesRead { get; }

    /// <summary>The homes that were not read, each with its reason.</summary>
    public IReadOnlyList<UnreadRuleSet> HomesNotRead { get; }

    /// <summary>Rules read but not turned into edges, counted by kind.</summary>
    public IReadOnlyList<NonEdgeRules> RulesNotEdges { get; }

    /// <summary>How many distinct mods the rules name.</summary>
    public int NodeCount { get; }

    /// <summary>How many distinct edges the graph holds.</summary>
    public int EdgeCount { get; }

    /// <summary>
    /// Builds the graph over the rule sets that were read, naming those that
    /// were not.
    /// </summary>
    /// <param name="read">The rule sets available.</param>
    /// <param name="notRead">The homes that could not be read, with reasons.</param>
    /// <returns>The graph, and the cycles in it.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static OrderingGraph Over(
        IReadOnlyList<OrderingRuleSet> read,
        IReadOnlyList<UnreadRuleSet> notRead)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(notRead);

        var edges = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var nodes = new SortedSet<string>(StringComparer.Ordinal);
        var skipped = new Dictionary<OrderingRuleKind, int>();

        foreach (var rule in read.SelectMany(set => set.Rules))
        {
            nodes.Add(rule.Source);
            nodes.Add(rule.Reference);

            switch (rule.Kind)
            {
                case OrderingRuleKind.Before:
                    Connect(edges, rule.Source, rule.Reference);
                    break;
                case OrderingRuleKind.After:
                    Connect(edges, rule.Reference, rule.Source);
                    break;
                default:
                    skipped[rule.Kind] = skipped.GetValueOrDefault(rule.Kind) + 1;
                    break;
            }
        }

        return new OrderingGraph(
            FindCycles(nodes, edges),
            read.Select(set => set.Home).ToList(),
            notRead.ToList(),
            skipped.OrderBy(pair => pair.Key)
                .Select(pair => new NonEdgeRules(pair.Key, pair.Value))
                .ToList(),
            nodes.Count,
            edges.Sum(pair => pair.Value.Count));
    }

    private static void Connect(
        SortedDictionary<string, SortedSet<string>> edges,
        string from,
        string to)
    {
        if (!edges.TryGetValue(from, out var destinations))
        {
            destinations = new SortedSet<string>(StringComparer.Ordinal);
            edges[from] = destinations;
        }

        destinations.Add(to);
    }

    /// <remarks>
    /// Iterative rather than recursive: a curated list naming thousands of mods
    /// produces paths long enough to matter, and a stack overflow inside a check
    /// that exists to report a defect is a failure toward silence.
    /// </remarks>
    private static List<OrderingCycle> FindCycles(
        SortedSet<string> nodes,
        SortedDictionary<string, SortedSet<string>> edges)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var found = new List<OrderingCycle>();

        foreach (var start in nodes)
        {
            if (state.GetValueOrDefault(start) != 0)
            {
                continue;
            }

            state[start] = Open;
            var path = new List<string> { start };
            var walk = new Stack<(string Node, IEnumerator<string> Remaining)>();
            walk.Push((start, Neighbours(edges, start).GetEnumerator()));

            while (walk.Count > 0)
            {
                var (node, rest) = walk.Peek();

                if (!rest.MoveNext())
                {
                    state[node] = Closed;
                    walk.Pop();
                    path.RemoveAt(path.Count - 1);
                    continue;
                }

                var next = rest.Current;
                var colour = state.GetValueOrDefault(next);

                if (colour == Open)
                {
                    var from = path.IndexOf(next);
                    var cycle = path.GetRange(from, path.Count - from);
                    cycle.Add(next);

                    found.Add(new OrderingCycle(cycle));
                }
                else if (colour == 0)
                {
                    state[next] = Open;
                    path.Add(next);
                    walk.Push((next, Neighbours(edges, next).GetEnumerator()));
                }
            }
        }

        return found;
    }

    private static IEnumerable<string> Neighbours(
        SortedDictionary<string, SortedSet<string>> edges,
        string node) =>
        edges.TryGetValue(node, out var destinations) ? destinations : [];
}
