using Ripperdoc.Core.Diagnosis;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// A curated list's manifest, joined to the manager's own identity.
/// </summary>
/// <remarks>
/// Every manifest here is authored. The join under test is the one the
/// characterisation measured: a rule side names a file, the file names a
/// declared mod, and the declared mod names the manager's mod id.
/// </remarks>
public sealed class CollectionManifestTests : IDisposable
{
    private const string Game = "a-game";
    private const string Profile = "profile-one";
    private const string Container = "mod-list";

    private readonly string staging =
        Directory.CreateTempSubdirectory("ripperdoc-staging-").FullName;

    public void Dispose() => Directory.Delete(staging, recursive: true);

    [Fact]
    public void TheManifestIsFoundWhereTheStateSaysTheListIsStaged()
    {
        using var scratch = State();

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Equal(
            [Path.Combine(staging, Container, CollectionManifest.FileName)],
            CollectionManifest.PathsIn(reading));
    }

    [Fact]
    public void ARuleSideNamingAFileBecomesARuleAboutTheManagersOwnModIds()
    {
        using var scratch = State();
        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var path = Written(Manifest("""
            {"type":"before",
             "source":{"fileMD5":"hash-of-a","logicalFileName":"a.zip","versionMatch":"*"},
             "reference":{"fileMD5":"hash-of-b","logicalFileName":"b.zip","versionMatch":"*"}}
            """));

        var manifest = CollectionManifest.In(path, reading)!;

        Assert.Equal(
            new OrderingRule("mod-a", "mod-b", OrderingRuleKind.Before),
            Assert.Single(manifest.Rules.Rules));
        Assert.Equal(2, manifest.DeclaredMods);
        Assert.Equal(0, manifest.DeclaredModsNotInTheState);
        Assert.Empty(manifest.RulesNotResolved);
    }

    /// <summary>
    /// The two documents spell the same value with different capitals, and the
    /// join reads both.
    /// </summary>
    [Fact]
    public void ASideNamingOnlyALogicalFileNameJoinsThroughTheDeclaredMod()
    {
        using var scratch = State();
        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var path = Written(Manifest("""
            {"type":"after",
             "source":{"logicalFileName":"a.zip","versionMatch":"*"},
             "reference":{"logicalFileName":"b.zip","versionMatch":"*"}}
            """));

        Assert.Equal(
            new OrderingRule("mod-a", "mod-b", OrderingRuleKind.After),
            Assert.Single(CollectionManifest.In(path, reading)!.Rules.Rules));
    }

    /// <summary>
    /// A side the manifest does not declare is counted, never given a node of
    /// its own.
    /// </summary>
    [Fact]
    public void ASideNamingAFileTheListDoesNotDeclareIsResidue()
    {
        using var scratch = State();
        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var path = Written(Manifest("""
            {"type":"before",
             "source":{"fileMD5":"hash-of-a","logicalFileName":"a.zip","versionMatch":"*"},
             "reference":{"fileMD5":"hash-of-nothing","logicalFileName":"z.zip","versionMatch":"*"}}
            """));

        var manifest = CollectionManifest.In(path, reading)!;

        Assert.Empty(manifest.Rules.Rules);
        Assert.Equal([new UnresolvedRules("before", 1)], manifest.RulesNotResolved);
    }

    /// <summary>
    /// A mod the list declares and the manager never installed is not a node.
    /// </summary>
    [Fact]
    public void ADeclaredModTheStateDoesNotKnowIsCountedAndIsNotANode()
    {
        using var scratch = State();
        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var path = Written(
            """
            {"mods":[
              {"name":"A","source":{"md5":"hash-of-a","logicalFilename":"a.zip","fileId":101}},
              {"name":"Z","source":{"md5":"never-installed","logicalFilename":"z.zip","fileId":999}}],
             "modRules":[
              {"type":"before",
               "source":{"fileMD5":"hash-of-a","versionMatch":"*"},
               "reference":{"fileMD5":"never-installed","versionMatch":"*"}}]}
            """);

        var manifest = CollectionManifest.In(path, reading)!;

        Assert.Equal(1, manifest.DeclaredModsNotInTheState);
        Assert.Empty(manifest.Rules.Rules);
        Assert.Equal([new UnresolvedRules("before", 1)], manifest.RulesNotResolved);
    }

    [Fact]
    public void NoFileAtThePathIsNoManifestRatherThanAnEmptyOne()
    {
        using var scratch = State();

        Assert.Null(CollectionManifest.In(
            Path.Combine(staging, Container, CollectionManifest.FileName),
            ManagerStateReading.Of(scratch.Write(), Game)!));
    }

    [Fact]
    public void SomethingThatIsNotAManifestIsRefusedByName()
    {
        using var scratch = State();
        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Contains(
            "carries no 'mods' array",
            Assert.Throws<StateReadException>(
                () => CollectionManifest.In(Written("""{"something":"else"}"""), reading)).Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "not readable as JSON",
            Assert.Throws<StateReadException>(
                () => CollectionManifest.In(Written("not json at all"), reading)).Message,
            StringComparison.Ordinal);
    }

    private static string Manifest(string rule) =>
        """
        {"mods":[
          {"name":"A","source":{"md5":"hash-of-a","logicalFilename":"a.zip","fileId":101}},
          {"name":"B","source":{"md5":"hash-of-b","logicalFilename":"b.zip","fileId":102}}],
         "modRules":[
        """ + rule + "]}";

    private string Written(string text)
    {
        var directory = Path.Combine(staging, Container);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, CollectionManifest.FileName);
        File.WriteAllText(path, text);

        return path;
    }

    private SyntheticStateDatabase State()
    {
        var scratch = new SyntheticStateDatabase();

        scratch.Table(
            ($"persistent###profiles###{Profile}###gameId", $"\"{Game}\""),
            ($"persistent###profiles###{Profile}###modState###mod-a###enabled", "true"),
            ($"persistent###profiles###{Profile}###modState###mod-b###enabled", "true"),
            ($"persistent###profiles###{Profile}###modState###{Container}###enabled", "true"),
            ($"persistent###mods###{Game}###mod-a###installationPath", "\"mod-a\""),
            ($"persistent###mods###{Game}###mod-a###type", "\"\""),
            ($"persistent###mods###{Game}###mod-a###attributes###fileMD5", "\"hash-of-a\""),
            ($"persistent###mods###{Game}###mod-a###attributes###fileId", "101"),
            ($"persistent###mods###{Game}###mod-b###installationPath", "\"mod-b\""),
            ($"persistent###mods###{Game}###mod-b###type", "\"\""),
            ($"persistent###mods###{Game}###mod-b###attributes###fileMD5", "\"hash-of-b\""),
            ($"persistent###mods###{Game}###mod-b###attributes###fileId", "102"),
            ($"persistent###mods###{Game}###{Container}###installationPath", $"\"{Container}\""),
            ($"persistent###mods###{Game}###{Container}###type", "\"collection\""),
            ($"settings###mods###installPath###{Game}", System.Text.Json.JsonSerializer.Serialize(staging)),
            ($"settings###profiles###lastActiveProfile###{Game}", $"\"{Profile}\""));

        return scratch;
    }
}
