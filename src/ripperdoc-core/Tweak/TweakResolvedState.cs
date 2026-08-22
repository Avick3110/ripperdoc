namespace Ripperdoc.Core.Tweak;

/// <summary>
/// The tweak layer replayed: which value each name ends up with, who put it
/// there, and what the losers were.
/// </summary>
/// <remarks>
/// <para>
/// The framework this reproduces reports nothing when two files write one
/// value. It keeps the last writer and discards the rest before anything could
/// observe them, so there is no signal to forward and a contest has to be
/// derived by replaying the whole layer and watching a name change hands.
/// Surfacing it at all is the point.
/// </para>
/// <para>
/// Every contributor is kept, not just the winner. A resolved state that
/// remembers only what won can say what the value is and never why, and why is
/// the half a reader cannot work out for themselves.
/// </para>
/// </remarks>
public sealed class TweakResolvedState
{
    private TweakResolvedState(
        TweakLayer layer,
        IReadOnlyList<ResolvedFlat> flats,
        IReadOnlyList<TweakCollision> collisions,
        IReadOnlyList<UnaddressableFlatName> unaddressable,
        IReadOnlyList<TweakUnhandled> unhandled,
        bool inheritanceWasReplayed,
        string inheritanceDescription)
    {
        Layer = layer;
        Flats = flats;
        Collisions = collisions;
        Unaddressable = unaddressable;
        Unhandled = unhandled;
        InheritanceWasReplayed = inheritanceWasReplayed;
        InheritanceDescription = inheritanceDescription;
    }

    /// <summary>The layer this state was replayed from.</summary>
    public TweakLayer Layer { get; }

    /// <summary>
    /// Whether a value moving from a shipped record to the records cloned from
    /// it was replayed at all.
    /// </summary>
    /// <remarks>
    /// False where the inputs for that route were not supplied. It is the
    /// difference between "no value moved" and "nothing looked", and a report
    /// that cannot tell a reader which of those it means is telling them the
    /// wrong one half the time.
    /// </remarks>
    public bool InheritanceWasReplayed { get; }

    /// <summary>What the inheritance route was replayed against, or why it was not.</summary>
    public string InheritanceDescription { get; }

    /// <summary>Every value the layer writes, by name, in a stable order.</summary>
    public IReadOnlyList<ResolvedFlat> Flats { get; }

    /// <summary>
    /// Every value more than one origin wrote, in a stable order.
    /// </summary>
    public IReadOnlyList<TweakCollision> Collisions { get; }

    /// <summary>
    /// Names the layer writes that the database cannot address, with the reason.
    /// </summary>
    /// <remarks>
    /// A name with no identifier is not a value the game will ever hold. Carried
    /// as its own list rather than dropped, because a mod writing one is writing
    /// into nothing and has no way to find that out.
    /// </remarks>
    public IReadOnlyList<UnaddressableFlatName> Unaddressable { get; }

    /// <summary>
    /// Everything in the layer this replay did not account for.
    /// </summary>
    /// <remarks>
    /// A contest computed while quietly ignoring an input that could write the
    /// same value is a wrong answer wearing a complete provenance block. This
    /// list is what stops that: it is what the verdicts above do not cover, and
    /// it is reported with them rather than instead of them.
    /// </remarks>
    public IReadOnlyList<TweakUnhandled> Unhandled { get; }

    /// <summary>
    /// Replay a layer.
    /// </summary>
    /// <param name="layer">The layer, enumerated in read order.</param>
    /// <param name="documents">
    /// The layer's files as read, in the same order as
    /// <see cref="TweakLayer.Files"/>.
    /// </param>
    /// <param name="inheritance">
    /// Which shipped records were cloned from which.
    /// <see cref="TweakInheritanceMap.None"/> turns that route off rather than
    /// reporting that nothing travelled it.
    /// </param>
    /// <param name="values">
    /// The values the database already holds, used to decide whether a copy
    /// still agrees with what it was copied from. Null turns the route off for
    /// the same reason.
    /// </param>
    /// <returns>The resolved state.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="layer"/>, <paramref name="documents"/> or
    /// <paramref name="inheritance"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The documents do not correspond to the layer's read order.
    /// </exception>
    public static TweakResolvedState Replay(
        TweakLayer layer,
        IReadOnlyList<TweakDocument> documents,
        TweakInheritanceMap inheritance,
        ITweakValueSource? values)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(inheritance);

        if (documents.Count != layer.Files.Count)
        {
            throw new ArgumentException(
                $"The layer reads {layer.Files.Count} files and {documents.Count} documents were supplied; "
                + "a replay over a different set of files than the one that was enumerated would report "
                + "positions that do not exist.",
                nameof(documents));
        }

        for (var index = 0; index < documents.Count; index++)
        {
            if (!string.Equals(documents[index].RelativePath, layer.Files[index].RelativePath, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Document {index + 1} is '{documents[index].RelativePath}' where the layer reads "
                    + $"'{layer.Files[index].RelativePath}' at that position.",
                    nameof(documents));
            }
        }

        var flats = new Dictionary<string, List<TweakContribution>>(StringComparer.Ordinal);

        // A record reached only through the inheritance metadata has no name
        // anywhere - that metadata keys by identifier and carries no text - so
        // its identifier is carried here rather than computed back from the
        // stand-in name it is reported under, which would address something
        // else entirely.
        var identifiers = new Dictionary<string, ulong>(StringComparer.Ordinal);
        var unhandled = new List<TweakUnhandled>();
        var declarations = new List<(TweakFile File, TweakRecordDeclaration Declaration)>();

        for (var index = 0; index < documents.Count; index++)
        {
            var file = layer.Files[index];
            var document = documents[index];

            unhandled.AddRange(document.Unhandled);

            foreach (var write in document.Writes)
            {
                Contributions(flats, write.FlatName).Add(new TweakContribution(
                    file,
                    write.Line,
                    write.ValueText,
                    write.Kind,
                    TweakContributionRoute.Written,
                    Inheritance: null));
            }

            foreach (var declaration in document.Declarations)
            {
                declarations.Add((file, declaration));
            }
        }

        InheritIntoClones(flats, declarations);

        // Both inputs or neither. Deciding whether a copy still agrees with its
        // source needs the values, and knowing which records are copies needs
        // the map; with one of them the route can only be walked halfway, and a
        // half-walked route reports some of the movement as though it were all
        // of it.
        var inheritanceWasReplayed = inheritance.WasRead && values is not null;
        if (inheritanceWasReplayed)
        {
            Reinherit(flats, identifiers, inheritance, values!);
        }

        var resolved = new List<ResolvedFlat>(flats.Count);
        var unaddressable = new List<UnaddressableFlatName>();
        var collisions = new List<TweakCollision>();

        foreach (var (name, contributions) in flats.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (identifiers.TryGetValue(name, out var known))
            {
                var reached = new ResolvedFlat(name, known, contributions);
                resolved.Add(reached);

                if (TweakCollision.For(reached) is { } contested)
                {
                    collisions.Add(contested);
                }

                continue;
            }

            var addressing = Address(name, out var identifier);
            if (addressing != FlatAddressing.Addressable)
            {
                unaddressable.Add(new UnaddressableFlatName(name, addressing, contributions));
                continue;
            }

            var flat = new ResolvedFlat(name, identifier, contributions);
            resolved.Add(flat);

            var collision = TweakCollision.For(flat);
            if (collision is not null)
            {
                collisions.Add(collision);
            }
        }

        return new TweakResolvedState(
            layer,
            resolved,
            collisions,
            unaddressable,
            unhandled,
            inheritanceWasReplayed,
            inheritanceWasReplayed
                ? $"{inheritance.Description}, against {values!.Description}"
                : "not replayed - " + (inheritance.WasRead ? "no value source" : inheritance.Description));
    }

    // A clone takes the value its source holds at the moment the clone is
    // resolved, and the sources are resolved in the order the files declared
    // them. A property the clone sets itself is not replaced, so an explicit
    // write beats an inherited one however late it was made - which is the one
    // rule here that is not last-writer-wins.
    //
    // Only names the layer itself writes are carried through. A property whose
    // value comes from the shipped database is inherited too, but no file wrote
    // it, so nothing can be contesting it and it cannot be part of a verdict.
    private static void InheritIntoClones(
        Dictionary<string, List<TweakContribution>> flats,
        IReadOnlyList<(TweakFile File, TweakRecordDeclaration Declaration)> declarations)
    {
        // A record declared twice keeps the first declaration's source, so a
        // later one naming a different base changes nothing and must not be
        // replayed as though it did.
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (file, declaration) in declarations)
        {
            if (!declared.Add(declaration.RecordName) || declaration.BaseName is null)
            {
                continue;
            }

            var prefix = declaration.BaseName + TweakFileReader.PropertySeparator;

            foreach (var baseName in flats.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                var property = baseName[prefix.Length..];
                if (property.Contains(TweakFileReader.PropertySeparator, StringComparison.Ordinal))
                {
                    continue;
                }

                var inheritedName = declaration.RecordName + TweakFileReader.PropertySeparator + property;

                // A property the declaring file sets itself never inherits: the
                // file chose both the base and the override, so there is nothing
                // to explain. A write from any OTHER file does not stop the
                // inheritance being recorded, because that is a value moving
                // between mods that neither of them asked for and it is the
                // whole reason this route is modelled.
                if (flats.TryGetValue(inheritedName, out var existing)
                    && existing.Any(contribution =>
                        contribution.Route == TweakContributionRoute.Written
                        && contribution.File.RelativePath == file.RelativePath))
                {
                    continue;
                }

                // What comes across is what the source resolves to, which is not
                // the same as its last write: a value written by name beats one
                // the source itself inherited, whatever order they arrived in.
                // Asked before the entry is created, so a base whose value is
                // only ever mutated leaves no empty value behind here.
                var source = ResolvedFlat.Decide(flats[baseName]);
                if (source is null)
                {
                    continue;
                }

                Contributions(flats, inheritedName).Add(new TweakContribution(
                    file,
                    declaration.Line,
                    source.ValueText,
                    TweakWriteKind.Assignment,
                    TweakContributionRoute.InheritedFromBase,
                    new TweakInheritance(baseName, source)));
            }
        }
    }

    // A value written to a record that shipped copies of itself moves onto
    // those copies - but only where the copy still agrees with it, because a
    // copy holding something different was overridden when the game's data was
    // built and is not following its source any more. A copy the layer writes
    // to by name is left alone for the same reason, whichever file did it and
    // however late.
    //
    // It travels: a copy that is itself a source passes the value on, so one
    // write can move a value across a whole family. The walk is by generation
    // and a name already carried is never re-queued, so a family that records
    // itself as its own ancestor terminates instead of circling.
    private static void Reinherit(
        Dictionary<string, List<TweakContribution>> flats,
        Dictionary<string, ulong> identifiers,
        TweakInheritanceMap inheritance,
        ITweakValueSource values)
    {
        // Kept as identifiers, not names. A record reached through the metadata
        // is only ever known by identifier, so a name comparison would fail to
        // see the layer's own write to that same value and would carry a second
        // answer for it under a different name.
        var written = new HashSet<ulong>();
        foreach (var (name, contributions) in flats)
        {
            if (contributions.Any(c => c.Route == TweakContributionRoute.Written)
                && Addressable(name, out var writtenId))
            {
                written.Add(writtenId);
            }
        }

        var carried = new HashSet<ulong>();
        var generation = new List<(string FlatName, string RecordName, string Property)>();

        foreach (var flatName in flats.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray())
        {
            var separator = flatName.LastIndexOf(TweakFileReader.PropertySeparator);
            if (separator <= 0
                || !Addressable(flatName, out var startId)
                || !written.Contains(startId))
            {
                continue;
            }

            generation.Add((flatName, flatName[..separator], flatName[(separator + 1)..]));
        }

        while (generation.Count > 0)
        {
            var next = new List<(string FlatName, string RecordName, string Property)>();

            foreach (var (flatName, recordName, property) in generation)
            {
                if (!identifiers.TryGetValue(flatName, out var flatId)
                    && !Addressable(flatName, out flatId))
                {
                    continue;
                }

                if (!identifiers.TryGetValue(recordName, out var recordId)
                    && !Addressable(recordName, out recordId))
                {
                    continue;
                }

                if (!values.HoldsRecord(recordId) || !values.HoldsValue(flatId))
                {
                    continue;
                }

                var source = ResolvedFlat.Decide(flats[flatName]);
                if (source is null)
                {
                    continue;
                }

                foreach (var descendantId in inheritance.Descendants(recordId))
                {
                    if (!values.HoldsRecord(descendantId))
                    {
                        continue;
                    }

                    if (!TweakIdentifier.TryForField(descendantId, property, out var descendantFlatId))
                    {
                        continue;
                    }

                    // Named without a name: the metadata keys records by
                    // identifier and carries no text, so a record reached only
                    // this way can be reported by identifier and never by name.
                    var descendantName = $"<record 0x{descendantId:X}>";
                    var descendantFlatName = descendantName + TweakFileReader.PropertySeparator + property;

                    if (written.Contains(descendantFlatId) || carried.Contains(descendantFlatId))
                    {
                        continue;
                    }

                    if (!values.HoldsValue(descendantFlatId)
                        || !values.ValuesMatch(descendantFlatId, flatId))
                    {
                        continue;
                    }

                    // Marked only once it is actually being carried. Marked
                    // before the value was compared, a source that reaches this
                    // record and does not match would consume the mark and
                    // silence a later source that does.
                    carried.Add(descendantFlatId);

                    identifiers[descendantFlatName] = descendantFlatId;
                    identifiers[descendantName] = descendantId;

                    Contributions(flats, descendantFlatName).Add(new TweakContribution(
                        source.File,
                        source.Line,
                        source.ValueText,
                        TweakWriteKind.Assignment,
                        TweakContributionRoute.InheritedFromShippedRecord,
                        new TweakInheritance(flatName, source)));

                    if (inheritance.IsSource(descendantId))
                    {
                        next.Add((descendantFlatName, descendantName, property));
                    }
                }
            }

            generation = next;
        }
    }

    private static bool Addressable(string name, out ulong identifier) =>
        Address(name, out identifier) == FlatAddressing.Addressable;

    private static List<TweakContribution> Contributions(
        Dictionary<string, List<TweakContribution>> flats,
        string name)
    {
        if (!flats.TryGetValue(name, out var contributions))
        {
            contributions = [];
            flats[name] = contributions;
        }

        return contributions;
    }

    // Built from the identifier arithmetic's own published limits rather than by
    // catching what it throws, so that a name with no identifier is a verdict
    // this replay reached rather than an error it recovered from.
    private static FlatAddressing Address(string name, out ulong identifier)
    {
        identifier = 0;

        if (name.Length > TweakIdentifier.MaxNameLength)
        {
            return FlatAddressing.NameTooLong;
        }

        if (!TweakIdentifier.IsWithinRange(name))
        {
            return FlatAddressing.NameOutsideRange;
        }

        identifier = TweakIdentifier.Of(name);
        return FlatAddressing.Addressable;
    }
}

/// <summary>
/// One value the layer writes, with everyone who wrote it.
/// </summary>
/// <param name="Name">The value's full name.</param>
/// <param name="Identifier">The identifier the database keys it by.</param>
/// <param name="Contributions">
/// Everyone who wrote it, in the order the writes were applied.
/// </param>
public sealed record ResolvedFlat(
    string Name,
    ulong Identifier,
    IReadOnlyList<TweakContribution> Contributions)
{
    /// <summary>
    /// The contribution that decided the value, or null where every
    /// contribution mutates rather than replaces.
    /// </summary>
    /// <remarks>
    /// A value written by name is never replaced by one arriving through
    /// inheritance, whichever was read first, so the written ones are asked
    /// before read order is. Among writes of the same route the last one
    /// applied wins.
    /// </remarks>
    public TweakContribution? Winner => Decide(Contributions);

    /// <summary>
    /// Which of these writes decides the value.
    /// </summary>
    /// <param name="contributions">The writes, in the order they were applied.</param>
    /// <returns>The one that decides, or null where none replaces the value.</returns>
    /// <remarks>
    /// The rule has one home because it is asked in more than one place. What a
    /// value resolves to is also what a record cloned from it inherits, so a
    /// second copy of this rule that fell behind would hand a clone a value its
    /// own source had already lost - and nothing else in the layer would
    /// contradict it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="contributions"/> is null.</exception>
    public static TweakContribution? Decide(IReadOnlyList<TweakContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        return contributions
                .LastOrDefault(contribution =>
                    contribution.Kind == TweakWriteKind.Assignment
                    && contribution.Route == TweakContributionRoute.Written)
            ?? contributions.LastOrDefault(contribution => contribution.Kind == TweakWriteKind.Assignment);
    }

    /// <summary>Everyone whose write did not decide the value.</summary>
    public IEnumerable<TweakContribution> Overridden => Contributions
        .Where(contribution => contribution.Kind == TweakWriteKind.Assignment
            && !ReferenceEquals(contribution, Winner));
}

/// <summary>
/// One write of one value.
/// </summary>
/// <param name="File">The file that wrote it, and its position in the read order.</param>
/// <param name="Line">The line in that file.</param>
/// <param name="ValueText">The value as written.</param>
/// <param name="Kind">Whether the write replaces the value or mutates it.</param>
/// <param name="Route">Whether the file named this value or moved it indirectly.</param>
/// <param name="Inheritance">
/// Where the value came from and who supplied it, where the route is indirect;
/// null where the file wrote the value itself.
/// </param>
public sealed record TweakContribution(
    TweakFile File,
    int Line,
    string ValueText,
    TweakWriteKind Kind,
    TweakContributionRoute Route,
    TweakInheritance? Inheritance)
{
    /// <summary>
    /// The part of the layer whose value this is - the top directory of the
    /// tweak layer, which is how a manual install keeps one mod's files apart
    /// from another's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an inherited value this is the origin that set the value, not the one
    /// that declared the clone which carried it. The contest is over whose value
    /// wins, and the clone is the route rather than a party to it - both are
    /// named in the explanation, and only one of them can be the answer to
    /// "whose value is this".
    /// </para>
    /// <para>
    /// The directory is the best available answer and not a guaranteed one:
    /// nothing stops one mod shipping two directories or two mods sharing one.
    /// Reported as the origin it is rather than asserted to be a mod identity.
    /// </para>
    /// </remarks>
    public string OriginDirectory =>
        (Inheritance?.Source.File ?? File).RelativePath.Split(TweakFile.PathSeparator)[0];
}

/// <summary>
/// How an inherited value reached the record that ended up holding it.
/// </summary>
/// <param name="SourceFlatName">The value it was inherited from.</param>
/// <param name="Source">
/// The write that put the value there, which is a write of
/// <paramref name="SourceFlatName"/> and not of the value being explained.
/// </param>
public sealed record TweakInheritance(string SourceFlatName, TweakContribution Source);

/// <summary>
/// Whether a file wrote a value by name or moved it without naming it.
/// </summary>
public enum TweakContributionRoute
{
    /// <summary>The file names this value and writes it.</summary>
    Written,

    /// <summary>
    /// The file declared a record cloned from another, and this value came
    /// across with the clone.
    /// </summary>
    InheritedFromBase,

    /// <summary>
    /// The file wrote to a record the shipped data was copied from, and the
    /// value moved onto a copy the file never named.
    /// </summary>
    InheritedFromShippedRecord,
}

/// <summary>
/// Why a name the layer writes has no identifier.
/// </summary>
public enum FlatAddressing
{
    /// <summary>The name has an identifier.</summary>
    Addressable,

    /// <summary>The name is longer than the identifier's length field can hold.</summary>
    NameTooLong,

    /// <summary>
    /// The name carries a character with no defined place in an identifier.
    /// </summary>
    NameOutsideRange,
}

/// <summary>
/// A name the layer writes that the database cannot address.
/// </summary>
/// <param name="Name">The name as written.</param>
/// <param name="Addressing">Why it has no identifier.</param>
/// <param name="Contributions">Who wrote it.</param>
public sealed record UnaddressableFlatName(
    string Name,
    FlatAddressing Addressing,
    IReadOnlyList<TweakContribution> Contributions);
