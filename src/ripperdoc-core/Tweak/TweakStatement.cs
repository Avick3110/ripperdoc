namespace Ripperdoc.Core.Tweak;

/// <summary>
/// One thing a tweak file says, in the order the file says it.
/// </summary>
/// <remarks>
/// The statements are deliberately close to the framework's own reading rather
/// than to the file's syntax. What a replay needs to know is which values a file
/// writes and in what order; how the file spelled that is the reader's problem
/// and stops here.
/// </remarks>
public abstract record TweakStatement(int Line)
{
    /// <summary>What this statement is, in words fit for a report.</summary>
    public abstract string Summary { get; }
}

/// <summary>
/// A file declaring a record - creating one of a named type, or cloning one.
/// </summary>
/// <param name="Line">The line the declaration starts on.</param>
/// <param name="RecordName">The record's full name.</param>
/// <param name="DeclaredTypeName">
/// The type name as the file spelled it, or null where the file cloned instead.
/// </param>
/// <param name="BaseName">
/// The record cloned from, or null where the file named a type instead.
/// </param>
public sealed record TweakRecordDeclaration(
    int Line,
    string RecordName,
    string? DeclaredTypeName,
    string? BaseName) : TweakStatement(Line)
{
    /// <inheritdoc />
    public override string Summary => BaseName is null
        ? $"declares {RecordName} of type {DeclaredTypeName}"
        : $"declares {RecordName} as a clone of {BaseName}";
}

/// <summary>
/// A file writing a value.
/// </summary>
/// <param name="Line">The line the write is on.</param>
/// <param name="FlatName">The full name of the value written.</param>
/// <param name="ValueText">
/// The value as the file wrote it, rendered so that two writes can be compared
/// and shown.
/// </param>
/// <param name="Kind">Whether the write replaces the value or mutates it.</param>
/// <param name="OwningRecordName">
/// The record the value belongs to, or null where the file wrote the value by
/// its full name without naming a record.
/// </param>
public sealed record TweakFlatWrite(
    int Line,
    string FlatName,
    string ValueText,
    TweakWriteKind Kind,
    string? OwningRecordName) : TweakStatement(Line)
{
    /// <inheritdoc />
    public override string Summary => Kind == TweakWriteKind.Assignment
        ? $"sets {FlatName} to {ValueText}"
        : $"mutates {FlatName} ({ValueText})";
}

/// <summary>
/// Something in the file this reader will not claim to have replayed.
/// </summary>
/// <remarks>
/// Present so that a file is never partly read in silence. A construct that is
/// carried here is a construct the resolved state says it does not account for,
/// and any verdict that could have been changed by it is qualified.
/// </remarks>
/// <param name="Line">The line the construct is on.</param>
/// <param name="Path">Where in the file it is, by name.</param>
/// <param name="Reason">Why it was not replayed, in a sentence.</param>
public sealed record TweakUnhandled(int Line, string Path, string Reason) : TweakStatement(Line)
{
    /// <inheritdoc />
    public override string Summary => $"{Path}: {Reason}";
}

/// <summary>
/// Whether a write replaces a value or changes it in place.
/// </summary>
/// <remarks>
/// The two contest differently, which is why they are told apart at the point
/// of reading rather than later. Two files replacing one value is a contest one
/// of them loses silently; two files appending to one array is composition, and
/// reporting it as a contest would send a reader after a problem that is not
/// there.
/// </remarks>
public enum TweakWriteKind
{
    /// <summary>The value is replaced.</summary>
    Assignment,

    /// <summary>Elements are added to or removed from the existing value.</summary>
    Mutation,
}
