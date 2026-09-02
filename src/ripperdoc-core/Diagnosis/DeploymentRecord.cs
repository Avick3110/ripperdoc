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
    /// <para>
    /// Absence returns null and is not an error: a directory with no record is
    /// the ordinary state of one the manager has purged, or never managed. What
    /// callers must not do is treat that null as an empty deployment - the
    /// difference between "nothing is deployed" and "nothing here can say what
    /// is deployed" is the whole of it.
    /// </para>
    /// <para>
    /// The record is opened once and the open's own not-found report is the
    /// absence signal. Asking whether the file exists and then reading it is
    /// two operations against a file another process owns, and between them the
    /// manager can purge the deployment or replace the record - which is the
    /// state this reader is most likely to be run in, not a remote one.
    /// </para>
    /// </remarks>
    public static DeploymentRecord? In(string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(gameDirectory);

        var path = Path.Combine(gameDirectory, FileName);

        try
        {
            // The share the sibling log reader takes, for the same reason: the
            // owner of these files is a manager that may be mid-deploy, and a
            // reader stricter than the writer refuses files it could have read.
            using var file = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var text = new StreamReader(file);

            return Parse(text.ReadToEnd(), path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Absence is the two catches above and nothing else. A denial or a
            // sharing violation escaping raw would leave a caller that asked
            // for a record holding a platform exception instead of an answer,
            // and take down whatever else that caller had already read.
            throw new DiagnosisReadException(
                $"'{path}' is there and could not be read: {error.Message.TrimEnd('.')}. "
                + "Nothing here can say which mod supplied which deployed file, which is not "
                + "the same as a directory carrying no record. Check that this process may "
                + "read the file and that the manager is not holding it.",
                error);
        }
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
                // Present is not enough: an entry holding an empty path or an
                // empty mod is the same deployed file nothing would attribute,
                // and it survives further than a missing one does.
                if (entry.ValueKind != JsonValueKind.Object
                    || entry.TryGetProperty("relPath", out var relative) is false
                    || entry.TryGetProperty("source", out var mod) is false
                    || relative.ValueKind != JsonValueKind.String
                    || mod.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(relative.GetString())
                    || string.IsNullOrWhiteSpace(mod.GetString()))
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

    /// <summary>
    /// Which mod supplied each deployed path.
    /// </summary>
    /// <returns>The mod that supplied each path this record claims.</returns>
    /// <exception cref="DiagnosisReadException">
    /// The record attributes one path to more than one mod.
    /// </exception>
    /// <remarks>
    /// Two entries claiming one path put the answer at the mercy of which one a
    /// dictionary happened to keep, so this refuses rather than picks. Entries
    /// that agree on the mod are not a contest: they resolve to one answer
    /// however many times they appear, and refusing them would reject a record
    /// that attributes correctly.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ClaimsByPath()
    {
        var contested = Files
            .GroupBy(file => Normalised(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group =>
                group.Select(file => file.SourceMod).Distinct(StringComparer.Ordinal).Count() > 1);

        if (contested is not null)
        {
            throw new DiagnosisReadException(
                $"The deployment record attributes '{contested.Key}' to more than one mod - "
                + string.Join(
                    ", ",
                    contested.Select(file => $"'{file.SourceMod}'")
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal))
                + ". Nothing here can say which of them supplied it, and picking one would "
                + "attribute that file's compile errors to a mod on the strength of an ordering.");
        }

        var claims = new Dictionary<string, string>(Files.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var file in Files)
        {
            claims[Normalised(file.RelativePath)] = file.SourceMod;
        }

        return claims;
    }

    /// <summary>
    /// The separator the join reads paths under.
    /// </summary>
    /// <param name="path">A path as its writer spelled it.</param>
    /// <returns>The same path under one separator.</returns>
    /// <remarks>
    /// The record and the compiler are two writers on one machine and they do
    /// not agree on the separator, so the join is done under one of them.
    /// </remarks>
    internal static string Normalised(string path) => path.Replace('\\', '/');

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : string.Empty;
}
