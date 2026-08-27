namespace Ripperdoc.Core.Script;

/// <summary>
/// The resolved state of a script layer: what every annotated method resolves
/// to, under the measured compile order.
/// </summary>
public sealed class ScriptLayer
{
    private readonly Dictionary<MethodIdentity, MethodContest> _byMethod;

    private ScriptLayer(
        ScriptEnumeration enumeration,
        IReadOnlyList<ScriptFileReading> readings,
        Dictionary<MethodIdentity, MethodContest> byMethod)
    {
        Enumeration = enumeration;
        Readings = readings;
        _byMethod = byMethod;
        Methods = byMethod.Values
            .OrderBy(contest => contest.Method.Display, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The compile order this state was resolved against.</summary>
    public ScriptEnumeration Enumeration { get; }

    /// <summary>Every source that was read, in compile order.</summary>
    public IReadOnlyList<ScriptFileReading> Readings { get; }

    /// <summary>Every annotated method, ordered by name for reproducibility.</summary>
    public IReadOnlyList<MethodContest> Methods { get; }

    /// <summary>Methods that more than one resolved source replaces.</summary>
    public IReadOnlyList<MethodContest> Contested =>
        Methods.Where(contest => contest.IsContested).ToList();

    /// <summary>Methods that at least one resolved source wraps.</summary>
    public IReadOnlyList<MethodContest> Wrapped =>
        Methods.Where(contest => contest.Wraps.Count > 0).ToList();

    /// <summary>
    /// Every replacement that is overridden and does nothing.
    /// </summary>
    /// <remarks>
    /// The silent losers, gathered across the whole layer. Nothing else in the
    /// toolchain reports these: the compiler's warnings name the replacements
    /// that <em>win</em>, and the first loser of every contest appears in no
    /// diagnostic at all.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> SilentlyOverriddenReplacements =>
        Contested.SelectMany(contest => contest.Overridden).ToList();

    /// <summary>
    /// Wraps read to hold no call to the method they wrap.
    /// </summary>
    /// <remarks>
    /// Measured to compile with no error and no warning. Wraps whose body could
    /// not be read are not here; they are in
    /// <see cref="WrapsWhoseBodyCouldNotBeRead" />.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> WrapsThatDropTheChain =>
        Methods.SelectMany(contest => contest.Wraps)
            .Where(annotation => annotation.IsWrapThatDropsTheChain)
            .ToList();

    /// <summary>
    /// Wraps whose body this engine could not read to the end.
    /// </summary>
    public IReadOnlyList<ScriptAnnotation> WrapsWhoseBodyCouldNotBeRead =>
        Methods.SelectMany(contest => contest.Wraps)
            .Where(annotation => annotation.BodyCouldNotBeRead)
            .ToList();

    /// <summary>
    /// Every annotation held out of its contest because it carries a gate.
    /// </summary>
    /// <remarks>
    /// Reported rather than dropped, and reported rather than counted in. Each
    /// of these may or may not be compiled, and this engine reads the gate
    /// without deciding it.
    /// </remarks>
    public IReadOnlyList<ScriptAnnotation> UndeterminedAnnotations =>
        Methods.SelectMany(contest => contest.Undetermined).ToList();

    /// <summary>
    /// What this method resolves to, or <see langword="null" /> when nothing
    /// annotates it.
    /// </summary>
    public MethodContest? ContestFor(MethodIdentity method) =>
        method is not null && _byMethod.TryGetValue(method, out var contest) ? contest : null;

    /// <summary>
    /// Resolves a script layer from a directory.
    /// </summary>
    /// <param name="scriptDirectory">The directory the game walks.</param>
    /// <param name="pluginSources">
    /// Scripts contributed by runtime-extension plugins, in the order the
    /// plugins register them. Omitting them is honest and is recorded: every
    /// result this state reports then says it could be displaced.
    /// </param>
    public static ScriptLayer Read(string scriptDirectory, IReadOnlyList<string>? pluginSources = null)
    {
        var enumeration = ScriptSourceOrder.Of(scriptDirectory, pluginSources);
        var readings = new List<ScriptFileReading>(enumeration.Sources.Count);

        foreach (var source in enumeration.Sources)
        {
            var full = source.Origin == ScriptSourceOrigin.ScriptDirectory
                ? Path.Combine(scriptDirectory, source.Path)
                : source.Path;

            string text;
            try
            {
                text = File.ReadAllText(full);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new ScriptReadException(
                    $"The script source '{source.Display}' was enumerated and could not be read - it "
                    + $"raised {exception.GetType().Name}: {exception.Message}. No resolved state is "
                    + "reported: a source whose annotations are unknown may hold the replacement that "
                    + "wins, and a state built without it would name a different mod with nothing said.",
                    exception);
            }

            readings.Add(ScriptAnnotationReader.Read(source, text));
        }

        return Of(enumeration, readings);
    }

    /// <summary>
    /// Resolves a script layer from readings already taken.
    /// </summary>
    /// <remarks>
    /// The seam the checks drive: a reading is a source plus its annotations,
    /// and building one directly needs no file system.
    /// </remarks>
    public static ScriptLayer Of(ScriptEnumeration enumeration, IReadOnlyList<ScriptFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(enumeration);
        ArgumentNullException.ThrowIfNull(readings);

        var replacements = new Dictionary<MethodIdentity, List<ScriptAnnotation>>();
        var wraps = new Dictionary<MethodIdentity, List<ScriptAnnotation>>();
        var undetermined = new Dictionary<MethodIdentity, List<ScriptAnnotation>>();

        // Compile order decides the winner, so the annotations are gathered in
        // it rather than sorted afterwards by a key that would have to encode
        // the same thing a second time.
        foreach (var reading in readings.OrderBy(reading => reading.Source.Rank))
        {
            foreach (var annotation in reading.Annotations)
            {
                var into = annotation.IsGated
                    ? undetermined
                    : annotation.Kind == ScriptAnnotationKind.ReplaceMethod ? replacements : wraps;

                if (!into.TryGetValue(annotation.Method, out var list))
                {
                    list = [];
                    into[annotation.Method] = list;
                }

                list.Add(annotation);
            }
        }

        var byMethod = new Dictionary<MethodIdentity, MethodContest>();
        foreach (var method in replacements.Keys.Concat(wraps.Keys).Concat(undetermined.Keys).Distinct())
        {
            byMethod[method] = new MethodContest(
                method,
                replacements.TryGetValue(method, out var r) ? r : [],
                wraps.TryGetValue(method, out var w) ? w : [],
                undetermined.TryGetValue(method, out var u) ? u : [],
                enumeration.PluginPosture);
        }

        return new ScriptLayer(enumeration, readings, byMethod);
    }
}
