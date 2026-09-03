using System.Diagnostics.CodeAnalysis;

namespace Ripperdoc.Core.ManagerState;

/// <summary>
/// An index from a spelling to the one thing that answers to it, and the
/// spellings more than one thing answered to.
/// </summary>
/// <remarks>
/// <para>
/// A spelling two of <typeparamref name="T" /> answer to identifies neither.
/// Taking whichever was read first attributes a rule to a mod on the strength
/// of an ordering, so a contested spelling is dropped from the index and named
/// in <see cref="Contested" />, where a caller can see that it decided nothing.
/// </para>
/// <para>
/// One place, so that the rule is a property of the code rather than a habit
/// each site keeps.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// What a spelling names, and the identity a contest is decided on: two
/// spellings that name one value of it are one answer, and two values are a
/// contest whatever the two stand for further out. A manager's reading names a
/// mod id, so a mod recorded twice under one spelling is not a contest; a
/// manifest names a declared mod's position, so one mod declared twice is two
/// positions and its spellings are contested.
/// </typeparam>
internal sealed class SpellingIndex<T>
{
    private readonly Dictionary<string, T> named;

    private SpellingIndex(Dictionary<string, T> named, IReadOnlyList<string> contested)
    {
        this.named = named;
        Contested = contested;
    }

    /// <summary>
    /// The spellings more than one thing answered to, under the field that
    /// spelled them.
    /// </summary>
    internal IReadOnlyList<string> Contested { get; }

    /// <summary>
    /// An index over the spellings a field gave, with the contested ones out of
    /// it.
    /// </summary>
    /// <param name="field">The field the spellings were read from, for a report.</param>
    /// <param name="spellings">Each spelling with the thing that answers to it.</param>
    /// <returns>The index.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <remarks>
    /// A spelling that is absent or empty is one no document wrote down, and it
    /// is neither in the index nor a contest. Spellings are compared ordinally,
    /// because what a document wrote is the identity: folding case would let one
    /// document's spelling answer for another's, which is the attribution this
    /// type exists to refuse.
    /// </remarks>
    internal static SpellingIndex<T> Of(
        string field, IEnumerable<(string? Spelling, T Names)> spellings)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(spellings);

        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        var contested = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (spelling, names) in spellings)
        {
            if (spelling is not { Length: > 0 } written)
            {
                continue;
            }

            if (index.TryGetValue(written, out var held)
                && !EqualityComparer<T>.Default.Equals(held, names))
            {
                contested.Add(written);
            }

            index[written] = names;
        }

        foreach (var written in contested)
        {
            index.Remove(written);
        }

        return new SpellingIndex<T>(
            index,
            [.. contested.Select(written => $"{field} '{written}'").Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// What one spelling names, where one thing does.
    /// </summary>
    /// <param name="spelling">The spelling, as a document wrote it.</param>
    /// <param name="names">What it names.</param>
    /// <returns>Whether one thing answers to it.</returns>
    /// <remarks>
    /// Null is what a document that carries no such field spells, and it is
    /// answered rather than refused: a caller asks with what it read, and making
    /// an absent field an error would put the reading of one document at the
    /// mercy of a field another one happens to declare.
    /// </remarks>
    internal bool Names(string? spelling, [MaybeNullWhen(false)] out T names)
    {
        if (spelling is { } written)
        {
            return named.TryGetValue(written, out names);
        }

        names = default;

        return false;
    }
}
