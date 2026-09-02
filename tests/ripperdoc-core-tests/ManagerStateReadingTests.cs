using Ripperdoc.Core.Diagnosis;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// What the reader makes of an authored state: which profile is active, which
/// mods it asks for, and the rules the manager holds.
/// </summary>
public sealed class ManagerStateReadingTests
{
    private const string Game = "a-game";
    private const string Other = "another-game";
    private const string Active = "profile-one";
    private const string Idle = "profile-two";

    [Fact]
    public void TheProfileTheStateRecordsAsActiveIsTheOneRead()
    {
        using var scratch = Bench();

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Equal(Active, reading.SelectedProfile);
        Assert.Null(reading.WhyNoProfile);
        Assert.Equal([Active, Idle], reading.ProfileCandidates);
        Assert.Equal(3, reading.Wanted!.Count);
        Assert.Equal(2, reading.Wanted.Count(mod => mod.Enabled));
    }

    /// <summary>
    /// The disabled branch of the wanted-set filter, from the key shape the
    /// characterisation read rather than from a state that exercised it.
    /// </summary>
    [Fact]
    public void AModStateSaidToBeDisabledIsNotWantedFromKeyShapeNotFromAReading()
    {
        using var scratch = Bench();

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var disabled = Assert.Single(reading.Wanted!.Where(mod => !mod.Enabled));

        Assert.Equal("mod-c", disabled.Id);
        Assert.Equal(0, DeploymentPartition.Of(reading.Wanted!, null).Mods
            .Count(mod => mod.Id == "mod-c"));
    }

    /// <summary>
    /// The key that records the active profile is keyed by game, and the reader
    /// does not fall back to the one that is not.
    /// </summary>
    [Fact]
    public void TheProfileOfAnotherGameIsNotThisGamesActiveProfile()
    {
        using var scratch = Bench();

        var reading = ManagerStateReading.Of(scratch.Write(), Other)!;

        Assert.Null(reading.SelectedProfile);
        Assert.Empty(reading.ProfileCandidates);
        Assert.Null(reading.Wanted);
        Assert.Contains("no profile was selected", Because(reading), StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoKeyRecordingTheActiveProfileTheReaderRefusesToPick()
    {
        using var scratch = Bench(recordTheActiveProfile: false);

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Null(reading.SelectedProfile);
        Assert.Null(reading.Wanted);
        Assert.Equal([Active, Idle], reading.ProfileCandidates);
        Assert.Contains("lastActiveProfile", reading.WhyNoProfile!, StringComparison.Ordinal);
        Assert.Contains("2 profiles", reading.WhyNoProfile!, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyNamingAProfileThisGameDoesNotHaveIsRefusedRatherThanResolved()
    {
        using var scratch = Bench(activeProfile: "a-profile-that-is-not-here");

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Null(reading.SelectedProfile);
        Assert.Null(reading.Wanted);
        Assert.Contains("disagrees with itself", reading.WhyNoProfile!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIdentityLawIsReportedRatherThanAssumed()
    {
        using var scratch = Bench();

        Assert.Empty(ManagerStateReading.Of(scratch.Write(), Game)!.InstallationPathIsNotTheId);
    }

    [Fact]
    public void AModWhoseInstallationPathIsNotItsIdIsNamed()
    {
        using var scratch = Bench(installationPathOfC: "somewhere-else");

        Assert.Equal(
            ["mod-c"],
            ManagerStateReading.Of(scratch.Write(), Game)!.InstallationPathIsNotTheId);
    }

    /// <summary>
    /// A rule whose reference resolves becomes an edge-bearing rule; one that
    /// does not is counted under the kind it declared.
    /// </summary>
    [Fact]
    public void RulesResolveToModIdsAndTheRestAreCountedNotInvented()
    {
        using var scratch = Bench();

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;
        var rule = Assert.Single(reading.Rules.Rules);

        Assert.Equal(new OrderingRule("mod-a", "mod-b", OrderingRuleKind.After), rule);
        Assert.Equal(
            [new UnresolvedRules("requires", 1)],
            reading.RulesNotResolved);
        Assert.Contains("state", reading.Rules.Home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARuleReferenceNamingAnArchiveResolvesToTheModThatHoldsIt()
    {
        using var scratch = Bench(referenceByArchive: true);

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Equal(
            new OrderingRule("mod-a", "mod-b", OrderingRuleKind.After),
            Assert.Single(reading.Rules.Rules));
    }

    [Fact]
    public void AWantedFlagThatIsNotTrueOrFalseIsRefusedByName()
    {
        using var scratch = Bench(enabledOfA: "\"yes\"");

        var refusal = Assert.Throws<StateReadException>(
            () => ManagerStateReading.Of(scratch.Write(), Game));

        Assert.Contains("true or false", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("mod-a", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadingSaysTheInUseSignatureIsNotEstablished()
    {
        using var scratch = Bench();

        Assert.Contains(
            ManagerStateReading.Of(scratch.Write(), Game)!.State.Caveats,
            caveat => caveat.Contains("not established", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every prefix the reading materialises under is one it declares, and the
    /// credentials namespace is under none of them.
    /// </summary>
    [Fact]
    public void NothingUnderTheCredentialsNamespaceIsMaterialised()
    {
        using var scratch = Bench();

        var reading = ManagerStateReading.Of(scratch.Write(), Game)!;

        Assert.Contains(reading.State.Values, pair => pair.Key.StartsWith("persistent###mods###"));
        Assert.DoesNotContain(reading.State.Values, pair => pair.Key.StartsWith("confidential"));
        Assert.Contains(reading.State.Values.Keys, key => ManagerStateReading.Prefixes(Game)
            .Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.All(
            reading.State.Values.Keys,
            key => Assert.Contains(
                ManagerStateReading.Prefixes(Game),
                prefix => key.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static string Because(ManagerStateReading reading) =>
        ManagerDiagnosis.Of(reading.State.Directory, reading.GameId, Path.GetTempPath())
            .Ordering.HomesNotRead.Select(home => home.Reason).Aggregate(string.Empty, (a, b) => a + b);

    /// <summary>
    /// A state shaped like a manager's, built out of the key shapes the
    /// characterisation published. Every id here is invented.
    /// </summary>
    private static SyntheticStateDatabase Bench(
        bool recordTheActiveProfile = true,
        string activeProfile = Active,
        string installationPathOfC = "mod-c",
        string enabledOfA = "true",
        bool referenceByArchive = false)
    {
        var scratch = new SyntheticStateDatabase();
        var reference = referenceByArchive
            ? """{"archiveId":"archive-of-b"}"""
            : """{"id":"mod-b"}""";

        scratch.Table(
            ($"persistent###profiles###{Active}###gameId", $"\"{Game}\""),
            ($"persistent###profiles###{Idle}###gameId", $"\"{Game}\""),
            ($"persistent###profiles###{Active}###modState###mod-a###enabled", enabledOfA),
            ($"persistent###profiles###{Active}###modState###mod-a###enabledTime", "1700000000"),
            ($"persistent###profiles###{Active}###modState###mod-b###enabled", "true"),
            ($"persistent###profiles###{Active}###modState###mod-c###enabled", "false"),
            ($"persistent###profiles###{Active}###modState###mod-c###disabledTime", "1700000001"),
            ($"persistent###mods###{Game}###mod-a###installationPath", "\"mod-a\""),
            ($"persistent###mods###{Game}###mod-a###type", "\"\""),
            ($"persistent###mods###{Game}###mod-a###archiveId", "\"archive-of-a\""),
            ($"persistent###mods###{Game}###mod-a###attributes###fileMD5", "\"hash-of-a\""),
            ($"persistent###mods###{Game}###mod-a###attributes###fileId", "101"),
            ($"persistent###mods###{Game}###mod-a###rules",
                $$"""[{"type":"after","reference":{{reference}}},"""
                + """{"type":"requires","reference":{"logicalFileName":"something not here"}}]"""),
            ($"persistent###mods###{Game}###mod-b###installationPath", "\"mod-b\""),
            ($"persistent###mods###{Game}###mod-b###type", "\"\""),
            ($"persistent###mods###{Game}###mod-b###archiveId", "\"archive-of-b\""),
            ($"persistent###mods###{Game}###mod-b###attributes###fileMD5", "\"hash-of-b\""),
            ($"persistent###mods###{Game}###mod-b###attributes###fileId", "102"),
            ($"persistent###mods###{Game}###mod-c###installationPath", $"\"{installationPathOfC}\""),
            ($"persistent###mods###{Game}###mod-c###type", "\"collection\""),
            ($"persistent###mods###{Game}###mod-c###attributes###fileMD5", "\"hash-of-c\""),
            ($"settings###mods###installPath###{Game}", "\"a-staging-root\""),
            ("confidential###account###apiKey", "\"a secret\""));

        if (recordTheActiveProfile)
        {
            scratch.Log(
                ($"settings###profiles###lastActiveProfile###{Game}", $"\"{activeProfile}\""),
                ("settings###profiles###activeProfileId", "\"a-profile-of-another-game\""));
        }

        return scratch;
    }
}
