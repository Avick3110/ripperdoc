namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One mod the manager knows about, as its own state describes it.
/// </summary>
/// <param name="Id">
/// The manager's identity for it - the same string its staging directory is
/// named, its installation path is recorded as, and its deployed files are
/// attributed to.
/// </param>
/// <param name="Enabled">Whether the profile under test asks for it.</param>
/// <param name="Kind">
/// What the manager calls it. Empty for an ordinary mod; a mod the manager
/// gives a kind is not necessarily one that deploys anything.
/// </param>
/// <remarks>
/// <strong>One identity, carried whole.</strong> The id is a composite of a
/// display name, a numeric id, a version and a timestamp, and every one of
/// those parts is separately available and separately a worse key: names
/// collide, versions repeat, and the numeric id is shared by every file of a
/// mod. The manager already uses this one string in three places, so a reader
/// picking a different one is choosing to be wrong later.
/// </remarks>
public readonly record struct ManagerMod(string Id, bool Enabled, string Kind);
