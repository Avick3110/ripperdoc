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
public sealed class TweakDatabaseSource : IShippedRecordSource
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

        var fingerprint = ComputeFingerprint(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new TweakDBReader(stream);

        var outcome = reader.ReadFile(out TweakDB? database);
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
            storageType = RedReflection.GetRedTypeFromCSType(valueType, Flags.Empty);
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

    private static string ComputeFingerprint(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var algorithm = SHA256.Create();

        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLower(CultureInfo.InvariantCulture);
    }
}
