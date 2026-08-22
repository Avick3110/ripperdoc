using System.Globalization;
using Ripperdoc.Core;
using Ripperdoc.Core.Schema;
using Ripperdoc.Core.Tweak;
using Xunit;
using Xunit.Abstractions;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The slice, end to end, over a real installed tweak layer.
/// </summary>
/// <remarks>
/// <para>
/// Tier (ii): this reads a real install's tweak directory, which no runner has
/// and which is other people's mod content that this project does not carry.
/// The gate runs it when the environment names a layer and announces it as
/// skipped, by name, when nothing does. Run outside the gate with no layer
/// named, it fails rather than passing quietly.
/// </para>
/// <para>
/// What is asserted here is what holds of any layer, not what holds of one
/// install. A real layer changes whenever its owner installs a mod, so counts
/// taken from one would turn an ordinary install into a red run - the numbers
/// are reported instead, and the invariants are what fails.
/// </para>
/// </remarks>
[Trait(TierTrait.Name, TierTrait.InstalledTweakLayer)]
public class InstalledTweakLayerTests
{
    private readonly ITestOutputHelper _output;

    public InstalledTweakLayerTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The environment variable naming the layer, derived from the brand rather
    /// than spelled out, so a rebrand does not leave a stale name here.
    /// </summary>
    public static string VariableName => Branding.Name.ToUpperInvariant() + "_TWEAKS_PATH";

    /// <summary>
    /// The environment variable naming the framework's own metadata file, which
    /// declares the properties it adds to record types beyond the ones the type
    /// model has.
    /// </summary>
    public static string ExtraFlatsVariableName => Branding.Name.ToUpperInvariant() + "_EXTRA_FLATS_PATH";

    /// <summary>
    /// The environment variable naming the framework's inheritance metadata,
    /// which records which shipped records were copied from which.
    /// </summary>
    public static string InheritanceVariableName => Branding.Name.ToUpperInvariant() + "_INHERITANCE_PATH";

    /// <summary>
    /// The environment variable naming the shipped database, whose values decide
    /// whether a copy still follows what it was copied from.
    /// </summary>
    public static string DatabaseVariableName => Branding.Name.ToUpperInvariant() + "_TWEAKDB_PATH";

    private static string LayerPath => Required(VariableName);

    private static string Required(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);

        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidOperationException(
                $"These checks read a real install's tweak lane, which no runner has. Set {variable} to run "
                + "them. The gate script announces them as skipped, by name, when it cannot run them - an "
                + "absent input is never reported as a pass.")
            : path;
    }

    [Fact]
    public void EveryFileInTheLayerIsEitherReplayedOrNamedAsSomethingThatWasNot()
    {
        var layer = TweakLayer.Enumerate(LayerPath);
        var state = Replay(layer);

        _output.WriteLine(Report(state));

        // The accounting has to close. A file that is neither replayed nor named
        // is a file whose writes could be deciding a value this state reports on,
        // with nothing saying so.
        var replayed = layer.Files
            .Where(file => file.Format == TweakFileFormat.Yaml)
            .Select(file => file.RelativePath);
        var named = state.Unhandled.Select(unhandled => unhandled.Path)
            .Concat(layer.Unread.Select(file => file.RelativePath));

        Assert.Empty(layer.Present
            .Select(file => file.RelativePath)
            .Except(replayed.Concat(named), StringComparer.Ordinal));
    }

    [Fact]
    public void TheReadOrderRunsFirstGroupThenTheRestThenLastGroupWithoutGaps()
    {
        var layer = TweakLayer.Enumerate(LayerPath);

        Assert.Equal(
            Enumerable.Range(1, layer.Files.Count),
            layer.Files.Select(file => file.ReadPosition));

        var groups = layer.Files.Select(file => (int)file.Group).ToArray();
        Assert.Equal(groups.OrderBy(group => group), groups);
    }

    [Fact]
    public void EveryContestTheToolFindsIsExplainedRatherThanMerelyCounted()
    {
        var layer = TweakLayer.Enumerate(LayerPath);
        var state = Replay(layer);

        _output.WriteLine($"inheritance route: {state.InheritanceDescription}.");
        Assert.True(state.InheritanceWasReplayed);
        _output.WriteLine(
            $"{state.Collisions.Count} contested value(s) in this layer.");

        foreach (var collision in state.Collisions)
        {
            _output.WriteLine(collision.Explain());

            Assert.NotEmpty(collision.Overridden);
            Assert.NotEqual(0UL, collision.Identifier);
            Assert.Equal(TweakIdentifier.Of(collision.FlatName), collision.Identifier);

            // The three the exit criterion asks for: the value, whose it is, and
            // why. A contest missing any one of them is a count wearing a
            // sentence.
            var explanation = collision.Explain();
            Assert.Contains(collision.FlatName, explanation, StringComparison.Ordinal);
            Assert.Contains(collision.Winner.File.RelativePath, explanation, StringComparison.Ordinal);
            Assert.Contains(TweakCollision.Describe(collision.Rule), explanation, StringComparison.Ordinal);

            foreach (var overridden in collision.Overridden)
            {
                Assert.NotEqual(collision.Winner.OriginDirectory, overridden.OriginDirectory);
            }
        }
    }

    [Fact]
    public void EveryValueTheLayerWritesIsAddressableOrIsNamedAsNotBeing()
    {
        var layer = TweakLayer.Enumerate(LayerPath);
        var state = Replay(layer);

        _output.WriteLine(
            $"{state.Flats.Count} value(s) addressable, {state.Unaddressable.Count} not.");

        foreach (var name in state.Unaddressable)
        {
            _output.WriteLine($"  {name.Addressing}: {name.Name}");
        }

        Assert.All(state.Flats, flat => Assert.NotEqual(0UL, flat.Identifier));
        Assert.All(state.Unaddressable, name => Assert.NotEmpty(name.Contributions));
    }

    [Fact]
    public void PropertiesTheSchemaLacksAreCountedAndNamedRatherThanPassedOver()
    {
        var layer = TweakLayer.Enumerate(LayerPath);
        var documents = TweakFileReader.ReadLayer(layer, LayerPath);
        var schema = RecordSchemaDerivation.Derive(
            ReflectedRecordTypeSource.FromPinnedTypeModel().Read(),
            "the pinned type model");

        var extraFlats = TweakExtraFlats.OpenReadOnly(Required(ExtraFlatsVariableName));
        var reading = TweakSchemaReading.Of(documents, schema, extraFlats);

        _output.WriteLine($"resolved against {reading.PropertySourceDescription}.");
        Assert.True(reading.UnknownPropertiesWereCounted);

        _output.WriteLine(
            $"{reading.RecordsWithAResolvedType} record(s) declared with a resolvable type; "
            + $"{reading.RecordsWithNoResolvedType.Count} record(s) written to whose type needs the shipped database.");
        _output.WriteLine(
            $"{reading.PropertiesChecked} property write(s) checked against the schema; "
            + $"{reading.PropertiesTheSchemaLacks.Count} name(s) the schema does not have.");
        _output.WriteLine($"{reading.TypesTheSchemaLacks.Count} declared type(s) the schema does not have.");

        foreach (var unknown in reading.TypesTheSchemaLacks)
        {
            _output.WriteLine($"  unknown type: {unknown.SpelledAs} (as {unknown.Canonical}) on {unknown.RecordName}");
        }

        foreach (var unknown in reading.PropertiesTheSchemaLacks)
        {
            _output.WriteLine(
                $"  unknown property: {unknown.RecordName}.{unknown.PropertyName} on {unknown.TypeName} "
                + $"at {unknown.RelativePath}:{unknown.Line}");
        }

        // The accounting, not the count: every property write is either checked
        // against a type or attributed to a record whose type this run could not
        // resolve. A write that is neither would be one the reading passed over
        // while reporting on the rest.
        var writesOnRecords = documents
            .SelectMany(document => document.Writes)
            .Count(write => write.OwningRecordName is not null);
        var attributed = documents
            .SelectMany(document => document.Writes)
            .Count(write => write.OwningRecordName is { } name
                && reading.RecordsWithNoResolvedType.Contains(name, StringComparer.Ordinal));

        Assert.Equal(writesOnRecords, reading.PropertiesChecked + attributed);
    }

    // The whole tier reads one install's tweak lane, so the inputs are gathered
    // in one place: the layer, the framework's inheritance metadata, and the
    // shipped database whose values decide whether a copy still follows what it
    // was copied from.
    private static TweakResolvedState Replay(TweakLayer layer) => TweakResolvedState.Replay(
        layer,
        TweakFileReader.ReadLayer(layer, LayerPath),
        TweakInheritanceMap.OpenReadOnly(Required(InheritanceVariableName)),
        TweakDatabaseSource.OpenReadOnly(Required(DatabaseVariableName)));

    private static string Report(TweakResolvedState state)
    {
        var layer = state.Layer;
        var lines = new List<string>
        {
            $"{layer.Present.Count} file(s) present, {layer.Files.Count} read by the framework.",
            $"  by format: "
            + string.Join(", ", layer.Present
                .GroupBy(file => file.Format)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key} {group.Count().ToString(CultureInfo.InvariantCulture)}")),
            $"  by group: "
            + string.Join(", ", layer.Files
                .GroupBy(file => file.Group)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key} {group.Count().ToString(CultureInfo.InvariantCulture)}")),
            $"  enumeration already collated: {layer.EnumerationIsCollated}",
            $"{state.Flats.Count} value(s) written, {state.Collisions.Count} contested.",
            $"{state.Unhandled.Count} construct(s) not replayed.",
        };

        lines.AddRange(state.Unhandled
            .Select(unhandled => $"  not replayed: {unhandled.Summary}")
            .Take(20));

        return string.Join(Environment.NewLine, lines);
    }
}
