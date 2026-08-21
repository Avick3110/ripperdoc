using System.Reflection;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The pin guard.
/// </summary>
/// <remarks>
/// The engine inherits its resource type model from WolvenKit rather than
/// hand-writing it, so the pinned version is part of the engine's behaviour,
/// not just its build. A drift in that version changes what the type model
/// says while every other test keeps passing - which is exactly the silent
/// failure this project refuses. These assertions run on a bare runner: no
/// game, no dump, no game-derived bytes.
/// </remarks>
public class PinnedDependencyTests
{
    private const string PinnedVersion = "8.20.0";

    [Theory]
    [InlineData("WolvenKit.RED4")]
    [InlineData("WolvenKit.Core")]
    public void PinnedAssemblyLoadsAtThePinnedVersion(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);
        var version = assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.Equal(
            PinnedVersion,
            $"{version!.Major}.{version.Minor}.{version.Build}");
    }
}
