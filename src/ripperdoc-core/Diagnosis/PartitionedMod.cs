namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One mod, where it fell, and why.
/// </summary>
/// <param name="Id">The manager's identity for the mod.</param>
/// <param name="Bucket">Where it fell.</param>
/// <param name="Reason">Why it fell there.</param>
/// <remarks>
/// The reason travels with every mod rather than only with the unhappy ones. A
/// count on its own leaves a reader to work out whether they have a problem,
/// and the difference between a mod that deploys nothing by construction and
/// one that failed to deploy is not visible in a number.
/// </remarks>
public readonly record struct PartitionedMod(string Id, PartitionBucket Bucket, string Reason);
