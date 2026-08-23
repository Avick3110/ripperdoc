using Ripperdoc.Core.Dump;
using Ripperdoc.Core.Schema;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Recovering a record type's fields from the shapes of its accessors.
/// </summary>
/// <remarks>
/// One check per accessor shape the generated type information actually
/// carries, and one per thing the recovery must not do: invent a field from an
/// accessor that only helps you reach one, and take the runtime's answer for
/// the stored form where the two differ.
/// </remarks>
public class DumpRecordTypeSourceTests
{
    private const string Description = "type information authored for this test";

    [Fact]
    public void AnAccessorTakingNothingAndGivingAValueIsAField()
    {
        var fields = FieldsOf(Record("gamedataProbeThing_Record", Accessor("Speed", "Float")));

        Assert.Equal("Float", fields["speed"].StorageType);
        Assert.Null(fields["speed"].ReferentTypeName);
    }

    [Fact]
    public void AnAccessorWritingIntoAnOutputIsTheSameFieldAsOneGivingTheValue()
    {
        var fields = FieldsOf(Record(
            "gamedataProbeThing_Record",
            OutputAccessor("Tags", "array:CName")));

        Assert.Equal("array:CName", Assert.Contains("tags", fields).StorageType);
        Assert.Single(fields);
    }

    [Theory]
    // The accessors the runtime registers around a field: how many, which one,
    // whether it holds a given value - none of which is a field.
    [InlineData("GetTagsCount", "Int32", null, null)]
    [InlineData("GetTagsItem", "CName", "Int32", "index")]
    [InlineData("GetTagsItemHandle", "whandle:gamedataProbeOther_Record", "Int32", "index")]
    [InlineData("TagsContains", "Bool", "CName", "item")]
    public void AnAccessorThatOnlyHelpsReachAFieldIsNotItselfOne(
        string accessorName,
        string returns,
        string? parameterType,
        string? parameterName)
    {
        var helper = parameterType is null
            ? Accessor(accessorName, returns)
            : new DumpFunction(accessorName, returns, [new DumpParameter(parameterName!, parameterType, false)]);

        var fields = FieldsOf(Record(
            "gamedataProbeThing_Record",
            OutputAccessor("Tags", "array:CName"),
            helper));

        Assert.Equal(new[] { "tags" }, fields.Keys);
    }

    [Theory]
    // A genuine field whose name happens to end the way one of the helper
    // accessors does. Each of these writes into an output parameter, which is
    // how the game states a field whose value does not come back as a return
    // value - so each is a field, and a rule matching the name alone would drop
    // it and the schema would never mention it again.
    [InlineData("TagsContains", "array:CName", "tagsContains")]
    [InlineData("GetLootCount", "Int32", "getLootCount")]
    [InlineData("GetStarterItem", "whandle:gamedataProbeOther_Record", "getStarterItem")]
    public void AFieldWhoseNameEndsLikeAHelperIsStillAField(
        string accessorName,
        string runtimeType,
        string stored)
    {
        var fields = FieldsOf(Record("gamedataProbeThing_Record", OutputAccessor(accessorName, runtimeType)));

        Assert.Contains(stored, fields.Keys);
    }

    [Theory]
    // The dump writes a method that returns nothing either way round, so both
    // spellings of "no value" have to mean it.
    [InlineData("None")]
    [InlineData(null)]
    public void AnAccessorGivingNoValueIsNotAFieldOfTypeNone(string? returns)
    {
        var fields = FieldsOf(Record(
            "gamedataProbeThing_Record",
            Accessor("Speed", "Float"),
            new DumpFunction("Reset", returns, [])));

        // "None" is a name that passes for a storage type, so a field of that
        // type is refused by nothing downstream and matches no stored value
        // anywhere - the schema simply carries a slot that cannot exist.
        Assert.Equal(new[] { "speed" }, fields.Keys);
    }

    [Fact]
    public void AnAccessorTakingAValueToLookForIsNotAFieldWhateverItIsCalled()
    {
        // The other side of it. A membership test reads its parameter rather
        // than writing into it, and an accessor that reads a parameter gives no
        // value of its own - so it is not a field, and that holds for a name
        // this reader has never heard of as much as for one it has.
        var fields = FieldsOf(Record(
            "gamedataProbeThing_Record",
            OutputAccessor("Tags", "array:CName"),
            new DumpFunction("TagsHoldsAnyOf", "Bool", [new DumpParameter("item", "CName", false)])));

        Assert.Equal(new[] { "tags" }, fields.Keys);
    }

    [Fact]
    public void TheSecondFormOfAReferenceAccessorIsNotItsOwnField()
    {
        var fields = FieldsOf(Record(
            "gamedataProbeThing_Record",
            Accessor("Owner", "whandle:gamedataProbeOther_Record"),
            Accessor("OwnerHandle", "handle:gamedataProbeOther_Record")));

        Assert.Equal(new[] { "owner" }, fields.Keys);
    }

    [Fact]
    public void AnAccessorEndingLikeAReferencesSecondFormIsAFieldWhenThereIsNoFirstForm()
    {
        // The drop rule keys on the accessor it claims to duplicate being there.
        // Keyed on the name alone it would swallow a real field whose name
        // happens to end this way.
        var fields = FieldsOf(Record("gamedataProbeThing_Record", Accessor("GripHandle", "CName")));

        Assert.Equal(new[] { "gripHandle" }, fields.Keys);
    }

    [Theory]
    [InlineData("whandle:gamedataProbeOther_Record", "TweakDBID", "gamedataProbeOther_Record")]
    [InlineData("handle:gamedataProbeOther_Record", "TweakDBID", "gamedataProbeOther_Record")]
    [InlineData("array:whandle:gamedataProbeOther_Record", "array:TweakDBID", "gamedataProbeOther_Record")]
    [InlineData("redResourceReferenceScriptToken", "raRef:CResource", null)]
    [InlineData("array:redResourceReferenceScriptToken", "array:raRef:CResource", null)]
    [InlineData("Float", "Float", null)]
    public void TheStoredFormIsRecoveredAndTheReferentIsKept(
        string runtimeType,
        string storageType,
        string? referent)
    {
        var fields = FieldsOf(Record("gamedataProbeThing_Record", Accessor("Target", runtimeType)));

        Assert.Equal(storageType, fields["target"].StorageType);
        Assert.Equal(referent, fields["target"].ReferentTypeName);
    }

    [Theory]
    // The accessor's capitalisation is the accessor's, not the stored value's,
    // so both spellings are carried and real data decides between them.
    [InlineData("Speed", "speed", "Speed")]
    [InlineData("GOGKey", "gogKey", "GOGKey")]
    [InlineData("AND", "and", "AND")]
    public void BothSpellingsOfAFieldNameAreCarried(string accessorName, string stored, string alternate)
    {
        var fields = FieldsOf(Record("gamedataProbeThing_Record", Accessor(accessorName, "Float")));

        Assert.Equal(stored, fields[stored].Name);
        Assert.Equal(new[] { alternate }, fields[stored].AlternateNames);
    }

    [Fact]
    public void AnAncestorThatIsNotARecordTypeContributesNoFields()
    {
        // These are the accessors the engine registers on everything - asking an
        // object its class or its identifier. Reading them as fields would put a
        // field on every record type that no record stores a value under.
        var schema = Derive(
            new DumpClass("IProbeScriptable", null, [], [Accessor("GetClassName", "CName")]),
            new DumpClass(
                "gamedataProbeThing_Record",
                "IProbeScriptable",
                [],
                [Accessor("Speed", "Float")]));

        Assert.Equal(new[] { "speed" }, schema.Find("gamedataProbeThing_Record")!.Fields.Keys);
    }

    [Fact]
    public void ARecordTypeInheritsFromAnotherRecordType()
    {
        var schema = Derive(
            Record("gamedataProbeBase_Record", Accessor("Shared", "Float")),
            new DumpClass(
                "gamedataProbeThing_Record",
                "gamedataProbeBase_Record",
                [],
                [Accessor("Own", "Bool")]));

        Assert.Equal(
            new[] { "own", "shared" },
            schema.Find("gamedataProbeThing_Record")!.Fields.Keys.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void AKeyTheDumpReaderCouldNotReadReachesTheSchemaAsAStatedFailure()
    {
        using var dump = SyntheticDump.Of(classes:
        [
            """{"name":"gamedataProbeThing_Record","flags":66,"somethingNewer":17}""",
        ]);

        var schema = RecordSchemaDerivation.Derive(
            new DumpRecordTypeSource(DumpTypeModel.Load(dump.JsonDirectory, Description)));

        Assert.Contains(
            schema.Failures,
            failure => failure.TypeName == "class.somethingNewer");
    }

    private static IReadOnlyDictionary<string, RecordField> FieldsOf(DumpClass type) =>
        Derive(type).Find(type.Name)!.Fields;

    private static RecordSchema Derive(params DumpClass[] classes) =>
        RecordSchemaDerivation.Derive(new DumpRecordTypeSource(ModelOf(classes)));

    private static DumpTypeModel ModelOf(params DumpClass[] classes)
    {
        using var dump = SyntheticDump.Of(classes: classes.Select(Document));
        return DumpTypeModel.Load(dump.JsonDirectory, Description);
    }

    private static string Document(DumpClass type)
    {
        var parent = type.ParentName is null ? string.Empty : $"\"parent\":{Quoted(type.ParentName)},";
        var functions = string.Join(",", type.Functions.Select(Document));
        return $"{{{parent}\"name\":{Quoted(type.Name)},\"flags\":66,\"funcs\":[{functions}]}}";
    }

    private static string Document(DumpFunction function)
    {
        var returns = function.ReturnTypeName is null
            ? string.Empty
            : $",\"return\":{{\"type\":{Quoted(function.ReturnTypeName)},\"flags\":64}}";

        var parameters = function.Parameters.Count == 0
            ? string.Empty
            : ",\"params\":[" + string.Join(",", function.Parameters.Select(parameter =>
                $"{{\"type\":{Quoted(parameter.TypeName)},\"name\":{Quoted(parameter.Name)},"
                + $"\"flags\":{(parameter.IsOutput ? 640 : 128)}}}")) + "]";

        return $"{{\"fullName\":{Quoted(function.Name)},\"shortName\":{Quoted(function.Name)},"
            + $"\"flags\":1{returns}{parameters}}}";
    }

    private static string Quoted(string value) => "\"" + value + "\"";

    private static DumpClass Record(string name, params DumpFunction[] functions) =>
        new(name, null, [], functions);

    private static DumpFunction Accessor(string name, string returns) => new(name, returns, []);

    private static DumpFunction OutputAccessor(string name, string writes) =>
        new(name, null, [new DumpParameter("outList", writes, true)]);
}
