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

        Assert.True(calls.CallsWrappedMethod);
        Assert.False(calls.IsWrapThatDropsTheChain);

        Assert.False(doesNot.CallsWrappedMethod);
        Assert.True(doesNot.IsWrapThatDropsTheChain);
    }

    [Fact]
    public void AReplacementIsNeverReportedAsDroppingAChain()
    {
        // A replacement has nothing beneath it to call, so the flag that means
        // "this ends the chain" must not fire on one. Without this the whole
        // silently-broken-chain report would name every replacement in the layer.
        var annotation = Assert.Single(Read(SyntheticScriptLayer.Replaces("T", "M")).Annotations);

        Assert.False(annotation.CallsWrappedMethod);
        Assert.False(annotation.IsWrapThatDropsTheChain);
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
