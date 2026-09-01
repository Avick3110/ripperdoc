namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// Every mod the manager wants and every mod it deployed, each in one bucket
/// with a reason.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The partition is exhaustive by construction.</strong> Every enabled
/// mod is emitted exactly once, and every mod the deployment record attributes
/// a file to is emitted exactly once. Nothing is filtered on the way through,
/// so a mod that fits no case cannot fall out of the reading unnoticed - there
/// is no path that drops one.
/// </para>
/// <para>
/// <strong>An absent deployment record is not an empty one.</strong> The
/// manager deploys by hard link, so a deployed file carries no mark of the mod
/// that supplied it and no read of the game directory recovers one. With no
/// record, every wanted mod is <see cref="PartitionBucket.Unresolvable" />
/// rather than missing: differencing against an empty set gives arithmetic that
/// works and an answer that is false.
/// </para>
/// </remarks>
public sealed class DeploymentPartition
{
    private DeploymentPartition(IReadOnlyList<PartitionedMod> mods, bool recordWasRead)
    {
        Mods = mods;
        RecordWasRead = recordWasRead;
    }

    /// <summary>Every mod, in one bucket each, ordered by identity.</summary>
    public IReadOnlyList<PartitionedMod> Mods { get; }

    /// <summary>Whether a deployment record was available to read.</summary>
    public bool RecordWasRead { get; }

    /// <summary>How many mods fell in one bucket.</summary>
    /// <param name="bucket">The bucket to count.</param>
    /// <returns>The number of mods in it.</returns>
    public int Count(PartitionBucket bucket) => Mods.Count(mod => mod.Bucket == bucket);

    /// <summary>
    /// Partitions what the manager wants against what it recorded deploying.
    /// </summary>
    /// <param name="known">Every mod the manager knows, enabled or not.</param>
    /// <param name="record">
    /// The deployment record, or null where the game directory carries none.
    /// </param>
    /// <returns>The partition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="known" /> is null.</exception>
    public static DeploymentPartition Of(IReadOnlyList<ManagerMod> known, DeploymentRecord? record)
    {
        ArgumentNullException.ThrowIfNull(known);

        var wanted = known.Where(mod => mod.Enabled).ToList();
        var mods = new List<PartitionedMod>(wanted.Count);

        if (record is null)
        {
            foreach (var mod in wanted)
            {
                mods.Add(new PartitionedMod(
                    mod.Id,
                    PartitionBucket.Unresolvable,
                    "the game directory carries no deployment record, and a deployed file carries "
                    + "no mark of the mod that supplied it - so nothing here can say whether this "
                    + "mod is deployed"));
            }

            return new DeploymentPartition(Ordered(mods), recordWasRead: false);
        }

        var supplying = record.Files
            .Select(file => file.SourceMod)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var mod in wanted)
        {
            mods.Add(supplying.Contains(mod.Id)
                ? new PartitionedMod(mod.Id, PartitionBucket.Deployed, "the record claims files from it")
                : new PartitionedMod(mod.Id, PartitionBucket.Missing, MissingBecause(mod)));
        }

        var enabled = wanted.Select(mod => mod.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var id in supplying.Where(id => !enabled.Contains(id)))
        {
            mods.Add(new PartitionedMod(
                id,
                PartitionBucket.Unclaimed,
                "the record attributes deployed files to it and the profile does not ask for it"));
        }

        return new DeploymentPartition(Ordered(mods), recordWasRead: true);
    }

    /// <remarks>
    /// A mod the manager gives a kind is a mod whose absence from the record may
    /// be its own shape rather than a failure - a container for a curated list
    /// declares no deployable content and deploys nothing by construction. The
    /// kind is reported rather than interpreted, because which kinds deploy
    /// nothing is the manager's business and not something measured here.
    /// </remarks>
    private static string MissingBecause(ManagerMod mod) =>
        string.IsNullOrEmpty(mod.Kind)
            ? "the record claims no file from it"
            : $"the record claims no file from it, and the manager calls it '{mod.Kind}' rather "
              + "than an ordinary mod";

    private static List<PartitionedMod> Ordered(List<PartitionedMod> mods)
    {
        mods.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return mods;
    }
}
