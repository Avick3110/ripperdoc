using Ripperdoc.Core.Tweak;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class TweakFileReaderTests
{
    private static TweakDocument ReadText(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "tweak-" + Guid.NewGuid().ToString("N")[..12] + ".yaml");
        File.WriteAllText(path, content);

        try
        {
            return TweakFileReader.Read(path, "probe\\thing.yaml");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AValueWrittenByItsFullNameAtTheTopLevelIsAWrite()
    {
        var document = ReadText("Probe.thing.amount: 42\n");

        var write = Assert.Single(document.Writes);

        Assert.Equal("Probe.thing.amount", write.FlatName);
        Assert.Equal("42", write.ValueText);
        Assert.Equal(TweakWriteKind.Assignment, write.Kind);
        Assert.Null(write.OwningRecordName);
    }

    [Fact]
    public void ARecordDeclaredWithATypeCarriesItsPropertiesAsWrites()
    {
        var document = ReadText("""
            Probe.widget:
              $type: gamedataProbeWidget_Record
              amount: 7
              label: hello
            """);

        var declaration = Assert.Single(document.Declarations);
        Assert.Equal("Probe.widget", declaration.RecordName);
        Assert.Equal("gamedataProbeWidget_Record", declaration.DeclaredTypeName);
        Assert.Null(declaration.BaseName);

        Assert.Equal(
            new[] { "Probe.widget.amount", "Probe.widget.label" },
            document.Writes.Select(write => write.FlatName));
        Assert.All(document.Writes, write => Assert.Equal("Probe.widget", write.OwningRecordName));
    }

    [Fact]
    public void ARecordDeclaredAsACloneNamesWhatItWasClonedFrom()
    {
        var document = ReadText("""
            Probe.copy:
              $base: Probe.original
              amount: 7
            """);

        var declaration = Assert.Single(document.Declarations);

        Assert.Equal("Probe.original", declaration.BaseName);
        Assert.Null(declaration.DeclaredTypeName);
        Assert.Equal("Probe.copy.amount", Assert.Single(document.Writes).FlatName);
    }

    [Fact]
    public void AValueGivenAnExplicitTypeAndValueIsAWriteAndNotARecord()
    {
        // The form a file uses to create a loose value rather than a record.
        // Read as a record declaration instead, the write disappears entirely -
        // out of the resolved state, out of any contest over it, and out of the
        // schema reading - with nothing saying so.
        var document = ReadText("Probe.thing.amount:\n  $type: Int32\n  $value: 42\n");

        var write = Assert.Single(document.Writes);

        Assert.Equal("Probe.thing.amount", write.FlatName);
        Assert.Equal("42", write.ValueText);
        Assert.Equal(TweakWriteKind.Assignment, write.Kind);
        Assert.Empty(document.Declarations);
    }

    [Fact]
    public void AnExplicitlyTypedValueThatIsASequenceStillMutatesWhenTagged()
    {
        var document = ReadText("Probe.thing.list:\n  $type: array:String\n  $value:\n    - !append Probe.one\n");

        Assert.Equal(TweakWriteKind.Mutation, Assert.Single(document.Writes).Kind);
    }

    [Fact]
    public void AnAttributeIsNeverReadAsAPropertyOrAsARecord()
    {
        var document = ReadText("""
            $game: 2.31
            Probe.widget:
              $type: gamedataProbeWidget_Record
              $props: AutoFlats
              amount: 7
            """);

        Assert.Equal("Probe.widget.amount", Assert.Single(document.Writes).FlatName);
        Assert.Single(document.Declarations);
    }

    [Theory]
    [InlineData("!append")]
    [InlineData("!append-once")]
    [InlineData("!prepend")]
    [InlineData("!prepend-once")]
    [InlineData("!append-from")]
    [InlineData("!prepend-from")]
    [InlineData("!merge")]
    [InlineData("!remove")]
    [InlineData("!remove-all")]
    public void ASequenceCarryingAnOperatorTagMutatesRatherThanReplaces(string tag)
    {
        var document = ReadText($"Probe.thing.list:\n  - {tag} Probe.item\n");

        Assert.Equal(TweakWriteKind.Mutation, Assert.Single(document.Writes).Kind);
    }

    [Fact]
    public void AnUntaggedSequenceReplacesRatherThanMutates()
    {
        var document = ReadText("Probe.thing.list:\n  - Probe.one\n  - Probe.two\n");

        var write = Assert.Single(document.Writes);

        Assert.Equal(TweakWriteKind.Assignment, write.Kind);
        Assert.Equal("[Probe.one, Probe.two]", write.ValueText);
    }

    [Fact]
    public void ASequenceMixingTaggedAndUntaggedItemsIsAMutation()
    {
        // The framework applies the tagged items and warns that the rest are
        // ignored, so the write mutates. Read as a replacement it would look
        // like a contest against every other writer of the same list.
        var document = ReadText("Probe.thing.list:\n  - !append Probe.one\n  - Probe.two\n");

        Assert.Equal(TweakWriteKind.Mutation, Assert.Single(document.Writes).Kind);
    }

    [Fact]
    public void AByteOrderMarkIsNotReadAsPartOfTheFirstName()
    {
        var path = Path.Combine(Path.GetTempPath(), "tweak-bom-" + Guid.NewGuid().ToString("N")[..12] + ".yaml");
        File.WriteAllText(path, "Probe.thing.amount: 1\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        try
        {
            var write = Assert.Single(TweakFileReader.Read(path, "probe\\thing.yaml").Writes);

            Assert.Equal("Probe.thing.amount", write.FlatName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnAnchorIsResolvedRatherThanReadAsText()
    {
        var document = ReadText("""
            Probe.first:
              $type: gamedataProbeWidget_Record
              amount: &shared 12
            Probe.second:
              $type: gamedataProbeWidget_Record
              amount: *shared
            """);

        Assert.Equal(
            new[] { "12", "12" },
            document.Writes.Select(write => write.ValueText));
    }

    [Fact]
    public void ATemplateIsNamedAsUnreplayedRatherThanGuessedAt()
    {
        var document = ReadText("""
            Probe.item_$(colour):
              $type: gamedataProbeWidget_Record
              $instances:
                - { colour: red }
                - { colour: blue }
            """);

        Assert.Empty(document.Writes);
        Assert.Empty(document.Declarations);
        var unhandled = Assert.Single(document.Unhandled);
        Assert.Contains("template", unhandled.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatCannotBeParsedSaysSoRatherThanComingBackEmpty()
    {
        var document = ReadText("Probe.thing: [unclosed\n");

        Assert.False(document.IsReadable);
        Assert.Empty(document.Writes);
        Assert.Single(document.Unhandled);
    }

    [Fact]
    public void AFileWhoseTopLevelIsNotAMappingIsNamedRatherThanReadAsNothing()
    {
        var document = ReadText("- one\n- two\n");

        Assert.True(document.IsReadable);
        Assert.Empty(document.Writes);
        Assert.Contains("not a mapping", Assert.Single(document.Unhandled).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASecondDocumentInOneFileIsNamedRatherThanReadOrDropped()
    {
        var document = ReadText("Probe.thing.amount: 1\n---\nProbe.other.amount: 2\n");

        Assert.Equal("Probe.thing.amount", Assert.Single(document.Writes).FlatName);
        Assert.Contains("second document", Assert.Single(document.Unhandled).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyFileIsReadableAndSaysNothing()
    {
        var document = ReadText(string.Empty);

        Assert.True(document.IsReadable);
        Assert.Empty(document.Statements);
    }

    [Fact]
    public void AValueDefinedInPlaceIsRecordedAsAWriteOfThePropertyThatHoldsIt()
    {
        var document = ReadText("""
            Probe.widget:
              $type: gamedataProbeWidget_Record
              detail:
                horsepower: 250
            """);

        var write = Assert.Single(document.Writes);

        Assert.Equal("Probe.widget.detail", write.FlatName);
        Assert.Equal("<defined in place>", write.ValueText);
    }

    [Fact]
    public void LineNumbersPointAtTheWriteRatherThanAtTheFile()
    {
        var document = ReadText("""
            Probe.widget:
              $type: gamedataProbeWidget_Record
              amount: 7
            """);

        Assert.Equal(3, Assert.Single(document.Writes).Line);
    }

    [Fact]
    public void AValueThatContainsItselfIsRefusedByNameRatherThanEndingTheProcess()
    {
        // An alias to the anchor it sits inside makes the value graph circular.
        // Rendering it by walking that graph never comes back, and the way it
        // fails is the worst kind available: the host is gone before any
        // handler runs, so there is no document, no note naming the file, and
        // nothing in a report to say a layer was only partly read. Refused by
        // name instead, which is what the rest of this reader does with a
        // construct it will not claim to have replayed.
        var document = ReadText("Probe.rec.a: &x\n  - 1\n  - *x\n");

        Assert.Empty(document.Writes);

        var unhandled = Assert.Single(document.Unhandled);
        Assert.Equal("Probe.rec.a", unhandled.Path);
        Assert.Contains("contains itself", unhandled.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnchorAliasedBesideItselfIsStillRendered()
    {
        // The other side of the guard above. One anchor used twice in the same
        // value is an ordinary way to write a file and the game applies it, so
        // a guard that refused every node it had already seen would report a
        // value the layer really writes as one this engine could not read -
        // and it would do it to a file with nothing wrong in it. What is
        // circular is a value reaching itself, not a value repeating itself.
        var document = ReadText("Probe.rec.b: [&y [1, 2], *y]\n");

        Assert.Empty(document.Unhandled);
        Assert.Equal("[[1, 2], [1, 2]]", Assert.Single(document.Writes).ValueText);
    }

    [Fact]
    public void AFileInTheOtherTweakLanguageComesBackNamedRatherThanParsedAsYaml()
    {
        using var layer = SyntheticTweakLayer.Of(("thing.tweak", "Probe.widget : ProbeWidget\n{\n  int amount = 7;\n}\n"));

        var enumerated = layer.Enumerate();
        var document = Assert.Single(TweakFileReader.ReadLayer(enumerated, layer.Root));

        Assert.Empty(document.Writes);
        Assert.Contains("other tweak language", Assert.Single(document.Unhandled).Reason, StringComparison.Ordinal);
    }
}
