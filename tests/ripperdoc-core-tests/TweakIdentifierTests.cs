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
