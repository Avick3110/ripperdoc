using Ripperdoc.Core.Archive;
using Ripperdoc.Naming;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The dictionary source's self-check.
/// </summary>
/// <remarks>
/// The dictionary loads into a resolver that lives for the life of the
/// process, so "before" is observable exactly once. Everything about the
/// transition is therefore asserted inside one fact rather than split across
/// several that would race each other for the only unloaded state there is.
/// </remarks>
public class DictionaryResourceNamesTests
{
    [Fact]
    public void PreparingTheSourceTakesTheResolverFromNoNamesToSome()
    {
        Assert.False(
            LoadedNameDictionary.IsLoaded(),
            "the resolver already held names before this check ran, so the load it is about to "
            + "verify cannot be told from one that had already happened");

        new DictionaryResourceNames().Prepare();

        Assert.True(LoadedNameDictionary.IsLoaded());

        // Idempotent: the source guards its own repeat, and a second call is
        // neither an error nor a second load.
        new DictionaryResourceNames().Prepare();
        Assert.True(LoadedNameDictionary.IsLoaded());
    }

    [Fact]
    public void TheDescriptionNamesTheDictionaryAndThePinnedPackageVersion()
    {
        // The provenance line a reader of an artifact sees. It has to say which
        // posture produced the run, because two runs of one directory under
        // different postures disagree about how many entries have names.
        var description = new DictionaryResourceNames().Description;

        Assert.Contains("WolvenKit.Common", description, StringComparison.Ordinal);
        Assert.Contains("8.20.0", description, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSourceIsAResourceNameSource()
    {
        Assert.IsAssignableFrom<IResourceNameSource>(new DictionaryResourceNames());
    }
}
