using Ripperdoc.Core.Reporting;
using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The lexical pass's boundary, checked against the pass rather than described
/// beside it.
/// </summary>
/// <remarks>
/// What this closes is a span declared as modelled and not handled. What it
/// cannot close is a category nobody declared, and that is stated on the type
/// rather than left for a reader to work out from an absence.
/// </remarks>
public sealed class ScriptTextSpanTests
{
    [Fact]
    public void EveryDeclaredSpanIsOneThePassHandles()
    {
        var unhandled = SpansThePassDoesNotHandle<ScriptTextSpan>(
            span => span.Source, span => span.HoldsOfBlanked);

        Assert.True(
            unhandled.Count == 0,
            "These spans are declared as modelled and are not handled: "
            + string.Join(", ", unhandled)
            + ". A span the pass does not handle is a place an annotation can be read out of "
            + "text that is not code, or real code hidden inside text that is.");
    }

    [Fact]
    public void TheCheckSeesASpanThePassDoesNotHandle()
    {
        // The permanent known-RED beside it. One member claims the pass blanks
        // something it does not, and the check has to name that member alone -
        // a cell that reddened both would red for a broken harness as readily
        // as for the defect.
        var unhandled = SpansThePassDoesNotHandle<UnhandledSpanProbe>(
            span => span.Source, span => span.HoldsOfBlanked);

        Assert.Equal(new[] { nameof(UnhandledSpanProbe.NotHandled) }, unhandled.ToList());
        Assert.Equal(2, DeclaredKinds.Of<UnhandledSpanProbe>().Count);
    }

    [Fact]
    public void TheDeclaredSpansAreReadWholeBeforeTheVerdictIsBelieved()
    {
        // A derivation coming back short would leave the cell above green by
        // finding nothing wrong with members it never read.
        //
        // Compared by count where the limits are compared by identity. Count is
        // sufficient only because this set is sealed - every reflected member is
        // then necessarily a constructed one - and that sealedness is itself
        // checked rather than assumed.
        Assert.NotEmpty(ScriptTextSpan.All);
        Assert.Equal(
            DeclaredKinds.Constructed<ScriptTextSpan>().Count,
            ScriptTextSpan.All.Count);
    }

    [Fact]
    public void AnUnmodelledShapeLeavesAnAnnotationUnresolvedRatherThanLive()
    {
        // The direction the gap is survivable in. An argument shape this engine
        // does not model is recorded as unresolved and reaches the result as a
        // limit; what is never produced is a live carrier taking a method.
        var source = new ScriptSource("a.reds", ScriptSourceOrigin.ScriptDirectory, 0);
        var reading = ScriptAnnotationReader.Read(
            source, "@replaceMethod(Mod.PlayerPuppet)\npublic func M() -> Void {}\n");

        Assert.Empty(reading.Annotations);
        Assert.Equal(new[] { 1 }, reading.AnnotationsNotResolvedToAMethod.ToList());
    }

    private static IReadOnlyList<string> SpansThePassDoesNotHandle<TSpan>(
        Func<TSpan, string> sourceOf,
        Func<TSpan, Func<string, bool>> holdsOf)
        where TSpan : class
    {
        var unhandled = new List<string>();

        foreach (var member in DeclaredKinds.Of<TSpan>())
        {
            if (!holdsOf(member.Kind)(ScriptText.Blanked(sourceOf(member.Kind))))
            {
                unhandled.Add(member.Name);
            }
        }

        return unhandled;
    }
}

/// <summary>
/// A span set carrying one member the pass does not handle, kept permanently.
/// </summary>
internal sealed class UnhandledSpanProbe
{
    public static readonly UnhandledSpanProbe Handled = new(
        "public func M() -> Void {} // @replaceMethod(PlayerPuppet)\n",
        blanked => !blanked.Contains("@replaceMethod", StringComparison.Ordinal));

    // A delimiter this pass has no branch for, whatever the language does with
    // it. Declaring it modelled is the defect: the pass copies it through and
    // the annotation stands as live code.
    public static readonly UnhandledSpanProbe NotHandled = new(
        "public func M() -> Void {\n  Log('@replaceMethod(PlayerPuppet)');\n}\n",
        blanked => !blanked.Contains("@replaceMethod", StringComparison.Ordinal));

    private UnhandledSpanProbe(string source, Func<string, bool> holdsOfBlanked)
    {
        Source = source;
        HoldsOfBlanked = holdsOfBlanked;
        DeclaredKinds.Register(this);
    }

    public string Source { get; }

    public Func<string, bool> HoldsOfBlanked { get; }
}
