using Ripperdoc.Core.ManagerState;

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// The diagnosis lane's readers wired to their inputs: what the manager wants,
/// what it recorded deploying, and every ordering rule either home declares.
/// </summary>
/// <remarks>
/// <para>
/// A composition of the readers rather than a reading of its own. Everything
/// here is a call to something that already answers for itself; what this adds
/// is that each home it could not read is <strong>named</strong>, with the
/// reader's own refusal as the reason, instead of being absent from a verdict
/// that then looks complete.
/// </para>
/// <para>
/// <strong>No wanted set means no partition</strong>, not an empty one.
/// Differencing an empty wanted set against a record gives every deployed mod
/// as unclaimed, which is arithmetic that works and an answer that is false.
/// </para>
/// <para>
/// <strong>A record that is there and could not be read means no partition
/// either</strong>, for the same reason on the other side. Partitioning against
/// a record that is absent from the reading reports every wanted mod as beyond
/// reach for want of a record - which is a sentence about a file that is
/// sitting in the directory.
/// </para>
/// </remarks>
public sealed class ManagerDiagnosis
{
    private ManagerDiagnosis(
        ManagerStateReading? state,
        DeploymentRecord? record,
        DeploymentPartition? partition,
        string? whyNoPartition,
        OrderingGraph ordering,
        IReadOnlyList<string> caveats)
    {
        State = state;
        Record = record;
        Partition = partition;
        WhyNoPartition = whyNoPartition;
        Ordering = ordering;
        Caveats = caveats;
    }

    /// <summary>What the manager's state said, or null where none could be read.</summary>
    public ManagerStateReading? State { get; }

    /// <summary>What the game directory recorded, or null where it carries none.</summary>
    public DeploymentRecord? Record { get; }

    /// <summary>The partition, or null where there is no wanted set to partition.</summary>
    public DeploymentPartition? Partition { get; }

    /// <summary>Why there is no partition, where there is none.</summary>
    public string? WhyNoPartition { get; }

    /// <summary>The ordering graph over every rule home that could be read.</summary>
    public OrderingGraph Ordering { get; }

    /// <summary>What this reading did not establish about itself.</summary>
    public IReadOnlyList<string> Caveats { get; }

    /// <summary>
    /// Reads a manager's state and a game directory, and reports what each home
    /// said or why it could not be read.
    /// </summary>
    /// <param name="stateDirectory">The manager's state directory.</param>
    /// <param name="gameId">The game, in the manager's own word for it.</param>
    /// <param name="gameDirectory">The directory the manager deploys into.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static ManagerDiagnosis Of(string stateDirectory, string gameId, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(stateDirectory);
        ArgumentNullException.ThrowIfNull(gameId);
        ArgumentNullException.ThrowIfNull(gameDirectory);

        var read = new List<OrderingRuleSet>();
        var notRead = new List<UnreadRuleSet>();
        var caveats = new List<string>();

        var state = Read(
            () => ManagerStateReading.Of(stateDirectory, gameId),
            $"the manager's state database in '{stateDirectory}'",
            notRead,
            out var stateRefusal);

        if (state is not null)
        {
            read.Add(state.Rules);
            caveats.AddRange(state.State.Caveats);
            ReadManifests(state, read, notRead);
        }
        else
        {
            if (stateRefusal is null)
            {
                notRead.Add(new UnreadRuleSet(
                    $"the manager's state database in '{stateDirectory}'",
                    "there is no state database in that directory, so neither the mods the "
                    + "manager wants nor the rules it holds about them can be read from it"));
            }

            notRead.Add(new UnreadRuleSet(
                "a curated list's manifest",
                "which curated lists are staged is read out of the manager's state, and none was "
                + "read - so no manifest was looked for"));
        }

        var record = Read(
            () => DeploymentRecord.In(gameDirectory),
            $"the deployment record in '{gameDirectory}'",
            out var recordRefusal);

        if (recordRefusal is not null)
        {
            caveats.Add(recordRefusal);
        }

        return new ManagerDiagnosis(
            state,
            record,
            state?.Wanted is null || recordRefusal is not null
                ? null
                : DeploymentPartition.Of(state.Wanted, record),
            state?.Wanted is null
                ? state is null
                    ? stateRefusal
                      ?? $"no manager state was read from '{stateDirectory}', so nothing here "
                         + "knows which mods were wanted - and the game directory cannot say, "
                         + "because a deployed file carries no mark of the mod that supplied it"
                    : state.WhyNoProfile
                : recordRefusal is null
                    ? null
                    : $"{recordRefusal} - so nothing here can say which of the mods the manager "
                      + "wants are deployed, and a partition built without it would report every "
                      + "one of them as though the game directory carried no record at all",
            OrderingGraph.Over(read, notRead),
            caveats);
    }

    private static void ReadManifests(
        ManagerStateReading state, List<OrderingRuleSet> read, List<UnreadRuleSet> notRead)
    {
        var paths = CollectionManifest.PathsIn(state);

        if (paths.Count == 0)
        {
            notRead.Add(new UnreadRuleSet(
                "a curated list's manifest",
                state.Wanted is null
                    ? "which curated lists are staged is a property of the profile, and no "
                      + "profile was selected - so nothing here can say where a manifest would be"
                    : state.StagingRoot is { Length: > 0 }
                        ? "the manager's state names no staged curated list for this game, so "
                          + "there is no manifest to read"
                        : "the manager's state does not record where it stages this game's mods, "
                          + "so nothing here can say where a curated list's manifest would be"));

            return;
        }

        foreach (var path in paths)
        {
            var manifest = Read(
                () => CollectionManifest.In(path, state), path, notRead, out var refusal);

            if (manifest is not null)
            {
                read.Add(manifest.Rules);
            }
            else if (refusal is null)
            {
                notRead.Add(new UnreadRuleSet(
                    path,
                    "the manager stages a curated list here and there is no manifest in it, so "
                    + "the rules that list declares are not in this graph"));
            }
        }
    }

    /// <remarks>
    /// The reader's own refusal becomes the reason the home is named as unread.
    /// A composition that swallowed it would report a home it could not read as
    /// a home with nothing in it.
    /// </remarks>
    private static T? Read<T>(Func<T?> read, string home, List<UnreadRuleSet> notRead, out string? refusal)
        where T : class
    {
        try
        {
            refusal = null;

            return read();
        }
        catch (StateReadException error)
        {
            refusal = error.Message;
            notRead.Add(new UnreadRuleSet(home, error.Message));

            return null;
        }
    }

    private static DeploymentRecord? Read(
        Func<DeploymentRecord?> read, string home, out string? refusal)
    {
        try
        {
            refusal = null;

            return read();
        }
        catch (DiagnosisReadException error)
        {
            refusal = $"{home} is there and could not be read: {error.Message}";

            return null;
        }
    }
}
