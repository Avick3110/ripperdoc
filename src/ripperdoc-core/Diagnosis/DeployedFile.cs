namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// One file the manager put into the game directory, and the mod it came from.
/// </summary>
/// <param name="RelativePath">Where it landed, relative to the game directory.</param>
/// <param name="SourceMod">The mod that supplied it.</param>
/// <remarks>
/// <strong>This pairing exists nowhere else.</strong> The manager deploys by
/// hard link, so a deployed file carries no mark of which mod supplied it and
/// no read of the game directory can recover one. Where the record is absent,
/// the deployed side is unknown rather than empty.
/// </remarks>
public readonly record struct DeployedFile(string RelativePath, string SourceMod);
