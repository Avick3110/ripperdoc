using Ripperdoc.Core.Drift;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// What a reading's fingerprint counts as part of the reading.
/// </summary>
/// <remarks>
/// Built from plain records, so this runs where there is no game and no dump -
/// which is the point, since the fingerprint is what a check reproducing a
/// measured number holds its input against.
/// </remarks>
public class TypeModelReadingTests
{
    [Fact]
    public void TwoReadingsSayingTheSameThingFingerprintAlike()
    {
        Assert.Equal(Reading().Fingerprint(), Reading().Fingerprint());
    }

    [Fact]
    public void AReadingThatCouldNotReadSomethingIsNotTheSameAsOneThatReadItAll()
    {
        // A description that yielded the same types and a different set of
        // things the reader could not take in is not the same input. Blind to
        // that, this would call them one - and a check that reproduces a number
        // measured against the first would believe the number applies to the
        // second, over types nobody compared.
        var read = Reading();
        var partlyRead = Reading("gameThing.value: the reader could not take this in");

        Assert.NotEqual(read.Fingerprint(), partlyRead.Fingerprint());
    }

    [Fact]
    public void WhichThingsCouldNotBeReadChangesTheFingerprintToo()
    {
        Assert.NotEqual(
            Reading("gameThing.value: one reason").Fingerprint(),
            Reading("gameThing.value: another reason").Fingerprint());
    }

    private static TypeModelReading Reading(params string[] failures) =>
        new("a description constructed for this test",
            new Dictionary<string, ModelClass>(StringComparer.Ordinal)
            {
                ["gameThing"] = new(
                    "gameThing",
                    null,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "Float" }),
            },
            new Dictionary<string, ModelEnum>(StringComparer.Ordinal),
            failures);
}
