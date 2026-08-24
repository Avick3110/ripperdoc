using Ripperdoc.Core.Archive;
using WolvenKit.Common.Services;

namespace Ripperdoc.Naming;

/// <summary>
/// The wider naming posture: the resource-name dictionary shipped in the
/// pinned library's companion package, loaded into the resolver the archive
/// reader already consults.
/// </summary>
/// <remarks>
/// Installing this source raises how many resources can be named. It changes
/// nothing about what is reported: every entry is still reported, and one with
/// no name is still reported by hash. The dictionary moves the boundary; it
/// does not move the contract.
/// <para>
/// The dictionary loads into a process-wide resolver inside the pinned
/// library, so its effect is not scoped to one reader or one inventory. That
/// is a property of the library rather than a choice made here, and it is why
/// <see cref="Prepare" /> is safe to call more than once but cannot be undone.
/// </para>
/// </remarks>
public sealed class DictionaryResourceNames : IResourceNameSource
{
    private static readonly object Gate = new();
    private static bool _prepared;

    /// <inheritdoc />
    public string Description =>
        $"resource-name dictionary from WolvenKit.Common {DictionaryPopulation.PackageVersion()}";

    /// <summary>
    /// Loads the dictionary, and confirms it actually loaded.
    /// </summary>
    /// <remarks>
    /// The confirmation is the point. The service that owns the dictionary
    /// reports a population of zero whether or not it loaded anything, so
    /// calling it and trusting it would leave the one failure that matters
    /// invisible: a run whose provenance claims dictionary coverage while every
    /// entry comes back by hash. So the load is verified against the resolver
    /// the names actually have to reach.
    /// </remarks>
    /// <exception cref="ResourceNameSourceException">
    /// The dictionary did not load, or the pinned library's resolver could not
    /// be inspected to confirm that it did.
    /// </exception>
    public void Prepare()
    {
        lock (Gate)
        {
            if (_prepared)
            {
                return;
            }

            try
            {
                new HashService().Load();
            }
            catch (Exception exception)
            {
                throw new ResourceNameSourceException(
                    "The resource-name dictionary in WolvenKit.Common could not be loaded, so an " +
                    "inventory read now would report entries by hash while claiming dictionary " +
                    "coverage. Check that the WolvenKit.Common package is the pinned version and " +
                    "that its embedded dictionary resource is intact.",
                    exception);
            }

            if (!DictionaryPopulation.AnyNames())
            {
                throw new ResourceNameSourceException(
                    "The resource-name dictionary reported no failure but left the pinned " +
                    "library's resolver holding no names at all. Every entry would be reported by " +
                    "hash while this run's provenance claimed dictionary coverage, so the read is " +
                    "refused instead. Check that the WolvenKit.Common package is the pinned " +
                    "version and that its embedded dictionary resource is intact.");
            }

            _prepared = true;
        }
    }
}
