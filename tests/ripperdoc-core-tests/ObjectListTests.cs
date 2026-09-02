using System.Text.Json;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The one door a JSON list goes through before any element of it is asked for
/// a property.
/// </summary>
public sealed class ObjectListTests
{
    /// <summary>
    /// A value that is not a list is refused by name, carrying the kind found
    /// and what this reader modelled, rather than leaving as the platform's own
    /// exception.
    /// </summary>
    /// <remarks>
    /// The primitive exists so that a site added later cannot get the shape
    /// wrong. A caller that has not checked the kind itself is exactly that
    /// site, and the refusal it gets is the one the composition above it
    /// catches.
    /// </remarks>
    [Theory]
    [InlineData("42", "Number")]
    [InlineData("\"after\"", "String")]
    [InlineData("{}", "Object")]
    [InlineData("null", "Null")]
    public void AValueThatIsNotAListIsRefusedByName(string json, string kind)
    {
        using var document = JsonDocument.Parse(json);

        var refusal = Assert.Throws<StateReadException>(
            () => ObjectList.In(document.RootElement, "a fixture", "a rule"));

        Assert.Equal(
            $"a fixture holds {kind} where this reader models a list, each element a rule. It is "
            + "a shape this reader has not been shown - report it rather than reading past it.",
            refusal.Message);
    }

    /// <summary>
    /// A list of objects reads, so the refusal above is about the kind and not
    /// about the door.
    /// </summary>
    [Fact]
    public void AListOfObjectsReads()
    {
        using var document = JsonDocument.Parse("""[{"a":1},{"b":2}]""");

        var elements = ObjectList.In(document.RootElement, "a fixture", "a rule");

        Assert.Equal(2, elements.Count);
        Assert.All(elements, element => Assert.Equal(JsonValueKind.Object, element.ValueKind));
    }

    /// <summary>
    /// An element that is not an object is refused by its position, which is a
    /// different sentence from the one the kind of the whole list gets.
    /// </summary>
    [Fact]
    public void AnElementThatIsNotAnObjectIsRefusedByItsPosition()
    {
        using var document = JsonDocument.Parse("""[{"a":1},7]""");

        var refusal = Assert.Throws<StateReadException>(
            () => ObjectList.In(document.RootElement, "a fixture", "a rule"));

        Assert.Equal(
            "a fixture holds Number at position 1 where this reader models a rule. It is a shape "
            + "this reader has not been shown - report it rather than reading past it.",
            refusal.Message);
    }
}
