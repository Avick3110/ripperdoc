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
        IReadOnlyList<TweakUnhandled> unhandled)
    {
        Layer = layer;
        Flats = flats;
        Collisions = collisions;
        Unaddressable = unaddressable;
        Unhandled = unhandled;
    }

    /// <summary>The layer this state was replayed from.</summary>
    public TweakLayer Layer { get; }

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
    /// <returns>The resolved state.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The documents do not correspond to the layer's read order.
    /// </exception>
    public static TweakResolvedState Replay(TweakLayer layer, IReadOnlyList<TweakDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(documents);

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

        var resolved = new List<ResolvedFlat>(flats.Count);
        var unaddressable = new List<UnaddressableFlatName>();
        var collisions = new List<TweakCollision>();

        foreach (var (name, contributions) in flats.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
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

        return new TweakResolvedState(layer, resolved, collisions, unaddressable, unhandled);
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
                var contributions = Contributions(flats, inheritedName);

                // A property the declaring file sets itself never inherits: the
                // file chose both the base and the override, so there is nothing
                // to explain. A write from any OTHER file does not stop the
                // inheritance being recorded, because that is a value moving
                // between mods that neither of them asked for and it is the
                // whole reason this route is modelled.
                if (contributions.Any(contribution =>
                        contribution.Route == TweakContributionRoute.Written
                        && contribution.File.RelativePath == file.RelativePath))
                {
                    continue;
                }

                var source = flats[baseName].LastOrDefault(c => c.Kind == TweakWriteKind.Assignment);
                if (source is null)
                {
                    continue;
                }

                contributions.Add(new TweakContribution(
                    file,
                    declaration.Line,
                    source.ValueText,
                    TweakWriteKind.Assignment,
                    TweakContributionRoute.InheritedFromBase,
                    new TweakInheritance(baseName, source)));
            }
        }
    }

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
    /// A value written by name is never replaced by one arriving through a
    /// clone, whichever was read first, so the written ones are asked before
    /// read order is. Among writes of the same route the last one applied wins.
    /// </remarks>
    public TweakContribution? Winner => Contributions
            .LastOrDefault(contribution =>
                contribution.Kind == TweakWriteKind.Assignment
                && contribution.Route == TweakContributionRoute.Written)
        ?? Contributions.LastOrDefault(contribution => contribution.Kind == TweakWriteKind.Assignment);

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
