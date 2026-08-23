using Ripperdoc.Core.Drift;
using System.Text.RegularExpressions;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The recovery instruction, held against the gate it describes.
/// </summary>
/// <remarks>
/// <para>
/// A sentence telling somebody how to recover is a claim about what running
/// this project actually does, and a claim gets a check like any other. The
/// failure it exists to catch has happened here twice: a message named one of
/// the tier's preconditions, the reader set that one variable, and the gate
/// announced the tier as skipped and produced none of the file the message
/// promised. Rewording it correctly once does not stop the third time - the
/// script and the sentence live in different files and nothing held them
/// together.
/// </para>
/// <para>
/// So the script is the source of truth and the sentence is checked against it.
/// Both directions: every variable the tier really needs has to be named, and
/// no variable it does not need may be, because a sentence that listed all of
/// them would satisfy a one-directional check while sending the reader to set
/// four things for a tier that needs two.
/// </para>
/// </remarks>
public class GatePreconditionTests
{
    [Fact]
    public void TheRecoveryInstructionNamesExactlyWhatTheGateRequiresOfTheDumpTier()
    {
        var script = GateScript();

        var required = VariablesRequiredByTheDumpTier(script);
        var named = VariablesNamedIn(GateRecovery.HowToTakeAFreshReceipt);

        Assert.NotEmpty(required);
        Assert.Equal(required, named);
    }

    [Fact]
    public void TheRecoveryInstructionNamesTheFileTheGateFingerprintsTheDatabaseAgainst()
    {
        var script = GateScript();

        // The tier also refuses to run against a database that is not the one
        // the counts were measured on, and the gate decides that by comparing
        // against a file in the repository. A reader who sets both variables
        // and still meets a skip needs to know that is why.
        Assert.Contains("measured_sha", DumpTierBlock(script), StringComparison.Ordinal);
        Assert.Contains(
            GateRecovery.MeasuredDatabaseFile,
            GateRecovery.HowToTakeAFreshReceipt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheGateLooksForTheAuditReportUnderTheNameTheRunWritesIt()
    {
        // Two files apart, one name. The run writes this and the gate reads it,
        // and if they ever spell it differently the gate finds nothing, reports
        // a comparison that never ran as a green tier, and says so in a summary
        // nobody has reason to doubt.
        Assert.Contains(DriftReceipt.AuditStatusFileName, GateScript(), StringComparison.Ordinal);
        Assert.Contains($"= \"{DriftReceipt.AuditRanStatus}\"", GateScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGateScriptIsWhereThisCheckThinksItIs()
    {
        // A check that cannot find its subject and says nothing is worse than
        // no check. If the layout moves, this is the one that says so.
        Assert.Contains("rtti_dump_variable", GateScript(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The environment variables the gate insists on before it will run the
    /// dump tier, read out of the gate itself.
    /// </summary>
    private static SortedSet<string> VariablesRequiredByTheDumpTier(string script)
    {
        // The script holds each variable's name in a shell variable of its own,
        // so the tier's conditions refer to them indirectly. Resolved here the
        // same way the script resolves them, rather than by looking for
        // RIPPERDOC_ spellings in the block - which would find none.
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match assignment in Regex.Matches(script, @"^(\w+_variable)=""([A-Z0-9_]+)""", RegexOptions.Multiline))
        {
            names[assignment.Groups[1].Value] = assignment.Groups[2].Value;
        }

        Assert.NotEmpty(names);

        var required = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match reference in Regex.Matches(DumpTierBlock(script), @"\$(\w+_variable)"))
        {
            Assert.True(
                names.TryGetValue(reference.Groups[1].Value, out var name),
                $"the gate's dump tier refers to ${reference.Groups[1].Value}, which nothing in the script "
                + "assigns a variable name to");

            required.Add(name!);
        }

        return required;
    }

    private static SortedSet<string> VariablesNamedIn(string sentence) =>
        new(
            Regex.Matches(sentence, @"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b").Select(match => match.Value),
            StringComparer.Ordinal);

    /// <summary>
    /// The part of the gate that decides whether the dump tier runs - from
    /// where it reads the dump's path to the summary that follows every tier.
    /// </summary>
    private static string DumpTierBlock(string script)
    {
        var start = script.IndexOf("rtti_dump_path=", StringComparison.Ordinal);
        var end = script.IndexOf("gate summary", StringComparison.Ordinal);

        Assert.True(start >= 0, "the gate script no longer reads a dump path where this check looks for it");
        Assert.True(end > start, "the gate script no longer has a summary after the dump tier");

        return script[start..end];
    }

    private static string GateScript()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "ci-checks.sh");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        Assert.Fail(
            "This check reads the gate script and could not find 'scripts/ci-checks.sh' above "
            + $"'{AppContext.BaseDirectory}'. It is not skipped when it cannot find its subject, because a "
            + "recovery instruction nothing is holding to the gate is exactly what this exists to prevent.");
        return string.Empty;
    }
}
