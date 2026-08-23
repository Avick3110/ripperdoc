using Ripperdoc.Core.Drift;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The rules the drift audit compares by, on descriptions built here.
/// </summary>
/// <remarks>
/// <para>
/// The audit's inputs are two readings, and a reading is a plain record. So the
/// rules that decide what counts as a disagreement can be exercised on a bare
/// runner with no game, no dump and no install - which matters more here than
/// almost anywhere, because this audit is the only thing that will ever report
/// the pinned type model having stopped describing the game.
/// </para>
/// <para>
/// Its failure direction is the quiet one. A rule that is too forgiving finds
/// fewer disagreements, and fewer disagreements is what a healthy run looks
/// like - so an audit whose rules nothing checks can be weakened to nothing and
/// report success the whole way down. Every rule below therefore has both arms:
/// the case it is meant to forgive, and the case it must still catch.
/// </para>
/// </remarks>
public class TypeModelAuditTests
{
    // The root the compiled model gives every one of its classes, which the
    // game's own description of its types does not have.
    private static readonly string CompiledRoot = nameof(WolvenKit.RED4.Types.RedBaseClass);

    // The width a description states when it does not state one.
    private const int WidthNotStated = 0;

    [Fact]
    public void AClassTheModelDoesNotHaveIsReported()
    {
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null, ("value", "Float"))),
            Compiled());

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.ClassAbsentFromModel, divergence.Kind);
        Assert.Equal("gameThing", divergence.TypeName);
    }

    [Fact]
    public void AClassTheModelCarriesMoreOfIsNotDrift()
    {
        // One direction on purpose: the model is allowed to know more than the
        // game registers. The extra is editor-time structure, and counting it
        // would bury the real findings under thousands of expected ones.
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null)),
            Compiled(
                Class("gameThing", CompiledRoot),
                Class("editorOnlyThing", CompiledRoot, ("value", "Float"))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void APropertyTheModelDoesNotHaveIsReported()
    {
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null, ("value", "Float"))),
            Compiled(Class("gameThing", CompiledRoot)));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.PropertyAbsentFromModel, divergence.Kind);
        Assert.Equal("value", divergence.MemberName);
    }

    [Fact]
    public void APropertyTheModelStoresAsAnotherTypeIsReported()
    {
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null, ("value", "Float"))),
            Compiled(Class("gameThing", CompiledRoot, ("value", "CName"))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.PropertyTypeDiffers, divergence.Kind);
        Assert.Equal("Float", divergence.Generated);
        Assert.Equal("CName", divergence.Compiled);
    }

    [Fact]
    public void AClassTheGameSaysDerivesFromNothingAgreesWithOneRootedAtTheModelsOwnRoot()
    {
        // The forgiving arm of the parent rule. Every class in the compiled
        // model is rooted at a type the game's description has no name for, so
        // compared as written every single one of them would read as drift.
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null)),
            Compiled(Class("gameThing", CompiledRoot)));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void AClassTheGameSaysDerivesFromSomethingIsNotForgivenTheModelsRoot()
    {
        // The arm that must still catch. The forgiveness above is for one
        // pairing only - the game saying nothing - and a rule that let the root
        // stand in for any parent would forgive a class that really did move.
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", "gameParent")),
            Compiled(Class("gameThing", CompiledRoot)));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.ParentDiffers, divergence.Kind);
        Assert.Equal("gameParent", divergence.Generated);
        Assert.Equal(CompiledRoot, divergence.Compiled);
    }

    [Fact]
    public void AClassTheGameSaysDerivesFromNothingIsNotForgivenSomeOtherParent()
    {
        // And the forgiveness is for the root and not for null-against-anything.
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null)),
            Compiled(Class("gameThing", "somethingElse")));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.ParentDiffers, divergence.Kind);
        Assert.Null(divergence.Generated);
        Assert.Equal("somethingElse", divergence.Compiled);
    }

    [Fact]
    public void TwoDescriptionsNamingTheSameParentAgree()
    {
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", "gameParent")),
            Compiled(Class("gameThing", "gameParent")));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void AnEnumerationTheModelDoesNotHaveIsReported()
    {
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 0))),
            Compiled());

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumAbsentFromModel, divergence.Kind);
        Assert.Equal("gameMood", divergence.TypeName);
    }

    [Fact]
    public void AMemberTheModelDoesNotHaveIsReported()
    {
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 0))),
            CompiledEnums(Enumeration("gameMood", 32, ("Angry", 1))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberAbsentFromModel, divergence.Kind);
        Assert.Equal("Calm", divergence.MemberName);
    }

    [Fact]
    public void AMemberStandingForAnotherValueIsReported()
    {
        // The arm the whole enumeration comparison exists for, and the one
        // whose failure is invisible: a value read through the wrong member is
        // wrong and looks fine.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 1))),
            CompiledEnums(Enumeration("gameMood", 8, ("Calm", 2))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberValueDiffers, divergence.Kind);
        Assert.Equal("1", divergence.Generated);
        Assert.Equal("2", divergence.Compiled);
    }

    [Fact]
    public void AMemberTheTwoDescriptionsWriteWithDifferentSignsStandsForTheSameBits()
    {
        // The forgiving arm of the value rule. Once the top bit of the width is
        // set, one side writes the signed number and the other the unsigned
        // one, and both do - so both are cut down to the bits the value
        // occupies before they are compared.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 255))),
            CompiledEnums(Enumeration("gameMood", 8, ("Calm", -1))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void ADifferenceInsideTheWidthIsNotForgivenByCuttingTheValueDown()
    {
        // The arm that must still catch. Masking forgives bits above the width
        // and nothing below it.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 3))),
            CompiledEnums(Enumeration("gameMood", 8, ("Calm", 4))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberValueDiffers, divergence.Kind);
    }

    [Fact]
    public void ValuesThatWouldAgreeUnderSomeWidthDoNotAgreeWhenNoWidthIsStated()
    {
        // Nothing said how wide the value is, so cutting it down would be
        // cutting it down to a guess - and a guessed width calls two different
        // values equal.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 255))),
            CompiledEnums(Enumeration("gameMood", WidthNotStated, ("Calm", -1))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberValueDiffers, divergence.Kind);
    }

    [Fact]
    public void AFullWidthValueIsComparedAsWrittenRatherThanCutDown()
    {
        // At the full width there is nothing above the value to discard, and a
        // mask built from it would be the whole word - so the two are compared
        // outright and a real difference stands.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 255))),
            CompiledEnums(Enumeration("gameMood", 64, ("Calm", -1))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberValueDiffers, divergence.Kind);
    }

    [Fact]
    public void AMemberNamedInAWayNoIdentifierCanBeSpelledIsMatchedThroughTheRewriting()
    {
        // The game names members with spaces, punctuation and leading digits,
        // and whatever generates the model rewrites them. Compared as written,
        // every such member reads as absent from a model that has it.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("2 shot", 7))),
            CompiledEnums(Enumeration("gameMood", 32, ("_2_shot", 7))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void AMemberWhoseRewrittenNameStillNeedsAMarkIsMatchedUnderTheMark()
    {
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 7))),
            CompiledEnums(Enumeration("gameMood", 32, ("Calm_", 7))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void AMemberIsMatchedOnItsOwnNameBeforeTheMarkedOne()
    {
        // The marked spelling is looked for second, so a member whose name
        // needs no mark is compared against the entry that really is its own
        // rather than against a differently-named neighbour.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 7))),
            CompiledEnums(Enumeration("gameMood", 32, ("Calm", 7), ("Calm_", 9))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void ANameTheModelRegistersTwiceIsCorroboratedByEitherRegistration()
    {
        // The game registers one name twice with two values on at least one
        // enumeration. A member is corroborated when the model has that name
        // standing for the same thing, whichever registration that is; keyed by
        // name, one of the two would simply have been dropped.
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 9))),
            CompiledEnums(Enumeration("gameMood", 32, ("Calm", 7), ("Calm", 9))));

        Assert.Empty(audit.Divergences);
    }

    [Fact]
    public void ANameTheModelRegistersTwiceUnderNeitherValueIsStillReported()
    {
        var audit = TypeModelAudit.Run(
            GeneratedEnums(Enumeration("gameMood", WidthNotStated, ("Calm", 11))),
            CompiledEnums(Enumeration("gameMood", 32, ("Calm", 7), ("Calm", 9))));

        var divergence = Assert.Single(audit.Divergences);
        Assert.Equal(DivergenceKind.EnumMemberValueDiffers, divergence.Kind);
        Assert.Equal("7 or 9", divergence.Compiled);
    }

    [Fact]
    public void APropertyIsNotComparedOnAClassTheModelDoesNotHaveAtAll()
    {
        // One divergence for the class, not one per property under it. A class
        // the model has never heard of would otherwise report its whole
        // property set as absent and drown the finding that matters.
        var audit = TypeModelAudit.Run(
            Generated(Class("gameThing", null, ("value", "Float"), ("other", "CName"))),
            Compiled());

        Assert.Single(audit.Divergences);
        Assert.Equal(0, audit.PropertiesCompared);
    }

    private static TypeModelReading Generated(params ModelClass[] classes) =>
        new("the game's own description, constructed for this test",
            classes.ToDictionary(type => type.Name, type => type, StringComparer.Ordinal),
            new Dictionary<string, ModelEnum>(StringComparer.Ordinal),
            Array.Empty<string>());

    private static TypeModelReading Compiled(params ModelClass[] classes) =>
        new("the pinned model, constructed for this test",
            classes.ToDictionary(type => type.Name, type => type, StringComparer.Ordinal),
            new Dictionary<string, ModelEnum>(StringComparer.Ordinal),
            Array.Empty<string>());

    private static TypeModelReading GeneratedEnums(params ModelEnum[] enums) =>
        new("the game's own description, constructed for this test",
            new Dictionary<string, ModelClass>(StringComparer.Ordinal),
            enums.ToDictionary(declared => declared.Name, declared => declared, StringComparer.Ordinal),
            Array.Empty<string>());

    private static TypeModelReading CompiledEnums(params ModelEnum[] enums) =>
        new("the pinned model, constructed for this test",
            new Dictionary<string, ModelClass>(StringComparer.Ordinal),
            enums.ToDictionary(declared => declared.Name, declared => declared, StringComparer.Ordinal),
            Array.Empty<string>());

    private static ModelClass Class(string name, string? parent, params (string Name, string Type)[] properties) =>
        new(name, parent, properties.ToDictionary(
            property => property.Name,
            property => property.Type,
            StringComparer.Ordinal));

    private static ModelEnum Enumeration(string name, int widthInBits, params (string Name, long Value)[] members) =>
        new(name, members.Select(member => new ModelEnumMember(member.Name, member.Value)).ToArray(), widthInBits);
}
