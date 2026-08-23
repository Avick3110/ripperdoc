using System.Text.Json;
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
/// accepted result of the audit was taken against exactly this build of the
/// pinned dependency. Bump the dependency and leave the audit unrun, and this
/// goes red naming what to do.
/// </para>
/// <para>
/// It says which build and not what that build contains, because which build is
/// a property of the file and what it contains is read by reflecting over it -
/// and a reflected reading is not stable enough to hold a committed file
/// against. The content comparison is the audit's, at the tier that has both
/// descriptions of the game to compare.
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
    public void TheAcceptedAuditWasTakenAgainstThisBuildOfTheDependency()
    {
        var receipt = Receipt();
        var identity = TypeModelReading.PinnedAssemblyIdentity();

        Assert.True(
            string.Equals(receipt.TypeModelAssemblyIdentity, identity, StringComparison.Ordinal),
            $"The accepted drift audit was taken against dependency build {receipt.TypeModelAssemblyIdentity}, "
            + $"and this one is {identity}. The audit's result therefore does not describe the dependency "
            + "being built here. " + GateRecovery.HowToTakeAFreshReceipt);
    }

    [Fact]
    public void TheAcceptedAuditNamesWhichGeneratedInformationItWasTakenFrom()
    {
        var receipt = Receipt();

        // The other half of what the receipt identifies. A machine with no
        // generated type information cannot check this one for itself, which is
        // exactly why it is written down: the tier that does have generated
        // information holds its own reading against it, and a check reproducing
        // a number measured from one dump can refuse to believe the number
        // applies to a different one.
        Assert.NotEmpty(receipt.GeneratedTypeInformationFingerprint);
        Assert.Equal(64, receipt.GeneratedTypeInformationFingerprint.Length);
        Assert.All(
            receipt.GeneratedTypeInformationFingerprint,
            character => Assert.True(
                char.IsAsciiDigit(character) || (character >= 'a' && character <= 'f'),
                $"'{character}' is not a lower-case hexadecimal digit"));
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
        //
        // Checked by what the file is allowed to hold rather than by looking
        // for names it is not. A list of forbidden spellings is a list of
        // today's divergences: it passes the moment the game names something it
        // has not named before, which is exactly when this check matters. Here
        // every key is one of a known set and every value is a number, a digest,
        // or one of two strings this project wrote - so a name the game
        // declares has nowhere in the file to be.
        using var receipt = JsonDocument.Parse(File.ReadAllText(ReceiptPath));
        var root = receipt.RootElement;

        Assert.Equal(
            new[]
            {
                "classesCompared", "dependency", "divergenceCounts", "divergenceFingerprint",
                "enumMembersCompared", "enumsCompared", "generatedFrom",
                "generatedTypeInformationFingerprint", "propertiesCompared", "readFailures",
                "reflectionReadingPin", "typeModelAssemblyIdentity", "typeModelReadingFingerprint",
            },
            root.EnumerateObject().Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));

        // The four that are text, each held against what this project put
        // there rather than against a shape that a type name would also fit.
        Assert.Equal(TypeModelReading.FromPinnedTypeModel().DependencyVersion, Text(root, "dependency"));
        Assert.Equal(RttiDumpFixture.GeneratedDescription, Text(root, "generatedFrom"));
        Assert.Equal(DriftReceipt.InterimPinStatus, Text(root, "reflectionReadingPin"));
        AssertIsDigest(Text(root, "typeModelAssemblyIdentity"), 32);
        AssertIsDigest(Text(root, "typeModelReadingFingerprint"), 64);
        AssertIsDigest(Text(root, "generatedTypeInformationFingerprint"), 64);
        AssertIsDigest(Text(root, "divergenceFingerprint"), 64);

        foreach (var name in new[]
                 {
                     "classesCompared", "propertiesCompared", "enumsCompared", "enumMembersCompared",
                     "readFailures",
                 })
        {
            Assert.Equal(JsonValueKind.Number, root.GetProperty(name).ValueKind);
        }

        // The one nested object. Its keys are the kinds of divergence this
        // engine can find - names this project chose - and its values are
        // counts. A type the game declares cannot appear as either.
        Assert.Equal(
            Enum.GetNames<DivergenceKind>().OrderBy(name => name, StringComparer.Ordinal),
            root.GetProperty("divergenceCounts")
                .EnumerateObject()
                .Select(kind => kind.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        Assert.All(
            root.GetProperty("divergenceCounts").EnumerateObject(),
            kind => Assert.Equal(JsonValueKind.Number, kind.Value.ValueKind));
    }

    private static string Text(JsonElement root, string name)
    {
        var field = root.GetProperty(name);
        Assert.Equal(JsonValueKind.String, field.ValueKind);
        return field.GetString()!;
    }

    private static void AssertIsDigest(string value, int length)
    {
        Assert.Equal(length, value.Length);
        Assert.All(
            value,
            character => Assert.True(
                char.IsAsciiDigit(character) || (character >= 'a' && character <= 'f'),
                $"'{character}' is not a lower-case hexadecimal digit"));
    }

    [Fact]
    public void AReceiptThatIsNotThereIsRefusedRatherThanTreatedAsNothingToCheck()
    {
        var absent = Path.Combine(Path.GetTempPath(), "ripperdoc-absent-" + Guid.NewGuid().ToString("n") + ".json");

        Assert.Throws<FileNotFoundException>(() => DriftReceipt.Read(absent));
    }

    internal static string ReceiptPath => Path.Combine(AppContext.BaseDirectory, DriftReceipt.FileName);

    private static DriftReceipt Receipt() => DriftReceipt.Read(ReceiptPath);
}
