using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ripperdoc.Core.Reporting;

/// <summary>
/// The members of a kind set, read back from the type that declares them.
/// </summary>
/// <remarks>
/// <para>
/// A kind set is a type whose members are its own public static fields of that
/// type. Reading them back rather than listing them elsewhere is what keeps a
/// kind's declaration the only place it has to be written down: everything that
/// consumes the set consults the declarations, so a set and its consumers
/// cannot come apart.
/// </para>
/// <para>
/// Two readings are kept and compared by identity rather than by count.
/// <see cref="Of{TKind}" /> reflects over the declaring type;
/// <see cref="Constructed{TKind}" /> is what the members recorded of themselves
/// as they were built. A member written in a shape reflection does not reach -
/// a property, or a field on a derived type - sits in one reading and not the
/// other, and comparing the two is the only way that difference is visible.
/// </para>
/// <para>
/// An empty reflected reading is refused rather than returned. Every question
/// asked of a kind set is a completeness question, and an empty set answers all
/// of them affirmatively - so a derivation that finds nothing must fail loudly
/// rather than report that nothing is missing. The refusal is
/// <see cref="Of{TKind}" />'s alone; <see cref="Constructed{TKind}" /> reports
/// what registered itself and returns an empty list when nothing did, which is
/// safe only because it is never read except beside a reading that refuses.
/// </para>
/// <para>
/// A reading short by less than all of it is refused on the same ground and
/// needs its own refusal, because the count check cannot see it: a declaration
/// read before its own initialiser has run holds no member, and a set read
/// back that way comes back the right length with holes in it. Nothing
/// re-enters a kind set here today, and the refusal is what keeps that from
/// being the reason the derivation is trusted.
/// </para>
/// <para>
/// One shape sits outside both readings rather than in one of them: a member
/// declared on a type derived from the kind set. Reflection is asked for the
/// set's own declarations, and registration keys on the runtime type, so such a
/// member is absent from each and the comparison stays green. Every kind set
/// here is sealed, which is what prevents it, and that is the reason they are
/// sealed rather than a coincidence.
/// </para>
/// <para>
/// Reflection over static fields is lost to trimming, which is the hazard
/// <c>BUILD_PLAN_v2</c> §6 rule 6 already names for the schema layer and
/// answers the same way: publish with trimming off.
/// </para>
/// </remarks>
internal static class DeclaredKinds
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, List<object>> Built = [];

    /// <summary>
    /// Records a member as it is constructed, for the second reading.
    /// </summary>
    /// <param name="kind">The member being built.</param>
    internal static void Register(object kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        lock (Gate)
        {
            if (!Built.TryGetValue(kind.GetType(), out var members))
            {
                members = [];
                Built[kind.GetType()] = members;
            }

            members.Add(kind);
        }
    }

    /// <summary>
    /// Every member <typeparamref name="TKind" /> declares, by name.
    /// </summary>
    /// <typeparam name="TKind">The kind set.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// The type declares no member of its own type, or one of its declarations
    /// read back holding no member of the set.
    /// </exception>
    /// <remarks>
    /// Ordered by name rather than by declaration, because a result carrying
    /// this order has to be reproducible and the order in which reflection
    /// returns fields is not part of any contract. Nothing reads meaning from
    /// the sequence.
    /// </remarks>
    internal static IReadOnlyList<KindMember<TKind>> Of<TKind>()
        where TKind : class
    {
        Initialise<TKind>();

        var declared = typeof(TKind)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType == typeof(TKind))
            .Select(field => field.GetValue(null) is TKind value
                ? new KindMember<TKind>(field.Name, value)
                : throw new InvalidOperationException(
                    $"'{field.Name}' on {typeof(TKind).FullName} read back as no member of the "
                    + "set. A reading taken while the declarations are still being initialised "
                    + "comes back short with its count intact, which is the broken derivation "
                    + "the empty-set refusal below exists to name."))
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .ToList();

        if (declared.Count == 0)
        {
            throw new InvalidOperationException(
                $"No member was found declared on {typeof(TKind).FullName}. Nothing is reported from an "
                + "empty kind set: every check over one asks whether some member is unaccounted for, and "
                + "an empty set answers no to all of them - so a reading that finds nothing is a broken "
                + "derivation reporting itself as a complete one.");
        }

        return declared;
    }

    /// <summary>
    /// Every member of <typeparamref name="TKind" /> that recorded itself as it
    /// was constructed.
    /// </summary>
    /// <typeparam name="TKind">The kind set.</typeparam>
    internal static IReadOnlyList<TKind> Constructed<TKind>()
        where TKind : class
    {
        Initialise<TKind>();

        lock (Gate)
        {
            return Built.TryGetValue(typeof(TKind), out var members)
                ? members.Cast<TKind>().ToList()
                : [];
        }
    }

    /// <summary>
    /// Runs <typeparamref name="TKind" />'s static field initialisers.
    /// </summary>
    /// <remarks>
    /// Both readings have to be taken against the same state, and the
    /// constructed one exists only once the initialisers have run. Reflecting
    /// over the fields does not itself trigger them.
    /// </remarks>
    private static void Initialise<TKind>() =>
        RuntimeHelpers.RunClassConstructor(typeof(TKind).TypeHandle);
}

/// <summary>
/// One member of a kind set, with the name it is declared under.
/// </summary>
/// <typeparam name="TKind">The kind set.</typeparam>
/// <param name="Name">The field's own name.</param>
/// <param name="Kind">The member.</param>
/// <remarks>
/// The name is taken from the declaration rather than carried on the member,
/// so a member cannot be named one thing and declared as another.
/// </remarks>
internal sealed record KindMember<TKind>(string Name, TKind Kind)
    where TKind : class;
