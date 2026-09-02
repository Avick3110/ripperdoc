using Ripperdoc.Core.Diagnosis;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The deployment record reader against a record the running user is refused.
/// </summary>
/// <remarks>
/// The sibling arm in <see cref="ManagerDiagnosisTests" /> drives the same
/// refusal through a sharing violation, which runs on any platform. This one
/// drives it through an ACL, which is the condition an ordinary bench actually
/// produces.
/// </remarks>
[Trait(TierTrait.Name, TierTrait.DeniedDirectory)]
public sealed class DeniedDeploymentRecordTests : IDisposable
{
    private const string Game = "a-game";
    private const string Profile = "profile-one";

    private readonly DeniedPaths denied = new();

    private readonly string gameDirectory =
        Directory.CreateTempSubdirectory("ripperdoc-denied-record-").FullName;

    public void Dispose()
    {
        denied.Dispose();

        try
        {
            Directory.Delete(gameDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is not worth failing a check over.
        }
    }

    /// <summary>
    /// A record the process is refused is said to be there and unreadable,
    /// rather than escaping as a platform exception.
    /// </summary>
    [Fact]
    public void ADeniedRecordIsRefusedByNameRatherThanEscapingRaw()
    {
        var record = Path.Combine(gameDirectory, DeploymentRecord.FileName);
        File.WriteAllText(record, """{"files":[]}""");
        Deny(record);

        var refusal = Assert.Throws<DiagnosisReadException>(
            () => DeploymentRecord.In(gameDirectory));

        Assert.Contains(record, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("there and could not be read", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A directory whose record is refused carries no partition, and says so
    /// rather than reading as one carrying no record.
    /// </summary>
    [Fact]
    public void ADeniedRecordLeavesNoPartitionAndSaysWhy()
    {
        // A profile the state names active, so the only thing standing between
        // this directory and a partition is the record.
        using var scratch = new SyntheticStateDatabase();
        scratch.Table(
            ($"persistent###profiles###{Profile}###gameId", $"\"{Game}\""),
            ($"persistent###profiles###{Profile}###modState###mod-a###enabled", "true"),
            ($"persistent###mods###{Game}###mod-a###installationPath", "\"mod-a\""),
            ($"persistent###mods###{Game}###mod-a###type", "\"\""),
            ($"settings###profiles###lastActiveProfile###{Game}", $"\"{Profile}\""));

        var record = Path.Combine(gameDirectory, DeploymentRecord.FileName);
        File.WriteAllText(record, """{"files":[]}""");
        Deny(record);

        var diagnosis = ManagerDiagnosis.Of(scratch.Write(), Game, gameDirectory);

        Assert.Null(diagnosis.Record);
        Assert.Null(diagnosis.Partition);
        Assert.Contains("could not be read", diagnosis.WhyNoPartition!, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "carries no deployment record", diagnosis.WhyNoPartition!, StringComparison.Ordinal);

        // The half a raw throw took down with it.
        Assert.NotNull(diagnosis.State);
    }

    /// <remarks>
    /// The refusal is confirmed rather than assumed: a process holding a
    /// privilege that walks through it would leave these checks asserting
    /// nothing.
    /// </remarks>
    private void Deny(string path)
    {
        denied.Deny(path, "(R)");

        Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllBytes(path));
    }
}
