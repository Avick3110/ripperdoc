using System.Text.Json;

namespace Ripperdoc.Core.Diagnosis;

/// <summary>
/// What the manager recorded of its last deployment.
/// </summary>
/// <param name="Method">How it put the files there, in the manager's own word.</param>
/// <param name="GameId">The game the record is for, in the manager's own word.</param>
/// <param name="TargetPath">The directory it deployed into, as the manager recorded it.</param>
/// <param name="Files">Every file it claims to have deployed, with its source mod.</param>
/// <remarks>
/// The method is carried rather than interpreted. It decides whether a deployed
/// file shares storage with its staged original, which a later reading may want
/// and this one has no measurement about.
/// </remarks>
public sealed record DeploymentRecord(
    string Method, string GameId, string TargetPath, IReadOnlyList<DeployedFile> Files)
{
    /// <summary>The file name the manager writes this record under.</summary>
    public const string FileName = "vortex.deployment.json";

    /// <summary>
    /// Reads the record a game directory carries, or reports that it has none.
    /// </summary>
    /// <param name="gameDirectory">The game directory to look in.</param>
    /// <returns>
    /// The record, or null where the directory carries none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="gameDirectory" /> is null.</exception>
    /// <exception cref="DiagnosisReadException">
    /// The record is present and could not be read.
    /// </exception>
    /// <remarks>
    /// Absence returns null and is not an error: a directory with no record is
    /// the ordinary state of one the manager has purged, or never managed. What
    /// callers must not do is treat that null as an empty deployment - the
    /// difference between "nothing is deployed" and "nothing here can say what
    /// is deployed" is the whole of it.
    /// </remarks>
    public static DeploymentRecord? In(string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(gameDirectory);

        var path = Path.Combine(gameDirectory, FileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path), path) : null;
    }

    /// <summary>
    /// Reads a record from the text of one.
    /// </summary>
    /// <param name="json">The record's text.</param>
    /// <param name="source">What to name in a failure.</param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="DiagnosisReadException">The text is not a record this reader knows.</exception>
    public static DeploymentRecord Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(source);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            throw new DiagnosisReadException(
                $"'{source}' is not readable as JSON, so nothing here can say which mod supplied "
                + "which deployed file.", error);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("files", out var files)
                || files.ValueKind != JsonValueKind.Array)
            {
                throw new DiagnosisReadException(
                    $"'{source}' carries no 'files' array, so it is not a deployment record this "
                    + "reader knows. Reading it as an empty one would report every wanted mod as "
                    + "missing.");
            }

            var deployed = new List<DeployedFile>(files.GetArrayLength());

            foreach (var entry in files.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || entry.TryGetProperty("relPath", out var relative) is false
                    || entry.TryGetProperty("source", out var mod) is false
                    || relative.ValueKind != JsonValueKind.String
                    || mod.ValueKind != JsonValueKind.String)
                {
                    throw new DiagnosisReadException(
                        $"'{source}' holds an entry without both a path and a source mod. An entry "
                        + "skipped here is a deployed file nothing would attribute, which reads as "
                        + "a mod that deployed less than it did.");
                }

                deployed.Add(new DeployedFile(relative.GetString()!, mod.GetString()!));
            }

            return new DeploymentRecord(
                Text(root, "deploymentMethod"),
                Text(root, "gameId"),
                Text(root, "targetPath"),
                deployed);
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : string.Empty;
}
