using Ripperdoc.Core.Tweak;
using WolvenKit.RED4.Types;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The arbiter, checked against the pinned type model's own conversion.
/// </summary>
/// <remarks>
/// Every schema claim this engine makes is checked by turning a name into an
/// identifier, so an identifier computed wrongly would turn the whole coverage
/// story into a confident fiction. These run on a bare runner: the arithmetic
/// needs no game, no install and nothing generated from one.
/// </remarks>
public class TweakIdentifierTests
{
    [Theory]
    [InlineData("Items.money")]
    [InlineData("Items.money.entityName")]
    [InlineData("Character.Panam")]
    [InlineData("a")]
    [InlineData("Items.Preset_Nue_Default.quality")]
    public void IdentifierMatchesThePinnedTypeModelsOwnConversion(string name)
    {
        TweakDBID expected = name;

        Assert.Equal((ulong)expected, TweakIdentifier.Of(name));
    }

    [Theory]
    [InlineData("Items.money", "entityName")]
    [InlineData("Character.Panam", "displayName")]
    [InlineData("a", "b")]
    public void FieldIdentifierIsComputableFromTheRecordIdentifierAlone(string record, string field)
    {
        // The property the whole validation sweep rests on: a field's
        // identifier follows from its record's identifier and the field name,
        // so a schema can be checked against every shipped value without any
        // table mapping identifiers back to names.
        var fromWholeName = TweakIdentifier.Of(record + TweakIdentifier.FieldSeparator + field);
        var fromRecord = TweakIdentifier.ForField(TweakIdentifier.Of(record), field);

        Assert.Equal(fromWholeName, fromRecord);
    }

    [Fact]
    public void IdentifierCarriesTheNameLengthAndChecksum()
    {
        const string name = "Items.money";
        var identifier = TweakIdentifier.Of(name);

        Assert.Equal(name.Length, TweakIdentifier.LengthOf(identifier));
        Assert.Equal(TweakIdentifier.Checksum(0u, name), TweakIdentifier.ChecksumOf(identifier));
    }

    [Fact]
    public void ChecksumContinuesFromWhereItLeftOff()
    {
        var wholeAtOnce = TweakIdentifier.Checksum(0u, "Items.money");
        var inTwoParts = TweakIdentifier.Checksum(TweakIdentifier.Checksum(0u, "Items."), "money");

        Assert.Equal(wholeAtOnce, inTwoParts);
    }

    [Fact]
    public void ANameOutsideAsciiIsRefusedBecauseTheConversionItIsCheckedAgainstCollidesIt()
    {
        // The pinned conversion replaces such a character with a placeholder,
        // so an accented name and a name with a literal question mark come out
        // as the same identifier. Reproducing that would mean addressing the
        // wrong value and never knowing; refusing says so instead. If a later
        // pin stops colliding them, this fails and the rule gets revisited.
        TweakDBID accented = "Items.caf\u00e9";
        TweakDBID placeholder = "Items.caf?";
        Assert.Equal((ulong)placeholder, (ulong)accented);

        var thrown = Assert.Throws<ArgumentException>(() => TweakIdentifier.Of("Items.caf\u00e9"));
        Assert.Contains("no defined place", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLastAsciiCharacterIsStillAccepted()
    {
        var name = "Items." + TweakIdentifier.MaxCharacter;
        TweakDBID expected = name;

        Assert.Equal((ulong)expected, TweakIdentifier.Of(name));
    }

    [Fact]
    public void TheLongestAddressableNameStillWorks()
    {
        var name = new string('a', TweakIdentifier.MaxNameLength);

        Assert.Equal(TweakIdentifier.MaxNameLength, TweakIdentifier.LengthOf(TweakIdentifier.Of(name)));
    }

    [Fact]
    public void TheLengthBoundIsWhereThePinnedConversionStopsBeingUsable()
    {
        // The bound is pinned from outside rather than by restating the
        // constant: at the limit the two conversions still agree, and one
        // character further the pinned one produces an identifier whose length
        // field no longer holds the length it was built from.
        var atLimit = new string('a', TweakIdentifier.MaxNameLength);
        TweakDBID pinnedAtLimit = atLimit;
        Assert.Equal((ulong)pinnedAtLimit, TweakIdentifier.Of(atLimit));
        Assert.True(TweakIdentifier.IsWellFormed((ulong)pinnedAtLimit));

        TweakDBID pinnedPastLimit = new string('a', TweakIdentifier.MaxNameLength + 1);
        Assert.False(TweakIdentifier.IsWellFormed((ulong)pinnedPastLimit));
    }

    [Fact]
    public void ANameTooLongToAddressIsRefusedRatherThanWrapped()
    {
        var name = new string('a', TweakIdentifier.MaxNameLength + 1);

        var thrown = Assert.Throws<ArgumentException>(() => TweakIdentifier.Of(name));
        Assert.Contains("has no identifier", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldNameThatWouldOverflowTheCombinedLengthIsRefused()
    {
        var record = TweakIdentifier.Of(new string('a', 200));

        Assert.Throws<ArgumentException>(() => TweakIdentifier.ForField(record, new string('b', 55)));
    }

    [Fact]
    public void TheLongestAddressablePairIsStillAddressable()
    {
        var record = TweakIdentifier.Of(new string('a', 200));

        Assert.True(TweakIdentifier.TryForField(record, new string('b', 54), out var addressable));
        Assert.Equal(TweakIdentifier.MaxNameLength, TweakIdentifier.LengthOf(addressable));
    }

    [Fact]
    public void AnIdentifierWithBitsAboveTheLengthFieldIsRefusedRatherThanMisread()
    {
        // The pinned conversion does not truncate the length it stores, so a
        // name too long to address comes back with bits set above the length
        // field. Reading the low eight of them would give a length the name
        // never had, and every field computed from it would miss silently.
        TweakDBID overlong = new string('a', 300);
        Assert.False(TweakIdentifier.IsWellFormed((ulong)overlong));

        var thrown = Assert.Throws<ArgumentException>(
            () => TweakIdentifier.ForField((ulong)overlong, "field"));
        Assert.Contains("above its length field", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("malformed record identifier")]
    [InlineData("field name outside the range")]
    [InlineData("combined name too long")]
    public void EveryReasonAPairHasNoIdentifierIsAnAnswerNotAnException(string reason)
    {
        // The totality half of the arm-per-branch rule: a sweep over millions
        // of pairs has to be able to record one and carry on. Each reason gets
        // its own case here rather than one standing in for the rest.
        var record = TweakIdentifier.Of("Items.money");
        var field = "entityName";

        switch (reason)
        {
            case "malformed record identifier":
                record = (ulong)(TweakDBID)new string('a', 300);
                break;
            case "field name outside the range":
                field = "caf\u00e9";
                break;
            default:
                record = TweakIdentifier.Of(new string('a', 200));
                field = new string('b', 55);
                break;
        }

        Assert.False(TweakIdentifier.TryForField(record, field, out var none));
        Assert.Equal(0ul, none);
    }

    [Fact]
    public void TheOtherArmStillProducesAnIdentifier()
    {
        Assert.True(TweakIdentifier.TryForField(TweakIdentifier.Of("Items.money"), "entityName", out var id));
        Assert.Equal(TweakIdentifier.Of("Items.money.entityName"), id);
    }

    [Fact]
    public void AFieldNameThatIsMissingRatherThanUnaddressableIsStillTheCallersMistake()
    {
        // Narrowing the totality claim to what it actually covers: bad data is
        // an answer, a broken caller is not.
        Assert.Throws<ArgumentNullException>(
            () => TweakIdentifier.TryForField(TweakIdentifier.Of("a"), null!, out _));
        Assert.Throws<ArgumentException>(
            () => TweakIdentifier.TryForField(TweakIdentifier.Of("a"), "", out _));
    }

    [Fact]
    public void AnUnaddressablePairSaysWhichReasonAppliesRatherThanTheUsualOne()
    {
        var overlong = (ulong)(TweakDBID)new string('a', 300);
        var malformed = Assert.Throws<ArgumentException>(
            () => TweakIdentifier.ForField(overlong, "field"));
        Assert.Contains("above its length field", malformed.Message, StringComparison.Ordinal);

        var outsideRange = Assert.Throws<ArgumentException>(
            () => TweakIdentifier.ForField(TweakIdentifier.Of("Items.money"), "caf\u00e9"));
        Assert.Contains("no defined place", outsideRange.Message, StringComparison.Ordinal);

        var tooLong = Assert.Throws<ArgumentException>(
            () => TweakIdentifier.ForField(TweakIdentifier.Of(new string('a', 200)), new string('b', 55)));
        Assert.Contains("length field holds at most", tooLong.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UnaddressableReason.MalformedRecordIdentifier)]
    [InlineData(UnaddressableReason.FieldNameOutsideRange)]
    [InlineData(UnaddressableReason.CombinedNameTooLong)]
    public void EachReasonComesBackAsItselfRatherThanAsAPlainFalse(UnaddressableReason expected)
    {
        var pair = expected switch
        {
            UnaddressableReason.MalformedRecordIdentifier =>
                ((ulong)(TweakDBID)new string('a', 300), "field"),
            UnaddressableReason.FieldNameOutsideRange =>
                (TweakIdentifier.Of("Items.money"), "caf\u00e9"),
            _ => (TweakIdentifier.Of(new string('a', 200)), new string('b', 55)),
        };

        Assert.False(TweakIdentifier.TryForField(pair.Item1, pair.Item2, out _, out var reason));
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void AnAddressablePairReportsNoReason()
    {
        Assert.True(TweakIdentifier.TryForField(TweakIdentifier.Of("Items.money"), "entityName", out _, out var reason));
        Assert.Equal(UnaddressableReason.None, reason);
    }

    [Theory]
    [InlineData(UnaddressableReason.MalformedRecordIdentifier, "above its length field")]
    [InlineData(UnaddressableReason.FieldNameOutsideRange, "no defined place")]
    [InlineData(UnaddressableReason.CombinedNameTooLong, "longer than")]
    public void EveryReasonDescribesItselfDifferently(UnaddressableReason reason, string expected)
    {
        Assert.Contains(expected, TweakIdentifier.Describe(reason), StringComparison.Ordinal);
    }

    [Fact]
    public void AskingWhyAPairFailedThatDidNotFailIsRefused()
    {
        // The other arm. None is not a reason, and neither is a value the enum
        // never had - answering either would be inventing a loss to report.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TweakIdentifier.Describe(UnaddressableReason.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TweakIdentifier.Describe((UnaddressableReason)99));
    }

    [Fact]
    public void EveryIdentifierThisArithmeticBuildsIsWellFormed()
    {
        Assert.True(TweakIdentifier.IsWellFormed(TweakIdentifier.Of("Items.money")));
        Assert.True(TweakIdentifier.IsWellFormed(
            TweakIdentifier.Of(new string('a', TweakIdentifier.MaxNameLength))));
        Assert.True(TweakIdentifier.IsWellFormed(
            TweakIdentifier.ForField(TweakIdentifier.Of("Items.money"), "entityName")));
    }

    [Fact]
    public void AnEmptyFieldNameIsRefused()
    {
        Assert.Throws<ArgumentException>(() => TweakIdentifier.ForField(TweakIdentifier.Of("Items.money"), ""));
    }

    [Fact]
    public void NullNamesAreRefused()
    {
        Assert.Throws<ArgumentNullException>(() => TweakIdentifier.Of(null!));
        Assert.Throws<ArgumentNullException>(() => TweakIdentifier.ForField(0, null!));
        Assert.Throws<ArgumentNullException>(() => TweakIdentifier.Checksum(0u, null!));
    }
}
