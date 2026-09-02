using System.Text.Json;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// The objects a JSON list holds, or a refusal naming the element that is
/// not one.
/// </summary>
/// <remarks>
/// Asking an element for a property is the platform's own failure where the
/// element is not an object, and that failure is not this reader's refusal.
/// So the shape is settled here, once, before any property is asked for - and
/// the list's own kind is settled first, because enumerating a value that is
/// not a list is the same platform failure one step earlier.
/// </remarks>
internal static class ObjectList
{
    /// <summary>
    /// Every element of a list, each one an object.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="what">What the list is, for a refusal.</param>
    /// <param name="of">What each element is modelled as, for a refusal.</param>
    /// <returns>The elements.</returns>
    /// <exception cref="StateReadException">
    /// The value is not a list, or an element of it is not an object.
    /// </exception>
    internal static IReadOnlyList<JsonElement> In(JsonElement list, string what, string of)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new StateReadException(
                $"{what} holds {list.ValueKind} where this reader models a list, each element "
                + $"{of}. It is a shape this reader has not been shown - report it rather than "
                + "reading past it.");
        }

        var elements = list.EnumerateArray().ToList();

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].ValueKind != JsonValueKind.Object)
            {
                throw new StateReadException(
                    $"{what} holds {elements[i].ValueKind} at position {i} where this reader "
                    + $"models {of}. It is a shape this reader has not been shown - report it "
                    + "rather than reading past it.");
            }
        }

        return elements;
    }
}
