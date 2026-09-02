using System.Text.Json;
using Ripperdoc.Core.Diagnosis;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// A curated list's own manifest, and the ordering rules it declares.
/// </summary>
/// <remarks>
/// <para>
/// The manifest is the second home of ordering intent, and it does not hold the
/// same rules as the manager's own. Its rule sides name <strong>files</strong>,
/// its declared mods name files, and neither names the identity the manager
/// keys everything else on - so every side is carried through two joins to that
/// one identity, and a side that does not survive both is reported rather than
/// given a node of its own.
/// </para>
/// <para>
/// <strong>Both homes' rules must land on one node space or the graph is wrong
/// in the direction that hides a cycle.</strong> A rule set keyed on anything
/// but the manager's mod id would sit beside the manager's own rules as a
/// disjoint graph, and a cycle running through both would be a cycle neither
/// half could see.
/// </para>
/// </remarks>
public sealed class CollectionManifest
{
    /// <summary>The name the manager writes a curated list's manifest under.</summary>
    public const string FileName = "collection.json";

    private CollectionManifest(
        string path,
        int declaredMods,
        int declaredModsNotInTheState,
        OrderingRuleSet rules,
        IReadOnlyList<UnresolvedRules> rulesNotResolved,
        IReadOnlyList<string> spellingsNamingMoreThanOneDeclaredMod)
    {
        Path = path;
        DeclaredMods = declaredMods;
        DeclaredModsNotInTheState = declaredModsNotInTheState;
        Rules = rules;
        RulesNotResolved = rulesNotResolved;
        SpellingsNamingMoreThanOneDeclaredMod = spellingsNamingMoreThanOneDeclaredMod;
    }

    /// <summary>The manifest this was read from.</summary>
    public string Path { get; }

    /// <summary>How many mods the list declares.</summary>
    public int DeclaredMods { get; }

    /// <summary>
    /// How many of those the manager's state does not know.
    /// </summary>
    /// <remarks>
    /// A list may declare a mod that was never installed. Such a mod is not a
    /// node, because the manager has no identity for it and inventing one is
    /// the second identity this reader exists to avoid.
    /// </remarks>
    public int DeclaredModsNotInTheState { get; }

    /// <summary>The rules, keyed on the manager's own identity, under their home.</summary>
    public OrderingRuleSet Rules { get; }

    /// <summary>Rules whose sides did not survive both joins, counted by declared kind.</summary>
    public IReadOnlyList<UnresolvedRules> RulesNotResolved { get; }

    /// <summary>
    /// Spellings more than one declared mod answers to, which resolve to none
    /// of them.
    /// </summary>
    /// <remarks>
    /// The manager's own reading drops a contested spelling on the same
    /// ground. A rule side naming one of these joins to nothing and is counted
    /// as residue, rather than being attributed to whichever mod the list
    /// declared first.
    /// </remarks>
    public IReadOnlyList<string> SpellingsNamingMoreThanOneDeclaredMod { get; }

    /// <summary>
    /// Where a manifest would be, for every curated list the manager stages.
    /// </summary>
    /// <param name="state">The manager's state for the game.</param>
    /// <returns>The paths, empty where the state stages no curated list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state" /> is null.</exception>
    /// <remarks>
    /// Both halves of the path come out of the state: the staging root from the
    /// setting that records it, the container from the mod the manager gives a
    /// kind of its own. Nothing here searches the disk for a file of that name.
    /// </remarks>
    public static IReadOnlyList<string> PathsIn(ManagerStateReading state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.StagingRoot is not { Length: > 0 } root || state.Wanted is null)
        {
            return [];
        }

        return
        [
            .. state.Wanted
                .Where(mod => mod.Kind.Equals("collection", StringComparison.Ordinal))
                .Select(mod => System.IO.Path.Combine(root, mod.Id, FileName))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Reads a manifest, or reports that there is none at the path.
    /// </summary>
    /// <param name="path">The manifest.</param>
    /// <param name="state">The manager's state, which owns what a mod id is.</param>
    /// <returns>The manifest, or null where there is no file at the path.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="StateReadException">The file is not a manifest this reader models.</exception>
    public static CollectionManifest? In(string path, ManagerStateReading state)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(state);

        byte[] bytes;

        try
        {
            bytes = StateFile.ReadAllBytes(path);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new StateReadException(
                $"'{path}' is there and could not be read: {error.Message.TrimEnd('.')}. The "
                + "manager stages a curated list here, so this is a manifest this reader is "
                + "refused rather than a list that declares no rules.",
                error);
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(bytes);
        }
        catch (JsonException error)
        {
            throw new StateReadException(
                $"'{path}' is not readable as JSON, so the ordering rules the curated list "
                + "declares cannot be read. A graph built without them is quieter than the "
                + "manager's own inputs.",
                error);
        }

        using (document)
        {
            return Read(path, state, document.RootElement);
        }
    }

    private static CollectionManifest Read(string path, ManagerStateReading state, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("mods", out var declared)
            || declared.ValueKind != JsonValueKind.Array)
        {
            throw new StateReadException(
                $"'{path}' carries no 'mods' array, so it is not a curated list's manifest this "
                + "reader knows. Every rule side names a file that only that list can resolve to "
                + "a mod, so reading the rules without it would key them on nothing.");
        }

        var mods = declared.EnumerateArray().ToList();
        var ambiguous = new List<string>();
        var byHash = Index(mods, "md5", ambiguous);
        var byLogical = Index(mods, "logicalFilename", ambiguous);
        var byName = Index(mods, "name", ambiguous, i => Text(mods[i], "name"));

        var identities = mods.Select(mod => state.Identify(
            Text(mod, "source", "md5"), Text(mod, "source", "fileId"))).ToList();

        var rules = new List<OrderingRule>();
        var missed = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var rule in Declared(path, root))
        {
            var kind = Text(rule, "type") ?? string.Empty;
            var source = Resolve(rule, "source", byHash, byLogical, byName, identities);
            var reference = Resolve(rule, "reference", byHash, byLogical, byName, identities);

            if (source is null || reference is null)
            {
                missed[kind] = missed.GetValueOrDefault(kind) + 1;
                continue;
            }

            rules.Add(new OrderingRule(source, reference, Kind(kind)));
        }

        return new CollectionManifest(
            path,
            mods.Count,
            identities.Count(identity => identity is null),
            new OrderingRuleSet($"the curated list's manifest '{path}'", rules),
            [
                .. missed.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new UnresolvedRules(pair.Key, pair.Value)),
            ],
            [.. ambiguous.Order(StringComparer.Ordinal)]);
    }

    private static IReadOnlyList<JsonElement> Declared(string path, JsonElement root) =>
        root.TryGetProperty("modRules", out var rules) && rules.ValueKind == JsonValueKind.Array
            ? ObjectList.In(rules, $"'{path}'", "a declared rule")
            : [];

    /// <remarks>
    /// The order is the one the characterisation measured, and it runs from the
    /// most specific spelling to the least: a hash names one file, a logical
    /// name names one the list happens not to have collided, and a display name
    /// is the last resort rather than the first.
    /// </remarks>
    private static string? Resolve(
        JsonElement rule,
        string side,
        Dictionary<string, int> byHash,
        Dictionary<string, int> byLogical,
        Dictionary<string, int> byName,
        List<string?> identities)
    {
        if (!rule.TryGetProperty(side, out var end) || end.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? at = null;

        foreach (var (index, value) in new (Dictionary<string, int> Index, string? Value)[]
        {
            (byHash, Text(end, "fileMD5")),
            (byLogical, Text(end, "logicalFileName")),
            (byName, Text(end, "logicalFileName")),
            (byLogical, Text(end, "fileExpression")),
        })
        {
            if (value is { Length: > 0 } spelling && index.TryGetValue(spelling, out var hit))
            {
                at = hit;
                break;
            }
        }

        return at is { } found ? identities[found] : null;
    }

    private static Dictionary<string, int> Index(
        List<JsonElement> mods, string field, List<string> ambiguous) =>
        Index(mods, field, ambiguous, i => Text(mods[i], "source", field));

    /// <remarks>
    /// A spelling two declared mods answer to is dropped rather than resolved
    /// to whichever the list happened to declare first. Taking the first
    /// attributes a rule to a mod on the strength of an array order, and an
    /// edge attributed that way can invent a cycle or hide one.
    /// </remarks>
    private static Dictionary<string, int> Index(
        List<JsonElement> mods, string field, List<string> ambiguous, Func<int, string?> spelling)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var contested = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < mods.Count; i++)
        {
            if (spelling(i) is not { Length: > 0 } value)
            {
                continue;
            }

            if (index.TryGetValue(value, out var held) && held != i)
            {
                contested.Add(value);
            }

            index[value] = i;
        }

        foreach (var value in contested)
        {
            index.Remove(value);
            ambiguous.Add($"{field} '{value}'");
        }

        return index;
    }

    private static OrderingRuleKind Kind(string declared) => declared switch
    {
        "before" => OrderingRuleKind.Before,
        "after" => OrderingRuleKind.After,
        "requires" => OrderingRuleKind.Requires,
        _ => OrderingRuleKind.Unmodelled,
    };

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
            ? Scalar(value)
            : null;

    private static string? Text(JsonElement element, string outer, string inner) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(outer, out var nested)
            ? Text(nested, inner)
            : null;

    /// <remarks>
    /// A file id is a number in one document and text in another, and both name
    /// the same file. The join reads either as the text of itself rather than
    /// missing on the spelling.
    /// </remarks>
    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };
}
