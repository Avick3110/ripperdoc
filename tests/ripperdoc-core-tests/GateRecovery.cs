using Ripperdoc.Core.Drift;

namespace Ripperdoc.Core.Tests;

/// <summary>
/// What a reader is told to do when the accepted drift result no longer
/// describes what is being built.
/// </summary>
/// <remarks>
/// <para>
/// One home for the sentence, because it is read by two audiences that must be
/// told the same thing: whoever meets the red tier (i) check, and the check
/// that holds the sentence against the gate script.
/// </para>
/// <para>
/// A recovery instruction is a claim about what this project offers. Naming one
/// precondition out of several does not merely under-inform - it sends the
/// reader to do the named thing, watch the tier announce itself as skipped, and
/// find no file where they were told one would be. So the preconditions here
/// are the gate's actual ones, and <see cref="GatePreconditionTests"/> reads
/// <c>scripts/ci-checks.sh</c> and refuses to let the two drift apart.
/// </para>
/// </remarks>
internal static class GateRecovery
{
    /// <summary>
    /// How to take a fresh receipt, preconditions included.
    /// </summary>
    internal static string HowToTakeAFreshReceipt =>
        "To take a fresh one, run the gate where the RTTI-dump checks can run. That tier needs all of: "
        + $"{RttiDumpFixture.VariableName} naming a dump's json directory; {ShippedDatabaseFixture.VariableName} "
        + "naming a shipped tweak database; and that database's sha256 matching the one recorded in "
        + "'tests/measured-database.sha256'. With any of them missing the gate announces the tier as skipped "
        + $"and nothing is produced. A run that does happen writes '{DriftReceipt.ProducedFileName}' beside "
        + $"the test binaries - copy it over 'tests/{DriftReceipt.FileName}'. Then run the gate again and "
        + "check that it does NOT announce the drift audit comparison as skipped: a receipt produced by a run "
        + "that could not compare has not been held against the game's description at all, and accepting one "
        + "pins whatever that run happened to read. A green gate alone does not say this - an announced skip "
        + "is green too. Do not edit the committed receipt to match this build by hand: the numbers in it are "
        + "an audit's result, and hand-editing them accepts a result nobody took.";

    /// <summary>The file the gate reads to learn whether the tier ran.</summary>
    internal const string MeasuredDatabaseFile = "tests/measured-database.sha256";
}
