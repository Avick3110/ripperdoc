using Ripperdoc.Core.Diagnosis;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Compile-failure attribution, over logs and records this project wrote.
/// </summary>
/// <remarks>
/// The error text is the compiler's shape and the mods are invented. What the
/// attribution turns on is the path an error names and the record entry that
/// claims it, so nothing real is needed to reproduce a row.
/// </remarks>
public sealed class CompileFailureReadingTests
{
    private const string Game = "C:/game";

    /// <summary>
    /// An error is joined to the mod the record says supplied the file it names.
    /// </summary>
    [Fact]
    public void AnErrorIsAttributedToTheModTheRecordClaimsItsSourceFrom()
    {
        var reading = Read(
            Error("UNRESOLVED_METHOD", "C:/game/r6/scripts/alpha/a.reds", 80, 1),
            Record(("alpha-1-0", "r6/scripts/alpha/a.reds")));

        var suspect = Assert.Single(reading.Suspects);
        Assert.Equal("alpha-1-0", suspect.ModId);
        var error = Assert.Single(suspect.Errors);
        Assert.Equal("UNRESOLVED_METHOD", error.Code);
        Assert.Equal(80, error.Line);
        Assert.Equal(1, error.Column);
        Assert.Empty(reading.SourcesOutsideTheGameDirectory);
        Assert.Empty(reading.SourcesTheRecordDoesNotClaim);
    }

    /// <summary>
    /// Several mods are each named with only the errors in their own sources.
    /// </summary>
    [Fact]
    public void EachSuspectCarriesOnlyItsOwnErrors()
    {
        var reading = Read(
            Error("UNRESOLVED_TYPE", "C:/game/r6/scripts/alpha/a.reds", 1, 1)
            + Error("UNRESOLVED_TYPE", "C:/game/r6/scripts/alpha/b.reds", 2, 1)
            + Error("UNRESOLVED_IMPORT", "C:/game/r6/scripts/beta/c.reds", 3, 1),
            Record(
                ("alpha-1-0", "r6/scripts/alpha/a.reds"),
                ("alpha-1-0", "r6/scripts/alpha/b.reds"),
                ("beta-2-0", "r6/scripts/beta/c.reds")));

        Assert.Equal(["alpha-1-0", "beta-2-0"], reading.Suspects.Select(suspect => suspect.ModId));
        Assert.Equal(2, reading.Suspects[0].Errors.Count);
        Assert.Equal("UNRESOLVED_IMPORT", Assert.Single(reading.Suspects[1].Errors).Code);
        Assert.Equal(3, reading.Errors.Count);
    }

    /// <summary>
    /// A source the record does not claim is reported by name rather than
    /// dropped, and implicates nobody.
    /// </summary>
    /// <remarks>
    /// A dropped source is an error nothing accounts for, which reads as a
    /// cleaner failure than there was.
    /// </remarks>
    [Fact]
    public void ASourceTheRecordDoesNotClaimIsReportedRatherThanDropped()
    {
        var reading = Read(
            Error("UNRESOLVED_TYPE", "C:/game/r6/scripts/orphan/x.reds", 1, 1),
            Record(("alpha-1-0", "r6/scripts/alpha/a.reds")));

        Assert.Empty(reading.Suspects);
        Assert.Equal(["C:/game/r6/scripts/orphan/x.reds"], reading.SourcesTheRecordDoesNotClaim);
        Assert.Empty(reading.SourcesOutsideTheGameDirectory);
    }

    /// <summary>
    /// A source outside the game directory is its own report, not folded in
    /// with the unclaimed ones.
    /// </summary>
    /// <remarks>
    /// A source the record could never have claimed is a different fact from one
    /// it should have claimed and did not. Folding them together would hide
    /// whichever is rarer, and on a healthy install that is the second.
    /// </remarks>
    [Fact]
    public void ASourceOutsideTheGameDirectoryIsItsOwnReport()
    {
        var reading = Read(
            Error("UNRESOLVED_TYPE", "D:/elsewhere/x.reds", 1, 1),
            Record(("alpha-1-0", "r6/scripts/alpha/a.reds")));

        Assert.Equal(["D:/elsewhere/x.reds"], reading.SourcesOutsideTheGameDirectory);
        Assert.Empty(reading.SourcesTheRecordDoesNotClaim);
    }

    /// <summary>
    /// An error line carrying no source is counted rather than passed over in
    /// silence.
    /// </summary>
    /// <remarks>
    /// The compiler ends a failed run with a summary line that is an error line
    /// with no source, so a non-zero count is ordinary. What it must not be is
    /// invisible: with no count a reader cannot tell that line from a shape this
    /// engine has never seen.
    /// </remarks>
    [Fact]
    public void AnErrorLineWithNoSourceIsCounted()
    {
        var reading = Read(
            Error("UNRESOLVED_TYPE", "C:/game/r6/scripts/alpha/a.reds", 1, 1)
            + "[ERROR - Thu, 02 Jan 2026 03:04:06 +0100] REDScript compilation has failed.\n",
            Record(("alpha-1-0", "r6/scripts/alpha/a.reds")));

        Assert.Single(reading.Errors);
        Assert.Equal(1, reading.ErrorLinesNotRead);
    }

    /// <summary>
    /// The reading is placed at the boot its log's contents carry, not the one
    /// its name does.
    /// </summary>
    [Fact]
    public void TheReadingIsPlacedByTheLogsContents()
    {
        var text = Error("UNRESOLVED_TYPE", "C:/game/r6/scripts/alpha/a.reds", 1, 1);
        var reading = CompileFailureReading.Read(
            LogAttribution.Read("compiler_r2026-06-07_08-09-10.log", text),
            text,
            Record(("alpha-1-0", "r6/scripts/alpha/a.reds")),
            Game);

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), reading.Log.Instant);
    }

    /// <summary>
    /// A log writing separators one way and a record writing them the other
    /// still join.
    /// </summary>
    /// <remarks>
    /// The compiler prints the platform's separators and the record stores its
    /// own; a join comparing them literally attributes nothing at all, which
    /// would read as a compile failure implicating no mod.
    /// </remarks>
    [Fact]
    public void SeparatorsAndCasingDoNotBreakTheJoin()
    {
        var reading = Read(
            Error("UNRESOLVED_TYPE", @"C:\game\r6\scripts\Alpha\A.reds", 1, 1),
            new DeploymentRecord("hardlink_activator", "a-game", Game,
                [new DeployedFile("r6/scripts/alpha/a.reds", "alpha-1-0")]));

        Assert.Equal("alpha-1-0", Assert.Single(reading.Suspects).ModId);
    }

    /// <summary>No entry point takes a null.</summary>
    [Fact]
    public void TheReaderRefusesAMissingArgument()
    {
        var record = Record(("alpha-1-0", "r6/scripts/alpha/a.reds"));
        var log = new AttributedLog("a.log", null, null);

        Assert.Throws<ArgumentNullException>(() => CompileFailureReading.Of(null!, record, Game));
        Assert.Throws<ArgumentNullException>(() => CompileFailureReading.Read(null!, "x", record, Game));
        Assert.Throws<ArgumentNullException>(() => CompileFailureReading.Read(log, null!, record, Game));
        Assert.Throws<ArgumentNullException>(() => CompileFailureReading.Read(log, "x", null!, Game));
        Assert.Throws<ArgumentNullException>(() => CompileFailureReading.Read(log, "x", record, null!));
    }

    private static CompileFailureReading Read(string text, DeploymentRecord record) =>
        CompileFailureReading.Read(LogAttribution.Read("compiler_rCURRENT.log", text), text, record, Game);

    private static string Error(string code, string path, int line, int column) =>
        $"[ERROR - Thu, 02 Jan 2026 03:04:05 +0100] [{code}] At {path}:{line}:{column}:\n"
        + "@wrapMethod(SomeType)\n^^^^^^^^^^^\nno method with this name exists on the target type\n\n";

    private static DeploymentRecord Record(params (string Mod, string Path)[] files) =>
        new("hardlink_activator", "a-game", Game,
            [.. files.Select(file => new DeployedFile(file.Path, file.Mod))]);
}
