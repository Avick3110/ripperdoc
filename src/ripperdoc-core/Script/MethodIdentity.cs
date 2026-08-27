namespace Ripperdoc.Core.Script;

/// <summary>
/// The method an annotation targets: a type name and a method name.
/// </summary>
/// <param name="TypeName">The type named in the annotation.</param>
/// <param name="MethodName">The name of the function declared beneath it.</param>
/// <remarks>
/// <strong>This identity is name-level, and it cannot tell overloads apart.</strong>
/// Two annotations targeting two different overloads of one name resolve to a
/// single identity here and are reported as contending when the compiler would
/// treat them as separate methods. Nothing measured says which of the two the
/// compiler does, because every target in the measurement had exactly one
/// method of its name on its type - so a signature-aware identity would be a
/// guess at a rule rather than an implementation of one, and this engine
/// reports the scope it actually resolves instead.
/// <para>
/// The consequence is bounded and one-directional: a contest reported over an
/// overloaded name may not be a contest. It cannot hide one.
/// </para>
/// <para>
/// Names compare with the ordinal comparer. The language is case sensitive in
/// its identifiers, so folding case here would merge two methods the compiler
/// keeps apart.
/// </para>
/// </remarks>
public sealed record MethodIdentity(string TypeName, string MethodName)
{
    /// <summary>How to name this method to a reader.</summary>
    public string Display => $"{TypeName}.{MethodName}";

    /// <summary>
    /// Whether this identity distinguishes overloads.
    /// </summary>
    /// <remarks>
    /// Always false, and present so that a caller reporting a contest can state
    /// the scope it was resolved at rather than leaving a reader to assume the
    /// stronger one. A signature-aware identity would make this a real
    /// question; today it is a fact about the engine, said out loud.
    /// </remarks>
    public static bool DistinguishesOverloads => false;
}
