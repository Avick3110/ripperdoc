using System.Reflection;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Every package of the inherited family is pinned to one version, and that
    /// version is the pinned one.
    /// </summary>
    /// <remarks>
    /// Read from the central pin file rather than from loaded assemblies,
    /// because the family is deliberately not all loaded into one process: the
    /// dictionary package is referenced only by the opt-in naming assembly, so
    /// a check that inspected what this test process happens to have loaded
    /// would pass while saying nothing about the member it could not see.
    /// <para>
    /// The family has to agree because these assemblies share types across
    /// package boundaries. A version split there does not fail to build - it
    /// resolves to one of the two and changes behaviour quietly.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheWholeInheritedFamilyIsPinnedToOneVersion()
    {
        var pinFile = PinnedPackages.FilePath();

        // The element first, then each attribute out of it independently. A
        // single pattern spelling the attributes in one order stops matching
        // when they are written in the other, and a pin guard that quietly
        // matches nothing is the shape of guard this project refuses.
        var declared = Regex.Matches(File.ReadAllText(pinFile), """<PackageVersion\s[^>]*/>""")
            .Select(element => (
                Id: Attribute(element.Value, "Include"),
                Version: Attribute(element.Value, "Version")))
            .Where(package => package.Id.StartsWith("WolvenKit.", StringComparison.Ordinal))
            .ToList();

        // A source-reading check fails toward green: no matches would satisfy
        // every assertion below while having inspected nothing.
        Assert.True(
            declared.Count >= 3,
            $"Expected the pin file to declare at least three WolvenKit packages, found {declared.Count} in {pinFile}.");

        Assert.All(declared, package => Assert.Equal(PinnedVersion, package.Version));
    }

    private static string Attribute(string element, string name)
    {
        var match = Regex.Match(element, name + "=\"(?<value>[^\"]*)\"");
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }
}
