using Ripperdoc.Core.Schema;

namespace Ripperdoc.Core.Dump;

/// <summary>
/// What a run should do about the generated schema before it needs one, and
/// what to tell whoever is waiting.
/// </summary>
/// <remarks>
/// <para>
/// The generated mode costs a setup step that nothing else in this engine
/// costs, and the step involves the user's own game and their own tooling
/// because nothing here may be redistributed. So the first thing a run does is
/// not to generate, but to work out which of a handful of situations it is in
/// and say so.
/// </para>
/// <para>
/// This is a reading, not a job. It looks, decides, and reports; generating is
/// a separate call the caller makes if it wants to. Rolling the two together
/// would make one verb that inspects, explains, generates and writes - and the
/// caller who only wanted to know whether an artifact was current would have no
/// way to ask.
/// </para>
/// </remarks>
public static class SchemaGeneration
{
    /// <summary>
    /// Work out what is available and what should happen next.
    /// </summary>
    /// <param name="inputs">Where to look.</param>
    /// <returns>The situation, and what it means.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputs"/> is null.</exception>
    public static GenerationReading Inspect(GenerationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var installedBuild = InstalledBuild(inputs.GameExecutablePath);
        var artifact = ReadArtifact(inputs.ArtifactPath);

        if (artifact.Unreadable is not null)
        {
            return new GenerationReading(
                GenerationState.ArtifactUnreadable,
                artifact.Unreadable,
                null,
                installedBuild);
        }

        if (artifact.Document is not null)
        {
            return Judge(artifact.Document, installedBuild, inputs);
        }

        var dump = InspectDump(inputs.DumpJsonDirectory);

        return dump.State switch
        {
            DumpAvailability.Absent => new GenerationReading(
                GenerationState.NothingToUseAndNothingToGenerateFrom,
                "There is no generated schema and no type information to generate one from. "
                + WhatIsNeeded,
                null,
                installedBuild),

            DumpAvailability.NotADump => new GenerationReading(
                GenerationState.NothingToUseAndNothingToGenerateFrom,
                $"There is no generated schema, and {dump.Detail} " + WhatIsNeeded,
                null,
                installedBuild),

            _ => new GenerationReading(
                GenerationState.ReadyToGenerate,
                "There is no generated schema yet and the type information to build one from is here. "
                + dump.Detail,
                null,
                installedBuild),
        };
    }

    /// <summary>
    /// Generate the schema artifact from type information and write it out.
    /// </summary>
    /// <param name="inputs">Where to read from and write to.</param>
    /// <param name="shipped">
    /// Shipped values to arbitrate the schema against, or null to generate an
    /// unarbitrated one.
    /// </param>
    /// <param name="generatedAt">When this generation is happening.</param>
    /// <returns>The artifact that was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputs"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// There is no usable type information to generate from. What is wrong, and
    /// what to do about it, is in the message.
    /// </exception>
    public static SchemaIr Generate(
        GenerationInputs inputs,
        IShippedRecordSource? shipped,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var dump = InspectDump(inputs.DumpJsonDirectory);
        if (dump.State != DumpAvailability.Usable)
        {
            throw new InvalidOperationException(
                $"Nothing can be generated: {dump.Detail} " + WhatIsNeeded);
        }

        var model = DumpTypeModel.Load(inputs.DumpJsonDirectory!, inputs.TypeInformationDescription);

        // Checked before the schema is built on it rather than after. A chain
        // that leaves the type information part way through resolves to a
        // shorter field set, and a schema that is quietly short is the shape of
        // wrong answer this engine exists not to give.
        var unresolved = UnresolvedParents(model);
        if (unresolved.Count > 0)
        {
            throw new InvalidOperationException(
                $"The type information names {unresolved.Count} parent type(s) it does not itself describe, "
                + $"beginning with '{unresolved[0]}'. Every inheritance chain has to end inside it or the "
                + "fields above the break are missing from the schema without anything saying so. This "
                + "usually means the type information was written only in part - generate it again.");
        }

        var reading = new DumpRecordTypeSource(model).Read();
        var schema = RecordSchemaDerivation.Derive(reading, model.Description);
        var manifest = shipped is null ? null : ValidationManifest.Build(schema, shipped);
        var artifact = SchemaIr.Create(schema, manifest, SchemaMode.GeneratedTypeInformation, generatedAt);

        SchemaIrDocument
            .Of(artifact, reading, InstalledBuild(inputs.GameExecutablePath))
            .Write(inputs.ArtifactPath);

        return artifact;
    }

    /// <summary>
    /// What producing the type information involves, and why this project does
    /// not simply ship it.
    /// </summary>
    /// <remarks>
    /// Kept as one sentence-set in one place because it is the answer to the
    /// only question a first run really raises, and an explanation assembled
    /// differently at each site is one that will eventually disagree with
    /// itself.
    /// </remarks>
    public const string WhatIsNeeded =
        "The type information is produced on this machine, from this install, with a separate tool that runs "
        + "inside the game once and writes out what the game says about its own types. It is not distributed "
        + "with this engine and never will be: it is derived from the publisher's own binary, so it is theirs "
        + "rather than ours to hand out, and every machine produces its own. Two things are worth knowing "
        + "before producing it. The tool writes a fresh copy every time the game starts, so it is removed "
        + "again afterwards. And the game registers types that mods add, so a copy taken from a modded "
        + "install describes that install rather than the game - take it before mods are installed.";

    private static GenerationReading Judge(
        SchemaIrDocument artifact,
        string? installedBuild,
        GenerationInputs inputs)
    {
        if (installedBuild is null)
        {
            return new GenerationReading(
                GenerationState.StalenessCannotBeChecked,
                $"There is a generated schema, generated on {artifact.GeneratedAt:yyyy-MM-dd} against "
                + $"{Describe(artifact.GameBuild)}. Whether the game has moved since cannot be checked from "
                + "here, because no game install was given to compare it against. It is being used as it is "
                + "- which is not the same as it having been found current.",
                artifact,
                null);
        }

        if (artifact.GameBuild is null)
        {
            return new GenerationReading(
                GenerationState.StalenessCannotBeChecked,
                $"There is a generated schema, generated on {artifact.GeneratedAt:yyyy-MM-dd}, and it "
                + "does not record which build of the game was installed at the time. The installed build "
                + $"is {installedBuild}, and there is nothing to compare it against. Generating again would "
                + "settle it.",
                artifact,
                installedBuild);
        }

        if (!string.Equals(artifact.GameBuild, installedBuild, StringComparison.Ordinal))
        {
            return new GenerationReading(
                GenerationState.ArtifactDescribesAnotherBuild,
                $"The generated schema was generated against {artifact.GameBuild} and the installed game "
                + $"is {installedBuild}. The game has moved since: types and fields the newer build added "
                + "are not in it, and anything it says about them is missing rather than wrong. Produce the "
                + "type information again and generate again. " + WhatIsNeeded,
                artifact,
                installedBuild);
        }

        return new GenerationReading(
            GenerationState.ArtifactCurrent,
            $"The generated schema was generated against {artifact.GameBuild}, which is still the "
            + "installed build. The game has not moved since it was generated - which is what can be "
            + "checked from here, and is not the same as the type information behind it having come from "
            + "that build.",
            artifact,
            installedBuild);
    }

    private static string Describe(string? build) =>
        build is null ? "an install whose build was not recorded" : build;

    private static (SchemaIrDocument? Document, string? Unreadable) ReadArtifact(string path)
    {
        if (!File.Exists(path))
        {
            return (null, null);
        }

        try
        {
            return (SchemaIrDocument.Read(path), null);
        }
        catch (InvalidDataException exception)
        {
            // Named as unreadable rather than treated as absent. Absent means
            // generate one; unreadable means something is there and cannot be
            // used, and quietly generating over it would destroy whatever it
            // was without anyone having looked.
            return (null,
                $"There is a file where the generated schema is kept and it cannot be read: "
                + $"{exception.Message} Nothing has been written over it.");
        }
    }

    private static DumpInspection InspectDump(string? jsonDirectory)
    {
        if (string.IsNullOrWhiteSpace(jsonDirectory))
        {
            return new DumpInspection(DumpAvailability.Absent, "no type information was named.");
        }

        if (!Directory.Exists(jsonDirectory))
        {
            return new DumpInspection(
                DumpAvailability.Absent,
                "the type information named is not there.");
        }

        foreach (var required in RequiredDirectories)
        {
            if (!Directory.Exists(Path.Combine(jsonDirectory, required)))
            {
                return new DumpInspection(
                    DumpAvailability.NotADump,
                    $"what was named has no '{required}' in it, so it is not what the tool writes out.");
            }
        }

        var classes = Directory
            .EnumerateFiles(Path.Combine(jsonDirectory, DumpTypeModel.ClassesDirectoryName), "*.json")
            .Take(1)
            .Count();
        if (classes == 0)
        {
            return new DumpInspection(
                DumpAvailability.NotADump,
                "what was named describes no classes at all, so it is empty or was not finished.");
        }

        return new DumpInspection(DumpAvailability.Usable, "It describes the game's own types.");
    }

    private static IReadOnlyList<string> UnresolvedParents(DumpTypeModel model) => model.Classes.Values
        .Select(type => type.ParentName)
        .Where(parent => parent is not null && !model.Classes.ContainsKey(parent))
        .Select(parent => parent!)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(parent => parent, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// Which build of the game is installed, or null where that cannot be told.
    /// </summary>
    /// <remarks>
    /// Null is an answer, not a failure: a run given no install, or given a
    /// path with nothing at it, does not know which build is there. Reporting
    /// it as a build would make every staleness check pass.
    /// </remarks>
    private static string? InstalledBuild(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    /// <summary>
    /// The directories this inspection looks for, which are the reader's own.
    /// </summary>
    /// <remarks>
    /// Bound to the reader rather than listed again, because this inspection is
    /// what tells a first run whether there is type information to generate
    /// from. Looking for fewer directories than the reader requires answers
    /// that a half-written capture is usable, and the read then fails on the
    /// missing one - with an exception carrying a machine path, out of a call
    /// the inspection had already said would work.
    /// </remarks>
    internal static IReadOnlyList<string> RequiredDirectories => DumpTypeModel.RequiredDirectoryNames;

    private enum DumpAvailability
    {
        Absent,
        NotADump,
        Usable,
    }

    private sealed record DumpInspection(DumpAvailability State, string Detail);
}

/// <summary>Where the generation step reads from and writes to.</summary>
/// <param name="ArtifactPath">Where the generated schema is kept.</param>
/// <param name="DumpJsonDirectory">
/// The type information's json directory, or null where none was named.
/// </param>
/// <param name="GameExecutablePath">
/// The game's own executable, read only for the build number it carries, or
/// null where none was named. That number is what an artifact records as the
/// build it was generated against, so it says which game was installed at
/// generation time and not which build the type information described. Nothing
/// is written to an install, ever.
/// </param>
/// <param name="TypeInformationDescription">
/// How the type information describes itself in anything produced from it.
/// Names the origin, never a machine path.
/// </param>
public sealed record GenerationInputs(
    string ArtifactPath,
    string? DumpJsonDirectory = null,
    string? GameExecutablePath = null,
    string TypeInformationDescription = "type information generated from this install");

/// <summary>What one look at the generation inputs found.</summary>
/// <param name="State">The situation.</param>
/// <param name="Explanation">
/// What it means and what to do, in sentences fit to show whoever is waiting.
/// </param>
/// <param name="Artifact">The generated schema that is there, or null.</param>
/// <param name="InstalledBuild">
/// Which build of the game is installed, or null where that could not be told.
/// </param>
public sealed record GenerationReading(
    GenerationState State,
    string Explanation,
    SchemaIrDocument? Artifact,
    string? InstalledBuild)
{
    /// <summary>
    /// Whether the game is still the build this schema was generated against.
    /// </summary>
    /// <remarks>
    /// True in one state only. Every other one is either no schema, a schema
    /// generated against another build, or a schema nobody could check - and
    /// the last of those is deliberately not folded in with the first, because
    /// "it might be current" and "it is current" are different answers.
    /// </remarks>
    public bool ArtifactIsKnownCurrent => State == GenerationState.ArtifactCurrent;
}

/// <summary>The situations a run can be in before it has a generated schema.</summary>
public enum GenerationState
{
    /// <summary>
    /// There is a generated schema and the game has not moved since it was
    /// generated.
    /// </summary>
    /// <remarks>
    /// Says the game is where it was, and no more. Whether the type information
    /// the schema was derived from came from that build is a different question
    /// and one nothing here can answer, because nothing in that information
    /// states a build.
    /// </remarks>
    ArtifactCurrent,

    /// <summary>
    /// There is a generated schema and the game has moved since it was
    /// generated.
    /// </summary>
    ArtifactDescribesAnotherBuild,

    /// <summary>
    /// There is a generated schema and nothing here can say whether it is
    /// current - no install was given to compare it against, or it never
    /// recorded which build it came from.
    /// </summary>
    /// <remarks>
    /// Its own state rather than either of its neighbours. Treated as current
    /// it would be a claim nobody checked; treated as stale it would send
    /// somebody through a setup step they may not need.
    /// </remarks>
    StalenessCannotBeChecked,

    /// <summary>
    /// There is no generated schema and the type information to build one is
    /// available.
    /// </summary>
    ReadyToGenerate,

    /// <summary>
    /// There is no generated schema and nothing to generate one from.
    /// </summary>
    NothingToUseAndNothingToGenerateFrom,

    /// <summary>
    /// Something is where the generated schema is kept and it cannot be read.
    /// </summary>
    ArtifactUnreadable,
}
