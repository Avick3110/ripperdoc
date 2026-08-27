namespace Ripperdoc.Core.Tests;

/// <summary>
/// The words no emitted sentence about a wrap chain may contain.
/// </summary>
/// <remarks>
/// <para>
/// The wrap order this engine reports is a compile order. These are the words a
/// reader would most naturally supply to turn that into a claim about run time,
/// which was not measured - so a sentence carrying one is making a claim this
/// project cannot support, whether or not it goes on to deny it. A disclaimer
/// has to name the stronger reading to deny it, and the named reading is what
/// survives a skim.
/// </para>
/// <para>
/// One list, read by every check that enforces it. Two copies drift, and the
/// copy that drifts is the one over the layer nobody re-reads.
/// </para>
/// </remarks>
internal static class NestingVocabulary
{
    internal static readonly string[] Forbidden =
    [
        "outermost",
        "innermost",
        "nesting",
        "runs first",
        "runs last",
    ];
}
