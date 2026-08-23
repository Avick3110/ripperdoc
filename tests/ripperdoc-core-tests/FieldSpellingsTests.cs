using Ripperdoc.Core.Schema;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The one home of "which names might this field's values be stored under",
/// read in both directions.
/// </summary>
/// <remarks>
/// Every case here is built from invented names. What the rule does is decided
/// by the shape of a field set and not by anything the game contains, so these
/// run on a bare runner - which matters, because the rule is what stands
/// between a guessed spelling and a field being condemned on another field's
/// stored values.
/// </remarks>
public class FieldSpellingsTests
{
    [Fact]
    public void AFieldIsAlwaysOfferedItsOwnNameFirst()
    {
        var type = TypeWith(
            new RecordFieldShape("value", "Float", ["Value", "VALUE"], null));

        Assert.Equal(new[] { "value", "Value", "VALUE" }, Spellings(type, "value"));
    }

    [Fact]
    public void AFieldTheSourceKnewTheNameOfIsOfferedThatNameAndNothingElse()
    {
        var type = TypeWith(new RecordFieldShape("value", "Float"));

        Assert.Equal(new[] { "value" }, Spellings(type, "value"));
    }

    [Fact]
    public void ASpellingThatIsAnotherFieldsOwnNameIsNotOfferedToTheFieldGuessingAtIt()
    {
        // The one exclusion. The other field is certain of that name and this
        // one is guessing at it, so the values there are the other field's and
        // reading them here would confirm or condemn this field on evidence
        // about a name it never had.
        var type = TypeWith(
            new RecordFieldShape("value", "Float", ["Value"], null),
            new RecordFieldShape("Value", "Int32"));

        Assert.Equal(new[] { "value" }, Spellings(type, "value"));
        Assert.Equal(new[] { "Value" }, Spellings(type, "Value"));
    }

    [Fact]
    public void ASpellingThatIsAnotherFieldsOwnNameResolvesToThatField()
    {
        // The reverse direction of the same exclusion, which is the half that
        // has to agree with it. A forward direction refusing to offer the name
        // while the reverse direction handed it to the guessing field would be
        // the two homes this type exists to collapse into one.
        var type = TypeWith(
            new RecordFieldShape("value", "Float", ["Value"], null),
            new RecordFieldShape("Value", "Int32"));

        Assert.Equal("Value", type.FindField("Value")!.Name);
        Assert.Equal("Int32", type.FindField("Value")!.StorageType);
    }

    [Fact]
    public void ASpellingTwoFieldsOnlyGuessAtIsOfferedToBothOfThem()
    {
        // The other arm, and the behaviour the exclusion is deliberately not
        // extended to. Neither field is certain of "Shared", so neither has a
        // claim that beats the other's - and dropping it from one would leave
        // that field never probed under a name its values might really use,
        // then reported as unconfirmed as though the records had been checked.
        var type = TypeWith(
            new RecordFieldShape("first", "Float", ["Shared"], null),
            new RecordFieldShape("second", "Int32", ["Shared"], null));

        Assert.Equal(new[] { "first", "Shared" }, Spellings(type, "first"));
        Assert.Equal(new[] { "second", "Shared" }, Spellings(type, "second"));
    }

    [Fact]
    public void ASpellingTwoFieldsOnlyGuessAtResolvesTheSameWayWhicheverOrderTheyArriveIn()
    {
        // The answer is observable and the field set does not promise an order,
        // so the answer is taken in one of this schema's own: by name. Built
        // both ways round here, because a rule that reads whichever field a
        // dictionary hands over first passes a single-ordering check and still
        // gives two machines two answers.
        var forwards = TypeWith(
            new RecordFieldShape("first", "Float", ["Shared"], null),
            new RecordFieldShape("second", "Int32", ["Shared"], null));
        var backwards = TypeWith(
            new RecordFieldShape("second", "Int32", ["Shared"], null),
            new RecordFieldShape("first", "Float", ["Shared"], null));

        Assert.Equal("first", forwards.FindField("Shared")!.Name);
        Assert.Equal("first", backwards.FindField("Shared")!.Name);
    }

    [Fact]
    public void AGuessAcceptedForOneFieldDoesNotTakeTheSpellingFromTheNext()
    {
        // The exclusion weighs a guess against the certain names only. Weighed
        // against the guesses already accepted, it would grow as it went and
        // drop "Shared" from whichever field it reached second - which is the
        // wider rule, and one whose outcome moves with the order of the set.
        var type = TypeWith(
            new RecordFieldShape("aaa", "Float", ["Shared"], null),
            new RecordFieldShape("bbb", "Int32", ["Shared"], null),
            new RecordFieldShape("ccc", "CName", ["Shared"], null));

        Assert.Equal(new[] { "aaa", "Shared" }, Spellings(type, "aaa"));
        Assert.Equal(new[] { "bbb", "Shared" }, Spellings(type, "bbb"));
        Assert.Equal(new[] { "ccc", "Shared" }, Spellings(type, "ccc"));
    }

    [Fact]
    public void AFieldListingItsOwnNameAmongTheGuessesIsOfferedItOnce()
    {
        var type = TypeWith(new RecordFieldShape("value", "Float", ["value", "Value"], null));

        Assert.Equal(new[] { "value", "Value" }, Spellings(type, "value"));
    }

    [Fact]
    public void AFieldTheSetWasNotBuiltFromGetsItsOwnNameAndNoGuesses()
    {
        // What the schema knows about a field it does not carry. Inventing
        // alternatives for it would be guessing on behalf of a caller who is
        // already off the map.
        var type = TypeWith(new RecordFieldShape("value", "Float", ["Value"], null));
        var stranger = new RecordField("elsewhere", "Float", "gamedataThing_Record", ["Elsewhere"], null);

        Assert.Equal(new[] { "elsewhere" }, type.Spellings.Of(stranger));
        Assert.Null(type.FindField("elsewhere"));
    }

    private static IReadOnlyList<string> Spellings(RecordType type, string fieldName) =>
        type.Spellings.Of(type.Fields[fieldName]);

    private static RecordType TypeWith(params RecordFieldShape[] fields) =>
        RecordSchemaDerivation.Derive(
                new RecordTypeSourceReading(
                    new[] { new RecordTypeShape("gamedataThing_Record", null, true, fields) },
                    Array.Empty<DerivationFailure>()),
                "a reading constructed for this test")
            .Find("gamedataThing_Record")!;
}
