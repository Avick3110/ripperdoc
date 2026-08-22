using System.Globalization;
using System.Security.Cryptography;
using Ripperdoc.Core.Schema;
using WolvenKit.RED4.TweakDB;
using WolvenKit.RED4.Types;

namespace Ripperdoc.Core.Tweak;

/// <summary>
/// A shipped tweak database on disk, read so that it can arbitrate a schema.
/// </summary>
/// <remarks>
/// <para>
/// The file belongs to the game's publisher and is not this project's to copy,
/// move or alter. It is opened for reading and shared, so the game and any
/// other tool can hold it open at the same time, and nothing here writes to a
/// game install.
/// </para>
/// <para>
/// Provenance names the file and its content fingerprint, never the directory
/// it was found in. A machine path in an artifact is a machine path in
/// everything the artifact is ever pasted into.
/// </para>
/// </remarks>
public sealed class TweakDatabaseSource : IShippedRecordSource, ITweakValueSource
{
    /// <summary>
    /// The type name reported for a record whose own type the database does not
    /// give.
    /// </summary>
    /// <remarks>
    /// Such a record is passed on under this name rather than skipped, so that
    /// it surfaces as a type the schema does not cover instead of disappearing
    /// from the count of records examined.
    /// </remarks>
    public const string UnknownRecordTypeName = "<record type not given by the database>";

    private readonly TweakDB _database;
    private readonly Dictionary<Type, string?> _storageTypeNames = new();

    private TweakDatabaseSource(TweakDB database, string name, string fingerprint, int storedValueCount)
    {
        _database = database;
        Name = name;
        Fingerprint = fingerprint;
        Description = $"{name}, sha256 {fingerprint}";
        StoredValueCount = storedValueCount;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>The database file's name, without the directory it was found in.</summary>
    public string Name { get; }

    /// <summary>
    /// The SHA-256 of the database's bytes, lower-case hexadecimal.
    /// </summary>
    /// <remarks>
    /// Which database a result came from is part of the result. Two files can
    /// be the same size, sit in the same directory and carry different content
    /// - one game build against another - so identity here is the hash and
    /// never the name or the size.
    /// </remarks>
    public string Fingerprint { get; }

    /// <inheritdoc />
    public int StoredValueCount { get; }

    /// <inheritdoc />
    public IEnumerable<ShippedRecord> Records
    {
        get
        {
            foreach (var identifier in _database.GetRecords())
            {
                // Asked through the record pool rather than through the
                // database's own convenience method, which searches for the
                // record instead of looking it up: over a whole shipped
                // database that difference is minutes against a second.
                var type = _database.Records.GetRecord(identifier);
                yield return new ShippedRecord((ulong)identifier, type?.Name ?? UnknownRecordTypeName);
            }
        }
    }

    /// <summary>
    /// A source over a database that is already in memory.
    /// </summary>
    /// <param name="database">The database to read.</param>
    /// <param name="name">What to call it in a provenance block.</param>
    /// <param name="fingerprint">
    /// The content fingerprint to report. A caller that did not read the
    /// database off a disk supplies whatever identifies it.
    /// </param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <remarks>
    /// The shipped database is not this project's to redistribute, so the only
    /// way this adapter's own behaviour can be checked on a machine without the
    /// game is against a database built in memory. That is the same route the
    /// rest of the engine's checks take, and it is why this exists beside the
    /// file-reading form rather than instead of it.
    /// </remarks>
    public static TweakDatabaseSource From(TweakDB database, string name, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fingerprint);

        return new TweakDatabaseSource(database, name, fingerprint, database.GetFlats().Count);
    }

    /// <summary>
    /// Open a shipped database for reading.
    /// </summary>
    /// <param name="path">The path of the database file.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">There is no file at that path.</exception>
    /// <exception cref="InvalidDataException">The file is not a readable database.</exception>
    public static TweakDatabaseSource OpenReadOnly(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("There is no tweak database at this path.", path);
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Fingerprinted and parsed from one open, in that order. Two opens
        // would leave a window between them, and the share mode this file is
        // opened with permits a writer in it - a game update is a writer. The
        // fingerprint would then describe one build while the records came
        // from another, and every artifact built on it would carry a wrong
        // answer wearing a complete provenance block, which is the one shape
        // of wrong answer nobody would think to doubt.
        var fingerprint = ComputeFingerprint(stream);
        stream.Position = 0;

        using var reader = new TweakDBReader(stream);

        EFileReadErrorCodes outcome;
        TweakDB? database;
        try
        {
            outcome = reader.ReadFile(out database);
        }
        catch (EndOfStreamException exception)
        {
            // A file that starts like a database and stops partway through it
            // ends the reader rather than being reported by it. This method
            // names one exception for a file that is not a readable database,
            // and a truncated file is one - left to escape, it hands a caller
            // that was told to expect two outcomes a third.
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as a tweak database: it ends before the "
                + "structure it declares does.",
                exception);
        }

        if (outcome != EFileReadErrorCodes.NoError || database is null)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' could not be read as a tweak database: {outcome}.");
        }

        return new TweakDatabaseSource(
            database,
            Path.GetFileName(path),
            fingerprint,
            database.GetFlats().Count);
    }

    /// <inheritdoc />
    public bool HoldsRecord(ulong identifier) => _database.Records.Exists(identifier);

    /// <summary>Whether the database holds a value under this identifier.</summary>
    /// <param name="identifier">The identifier to look up.</param>
    /// <returns>True if the value is there.</returns>
    public bool HoldsValue(ulong identifier) => _database.Flats.Exists(identifier);

    /// <summary>
    /// Whether two values the database holds are the same value.
    /// </summary>
    /// <param name="left">One identifier.</param>
    /// <param name="right">The other.</param>
    /// <returns>
    /// True only where both are present and equal. A pair where either is
    /// absent is not equal, because an absent value agrees with nothing.
    /// </returns>
    /// <remarks>
    /// Compared by value rather than by whatever handle the reader hands back.
    /// The database pools its values, so equal values share storage in the file
    /// - but that is the file's arrangement, and a reader is free to give every
    /// lookup its own object. Comparing what the reader returns would then find
    /// nothing equal to anything, and the answer would be that no two values in
    /// the database agree.
    /// </remarks>
    public bool ValuesMatch(ulong left, ulong right)
    {
        if (!_database.Flats.Exists(left) || !_database.Flats.Exists(right))
        {
            return false;
        }

        var leftValue = _database.Flats.GetValue(left);
        var rightValue = _database.Flats.GetValue(right);

        if (leftValue is null || rightValue is null)
        {
            // One side unreadable is not agreement. Reported as a difference so
            // that a value which cannot be compared stops a propagation rather
            // than being carried through one that was never checked.
            return false;
        }

        return leftValue.GetType() == rightValue.GetType() && leftValue.Equals(rightValue);
    }

    /// <inheritdoc />
    public bool TryGetStoredValueType(ulong identifier, out string? storageType)
    {
        storageType = null;

        if (!_database.Flats.Exists(identifier))
        {
            return false;
        }

        var value = _database.Flats.GetValue(identifier);
        if (value is null)
        {
            return true;
        }

        var valueType = value.GetType();
        if (_storageTypeNames.TryGetValue(valueType, out var known))
        {
            storageType = known;
            return true;
        }

        try
        {
            // A stored value carries no annotation flags - it is an object, not
            // a declaration - so the name is resolved without them. Comparing
            // it against a schema field's name therefore rests on flags not
            // changing what a storage type is called, which holds for the
            // pinned type model and is the assumption to revisit if a later one
            // ever reports a field as contradicted for no visible reason.
            var name = RedReflection.GetRedTypeFromCSType(valueType, Flags.Empty);

            // The model answers a type it cannot map with a name that names no
            // storage type, so the answer is checked as well as taken - the
            // same check the derivation side makes of the same model. Handed on
            // as though it were a storage type, it would differ from whatever
            // the schema claims and mark the field contradicted: the strongest
            // claim this engine makes, asserted about a value whose type was
            // never read.
            storageType = StorageTypeName.IsUsable(name) ? name : null;
        }
        catch (Exception)
        {
            // The value is there; the type model cannot name its type. Reported
            // as present-but-unreadable rather than as absent, because those
            // say different things about the schema field being checked.
            storageType = null;
        }

        _storageTypeNames[valueType] = storageType;
        return true;
    }

    private static string ComputeFingerprint(Stream stream)
    {
        using var algorithm = SHA256.Create();

        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLower(CultureInfo.InvariantCulture);
    }
}
