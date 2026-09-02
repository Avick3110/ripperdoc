using System.Text.Json;
using Ripperdoc.Core.Diagnosis;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// What the manager's state says about one game: which profile is active, which
/// mods that profile asks for, and the manager's own ordering rules.
/// </summary>
/// <remarks>
/// <para>
/// The mod id is the one identity carried throughout - the staging directory
/// name, the recorded installation path, and the source of every deployed file.
/// Nothing here keys on a display name, a numeric id or a version.
/// </para>
/// <para>
/// <strong>The active profile is read, never guessed.</strong> Where the key
/// that records it is absent or names a profile that is not this game's,
/// <see cref="Wanted" /> is null and <see cref="WhyNoProfile" /> says what was
/// checked. An empty wanted set would read as a profile asking for nothing.
/// </para>
/// </remarks>
public sealed class ManagerStateReading
{
    private const string Profiles = "persistent###profiles###";
    private const string ProfileSettings = "settings###profiles###";
    private const string StagingSetting = "settings###mods###installPath###";
    private const string ModState = "###modState###";

    private readonly Dictionary<string, string> byFileHash;
    private readonly Dictionary<string, string> byFileId;

    private ManagerStateReading(
        StateDatabase state,
        string gameId,
        IReadOnlyList<string> profileCandidates,
        string? selectedProfile,
        string? whyNoProfile,
        IReadOnlyList<ManagerMod>? wanted,
        IReadOnlyList<string> installationPathIsNotTheId,
        IReadOnlyList<string> installationPathNotRecorded,
        OrderingRuleSet rules,
        IReadOnlyList<UnresolvedRules> rulesNotResolved,
        string? stagingRoot,
        Dictionary<string, string> byFileHash,
        Dictionary<string, string> byFileId,
        IReadOnlyList<string> fileSpellingsNamingMoreThanOneMod)
    {
        this.byFileHash = byFileHash;
        this.byFileId = byFileId;
        FileSpellingsNamingMoreThanOneMod = fileSpellingsNamingMoreThanOneMod;
        State = state;
        GameId = gameId;
        ProfileCandidates = profileCandidates;
        SelectedProfile = selectedProfile;
        WhyNoProfile = whyNoProfile;
        Wanted = wanted;
        InstallationPathIsNotTheId = installationPathIsNotTheId;
        InstallationPathNotRecorded = installationPathNotRecorded;
        Rules = rules;
        RulesNotResolved = rulesNotResolved;
        StagingRoot = stagingRoot;
    }

    /// <summary>The database this was read from.</summary>
    public StateDatabase State { get; }

    /// <summary>The game, in the manager's own word for it.</summary>
    public string GameId { get; }

    /// <summary>Every profile in the state that names this game.</summary>
    public IReadOnlyList<string> ProfileCandidates { get; }

    /// <summary>The profile the state records as this game's active one, or null.</summary>
    public string? SelectedProfile { get; }

    /// <summary>Why no profile was selected, where none was.</summary>
    public string? WhyNoProfile { get; }

    /// <summary>
    /// Every mod the manager knows for this game, with whether the active
    /// profile asks for it - or null where no profile could be selected.
    /// </summary>
    public IReadOnlyList<ManagerMod>? Wanted { get; }

    /// <summary>
    /// Mods whose recorded installation path is not their own id.
    /// </summary>
    /// <remarks>
    /// The identity law holds where this is empty. It is reported rather than
    /// asserted, because a state that broke it would be a measurement about the
    /// manager and not a defect in this reader.
    /// </remarks>
    public IReadOnlyList<string> InstallationPathIsNotTheId { get; }

    /// <summary>
    /// Mods for which the manager recorded no installation path.
    /// </summary>
    public IReadOnlyList<string> InstallationPathNotRecorded { get; }

    /// <summary>The manager's own rules, under the name of the home they came from.</summary>
    public OrderingRuleSet Rules { get; }

    /// <summary>Rules read whose reference resolved to no mod, counted by declared kind.</summary>
    public IReadOnlyList<UnresolvedRules> RulesNotResolved { get; }

    /// <summary>
    /// Where the manager stages this game's mods, as it records it, or null
    /// where it records none.
    /// </summary>
    public string? StagingRoot { get; }

    /// <summary>
    /// File spellings that name more than one mod, and so identify none.
    /// </summary>
    /// <remarks>
    /// Reported rather than resolved by picking. A spelling two mods answer to
    /// is a spelling that decides nothing, and taking the first would attribute
    /// a rule to a mod on the strength of an ordering.
    /// </remarks>
    public IReadOnlyList<string> FileSpellingsNamingMoreThanOneMod { get; }

    /// <summary>
    /// The manager's own id for the mod that supplied a file, or null where no
    /// one mod did.
    /// </summary>
    /// <param name="fileHash">The file's hash, as another document spells it.</param>
    /// <param name="fileId">The file's id, as another document spells it.</param>
    /// <returns>The mod id, or null.</returns>
    /// <remarks>
    /// The hash first: it names one file, and a file id is the manager's word
    /// for the same thing only where the record carries one. A document that
    /// names a file the manager never installed resolves to null, which is the
    /// answer - not a node under the name the other document used.
    /// </remarks>
    public string? Identify(string? fileHash, string? fileId)
    {
        if (fileHash is { Length: > 0 } hash && byFileHash.TryGetValue(hash, out var byHash))
        {
            return byHash;
        }

        return fileId is { Length: > 0 } id && byFileId.TryGetValue(id, out var byId) ? byId : null;
    }

    /// <summary>The key prefixes this reading materialises values under.</summary>
    /// <remarks>
    /// Stated as data rather than spelled at the call site, so that what this
    /// reader reads is one list a check can hold against what it materialised.
    /// The database also holds account credentials, and they are under none of
    /// these.
    /// </remarks>
    public static IReadOnlyList<string> Prefixes(string gameId)
    {
        ArgumentNullException.ThrowIfNull(gameId);

        return
        [
            ProfileSettings,
            StagingSetting + gameId,
            Profiles,
            $"persistent###mods###{gameId}###",
        ];
    }

    /// <summary>
    /// Reads a manager's state for one game, or reports that there is no state
    /// to read.
    /// </summary>
    /// <param name="stateDirectory">The manager's state directory.</param>
    /// <param name="gameId">The game, in the manager's own word for it.</param>
    /// <returns>The reading, or null where the directory holds no database.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="StateReadException">
    /// The state is not one this reader models, or holds a value it cannot read.
    /// </exception>
    public static ManagerStateReading? Of(string stateDirectory, string gameId)
    {
        ArgumentNullException.ThrowIfNull(stateDirectory);
        ArgumentNullException.ThrowIfNull(gameId);

        var state = StateDatabase.In(stateDirectory, Prefixes(gameId));

        return state is null ? null : Of(state, gameId);
    }

    /// <summary>
    /// Reads a state already open, for one game.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="gameId">The game, in the manager's own word for it.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="StateReadException">
    /// The state holds a value this reader cannot read.
    /// </exception>
    public static ManagerStateReading Of(StateDatabase state, string gameId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gameId);

        var candidates = Candidates(state, gameId);
        var selected = Selected(state, gameId, candidates, out var why);
        var known = KnownMods(state, gameId);
        var wanted = selected is null ? null : Wanting(state, selected, known);
        var ambiguous = new List<string>();

        return new ManagerStateReading(
            state,
            gameId,
            candidates,
            selected,
            why,
            wanted,
            [.. known.Where(mod => mod.Value.InstallationPath is not null
                    && mod.Value.InstallationPath != mod.Key)
                .Select(mod => mod.Key).Order(StringComparer.Ordinal)],
            [.. known.Where(mod => mod.Value.InstallationPath is null)
                .Select(mod => mod.Key).Order(StringComparer.Ordinal)],
            ReadRules(state, gameId, known, ambiguous, out var unresolved),
            unresolved,
            Text(state, StagingSetting + gameId),
            FilesBy(state, gameId, known.Keys, "fileMD5", ambiguous),
            FilesBy(state, gameId, known.Keys, "fileId", ambiguous),
            [.. ambiguous.Order(StringComparer.Ordinal)]);
    }

    /// <remarks>
    /// A spelling two mods answer to is dropped from the index rather than
    /// resolved to whichever was read first, and named so a caller can see that
    /// it decided nothing.
    /// </remarks>
    private static Dictionary<string, string> FilesBy(
        StateDatabase state,
        string gameId,
        IEnumerable<string> mods,
        string attribute,
        List<string> ambiguous)
    {
        var prefix = $"persistent###mods###{gameId}###";
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        var contested = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in mods)
        {
            if (Scalar(state, $"{prefix}{id}###attributes###{attribute}") is not { Length: > 0 } spelling)
            {
                continue;
            }

            if (index.TryGetValue(spelling, out var held) && held != id)
            {
                contested.Add(spelling);
            }

            index[spelling] = id;
        }

        foreach (var spelling in contested)
        {
            index.Remove(spelling);
            ambiguous.Add($"{attribute} '{spelling}'");
        }

        return index;
    }

    private static IReadOnlyList<string> Candidates(StateDatabase state, string gameId) =>
        [.. state.KeysUnder(Profiles)
            .Where(key => key.EndsWith("###gameId", StringComparison.Ordinal))
            .Where(key => Text(state, key) == gameId)
            .Select(key => key.Split("###")[2])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <remarks>
    /// The per-game key, and no fallback. The neighbouring key naming the
    /// manager's active profile is not keyed by game, so on a machine whose
    /// owner last played a different game it names a profile belonging to that
    /// one - and a reader that fell back to it would answer with another game's
    /// mods rather than saying it could not tell.
    /// </remarks>
    private static string? Selected(
        StateDatabase state, string gameId, IReadOnlyList<string> candidates, out string? why)
    {
        var key = ProfileSettings + "lastActiveProfile###" + gameId;
        var named = Text(state, key);

        if (named is not null && candidates.Contains(named, StringComparer.Ordinal))
        {
            why = null;
            return named;
        }

        why = named is null
            ? $"the state carries no '{key}', so nothing in it says which of this game's "
              + $"{candidates.Count} profiles is active. Open the manager on this game once and "
              + "read again, or pass the profile to use."
            : $"'{key}' names a profile that is not among the {candidates.Count} this state has "
              + "for this game, so the state disagrees with itself about which profile is active. "
              + "Nothing here can pick one, and picking the largest or the most recent would be "
              + "inventing an answer.";

        return null;
    }

    /// <remarks>
    /// The path stays nullable rather than defaulting to empty: every id under
    /// the prefix is a known mod, including one carrying only attributes, and a
    /// path the manager never recorded is a different thing to report from one
    /// it recorded differently.
    /// </remarks>
    private static Dictionary<string, (string? InstallationPath, string Kind)> KnownMods(
        StateDatabase state, string gameId)
    {
        var prefix = $"persistent###mods###{gameId}###";
        var mods = new Dictionary<string, (string?, string)>(StringComparer.Ordinal);

        foreach (var id in state.KeysUnder(prefix)
            .Select(key => key.Substring(prefix.Length).Split("###")[0])
            .Distinct(StringComparer.Ordinal))
        {
            mods[id] = (
                Text(state, $"{prefix}{id}###installationPath"),
                Text(state, $"{prefix}{id}###type") ?? string.Empty);
        }

        return mods;
    }

    private static IReadOnlyList<ManagerMod> Wanting(
        StateDatabase state,
        string profile,
        Dictionary<string, (string? InstallationPath, string Kind)> known)
    {
        var prefix = Profiles + profile + ModState;
        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var key in state.KeysUnder(prefix)
            .Where(key => key.EndsWith("###enabled", StringComparison.Ordinal)))
        {
            enabled[key.Substring(prefix.Length).Split("###")[0]] = Flag(state, key);
        }

        // The union, so that a mod the profile names and the mod records do not,
        // or the reverse, comes out rather than falling between them.
        return
        [
            .. known.Keys.Concat(enabled.Keys).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(id => new ManagerMod(
                    id,
                    enabled.GetValueOrDefault(id),
                    known.TryGetValue(id, out var mod) ? mod.Kind : string.Empty)),
        ];
    }

    private static OrderingRuleSet ReadRules(
        StateDatabase state,
        string gameId,
        Dictionary<string, (string? InstallationPath, string Kind)> known,
        List<string> ambiguous,
        out IReadOnlyList<UnresolvedRules> unresolved)
    {
        var prefix = $"persistent###mods###{gameId}###";
        var byArchive = new Dictionary<string, string>(StringComparer.Ordinal);
        var contested = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in known.Keys)
        {
            if (Text(state, $"{prefix}{id}###archiveId") is not { Length: > 0 } archive)
            {
                continue;
            }

            if (byArchive.TryGetValue(archive, out var held) && held != id)
            {
                contested.Add(archive);
            }

            byArchive[archive] = id;
        }

        // One download installed twice gives two mods one archive, and a side
        // naming it names neither of them.
        foreach (var archive in contested)
        {
            byArchive.Remove(archive);
            ambiguous.Add($"archiveId '{archive}'");
        }

        var rules = new List<OrderingRule>();
        var missed = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var key in state.KeysUnder(prefix)
            .Where(key => key.EndsWith("###rules", StringComparison.Ordinal)))
        {
            var source = key.Substring(prefix.Length).Split("###")[0];

            foreach (var rule in RuleObjects(state, key))
            {
                var declared = Property(rule, "type") ?? string.Empty;
                var reference = Reference(rule, known, byArchive);

                if (reference is null)
                {
                    missed[declared] = missed.GetValueOrDefault(declared) + 1;
                    continue;
                }

                rules.Add(new OrderingRule(source, reference, Kind(declared)));
            }
        }

        unresolved =
        [
            .. missed.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new UnresolvedRules(pair.Key, pair.Value)),
        ];

        return new OrderingRuleSet($"the manager's per-mod rules in '{state.Directory}'", rules);
    }

    private static string? Reference(
        JsonElement rule,
        Dictionary<string, (string? InstallationPath, string Kind)> known,
        Dictionary<string, string> byArchive)
    {
        if (!rule.TryGetProperty("reference", out var reference)
            || reference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (Property(reference, "id") is { } id && known.ContainsKey(id))
        {
            return id;
        }

        if (Property(reference, "archiveId") is { } archive
            && byArchive.TryGetValue(archive, out var owner))
        {
            return owner;
        }

        return Property(reference, "idHint") is { } hint && known.ContainsKey(hint) ? hint : null;
    }

    private static OrderingRuleKind Kind(string declared) => declared switch
    {
        "before" => OrderingRuleKind.Before,
        "after" => OrderingRuleKind.After,
        "requires" => OrderingRuleKind.Requires,
        _ => OrderingRuleKind.Unmodelled,
    };

    private static string? Property(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Text(StateDatabase state, string key)
    {
        var text = state.Text(key);

        if (text is null)
        {
            return null;
        }

        var element = Parse(text, key);

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : throw new StateReadException(
                $"'{key}' holds {element.ValueKind} where this reader models text. The manager's "
                + "state is a shape this reader has not been shown - report it rather than "
                + "reading past it.");
    }

    /// <remarks>
    /// A file id is a number in the manager's state and text in a curated
    /// list's manifest, and both name the same file. The join reads either as
    /// the text of itself rather than missing on the spelling.
    /// </remarks>
    private static string? Scalar(StateDatabase state, string key)
    {
        var text = state.Text(key);

        if (text is null)
        {
            return null;
        }

        var element = Parse(text, key);

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };
    }

    private static bool Flag(StateDatabase state, string key)
    {
        var element = Parse(state.Text(key)!, key);

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new StateReadException(
                $"'{key}' holds {element.ValueKind} where this reader models true or false. "
                + "Whether the profile asks for that mod is the whole wanted set, and reading an "
                + "unmodelled value as either answer would put a mod in or out of it on a guess."),
        };
    }

    private static IReadOnlyList<JsonElement> RuleObjects(StateDatabase state, string key)
    {
        var element = Parse(state.Text(key)!, key);

        return element.ValueKind == JsonValueKind.Array
            ? ObjectList.In(element, $"'{key}'", "a rule")
            : throw new StateReadException(
                $"'{key}' holds {element.ValueKind} where this reader models a list of rules. "
                + "Reading it as no rules at all would report a graph quieter than the manager's.");
    }

    private static JsonElement Parse(string text, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            throw new StateReadException(
                $"'{key}' does not hold JSON, and every value in this database does. The state is "
                + "damaged, or this is not the manager's state database.",
                error);
        }
    }
}
