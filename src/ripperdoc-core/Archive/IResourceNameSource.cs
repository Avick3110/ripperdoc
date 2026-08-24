namespace Ripperdoc.Core.Archive;

/// <summary>
/// A source of names for the resources an archive carries.
/// </summary>
/// <remarks>
/// An archive addresses its entries by hash, not by path. Some entries carry
/// their own path and can be named from the archive alone; the rest can only
/// be named from a dictionary of known paths, and that dictionary ships in a
/// package the engine deliberately does not depend on.
/// <para>
/// So naming is a seam rather than a fact. The engine core resolves whatever
/// names are available to it and reports every remaining entry by hash; a
/// caller that wants the wider coverage installs a source that supplies it.
/// The engine's own package closure does not grow either way, which is the
/// point: the dictionary carries a mod editor's dependency tree, and this is a
/// library that a command-line client and a server both load.
/// </para>
/// <para>
/// What a source must never do is fail quietly. A source that cannot make its
/// names available throws from <see cref="Prepare"/> - because the alternative
/// is an inventory that reports thousands of entries by hash while its
/// provenance claims dictionary coverage, which is a wrong answer wearing the
/// label of a right one.
/// </para>
/// </remarks>
public interface IResourceNameSource
{
    /// <summary>
    /// What this source is, for the inventory's provenance block. Names the
    /// source and, where one applies, the package version it reads from, so a
    /// reader of the artifact can tell which naming posture produced it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Makes this source's names available, and throws if it cannot.
    /// </summary>
    /// <remarks>
    /// Called once before an inventory is read. Implementations that need to
    /// load something verify that the load actually happened rather than
    /// assuming it did.
    /// </remarks>
    /// <exception cref="ResourceNameSourceException">
    /// The source could not make its names available. The message says what was
    /// attempted and what to try next.
    /// </exception>
    void Prepare();
}
