using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The refusal that stands between an unverified answer and a verified one.
/// </summary>
/// <remarks>
/// The observation reads the pinned library's internals on purpose, and the
/// whole value of doing it that way rests on what happens when those internals
/// move: the run has to stop, saying which field it could not find and what to
/// confirm. That is a sentence a reader acts on, so it is measured rather than
/// assumed - driven at a field the library does not have, which is the shape a
/// version change would take.
/// </remarks>
[Collection(ResolverCollection.Name)]
public class LoadedNameDictionaryTests
{
    [Fact]
    public void AResolverWithoutTheExpectedPoolStopsTheRunAndNamesTheField()
    {
        var thrown = Assert.Throws<ResourceNameSourceException>(
            () => LoadedNameDictionary.IsLoaded("s_poolRenamedUpstream", "_nativePool"));

        Assert.Contains("s_poolRenamedUpstream", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Confirm the pinned WolvenKit version", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AResolverWithoutTheExpectedTableStopsTheRunAndNamesTheField()
    {
        var thrown = Assert.Throws<ResourceNameSourceException>(
            () => LoadedNameDictionary.IsLoaded("s_pool", "_nativePoolRenamedUpstream"));

        Assert.Contains("_nativePoolRenamedUpstream", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Confirm the pinned WolvenKit version", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheObservationItselfStillWorksAtTheRealFields()
    {
        // The canary for the two above: a refusal that fired at the real fields
        // too would make them pass while proving nothing about a version change.
        var thrown = Record.Exception(() => LoadedNameDictionary.IsLoaded());

        Assert.Null(thrown);
    }
}
