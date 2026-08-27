using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// What the annotation reader recognises in one source, and what it refuses to.
/// </summary>
public class ScriptAnnotationReaderTests
{
    private static ScriptSource Source(string path = "a.reds") =>
        new(path, ScriptSourceOrigin.ScriptDirectory, 0);

    private static ScriptFileReading Read(string text) => ScriptAnnotationReader.Read(Source(), text);

    [Fact]
    public void AReplacementIsReadWithItsTypeAndMethod()
    {
        var reading = Read("@replaceMethod(SomeType)\npublic func DoThing() -> String {\n  return \"x\";\n}\n");

        var annotation = Assert.Single(reading.Annotations);
        Assert.Equal(ScriptAnnotationKind.ReplaceMethod, annotation.Kind);
        Assert.Equal("SomeType", annotation.Method.TypeName);
        Assert.Equal("DoThing", annotation.Method.MethodName);
        Assert.Equal(1, annotation.Line);
    }

    [Fact]
    public void AWrapIsToldApartByWhetherItCallsWhatItWraps()
    {
        var calls = Assert.Single(Read(SyntheticScriptLayer.Wraps("T", "M")).Annotations);
        var doesNot = Assert.Single(Read(SyntheticScriptLayer.WrapsWithoutCalling("T", "M")).Annotations);

        Assert.Equal(WrappedCallReading.Calls, calls.WrappedCall);
        Assert.False(calls.IsWrapThatDropsTheChain);

        Assert.Equal(WrappedCallReading.DoesNotCall, doesNot.WrappedCall);
        Assert.True(doesNot.IsWrapThatDropsTheChain);
    }

    [Fact]
    public void AWrapWhoseBodyDoesNotCloseIsUnreadRatherThanAccused()
    {
        // The two failures look the same from a bool: a body read and holding no
        // call, and a body never read at all. Only the first names a mod.
        var annotation = Assert.Single(
            Read(SyntheticScriptLayer.WrapWithAnUnclosedBody("T", "M")).Annotations);

        Assert.Equal(WrappedCallReading.BodyNotResolved, annotation.WrappedCall);
        Assert.True(annotation.BodyCouldNotBeRead);
        Assert.False(annotation.IsWrapThatDropsTheChain);
    }

    [Fact]
    public void AnAnnotationBehindAGateIsMarkedAndOneWithoutIsNot()
    {
        var gated = Assert.Single(Read(SyntheticScriptLayer.GatedReplaces("T", "M")).Annotations);
        var plain = Assert.Single(Read(SyntheticScriptLayer.Replaces("T", "M")).Annotations);

        Assert.True(gated.IsGated);
        Assert.False(plain.IsGated);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\n\n\n")]
    [InlineData("\n// a comment between the two\n")]
    public void AGateReachesItsAnnotationAcrossBlankLinesAndComments(string between)
    {
        // Measured: neither a blank line nor a comment breaks the pairing.
        var text = "@if(ModuleExists(\"SomeOtherMod\"))" + between
            + SyntheticScriptLayer.Replaces("T", "M");

        Assert.True(Assert.Single(Read(text).Annotations).IsGated);
    }

    [Fact]
    public void AGateReachesOneDeclarationAndNotTheNext()
    {
        // Measured: the declaration after the gated one is compiled. An engine
        // that let the gate run on would report a live replacement as undecided.
        var text = SyntheticScriptLayer.GatedReplaces("T", "One")
            + SyntheticScriptLayer.Replaces("T", "Two");

        var annotations = Read(text).Annotations;

        Assert.Equal(2, annotations.Count);
        Assert.True(annotations[0].IsGated);
        Assert.False(annotations[1].IsGated);
    }

    [Fact]
    public void AGateWhoseConditionCarriesNestedParenthesesStillPairs()
    {
        // The condition holds a call of its own, so a scan that stopped at the
        // first close paren would end inside the condition and read the gate as
        // ending before it does.
        var text = "@if(!ModuleExists(\"SomeOtherMod\"))\n" + SyntheticScriptLayer.Replaces("T", "M");

        Assert.True(Assert.Single(Read(text).Annotations).IsGated);
    }

    [Fact]
    public void AGateInsideACommentGatesNothing()
    {
        var text = "// @if(ModuleExists(\"SomeOtherMod\"))\n" + SyntheticScriptLayer.Replaces("T", "M");

        Assert.False(Assert.Single(Read(text).Annotations).IsGated);
    }

    [Fact]
    public void AReplacementIsNeverReportedAsDroppingAChain()
    {
        // A replacement has nothing beneath it to call, so the flag that means
        // "this ends the chain" must not fire on one. Without this the whole
        // silently-broken-chain report would name every replacement in the layer.
        var annotation = Assert.Single(Read(SyntheticScriptLayer.Replaces("T", "M")).Annotations);

        Assert.Equal(WrappedCallReading.NotAWrap, annotation.WrappedCall);
        Assert.False(annotation.IsWrapThatDropsTheChain);
        Assert.False(annotation.BodyCouldNotBeRead);
    }

    [Theory]
    [InlineData("// @replaceMethod(T)\npublic func M() -> String { return \"x\"; }\n")]
    [InlineData("/* @replaceMethod(T)\npublic func M() -> String { return \"x\"; } */\n")]
    public void AnAnnotationInsideACommentIsNotOne(string text)
    {
        Assert.Empty(Read(text).Annotations);
    }

    [Fact]
    public void AnAnnotationInsideAStringIsNotOne()
    {
        Assert.Empty(Read("public func M() -> String {\n  return \"@replaceMethod(T) func X()\";\n}\n").Annotations);
    }

    [Fact]
    public void ACallInsideAStringIsNotACall()
    {
        // The chain-dropping report turns on this call being present. A wrap
        // that merely mentions the name in a message would otherwise read as
        // one that calls it, and the broken chain would go unreported.
        var annotation = Assert.Single(
            Read("@wrapMethod(T)\npublic func M() -> String {\n  return \"wrappedMethod()\";\n}\n").Annotations);

        Assert.True(annotation.IsWrapThatDropsTheChain);
    }

    [Fact]
    public void BlankingKeepsLineNumbersHonest()
    {
        var reading = Read("/* a\nmultiline\ncomment */\n@replaceMethod(T)\npublic func M() -> String { return \"x\"; }\n");

        Assert.Equal(4, Assert.Single(reading.Annotations).Line);
    }

    [Fact]
    public void BlankingKeepsLineNumbersHonestAcrossAnEscapedNewline()
    {
        // A backslash at the end of a line inside a string escapes the newline.
        // Blanking the escaped character along with the backslash would eat that
        // newline, and every line number after it would come back one short.
        var reading = Read("let s = \"a\\\nb\";\n@replaceMethod(T)\npublic func M() -> String { return \"x\"; }\n");

        Assert.Equal(3, Assert.Single(reading.Annotations).Line);
    }

    [Fact]
    public void AStrayQuoteCostsItsOwnLineAndNotTheRestOfTheFile()
    {
        // A literal run to the end of the file blanks every annotation after the
        // quote, and a contest one carrier short is reported as no contest. The
        // annotation below the stray quote is what tells the two bounds apart.
        var reading = Read(
            "public func L() -> Void {\n  let s = \"oops;\n}\n"
            + "@replaceMethod(T)\npublic func M() -> String { return \"x\"; }\n");

        var annotation = Assert.Single(reading.Annotations);
        Assert.Equal("T", annotation.Method.TypeName);
        Assert.Equal(4, annotation.Line);
    }

    [Fact]
    public void AnnotationShapedTextInsideAnInterpolatedStringIsNotAnAnnotation()
    {
        // Discriminating on purpose: one source carrying both a phantom inside a
        // string nested in an interpolation, and a real annotation beside it. A
        // reader that treats the whole literal as one span reads the phantom; one
        // that treats the interpolation as opaque loses the real annotation. Only
        // a reader that enters the interpolation as code, and blanks the string
        // nested in it, gets exactly one.
        var text =
            "public func Log() -> Void {\n"
            + "  FTLog(s\"note \\(GetText(\"@replaceMethod(Ghost)\\npublic func Boom() -> Void {}\"))\");\n"
            + "}\n"
            + "@replaceMethod(Real)\n"
            + "public func Genuine() -> String { return \"x\"; }\n";

        var annotation = Assert.Single(Read(text).Annotations);

        Assert.Equal("Real", annotation.Method.TypeName);
        Assert.Equal("Genuine", annotation.Method.MethodName);
    }

    [Fact]
    public void ABraceInsideAnInterpolatedStringDoesNotEndTheBody()
    {
        // The brace is inside a string nested in an interpolation. Exposed as
        // code it closes the body early, the call below it is never read, and the
        // wrap is named as one that ends the chain - an accusation out of a
        // mis-read. The compiler takes this source without complaint.
        var text =
            "@wrapMethod(T)\n"
            + "public func M() -> String {\n"
            + "  FTLog(s\"a \\(GetText(\"}\")) b\");\n"
            + "  return wrappedMethod();\n"
            + "}\n";

        var annotation = Assert.Single(Read(text).Annotations);

        Assert.Equal(WrappedCallReading.Calls, annotation.WrappedCall);
        Assert.False(annotation.IsWrapThatDropsTheChain);
    }

    [Fact]
    public void ACallInsideAnInterpolationIsRealCodeAndCounts()
    {
        // The other direction. The interpolation's contents are code, so the only
        // call to the wrapped method sitting inside one still counts.
        var text =
            "@wrapMethod(T)\n"
            + "public func M() -> String {\n"
            + "  return s\"prefix \\(wrappedMethod())\";\n"
            + "}\n";

        Assert.Equal(WrappedCallReading.Calls, Assert.Single(Read(text).Annotations).WrappedCall);
    }

    [Fact]
    public void ABareAnnotationDoesNotAdoptTheFunctionOfTheAnnotationAfterIt()
    {
        // The neighbour is an annotation this engine does not resolve, which is
        // the common case on a real layer. Bounded only at the next contending
        // annotation, the bare replacement adopts BrandNew and is reported as a
        // live replacement of a method nobody annotated.
        var reading = Read(
            "@replaceMethod(T)\n\n@addMethod(T)\npublic func BrandNew() -> Void {}\n");

        Assert.Empty(reading.Annotations);
        Assert.Equal([1], reading.AnnotationsWithNoDeclaration);
    }

    [Fact]
    public void AnAnnotationWithItsOwnFunctionIsStillReadWhenAnotherAnnotationFollows()
    {
        // The other direction of the same bound: tightening it must not stop a
        // real annotation from finding the declaration directly beneath it.
        var reading = Read(
            "@replaceMethod(T)\npublic func Mine() -> Void {}\n"
            + "@addMethod(T)\npublic func BrandNew() -> Void {}\n");

        var annotation = Assert.Single(reading.Annotations);
        Assert.Equal("Mine", annotation.Method.MethodName);
        Assert.Empty(reading.AnnotationsWithNoDeclaration);
    }

    [Fact]
    public void AnAnnotationWithNoDeclarationBeneathItIsReportedRatherThanDropped()
    {
        // The second annotation has a function; the first does not. Bounding the
        // search stops the first from adopting the second's declaration and
        // inventing a contest on a method nobody wrote one for.
        var reading = Read("@replaceMethod(T)\n\n@wrapMethod(U)\npublic func M() -> String { return \"x\" + wrappedMethod(); }\n");

        var annotation = Assert.Single(reading.Annotations);
        Assert.Equal(ScriptAnnotationKind.WrapMethod, annotation.Kind);
        Assert.Equal("U", annotation.Method.TypeName);
        Assert.Equal([1], reading.AnnotationsWithNoDeclaration);
    }

    [Fact]
    public void SeveralAnnotationsInOneSourceAreAllRead()
    {
        var reading = Read(
            SyntheticScriptLayer.Replaces("T", "One") + SyntheticScriptLayer.Wraps("T", "Two"));

        Assert.Equal(2, reading.Annotations.Count);
        Assert.Equal(["One", "Two"], reading.Annotations.Select(a => a.Method.MethodName));
    }

    [Fact]
    public void MethodIdentityIsNameLevelAndSaysSo()
    {
        // Two overloads of one name resolve to one identity. The engine reports
        // the scope it resolves rather than implying a signature-aware one.
        Assert.Equal(new MethodIdentity("T", "M"), new MethodIdentity("T", "M"));
        Assert.False(MethodIdentity.DistinguishesOverloads);
        Assert.NotEqual(new MethodIdentity("T", "M"), new MethodIdentity("T", "m"));
    }
}
