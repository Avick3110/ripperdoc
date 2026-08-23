using Ripperdoc.Core.Tweak;

namespace Ripperdoc.Core.Schema;

/// <summary>
/// The schema artifact: the derived record schema, what a shipped database had
/// to say about it, and where both came from.
/// </summary>
/// <remarks>
/// <para>
/// One value carries the schema and its own provenance because separating them
/// is how a schema ends up being used without anyone knowing which mode
/// produced it. A consumer that holds this holds the answer to "how do you
/// know?" as well as the answer.
/// </para>
/// <para>
/// The artifact is honest about being degraded. What the inherited mode cannot
/// do is listed in <see cref="SchemaProvenance.NamedLosses"/> in the artifact
/// itself, with the counts computed from this schema rather than quoted from a
/// document that can go stale.
/// </para>
/// </remarks>
public sealed class SchemaIr
{
    private SchemaIr(
        RecordSchema records,
        ReferenceGraph references,
        ValidationManifest? validation,
        SchemaProvenance provenance)
    {
        Records = records;
        References = references;
        Validation = validation;
        Provenance = provenance;
    }

    /// <summary>The derived record schema.</summary>
    public RecordSchema Records { get; }

    /// <summary>
    /// Which record types point at which, recovered from the schema.
    /// </summary>
    /// <remarks>
    /// Part of the artifact rather than something a consumer builds, because
    /// how much of it is typed is a property of the mode that produced the
    /// schema - and an artifact that carried the schema without the graph would
    /// hand every consumer the job of rediscovering that.
    /// </remarks>
    public ReferenceGraph References { get; }

    /// <summary>
    /// What shipped data confirmed, or null where nothing arbitrated the
    /// schema.
    /// </summary>
    /// <remarks>
    /// Null is a state the artifact reports rather than a state it hides: an
    /// unvalidated schema is usable and is not the same as a validated one,
    /// and a consumer is entitled to know which it has.
    /// </remarks>
    public ValidationManifest? Validation { get; }

    /// <summary>Where the schema came from and what it cannot do.</summary>
    public SchemaProvenance Provenance { get; }

    /// <summary>
    /// Assemble the artifact.
    /// </summary>
    /// <param name="records">The derived record schema.</param>
    /// <param name="validation">
    /// What shipped data confirmed, or null if the schema was not arbitrated.
    /// </param>
    /// <param name="mode">Which mode produced the schema.</param>
    /// <param name="generatedAt">When the artifact was assembled.</param>
    /// <returns>The artifact.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> is null.</exception>
    public static SchemaIr Create(
        RecordSchema records,
        ValidationManifest? validation,
        SchemaMode mode,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(records);

        var references = ReferenceGraph.Of(records);

        var provenance = new SchemaProvenance(
            mode,
            records.SourceDescription,
            validation?.SourceDescription,
            generatedAt,
            LossesOf(references, validation, mode));

        return new SchemaIr(records, references, validation, provenance);
    }

    /// <summary>
    /// How many fields hold a reference to another record.
    /// </summary>
    /// <param name="records">The schema to count over.</param>
    /// <returns>The number of resolved field slots storing a record identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> is null.</exception>
    /// <remarks>
    /// These are the edges of the reference graph. A stored identifier says
    /// which record is pointed at but not what kind of record is allowed there,
    /// so counting them is counting exactly what the inherited mode cannot
    /// type-check.
    /// </remarks>
    public static int ReferenceFieldCount(RecordSchema records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return ReferenceGraph.Of(records).Edges.Count;
    }

    private static IReadOnlyList<string> LossesOf(
        ReferenceGraph references,
        ValidationManifest? validation,
        SchemaMode mode)
    {
        var losses = new List<string>();

        // A loss belongs to the artifact, not to the mode, wherever the
        // artifact can be asked directly. This one can: the graph knows how
        // many of its own edges say what kind of record they point at, so the
        // shortfall is stated from the schema rather than inferred from how it
        // was made - and a mode that closes it partly is described as closing
        // it partly rather than as closing it.
        if (references.UntypedEdgeCount > 0)
        {
            losses.Add(
                $"{references.UntypedEdgeCount} of {references.Edges.Count} field slots storing a record "
                + "identifier do not say which kind of record they may point at, so a reference in them is "
                + "checked for existence and not for kind.");
        }

        if (mode == SchemaMode.InheritedTypeModel)
        {
            losses.Add(
                "Drift between the type model and the game actually installed cannot be detected in this "
                + "mode, because the type model is the only description of the game it has.");
            losses.Add(
                "Coverage of a newly patched game waits on the type model being updated; types and fields "
                + "the game adds before then are absent rather than reported as new.");
        }

        if (validation is null)
        {
            losses.Add(
                "No shipped data arbitrated this schema, so no field in it is confirmed - not one of them is "
                + "known to describe a value that really exists.");
        }
        else
        {
            // A field the data contradicts is the sharpest thing this artifact
            // knows about itself, and it belongs beside the rest rather than
            // only in the manifest a consumer might not read. The inherited
            // mode has none of these against a shipped database, so the line
            // stays absent there rather than being written as a zero.
            var contradicted = validation.Fields().Count(field => field.State == ValidationState.Contradicted);
            if (contradicted > 0)
            {
                losses.Add(
                    $"{contradicted} field slot(s) in this schema are contradicted by shipped values: the "
                    + "value is stored as a different type from the one claimed here. Reading one through "
                    + "this schema gets the wrong type, and which slots they are is in the validation "
                    + "manifest.");
            }

            if (validation.RecordTypesNotInSchema.Count > 0)
            {
                losses.Add(
                    $"{validation.RecordTypesNotInSchema.Count} record type(s) in the arbitrating database are "
                    + "absent from this schema, so their records were examined against nothing.");
            }

            // One line per reason that actually occurred, rather than one line
            // naming the reason that usually would. A pair can fail to have an
            // identifier three ways, and a reader told the wrong one goes
            // looking for long names among fields whose names are short.
            foreach (var reason in validation.UnaddressableFieldProbesByReason.Keys.OrderBy(reason => reason))
            {
                var count = validation.UnaddressableFieldProbesByReason[reason];
                if (count == 0)
                {
                    continue;
                }

                losses.Add(
                    $"{count} record-and-field pair(s) have no identifier at all, because "
                    + TweakIdentifier.Describe(reason)
                    + "; nothing could be stored under them and nothing was checked for them.");
            }
        }

        return losses;
    }
}

/// <summary>
/// Where a schema came from, and what that origin costs.
/// </summary>
/// <param name="Mode">Which mode produced the schema.</param>
/// <param name="TypeInformationSource">What the type information was read from.</param>
/// <param name="ValidatedAgainst">
/// What arbitrated the schema, or null if nothing did.
/// </param>
/// <param name="GeneratedAt">When the artifact was assembled.</param>
/// <param name="NamedLosses">
/// What this schema cannot do, stated plainly. A degraded mode that does not
/// say what it lost is indistinguishable from a complete one right up to the
/// moment it gives a wrong answer.
/// </param>
public sealed record SchemaProvenance(
    SchemaMode Mode,
    string TypeInformationSource,
    string? ValidatedAgainst,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> NamedLosses);

/// <summary>
/// Which of the two possible origins a schema has.
/// </summary>
/// <remarks>
/// The distinction is the whole reason provenance is carried at all. The two
/// modes produce schemas that look alike and differ in what they can be
/// trusted to do, so an artifact that did not name its mode would be inviting
/// exactly the confusion this enum exists to prevent.
/// </remarks>
public enum SchemaMode
{
    /// <summary>
    /// Read from a type model that ships with the engine's pinned dependency.
    /// Needs no setup, and is the mode this engine reaches for by default.
    /// </summary>
    InheritedTypeModel,

    /// <summary>
    /// Derived from type information generated on the machine the game is
    /// installed on. Costs a generation step and buys what the inherited mode
    /// names as lost.
    /// </summary>
    GeneratedTypeInformation,
}
