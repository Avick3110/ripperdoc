using System.Text.Json;
using Ripperdoc.Core.Dump;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The reader of generated type information, on dumps authored for each check.
/// </summary>
/// <remarks>
/// The cases are the ways a dump can say something this reader has to not lose:
/// a key it does not know, a member with no value, and a directory that is not
/// there. Each of those has a quiet failure available to it - drop the key,
/// default the value, read three directories and find two - and this is where
/// each one is shown not to be taken.
/// </remarks>
public class DumpTypeModelTests
{
    private const string Description = "type information authored for this test";

    [Fact]
    public void AClassIsReadWithItsParentAndItsAccessors()
    {
        using var dump = SyntheticDump.Of(classes:
        [
            """
            {"name":"gamedataProbeThing_Record","parent":"gamedataProbeBase_Record","flags":66,
             "funcs":[{"fullName":"Speed","shortName":"Speed","flags":1,
                       "return":{"type":"Float","flags":64}}]}
            """,
            """{"name":"gamedataProbeBase_Record","flags":66}""",
        ]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        Assert.Equal(2, model.Classes.Count);
        Assert.Equal("gamedataProbeBase_Record", model.Classes["gamedataProbeThing_Record"].ParentName);
        Assert.Null(model.Classes["gamedataProbeBase_Record"].ParentName);
        Assert.Equal("Speed", model.Classes["gamedataProbeThing_Record"].Functions.Single().Name);
        Assert.Equal("Float", model.Classes["gamedataProbeThing_Record"].Functions.Single().ReturnTypeName);
        Assert.Empty(model.UnrecognisedKeys);
        Assert.Equal(Description, model.Description);
    }

    [Fact]
    public void TwoClassesOfOneNameLeaveTheSecondStatedRatherThanSubstituted()
    {
        using var dump = SyntheticDump.Of(classes:
        [
            """{"name":"gamedataProbeThing_Record","flags":66}""",
            """{"name":"gamedataProbeThing_Record","parent":"gamedataProbeBase_Record","flags":66}""",
            """{"name":"gamedataProbeBase_Record","flags":66}""",
        ]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        // The first is kept, and which one that is stops mattering because the
        // fact that there were two is on the record. Silently keeping the last
        // would have made the model depend on the order the files were read in,
        // with nothing saying a choice had been made at all.
        Assert.Equal(2, model.Classes.Count);
        Assert.Null(model.Classes["gamedataProbeThing_Record"].ParentName);
        Assert.Contains(
            model.ReadFailures,
            failure => failure.Contains("more than one class", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEnumerationAndABitfieldOfOneNameAreNotDecidedByDirectoryOrder()
    {
        // The two share one namespace here, so this collision is invisible
        // unless it is stated: whichever directory is read second would quietly
        // replace the other, and the model would describe a bitfield where the
        // dump described an enumeration.
        using var dump = SyntheticDump.Of(
            enums: ["""{"name":"gamedataProbeKind","members":[{"name":"First","value":1}]}"""],
            bitfields: ["""{"name":"gamedataProbeKind","members":[{"name":"First","bit":0}]}"""]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        Assert.False(model.Enums["gamedataProbeKind"].IsBitfield);
        Assert.Contains(
            model.ReadFailures,
            failure => failure.Contains("more than one enumeration or bitfield", StringComparison.Ordinal));
    }

    [Fact]
    public void ADescriptionWithNoNameIsStatedRatherThanFiledUnderTheEmptyName()
    {
        using var dump = SyntheticDump.Of(
            classes: ["""{"flags":66}"""],
            enums: ["""{"members":[{"name":"First","value":1}]}"""]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        // Nothing can ask for it, so nothing pretends it is there.
        Assert.Empty(model.Classes);
        Assert.Empty(model.Enums);
        Assert.Equal(2, model.ReadFailures.Count);
        Assert.All(
            model.ReadFailures,
            failure => Assert.Contains("states no name", failure, StringComparison.Ordinal));
    }

    [Fact]
    public void AModelThatReadEverythingStatesNoFailure()
    {
        using var dump = SyntheticDump.Of(
            classes: ["""{"name":"gamedataProbeThing_Record","flags":66}"""],
            enums: ["""{"name":"gamedataProbeKind","members":[{"name":"First","value":1}]}"""]);

        Assert.Empty(DumpTypeModel.Load(dump.JsonDirectory, Description).ReadFailures);
    }

    [Fact]
    public void AParameterIsAnOutputOnlyWhenTheDumpMarksItAsOne()
    {
        using var dump = SyntheticDump.Of(classes:
        [
            """
            {"name":"gamedataProbeThing_Record","flags":66,
             "funcs":[{"fullName":"Written","shortName":"Written","flags":1,
                       "params":[{"type":"array:Int32","name":"outList","flags":640}]},
                      {"fullName":"Read","shortName":"Read","flags":1,
                       "return":{"type":"Bool","flags":64},
                       "params":[{"type":"Int32","name":"index","flags":128}]}]}
            """,
        ]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);
        var functions = model.Classes["gamedataProbeThing_Record"].Functions;

        Assert.True(functions.Single(function => function.Name == "Written").Parameters.Single().IsOutput);
        Assert.False(functions.Single(function => function.Name == "Read").Parameters.Single().IsOutput);
    }

    [Fact]
    public void AKeyThisReaderDoesNotReadIsNamedRatherThanDropped()
    {
        using var dump = SyntheticDump.Of(classes:
        [
            """
            {"name":"gamedataProbeThing_Record","flags":66,"somethingNewer":17,
             "funcs":[{"fullName":"Speed","shortName":"Speed","flags":1,"newerStill":true,
                       "return":{"type":"Float","flags":64}}]}
            """,
        ]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        Assert.Equal(new[] { "class.somethingNewer", "function.newerStill" }, model.UnrecognisedKeys);
    }

    [Fact]
    public void AnEnumerationIsReadWithItsMemberValues()
    {
        using var dump = SyntheticDump.Of(
            enums: ["""{"name":"ProbeChoice","members":[{"name":"First","value":0},{"name":"Second","value":7}]}"""],
            bitfields: ["""{"name":"ProbeFlags","members":[{"name":"Low","bit":0},{"name":"High","bit":3}]}"""]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        Assert.Equal(new[] { 0L, 7L }, model.Enums["ProbeChoice"].Members.Select(member => member.Value));
        Assert.False(model.Enums["ProbeChoice"].IsBitfield);

        // A bitfield states the bit and the comparison wants the number that bit
        // sets, so the two kinds of member arrive in the same language.
        Assert.Equal(new[] { 1L, 8L }, model.Enums["ProbeFlags"].Members.Select(member => member.Value));
        Assert.True(model.Enums["ProbeFlags"].IsBitfield);
    }

    [Fact]
    public void AnEnumerationWithNoMembersIsReadRatherThanRefused()
    {
        using var dump = SyntheticDump.Of(enums: ["""{"name":"ProbeEmpty","members":[]}"""]);

        var model = DumpTypeModel.Load(dump.JsonDirectory, Description);

        Assert.Empty(model.Enums["ProbeEmpty"].Members);
    }

    [Fact]
    public void AMemberStatingNoValueIsRefusedRatherThanPlacedAtZero()
    {
        using var dump = SyntheticDump.Of(enums: ["""{"name":"ProbeChoice","members":[{"name":"First"}]}"""]);

        var thrown = Assert.Throws<JsonException>(() => DumpTypeModel.Load(dump.JsonDirectory, Description));

        Assert.Contains("First", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABitfieldMemberStatingNoBitIsRefusedRatherThanPlacedAtZero()
    {
        using var dump = SyntheticDump.Of(bitfields: ["""{"name":"ProbeFlags","members":[{"name":"Low"}]}"""]);

        var thrown = Assert.Throws<JsonException>(() => DumpTypeModel.Load(dump.JsonDirectory, Description));

        Assert.Contains("Low", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("classes")]
    [InlineData("enums")]
    [InlineData("bitfields")]
    public void ADumpMissingOneOfItsDirectoriesIsRefusedAndTheDirectoryIsNamed(string missing)
    {
        using var dump = SyntheticDump.Of();
        dump.Remove(missing);

        var thrown = Assert.Throws<DirectoryNotFoundException>(
            () => DumpTypeModel.Load(dump.JsonDirectory, Description));

        Assert.Contains(missing, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APathThatIsNotADumpAtAllIsRefused()
    {
        var absent = Path.Combine(Path.GetTempPath(), "ripperdoc-absent-" + Guid.NewGuid().ToString("n"));

        Assert.Throws<DirectoryNotFoundException>(() => DumpTypeModel.Load(absent, Description));
    }
}
