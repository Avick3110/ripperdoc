using System.Text.RegularExpressions;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The drift tier's guard, held against the facts that need it.
/// </summary>
/// <remarks>
/// <para>
/// Reflecting the pinned type model does not give the same answer in every
/// process, so a run that read it differently from the run an accepted result
/// came out of is holding the game's description against a different opposite
/// number. Anything that run takes from the audit is a property of the process
/// rather than of the two descriptions, which is why the dump tier's
/// audit-reading facts return early on such a run and the gate prints a skip.
/// </para>
/// <para>
/// The guard is three lines a person types, and it was left off one of four
/// facts that needed it. Nothing held them together: they are neighbours in one
/// file, which is exactly the amount of holding-together that fails. So the rule
/// is written down here instead - a fact taken from the audit is guarded - and
/// the next one written without it says so on a runner with no dump at all.
/// </para>
/// <para>
/// This reads the source rather than running it, deliberately. The arm it is
/// about is the early return, which happens on roughly one process in four and
/// cannot be asked for; a check that waited to observe it would report nothing
/// most of the time and could never report the omission at all.
/// </para>
/// </remarks>
public class AuditGuardTests
{
    [Fact]
    public void EveryFactTakenFromTheAuditGoesThroughTheGuard()
    {
        var source = DumpTierSource();
        var taken = new List<string>();
        var unguarded = new List<string>();

        foreach (var (name, body) in MethodsIn(source))
        {
            if (!body.Contains(AuditReading, StringComparison.Ordinal))
            {
                continue;
            }

            taken.Add(name);
            if (!body.Contains(GuardCall, StringComparison.Ordinal))
            {
                unguarded.Add(name);
            }
        }

        // Both directions. An empty list of unguarded facts means nothing at
        // all if the parse found no facts to begin with - which is how a check
        // like this fails toward green when the file it reads is rearranged.
        Assert.NotEmpty(taken);
        Assert.True(
            unguarded.Count == 0,
            $"{string.Join(", ", unguarded)} read {AuditReading} without going through {GuardCall}. A "
            + "process whose reflected reading differs from the accepted result's is comparing against a "
            + "different opposite number, so what it finds there is a property of the process. Left "
            + "unguarded, such a run reddens a healthy tree in the same gate summary that prints the skip.");
    }

    /// <summary>What a fact taken from the audit reads.</summary>
    private const string AuditReading = "_fixture.Audit";

    /// <summary>What a fact taken from the audit has to go through first.</summary>
    private const string GuardCall = "ComparisonCannotRun()";

    /// <summary>
    /// Each method in the source and the text of its body, a body running to
    /// wherever the next method of any visibility begins.
    /// </summary>
    private static IEnumerable<(string Name, string Body)> MethodsIn(string source)
    {
        var declarations = Regex.Matches(
            source,
            @"^    (?:public|private|internal) \S+ (\w+)\(",
            RegexOptions.Multiline);

        Assert.NotEmpty(declarations);

        for (var index = 0; index < declarations.Count; index++)
        {
            var start = declarations[index].Index;
            var end = index + 1 < declarations.Count ? declarations[index + 1].Index : source.Length;

            yield return (declarations[index].Groups[1].Value, source[start..end]);
        }
    }

    private static string DumpTierSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName, "tests", "ripperdoc-core-tests", "RttiDumpTests.cs");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        Assert.Fail(
            "This check reads the dump tier's checks and could not find "
            + $"'tests/ripperdoc-core-tests/RttiDumpTests.cs' above '{AppContext.BaseDirectory}'. It is not "
            + "skipped when it cannot find its subject, because a guard nothing is holding the facts to is "
            + "exactly what this exists to prevent.");
        return string.Empty;
    }
}
