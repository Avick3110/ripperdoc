using Ripperdoc.Core.Drift;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The half of the drift gate that runs where there is no generated type
/// information - which is every runner, and most machines.
/// </summary>
/// <remarks>
/// <para>
/// The audit itself needs type information generated from a game install. This
/// does not, and it is what a build without one can honestly say: that the
/// accepted result of the audit was taken against exactly the compiled type
/// model this build uses. Bump the dependency and leave the audit unrun, and
/// this goes red naming what to do.
/// </para>
/// <para>
/// What it does not claim is that the audit is current with respect to the
/// game. Nothing without generated type information can know that, and the
/// receipt names the game version it was taken against precisely so that the
/// claim stays the size of the evidence.
/// </para>
/// </remarks>
public class DriftReceiptTests
{
    [Fact]
    public void TheAcceptedAuditWasTakenAgainstTheTypeModelThisBuildUses()
    {
        var receipt = Receipt();
        var compiled = TypeModelReading.FromPinnedTypeModel();

        Assert.True(
            string.Equals(
                receipt.CompiledTypeModelFingerprint,
                compiled.Reading.Fingerprint(),
                StringComparison.Ordinal),
            $"The accepted drift audit was taken against a type model whose fingerprint is "
            + $"{receipt.CompiledTypeModelFingerprint}, and this build's is {compiled.Reading.Fingerprint()}. "
            + "The audit's result therefore does not describe the dependency being built here. Re-run the "
            + $"tier (iii) checks on a machine with generated type information and accept the result they "
            + $"produce; do not edit '{ReceiptFileName}' to match this build without doing so.");
    }

    [Fact]
    public void TheAcceptedAuditNamesTheDependencyThisBuildPinsTo()
    {
        var receipt = Receipt();
        var compiled = TypeModelReading.FromPinnedTypeModel();

        // The fingerprint above would catch a bump on its own. This says which
        // dependency moved, in a sentence, rather than leaving whoever reads a
        // red run to work it out from two hashes.
        Assert.Equal(compiled.DependencyVersion, receipt.Dependency);
    }

    [Fact]
    public void TheAcceptedAuditReadsBackAsItWasWritten()
    {
        var receipt = Receipt();

        // Compared as the text it is written as, not as two objects. The
        // receipt carries a count per kind of divergence, and two maps holding
        // equal entries are not equal to each other - so an object comparison
        // would fail on a receipt that round-tripped perfectly.
        Assert.Equal(File.ReadAllText(ReceiptPath).ReplaceLineEndings("\n"), receipt.ToJson());
        Assert.NotEmpty(receipt.DivergenceFingerprint);
        Assert.NotEmpty(receipt.GeneratedFrom);
        Assert.NotEmpty(receipt.DivergenceCounts);
    }

    [Fact]
    public void TheAcceptedAuditComparedTheWholeOfBothDescriptions()
    {
        var receipt = Receipt();

        // A receipt recording an audit that compared almost nothing would pass
        // every other check here while saying nothing about drift. These are
        // the smallest numbers an audit of a whole description of the game
        // could honestly produce, not the numbers it produced.
        Assert.True(receipt.ClassesCompared > 1000, $"only {receipt.ClassesCompared} classes were compared");
        Assert.True(receipt.PropertiesCompared > 1000, $"only {receipt.PropertiesCompared} properties were compared");
        Assert.True(receipt.EnumsCompared > 100, $"only {receipt.EnumsCompared} enumerations were compared");
        Assert.True(receipt.EnumMembersCompared > 1000, $"only {receipt.EnumMembersCompared} members were compared");
    }

    [Fact]
    public void TheAcceptedAuditCarriesNothingTheGameDeclares()
    {
        // The receipt is committed to a public repository and the game's type
        // information is not this project's to publish. What diverged stays on
        // the machine that generated it; that it diverged, and whether the set
        // has changed since, is what travels.
        var text = File.ReadAllText(ReceiptPath);

        Assert.DoesNotContain("gamedata", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TEXFMT", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TCM_", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AReceiptThatIsNotThereIsRefusedRatherThanTreatedAsNothingToCheck()
    {
        var absent = Path.Combine(Path.GetTempPath(), "ripperdoc-absent-" + Guid.NewGuid().ToString("n") + ".json");

        Assert.Throws<FileNotFoundException>(() => DriftReceipt.Read(absent));
    }

    internal const string ReceiptFileName = "drift-receipt.json";

    internal static string ReceiptPath => Path.Combine(AppContext.BaseDirectory, ReceiptFileName);

    private static DriftReceipt Receipt() => DriftReceipt.Read(ReceiptPath);
}
