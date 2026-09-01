using System.Text;
using Ripperdoc.Core.Diagnosis;
using Ripperdoc.Core.Reporting;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// Attribution by the log's own contents, over logs this project wrote.
/// </summary>
/// <remarks>
/// The rotation is reproduced as a shape rather than replayed from a real
/// corpus: what makes a rotated log wrong is that its name carries one boot and
/// its body another, and neither the framework nor the mods it names are needed
/// to build that.
/// </remarks>
public sealed class LogAttributionTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ripperdoc-log-tests-" + Guid.NewGuid().ToString("N"));

    public LogAttributionTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a check over.
        }
    }

    /// <summary>
    /// Every grammar the engine declares reads the line it says it reads, and
    /// is the grammar credited for it.
    /// </summary>
    /// <remarks>
    /// Run through the whole entry point rather than by asking each grammar
    /// directly. A grammar asked whether it reads its own witness is being
    /// compared with itself; what matters is that it wins the witness under the
    /// same lowest-offset rule a real log goes through.
    /// </remarks>
    [Fact]
    public void EveryDeclaredGrammarReadsItsOwnWitness()
    {
        Assert.Empty(GrammarsFailingTheirWitness<LogTimestampGrammar>());
        Assert.Equal(3, DeclaredKinds.Of<LogTimestampGrammar>().Count);
    }

    /// <summary>
    /// Every grammar that built itself is one the reflected reading reaches.
    /// </summary>
    [Fact]
    public void EveryGrammarConstructedIsOneTheReflectedReadingReaches()
    {
        // The two readings of the same declarations, compared by identity. A
        // grammar written in a shape reflection does not reach sits in one
        // reading and not the other.
        var reflected = DeclaredKinds.Of<LogTimestampGrammar>().Select(member => member.Kind);
        var constructed = DeclaredKinds.Constructed<LogTimestampGrammar>();

        Assert.Equal(constructed.Count, DeclaredKinds.Of<LogTimestampGrammar>().Count);
        foreach (var grammar in constructed)
        {
            Assert.Contains(grammar, reflected, ReferenceEqualityComparer.Instance);
        }
    }

    /// <summary>
    /// The completeness check names a grammar that cannot read its own witness,
    /// and does not name its neighbour.
    /// </summary>
    /// <remarks>
    /// The permanent known-RED. A check that reddened every member would red
    /// for a broken harness as readily as for the defect, so the probe carries
    /// a working member beside the broken one. The declared count is asserted
    /// as well: a derivation coming back short would otherwise leave this green
    /// by finding nothing wrong with members it never read.
    /// </remarks>
    [Fact]
    public void TheCompletenessCheckTellsABrokenGrammarFromAWorkingOne()
    {
        Assert.Equal(2, DeclaredKinds.Of<GrammarProbe>().Count);
        Assert.Equal([nameof(GrammarProbe.Unreadable)], GrammarsFailingTheirWitness<GrammarProbe>());
    }

    /// <summary>
    /// A log named after one boot and filled with another is placed by its
    /// contents.
    /// </summary>
    /// <remarks>
    /// The measured trap: one framework rotates its current log and names the
    /// rotation after the boot that displaced it. A reader keying on the name
    /// would put this file eight hours later than the run it records.
    /// </remarks>
    [Fact]
    public void TheFileNameNeverDecidesTheInstant()
    {
        var log = LogAttribution.Read(
            "compiler_r2026-01-02_11-00-00.log",
            "[INFO - Thu, 02 Jan 2026 03:04:05 +0100] Compiling files in <scripts>:\n");

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), log.Instant);
        Assert.Equal(nameof(LogTimestampGrammar.LongForm), log.Grammar);
        Assert.True(log.IsAttributed);
    }

    /// <summary>
    /// Two logs of identical size and different content are placed at different
    /// boots.
    /// </summary>
    /// <remarks>
    /// Size is not identity, and in the measured corpus a rotation and the
    /// current log were the same byte length. A reader treating equal sizes as
    /// the same log would collapse two boots into one here.
    /// </remarks>
    [Fact]
    public void TwoLogsOfEqualSizeAreStillTwoBoots()
    {
        var rotated = Write(
            "compiler_r2026-01-02_11-00-00.log",
            "[INFO - Thu, 02 Jan 2026 03:04:05 +0100] Compiling files in <scripts>:\n");
        var current = Write(
            "compiler_rCURRENT.log",
            "[INFO - Thu, 02 Jan 2026 11:00:00 +0100] Compiling files in <scripts>:\n");

        Assert.Equal(new FileInfo(rotated).Length, new FileInfo(current).Length);
        Assert.NotEqual(LogAttribution.Of(rotated).Instant, LogAttribution.Of(current).Instant);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), LogAttribution.Of(rotated).Instant);
    }

    /// <summary>
    /// A log opening with a column header is still attributed.
    /// </summary>
    /// <remarks>
    /// This file is the reason the rule cannot be "fall back to the file name
    /// when the first line yields nothing": its name would attribute it
    /// correctly, and the same fallback is catastrophic on a rotation.
    /// </remarks>
    [Fact]
    public void AHeaderRowDoesNotStopAttribution()
    {
        var log = LogAttribution.Read(
            "tabular.log",
            "timestamp\tlevel\tevent\n2026-01-02T03:04:05.678\tINFO\tstarted\n");

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), log.Instant);
        Assert.Equal(nameof(LogTimestampGrammar.LineLeading), log.Grammar);
    }

    /// <summary>
    /// A byte-order mark ahead of the first line does not cost the log its
    /// stamp.
    /// </summary>
    /// <remarks>
    /// A mark left in the decoded text sits at offset zero, ahead of a stamp on
    /// the opening line, and a grammar anchored to the start of a line cannot
    /// match past it. The log would be unattributable on account of three bytes
    /// carrying no timestamp. The same content without the mark is read here
    /// too, so the row that passes is not passing for some other reason.
    /// </remarks>
    [Fact]
    public void AByteOrderMarkDoesNotHideTheFirstStamp()
    {
        const string Line = "2026-01-02T03:04:05.678\tINFO\tstarted\n";

        var marked = Path.Combine(_directory, "marked.log");
        var plain = Path.Combine(_directory, "plain.log");

        File.WriteAllBytes(marked, [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(Line)]);
        File.WriteAllBytes(plain, Encoding.UTF8.GetBytes(Line));

        var expected = new DateTime(2026, 1, 2, 3, 4, 5);

        Assert.Equal(expected, LogAttribution.Of(marked).Instant);
        Assert.Equal(nameof(LogTimestampGrammar.LineLeading), LogAttribution.Of(marked).Grammar);
        Assert.Equal(expected, LogAttribution.Of(plain).Instant);
    }

    /// <summary>
    /// Where two grammars both match, the one that matched earliest is taken.
    /// </summary>
    /// <remarks>
    /// Otherwise the answer depends on the order the set happens to be declared
    /// in, which nothing about a log makes meaningful.
    /// </remarks>
    [Fact]
    public void TheEarliestStampInTheHeadWins()
    {
        var log = LogAttribution.Read(
            "mixed.log",
            "[2026-01-02 03:04:05.678] started\n2026-06-07T08:09:10.111\tINFO\tlater\n");

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), log.Instant);
        Assert.Equal(nameof(LogTimestampGrammar.Bracketed), log.Grammar);
    }

    /// <summary>
    /// A log no declared grammar reads is reported unattributed, with nothing
    /// credited.
    /// </summary>
    [Fact]
    public void ALogNoGrammarReadsIsReportedUnattributed()
    {
        var log = LogAttribution.Read("opaque.log", "started\nfinished\n");

        Assert.Null(log.Instant);
        Assert.Null(log.Grammar);
        Assert.False(log.IsAttributed);
        Assert.Equal("opaque.log", log.FileName);
    }

    /// <summary>
    /// A stamp lying past the opening that is read is not found.
    /// </summary>
    /// <remarks>
    /// The bound is what keeps attribution from scaling with a corpus that runs
    /// to megabytes a boot, and the outcome it produces is a file reported
    /// unattributed rather than one placed at the wrong instant.
    /// </remarks>
    [Fact]
    public void AStampPastTheOpeningIsNotRead()
    {
        var path = Write(
            "late.log",
            new string('.', LogAttribution.HeadBytes)
            + "\n[2026-01-02 03:04:05.678] started\n");

        Assert.False(LogAttribution.Of(path).IsAttributed);
    }

    /// <summary>
    /// A stamp whose fields are out of range is refused rather than thrown out
    /// of the read.
    /// </summary>
    [Theory]
    [InlineData("[2026-13-02 03:04:05.678] month")]
    [InlineData("[2026-01-32 03:04:05.678] day")]
    [InlineData("[2026-02-30 03:04:05.678] february")]
    [InlineData("[2026-01-02 25:04:05.678] hour")]
    [InlineData("[2026-01-02 03:64:05.678] minute")]
    [InlineData("[2026-01-02 03:04:65.678] second")]
    [InlineData("[0000-01-02 03:04:05.678] year")]
    [InlineData("[INFO - Thu, 02 Jan 0000 03:04:05 +0100] year, long form")]
    [InlineData("[INFO - Thu, 02 Xxx 2026 03:04:05 +0100] month name")]
    public void AStampOutOfRangeIsRefused(string head)
    {
        Assert.False(LogAttribution.Read("malformed.log", head).IsAttributed);
    }

    /// <summary>
    /// A stamp written in decimal digits outside the ASCII ten is refused
    /// rather than thrown out of the read.
    /// </summary>
    /// <remarks>
    /// The patterns match every Unicode decimal and only the ASCII ten parse,
    /// so such a stamp reaches the field read. What the entry points document
    /// for a head they cannot place is an unattributed log, and an exception
    /// escaping the read is not that.
    /// </remarks>
    [Theory]
    [InlineData("[٢٠٢٦-٠١-٠٢ ٠٣:٠٤:٠٥.678] a plugin is starting...")]
    [InlineData("[２０２６-０１-０２ ０３:０４:０５.678] a plugin is starting...")]
    [InlineData("[INFO - Thu, ٠٢ Jan ٢٠٢٦ ٠٣:٠٤:٠٥ +0100] Compiling files in <scripts>:")]
    [InlineData("٢٠٢٦-٠١-٠٢T٠٣:٠٤:٠٥.678\tINFO\tstarted\n")]
    public void AStampInDigitsThisReaderCannotParseIsRefused(string head)
    {
        Assert.False(LogAttribution.Read("foreign-digits.log", head).IsAttributed);
    }

    /// <summary>
    /// A valid stamp on either side of a refused one is still read.
    /// </summary>
    /// <remarks>
    /// Beside the row above, so the refusal is seen to reject the malformed
    /// stamp rather than the whole file: a reader that gave up on the first bad
    /// match would pass every row of that theory while losing real logs.
    /// </remarks>
    [Fact]
    public void ARefusedStampDoesNotHideALaterValidOne()
    {
        var log = LogAttribution.Read(
            "recovering.log",
            "2026-13-02T03:04:05.678\tINFO\tbad\n2026-01-02T06:07:08.999\tINFO\tgood\n");

        Assert.Equal(new DateTime(2026, 1, 2, 6, 7, 8), log.Instant);
    }

    /// <summary>Neither entry point takes a null.</summary>
    [Fact]
    public void TheReaderRefusesAMissingArgument()
    {
        Assert.Throws<ArgumentNullException>(() => LogAttribution.Of(null!));
        Assert.Throws<ArgumentNullException>(() => LogAttribution.Read(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => LogAttribution.Read("x", null!));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static IReadOnlyList<string> GrammarsFailingTheirWitness<TGrammar>()
        where TGrammar : class, IWitnessedGrammar =>
        [.. DeclaredKinds.Of<TGrammar>()
            .Where(member =>
                LogAttribution.ReadWith<TGrammar>("witness.log", member.Kind.Witness).Grammar
                != member.Name)
            .Select(member => member.Name)];
}

/// <summary>
/// A grammar set carrying one member that cannot read its own witness, kept
/// permanently.
/// </summary>
/// <remarks>
/// The known-RED the completeness check is trusted on. <see cref="Unreadable" />
/// declares a witness its own pattern does not match - a grammar that would
/// silently never fire on a real log. <see cref="Readable" /> stands beside it
/// so the check is seen to tell the two apart.
/// </remarks>
internal sealed class GrammarProbe : IWitnessedGrammar
{
    /// <summary>A member whose pattern reads the line it declares.</summary>
    public static readonly GrammarProbe Readable = new("started at 2026", "log: started at 2026");

    /// <summary>A member whose pattern does not read the line it declares.</summary>
    public static readonly GrammarProbe Unreadable = new("finished at 2026", "log: started at 2026");

    private readonly string _needle;

    private GrammarProbe(string needle, string witness)
    {
        _needle = needle;
        Witness = witness;
        DeclaredKinds.Register(this);
    }

    /// <inheritdoc />
    public string Witness { get; }

    /// <inheritdoc />
    public (int Offset, DateTime Instant)? FirstIn(string head)
    {
        var at = head.IndexOf(_needle, StringComparison.Ordinal);
        return at < 0 ? null : (at, new DateTime(2026, 1, 1));
    }
}
