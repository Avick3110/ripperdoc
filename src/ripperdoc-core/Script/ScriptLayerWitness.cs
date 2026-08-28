namespace Ripperdoc.Core.Script;

/// <summary>
/// A script layer that brings one kind about, carried on that kind's own
/// declaration.
/// </summary>
/// <remarks>
/// <para>
/// A kind declared with no way to reach a result is the failure the completeness
/// check exists for, and a fixture kept beside the declarations is a second
/// place that has to be remembered. Carrying the witness on the declaration
/// makes the constructor the one gate: a kind that cannot be provoked cannot be
/// written down.
/// </para>
/// <para>
/// Sources rather than a prepared state. What is checked is that the kind
/// arises from the engine's own reading of a layer, so a witness that handed a
/// predicate the answer it was looking for would be checking the predicate
/// against itself.
/// </para>
/// <para>
/// Every byte here is authored. The rules these provoke are about names,
/// directory structure and annotation text.
/// </para>
/// </remarks>
internal sealed class ScriptLayerWitness
{
    internal ScriptLayerWitness(params (string Path, string Text)[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Length == 0)
        {
            throw new ArgumentException(
                "A witness with no source provokes nothing, and a kind whose witness provokes nothing "
                + "is the state the completeness check exists to name.",
                nameof(sources));
        }

        Sources = sources;
    }

    /// <summary>The layer's files, as relative path and contents.</summary>
    internal IReadOnlyList<(string Path, string Text)> Sources { get; }
}
