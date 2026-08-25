using Ripperdoc.Core.Archive;
using Xunit;

namespace Ripperdoc.Core.Tests;

public class ArchiveEntryTests
{
    [Fact]
    public void ANamedEntryReportsItsName()
    {
        var entry = new ArchiveEntry(7593874588658620903, @"base\ripperdoc\alpha.json", 31, 31);

        Assert.True(entry.IsNamed);
        Assert.Equal(@"base\ripperdoc\alpha.json", entry.Display);
    }

    [Fact]
    public void AnEntryWithNoNameReportsItsHashRatherThanNothing()
    {
        var entry = new ArchiveEntry(998489252173267588, Name: null, 512, 300);

        Assert.False(entry.IsNamed);
        Assert.Equal("998489252173267588", entry.Display);
    }

    [Fact]
    public void AnEmptyNameIsNoNameToBothPropertiesRatherThanToOne()
    {
        // Reachable through a constructor this type makes public, and every
        // count downstream is built on IsNamed - so an entry that read as named
        // and printed a blank cell was counted among the named ones while going
        // missing from the report.
        var entry = new ArchiveEntry(42, string.Empty, 1, 1);

        Assert.False(entry.IsNamed);
        Assert.Equal("42", entry.Display);
    }

    [Fact]
    public void AnEntryWithNoNameNeverDisplaysAsEmpty()
    {
        // The property that makes "report by hash, never omit" true where a
        // caller prints a row: there is no input for which the display is blank,
        // and a blank cell is how an entry goes missing from a report that
        // technically contained it.
        var entry = new ArchiveEntry(0, Name: null, 0, 0);

        Assert.False(string.IsNullOrWhiteSpace(entry.Display));
        Assert.Equal("0", entry.Display);
    }
}
