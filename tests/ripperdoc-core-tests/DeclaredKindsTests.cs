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
    public void AReadingTakenWhileTheDeclarationsAreStillRunningIsRefused()
    {
        // The reading the count check cannot see. Three fields are found and
        // three members counted, so the empty-set refusal passes it through
        // while two of the three hold nothing - and for a set whose members
        // are cached the reading that came back short is the one every later
        // caller gets.
        //
        // The re-entrant read is caught inside the set rather than let out of
        // it, so the state under test stays in this check instead of failing
        // whatever touches the type next.
        _ = ReEntrantSet.Third;

        Assert.Null(ReEntrantSet.MidInitialisationRead);
        var refusal = Assert.IsType<InvalidOperationException>(
            ReEntrantSet.FailureFromTheMidInitialisationRead);

        Assert.Contains(typeof(ReEntrantSet).FullName!, refusal.Message, StringComparison.Ordinal);

        // Which of the two unassigned fields reflection reaches first is not
        // contractual. That the one named is not the field which did hold a
        // member is: a refusal naming First would be reporting a fault other
        // than the one it found.
        Assert.DoesNotContain("'First'", refusal.Message, StringComparison.Ordinal);
        Assert.Single(
            new[] { "'Second'", "'Third'" }
                .Where(name => refusal.Message.Contains(name, StringComparison.Ordinal)));
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

    private sealed class ReEntrantSet
    {
        public static readonly ReEntrantSet First = new(readsItselfBack: false);
        public static readonly ReEntrantSet Second = new(readsItselfBack: true);
        public static readonly ReEntrantSet Third = new(readsItselfBack: false);

        internal static IReadOnlyList<KindMember<ReEntrantSet>>? MidInitialisationRead;
        internal static Exception? FailureFromTheMidInitialisationRead;

        private ReEntrantSet(bool readsItselfBack)
        {
            DeclaredKinds.Register(this);

            if (!readsItselfBack)
            {
                return;
            }

            try
            {
                MidInitialisationRead = DeclaredKinds.Of<ReEntrantSet>();
            }
            catch (Exception failure)
            {
                FailureFromTheMidInitialisationRead = failure;
            }
        }
    }

    private sealed class EmptySet
    {
        private EmptySet()
        {
        }
    }
}
