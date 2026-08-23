using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ripperdoc.Core.Drift;

/// <summary>
/// What the game says about its own types, held against what the pinned
/// dependency says about them.
/// </summary>
/// <remarks>
/// <para>
/// Inheriting a type model rather than generating one is cheaper right up to
/// the moment the model stops describing the game, and an inherited model with
/// nothing checking it cannot report that moment - it simply starts giving
/// confident wrong answers. This is the check that makes the moment visible.
/// </para>
/// <para>
/// The comparison runs in one direction on purpose: every type and property the
/// game registers should be in the model, and the model is allowed to carry
/// more. The extra is not drift. It is editor-time and tooling structure that
/// exists in files the game reads and never in the running game's own type
/// registry, so counting it as divergence would bury three real findings under
/// several thousand expected ones.
/// </para>
/// </remarks>
public sealed class TypeModelAudit
{
    private TypeModelAudit(
        IReadOnlyList<Divergence> divergences,
        string generatedDescription,
        string compiledDescription,
        int classesCompared,
        int propertiesCompared,
        int enumsCompared,
        int enumMembersCompared)
    {
        Divergences = divergences;
        GeneratedDescription = generatedDescription;
        CompiledDescription = compiledDescription;
        ClassesCompared = classesCompared;
        PropertiesCompared = propertiesCompared;
        EnumsCompared = enumsCompared;
        EnumMembersCompared = enumMembersCompared;
    }

    /// <summary>Everything the two descriptions disagree about, in a stable order.</summary>
    public IReadOnlyList<Divergence> Divergences { get; }

    /// <summary>What the generated side was read from.</summary>
    public string GeneratedDescription { get; }

    /// <summary>What the compiled side was read from.</summary>
    public string CompiledDescription { get; }

    /// <summary>How many classes the game registers were looked for in the model.</summary>
    public int ClassesCompared { get; }

    /// <summary>How many properties were compared.</summary>
    public int PropertiesCompared { get; }

    /// <summary>How many enumerations were compared.</summary>
    public int EnumsCompared { get; }

    /// <summary>How many enumeration members were compared.</summary>
    public int EnumMembersCompared { get; }

    /// <summary>How many divergences there are of each kind, including kinds with none.</summary>
    /// <returns>A count per kind.</returns>
    public IReadOnlyDictionary<DivergenceKind, int> CountsByKind()
    {
        var counts = Enum.GetValues<DivergenceKind>().ToDictionary(kind => kind, _ => 0);
        foreach (var divergence in Divergences)
        {
            counts[divergence.Kind]++;
        }

        return counts;
    }

    /// <summary>
    /// A fingerprint of exactly this set of divergences.
    /// </summary>
    /// <remarks>
    /// What a committed record of this audit can carry. The divergences
    /// themselves are derived from the game's own type information and do not
    /// go into a public repository; their fingerprint is not, and it changes if
    /// any divergence appears, disappears or changes - which is the whole of
    /// what a later run needs in order to say "this is no longer the state that
    /// was accepted". A count alone would not: a divergence appearing as
    /// another vanished would leave it unmoved.
    /// </remarks>
    public string DivergenceFingerprint
    {
        get
        {
            var builder = new StringBuilder();
            foreach (var divergence in Divergences)
            {
                builder.Append(divergence.Kind.ToString())
                    .Append(FieldSeparator)
                    .Append(divergence.TypeName)
                    .Append(FieldSeparator)
                    .Append(divergence.MemberName ?? string.Empty)
                    .Append(FieldSeparator)
                    .Append(divergence.Generated ?? string.Empty)
                    .Append(FieldSeparator)
                    .Append(divergence.Compiled ?? string.Empty)
                    .Append(RecordSeparator);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
                .ToLower(CultureInfo.InvariantCulture);
        }
    }

    // Separates the parts of a divergence inside the fingerprint's input. A
    // character no name or type can contain, so that two different divergences
    // cannot be run together into the same input text and hash alike.
    private const char FieldSeparator = '\u001f';

    // Ends one divergence's contribution, so that the parts of two adjacent
    // divergences cannot be read as one.
    private const char RecordSeparator = '\u001e';

    /// <summary>
    /// Audit a compiled type model against type information generated from an
    /// install.
    /// </summary>
    /// <param name="generated">What the game says about its own types.</param>
    /// <param name="compiled">What the pinned dependency says about them.</param>
    /// <returns>The audit.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TypeModelAudit Run(TypeModelReading generated, TypeModelReading compiled)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(compiled);

        var divergences = new List<Divergence>();
        var propertiesCompared = 0;
        var membersCompared = 0;

        foreach (var name in generated.Classes.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var fromGame = generated.Classes[name];

            if (!compiled.Classes.TryGetValue(name, out var fromModel))
            {
                divergences.Add(new Divergence(DivergenceKind.ClassAbsentFromModel, name, null, name, null));
                continue;
            }

            if (!SameParent(fromGame.ParentName, fromModel.ParentName))
            {
                divergences.Add(new Divergence(
                    DivergenceKind.ParentDiffers,
                    name,
                    null,
                    fromGame.ParentName,
                    fromModel.ParentName));
            }

            foreach (var property in fromGame.Properties.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                propertiesCompared++;

                if (!fromModel.Properties.TryGetValue(property.Key, out var modelled))
                {
                    divergences.Add(new Divergence(
                        DivergenceKind.PropertyAbsentFromModel,
                        name,
                        property.Key,
                        property.Value,
                        null));
                    continue;
                }

                if (!string.Equals(property.Value, modelled, StringComparison.Ordinal))
                {
                    divergences.Add(new Divergence(
                        DivergenceKind.PropertyTypeDiffers,
                        name,
                        property.Key,
                        property.Value,
                        modelled));
                }
            }
        }

        foreach (var name in generated.Enums.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var fromGame = generated.Enums[name];

            if (!compiled.Enums.TryGetValue(name, out var fromModel))
            {
                divergences.Add(new Divergence(DivergenceKind.EnumAbsentFromModel, name, null, name, null));
                continue;
            }

            // Every value the model gives a name, not one of them. The game
            // registers a name twice with two values on at least one
            // enumeration, so a member is corroborated when the model has that
            // name standing for the same thing - and the second registration of
            // the same name is then reported on its own merits rather than
            // measured against whichever value happened to be kept.
            var valuesByIdentifier = fromModel.Members
                .ToLookup(member => member.Name, member => member.Value, StringComparer.Ordinal);

            foreach (var member in fromGame.Members.OrderBy(entry => entry.Name, StringComparer.Ordinal))
            {
                membersCompared++;

                var identifier = AsIdentifier(member.Name);
                var modelled = valuesByIdentifier[identifier].ToArray();

                if (modelled.Length == 0)
                {
                    // A name that cannot stand as a compiled identifier on its
                    // own is written with a mark on the end by whatever
                    // generates the model. Looked for second rather than first,
                    // so a member whose name needs no such mark is matched on
                    // the name it actually has.
                    modelled = valuesByIdentifier[identifier + AdjustedNameSuffix].ToArray();
                }

                if (modelled.Length == 0)
                {
                    divergences.Add(new Divergence(
                        DivergenceKind.EnumMemberAbsentFromModel,
                        name,
                        member.Name,
                        member.Value.ToString(CultureInfo.InvariantCulture),
                        null));
                    continue;
                }

                if (modelled.Any(value => SameValue(member.Value, value, fromModel.ValueWidthInBits)))
                {
                    continue;
                }

                divergences.Add(new Divergence(
                    DivergenceKind.EnumMemberValueDiffers,
                    name,
                    member.Name,
                    member.Value.ToString(CultureInfo.InvariantCulture),
                    string.Join(
                        " or ",
                        modelled.Select(value => value.ToString(CultureInfo.InvariantCulture)))));
            }
        }

        return new TypeModelAudit(
            divergences,
            generated.Description,
            compiled.Description,
            generated.Classes.Count,
            propertiesCompared,
            generated.Enums.Count,
            membersCompared);
    }

    /// <summary>
    /// Whether the two descriptions agree about what a class derives from.
    /// </summary>
    /// <remarks>
    /// The compiled model gives every one of its classes a common root of its
    /// own, and the game's type information has no such type - a class the game
    /// describes as deriving from nothing is described by the model as deriving
    /// from that root. Compared as written, every one of those reads as drift,
    /// and the several thousand of them would bury the handful of real findings
    /// this audit exists to surface. Only that one pairing is treated as
    /// agreement: a class the game says derives from something, where the model
    /// says the root instead, is a real disagreement and stays one.
    /// </remarks>
    private static bool SameParent(string? generated, string? compiled) =>
        string.Equals(generated, compiled, StringComparison.Ordinal)
        || (generated is null && string.Equals(compiled, CompiledModelRootName, StringComparison.Ordinal));

    private const string CompiledModelRootName = nameof(WolvenKit.RED4.Types.RedBaseClass);

    /// <summary>
    /// A member name as a compiled identifier has to spell it.
    /// </summary>
    /// <remarks>
    /// The game names members with spaces, punctuation and leading digits, none
    /// of which a compiled identifier may carry, so whatever generates the model
    /// rewrites them. This applies the same rewriting so that the two sides are
    /// compared on what they mean rather than on how each is allowed to write
    /// it. A member whose name needs some other rewriting is reported as absent
    /// rather than guessed at.
    /// </remarks>
    private static string AsIdentifier(string memberName)
    {
        if (memberName.Length == 0)
        {
            return memberName;
        }

        var builder = new StringBuilder(memberName.Length + 1);

        if (char.IsDigit(memberName[0]))
        {
            builder.Append('_');
        }

        foreach (var character in memberName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether two member values stand for the same bits.
    /// </summary>
    /// <remarks>
    /// The two sides render the same bits differently where the top one is set:
    /// either may write the signed number or the unsigned one, and both do, in
    /// different places. Compared as written, every such member would be
    /// reported as diverging while standing for exactly the same thing - so
    /// both are cut down to the bits the value actually occupies and compared
    /// there.
    /// </remarks>
    private static bool SameValue(long generated, long compiled, int widthInBits)
    {
        if (generated == compiled)
        {
            return true;
        }

        // No width means nothing said how wide the value is, and cutting it
        // down to a guess would call two different values equal.
        if (widthInBits is TypeModelReading.ValueWidthNotStated or >= 64)
        {
            return false;
        }

        var mask = (1UL << widthInBits) - 1;
        return (unchecked((ulong)generated) & mask) == (unchecked((ulong)compiled) & mask);
    }

    // The mark whatever generates the model puts on the end of a member name
    // that cannot stand as a compiled identifier by itself.
    private const string AdjustedNameSuffix = "_";
}

/// <summary>
/// One thing the two descriptions of the game disagree about.
/// </summary>
/// <param name="Kind">What kind of disagreement it is.</param>
/// <param name="TypeName">The type it is on.</param>
/// <param name="MemberName">
/// The property or member it is on, or null where the disagreement is about the
/// type itself.
/// </param>
/// <param name="Generated">What the game says, or null where it says nothing.</param>
/// <param name="Compiled">What the model says, or null where it says nothing.</param>
public sealed record Divergence(
    DivergenceKind Kind,
    string TypeName,
    string? MemberName,
    string? Generated,
    string? Compiled);

/// <summary>The kinds of disagreement the audit can find.</summary>
public enum DivergenceKind
{
    /// <summary>The game registers a class the model does not have.</summary>
    ClassAbsentFromModel,

    /// <summary>The two disagree about what a class derives from.</summary>
    ParentDiffers,

    /// <summary>The game registers a property the model does not have.</summary>
    PropertyAbsentFromModel,

    /// <summary>The two disagree about the type a property is stored as.</summary>
    PropertyTypeDiffers,

    /// <summary>The game registers an enumeration the model does not have.</summary>
    EnumAbsentFromModel,

    /// <summary>The game registers an enumeration member the model does not have.</summary>
    EnumMemberAbsentFromModel,

    /// <summary>
    /// The two disagree about what an enumeration member stands for. The kind
    /// that changes behaviour without changing anything visible: a value read
    /// through the wrong member is wrong and looks fine.
    /// </summary>
    EnumMemberValueDiffers,
}
