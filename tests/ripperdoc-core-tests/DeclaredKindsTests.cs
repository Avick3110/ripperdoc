using Ripperdoc.Core.Reporting;
using Ripperdoc.Core.Script;
using Xunit;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// The derivation that every completeness check in this project rests on.
/// </summary>
/// <remarks>
/// A guard over a derived set fails toward green when the derivation breaks:
/// nothing found means nothing missing. So the derivation is checked before
/// anything is built on it, and it is checked in the direction that matters -
/// that it can come back short, and says so, rather than that it comes back
/// right on a set that happens to be correct.
/// </remarks>
public sealed class DeclaredKindsTests
{
    [Fact]
    public void EveryMemberDeclaredOnTheTypeIsRead()
    {
        var declared = DeclaredKinds.Of<ThreeMemberSet>();

        Assert.Equal(
            new[] { "Beta", "Delta", "Gamma" },
            declared.Select(member => member.Name).ToList());
    }

    [Fact]
    public void TheNameComesFromTheDeclarationRatherThanFromTheMember()
    {
        var declared = DeclaredKinds.Of<ThreeMemberSet>();

        // The member carries a label that disagrees with the field it is
        // declared under. A name taken from the member would report the label;
        // a name taken from the declaration cannot be made to disagree with
        // where the member actually lives.
        var gamma = declared.Single(member => member.Name == "Gamma");
        Assert.Equal("this label is not the field name", gamma.Kind.Label);
    }

    [Fact]
    public void AMemberReflectionCannotReachIsVisibleInTheOtherReading()
    {
        var reflected = DeclaredKinds.Of<SetWithAMemberBehindAProperty>()
            .Select(member => member.Kind)
            .ToList();
        var constructed = DeclaredKinds.Constructed<SetWithAMemberBehindAProperty>();

        // Compared by identity. Two readings of equal length would pass a count
        // comparison while holding different members, and the difference this
        // exists to catch is a member present in one reading only.
        Assert.Equal(2, constructed.Count);
        Assert.Single(reflected);
        Assert.DoesNotContain(
            SetWithAMemberBehindAProperty.BehindAProperty,
            reflected,
            ReferenceEqualityComparer.Instance);
        Assert.Contains(
            SetWithAMemberBehindAProperty.BehindAProperty,
            constructed,
            ReferenceEqualityComparer.Instance);
    }

    [Fact]
    public void ATypeDeclaringNoMemberIsRefusedRatherThanReadAsComplete()
    {
        // The known-RED at the root of every guard built on this: a derivation
        // that finds nothing must not be able to report that nothing is
        // missing. Permanent, and it is the cell that reds first if reflection
        // over static fields stops reaching them at all.
        var refusal = Assert.Throws<InvalidOperationException>(
            () => DeclaredKinds.Of<EmptySet>());

        Assert.Contains("broken derivation", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKindSetThisProjectDeclaresIsSealed()
    {
        // The remark on DeclaredKinds says sealedness is what keeps a member
        // declared on a derived type from sitting outside both readings, where
        // the identity comparison stays green over an incomplete set. That was
        // prose. This is the check.
        //
        // The population is named rather than derived, and is exactly as wide
        // as the names in it: which types are passed to Of is not something the
        // type system can be asked. A kind set added later and left out of this
        // list is not covered, and that is a property of this check rather than
        // a claim it is making about the project.
        foreach (var set in new[] { typeof(ScriptResolutionLimit), typeof(ScriptTextSpan) })
        {
            Assert.True(
                set.IsSealed,
                set.Name + " is read as a kind set and is not sealed, so a member declared on a "
                + "type derived from it would be absent from both readings while they went on "
                + "agreeing.");
        }
    }

    [Fact]
    public void AFieldOfAnotherTypeIsNotReadAsAMember()
    {
        var declared = DeclaredKinds.Of<ThreeMemberSet>();

        Assert.DoesNotContain("NotAMember", declared.Select(member => member.Name));
    }

    private sealed class ThreeMemberSet
    {
        public static readonly ThreeMemberSet Beta = new("Beta");
        public static readonly ThreeMemberSet Gamma = new("this label is not the field name");
        public static readonly ThreeMemberSet Delta = new("Delta");

        public static readonly string NotAMember = "a field of another type";

        private ThreeMemberSet(string label)
        {
            Label = label;
            DeclaredKinds.Register(this);
        }

        public string Label { get; }
    }

    private sealed class SetWithAMemberBehindAProperty
    {
        public static readonly SetWithAMemberBehindAProperty AsAField = new();

        private static readonly SetWithAMemberBehindAProperty Hidden = new();

        private SetWithAMemberBehindAProperty() => DeclaredKinds.Register(this);

        public static SetWithAMemberBehindAProperty BehindAProperty => Hidden;
    }

    private sealed class EmptySet
    {
        private EmptySet()
        {
        }
    }
}
