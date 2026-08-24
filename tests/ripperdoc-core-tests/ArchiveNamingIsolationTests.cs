using System.Reflection;
using System.Text.RegularExpressions;
using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The engine core does not carry the dictionary's dependency tree.
/// </summary>
/// <remarks>
/// This is the claim the whole two-assembly split exists to make, so it is
/// asserted rather than left to a reader of the project files. The dictionary
/// package brings Entity Framework, SQLite, SharpDX and a native texture
/// toolchain with it; the engine is a library that a command-line client and a
/// server both load, and a reference added to core for convenience would move
/// all of that into every client silently.
/// </remarks>
public class ArchiveNamingIsolationTests
{
    private const string DictionaryPackage = "WolvenKit.Common";

    [Fact]
    public void TheEngineCoreDoesNotReferenceTheDictionaryAssembly()
    {
        var referenced = typeof(IResourceNameSource).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToList();

        // A source-reading check fails toward green: an empty reference list
        // would satisfy the assertion below while proving nothing.
        Assert.Contains("WolvenKit.RED4", referenced);

        Assert.DoesNotContain(DictionaryPackage, referenced);
    }

    [Fact]
    public void TheEngineCoreProjectDoesNotTakeTheDictionaryPackage()
    {
        // The assembly check above passes if core references the package but
        // never touches a type from it, because an unused reference is dropped
        // at compile time. The project file is where the dependency would
        // actually be taken, so both are asserted.
        var packageReferences = Regex
            .Matches(
                File.ReadAllText(EngineCoreProjectPath()),
                """<PackageReference\s+Include="(?<id>[^"]+)""")
            .Select(match => match.Groups["id"].Value)
            .ToList();

        Assert.Contains("WolvenKit.RED4", packageReferences);
        Assert.DoesNotContain(DictionaryPackage, packageReferences);
    }

    [Fact]
    public void TheDefaultPostureNamesItselfWithoutClaimingItNamesNothing()
    {
        // An archive carries paths of its own, so the dictionary-less posture
        // still names a large share of what it reads. A description saying
        // "hash-only" would understate it, and understating coverage is as much
        // a wrong provenance line as overstating it.
        var description = new ArchiveOnlyResourceNames().Description;

        Assert.Contains("archive", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash-only", description, StringComparison.OrdinalIgnoreCase);
    }

    private static string EngineCoreProjectPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName, "src", "ripperdoc-core", "ripperdoc-core.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        Assert.Fail(
            "This check reads the engine core's project file and could not find it above "
            + $"'{AppContext.BaseDirectory}'. It fails rather than skipping, because the dependency "
            + "boundary it guards is the reason the naming assembly is separate at all.");
        return string.Empty;
    }
}
