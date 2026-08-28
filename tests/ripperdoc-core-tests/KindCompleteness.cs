using Ripperdoc.Core.Reporting;
using Ripperdoc.Core.Script;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Runs every declared kind's own witness through the engine and reports the
/// kinds that reached no result.
/// </summary>
/// <remarks>
/// Generic over the kind set so that the same code runs over a set that is
/// deliberately wrong. A completeness check nobody has watched fail is a check
/// of the fixtures it happened to be given.
/// </remarks>
internal static class KindCompleteness
{
    internal static IReadOnlyList<string> KindsReachingNoResult<TKind>()
        where TKind : class, IWitnessedKind
    {
        var declared = DeclaredKinds.Of<TKind>();
        var unreached = new List<string>();

        foreach (var member in declared)
        {
            var root = Directory.CreateTempSubdirectory("ripperdoc-witness-").FullName;
            try
            {
                foreach (var (path, text) in member.Kind.Witness.Sources)
                {
                    var full = Path.Combine(root, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    File.WriteAllText(full, text);
                }

                // The whole entry point, not the predicate on its own. A kind
                // asked directly whether it applies to a state built for it is
                // being compared with itself.
                var state = ScriptLayer.Read(root);

                if (!state.Methods.Any(contest => member.Kind.AppliesTo(contest)))
                {
                    unreached.Add(member.Name);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException)
                {
                    // A leftover temp directory is not worth failing a check over.
                }
            }
        }

        return unreached;
    }

    internal static int DeclaredCount<TKind>()
        where TKind : class, IWitnessedKind =>
        DeclaredKinds.Of<TKind>().Count;
}

/// <summary>
/// A kind set carrying one member that reaches no result, kept permanently.
/// </summary>
/// <remarks>
/// <para>
/// The known-RED the completeness check is trusted on. <see cref="Unwired" />
/// applies only when the reading took a source whose extension carries a
/// capital, and its witness supplies none - a member declared and left unwired,
/// which is the defect the check exists to catch.
/// </para>
/// <para>
/// <see cref="Wired" /> stands beside it so the check is seen to tell the two
/// apart. A cell that reddened every member would red for a broken harness just
/// as readily as for the defect.
/// </para>
/// </remarks>
internal sealed class UnwiredKindProbe : IWitnessedKind
{
    public static readonly UnwiredKindProbe Wired = new(
        contest => contest.Wraps.Count > 0,
        new ScriptLayerWitness(("a.reds", AWrap)));

    public static readonly UnwiredKindProbe Unwired = new(
        contest => contest.Enumeration.SourcesNotSpelledInLowerCase.Count > 0,
        new ScriptLayerWitness(("a.reds", AWrap)));

    private readonly Func<MethodContest, bool> _applies;
    private readonly ScriptLayerWitness _witness;

    private UnwiredKindProbe(Func<MethodContest, bool> applies, ScriptLayerWitness witness)
    {
        _applies = applies;
        _witness = witness;
        DeclaredKinds.Register(this);
    }

    public ScriptLayerWitness Witness => _witness;

    public bool AppliesTo(MethodContest contest) => _applies(contest);

    private const string AWrap =
        "@wrapMethod(PlayerPuppet)\npublic func OnGameAttached() -> String {\n"
        + "  return \"x\" + wrappedMethod();\n}\n";
}
