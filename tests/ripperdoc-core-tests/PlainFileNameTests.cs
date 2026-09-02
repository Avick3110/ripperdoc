using System.Reflection;
using Ripperdoc.Core.ManagerState;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The one door a name the state supplied goes through before it is joined to
/// a path, and the check that the joining member has no other one.
/// </summary>
public sealed class PlainFileNameTests
{
    // Empty, absent, carrying a directory part, and holding a character no
    // file name may hold.
    public static TheoryData<string?> NamesThatAreNotOne => new()
    {
        string.Empty,
        null,
        "sub/MANIFEST-000005",
        "MANIFEST-000005\0",
    };

    /// <summary>
    /// A name that is not one plain file name is refused by name, carrying what
    /// named it and what it was for.
    /// </summary>
    [Theory]
    [MemberData(nameof(NamesThatAreNotOne))]
    public void ANameThatIsNotOnePlainFileNameIsRefusedByName(string? named)
    {
        var refusal = Assert.Throws<StateReadException>(
            () => PlainFileName.Named(named, "a fixture", "the manifest in force"));

        Assert.Equal(
            $"a fixture names the manifest in force '{named}', and this reader models the manifest "
            + "in force as one plain file name in the directory it is read under. A name that is "
            + "not one either leaves that directory or is refused when it is opened, by the "
            + "platform rather than by this reader.",
            refusal.Message);
    }

    /// <summary>
    /// One plain file name is named and joins under a directory, so the
    /// refusals above are about the name and not about the door.
    /// </summary>
    [Fact]
    public void OnePlainFileNameIsNamedAndJoinsUnderADirectory()
    {
        var named = PlainFileName.Named("MANIFEST-000005", "a fixture", "the manifest in force");

        Assert.Equal("MANIFEST-000005", named.Name);
        Assert.Equal(
            Path.Combine("a-directory", "MANIFEST-000005"),
            PlainFileName.Under("a-directory", named));
    }

    /// <summary>
    /// A value that never went through the door is refused by name rather than
    /// joining as nothing.
    /// </summary>
    [Fact]
    public void AValueThatWasNeverNamedIsRefusedByName()
    {
        // Spelled with its type rather than left to the target, so that an
        // overload taking a raw string is a compiling change and the check
        // holding the joining member to one signature is what refuses it.
        var refusal = Assert.Throws<InvalidOperationException>(
            () => PlainFileName.Under("a-directory", default(PlainFileName)));

        Assert.Contains(
            "read before it was named", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The joining member takes a name that has been asked about and nothing
    /// else, so a site added later cannot join a raw string past it.
    /// </summary>
    /// <remarks>
    /// An identity check over the member's own parameters rather than a
    /// behavioural one: a behavioural arm per site passes on the day a site
    /// arrives without one, and an overload taking a string segment would be
    /// the way one could.
    /// </remarks>
    [Fact]
    public void TheJoiningMemberTakesNoRawStringSegment()
    {
        var joining = typeof(PlainFileName)
            .GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.Name == nameof(PlainFileName.Under));

        var only = Assert.Single(joining);

        Assert.Equal(
            [typeof(string), typeof(PlainFileName)],
            only.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }
}
