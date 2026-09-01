namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// Where one mod falls in the partition of wanted against deployed.
/// </summary>
/// <remarks>
/// The set is closed and every mod the partition is given lands in exactly one
/// of these, each with a reason. A fourth outcome arriving silently - a mod
/// dropped because it fitted nothing - is the failure this set exists to make
/// impossible rather than to describe.
/// </remarks>
public enum PartitionBucket
{
    /// <summary>Wanted, and the manager's record claims files from it.</summary>
    Deployed,

    /// <summary>Wanted, and the manager's record claims no file from it.</summary>
    Missing,

    /// <summary>Wanted, and nothing available can say whether it is deployed.</summary>
    Unresolvable,

    /// <summary>Deployed, and the profile does not ask for it.</summary>
    Unclaimed,
}
