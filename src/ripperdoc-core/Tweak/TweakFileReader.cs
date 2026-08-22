using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Ripperdoc.Core.Tweak;

/// <summary>
/// Reads one tweak file in the YAML language into the writes it performs.
/// </summary>
/// <remarks>
/// <para>
/// A parser is used rather than a scan over lines. The language puts type
/// declarations in sequence items as well as in mapping keys, and it carries its
/// mutation operators as YAML tags - both of which a line-anchored reader misses
/// or misreads, and neither of which announces itself when it goes wrong.
/// </para>
/// <para>
/// Nothing here resolves a value to its stored type. A contest is over a name,
/// and the values are carried as the files wrote them so that a report can show
/// both sides. Where the two spellings of one value differ but mean the same
/// thing, that is visible in the report rather than decided here.
/// </para>
/// </remarks>
public sealed class TweakFileReader
{
    /// <summary>The character that marks a key as an instruction rather than a name.</summary>
    public const char AttributeMarker = '$';

    /// <summary>The separator between a record's name and one of its property names.</summary>
    public const char PropertySeparator = '.';

    private const string TypeAttribute = "$type";
    private const string BaseAttribute = "$base";
    private const string ValueAttribute = "$value";
    private const string InstancesAttribute = "$instances";

    private static readonly string[] MutationTags =
    [
        "!append", "!append-once", "!append-from",
        "!prepend", "!prepend-once", "!prepend-from",
        "!merge", "!remove", "!remove-all",
    ];

    /// <summary>
    /// Read every file a layer reads, in the order the layer reads them.
    /// </summary>
    /// <param name="layer">The layer, enumerated.</param>
    /// <param name="rootPath">The directory the layer was enumerated from.</param>
    /// <returns>The documents, in read order.</returns>
    /// <remarks>
    /// A file in the other tweak language comes back as a document saying it was
    /// not replayed, rather than being handed to this reader or dropped. The
    /// framework acts on it, so a layer containing one is a layer whose replay is
    /// incomplete, and the report has to be able to say that.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<TweakDocument> ReadLayer(TweakLayer layer, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(rootPath);

        return layer.Files.Select(file =>
        {
            var path = Path.Combine(rootPath, file.RelativePath.Replace(TweakFile.PathSeparator, Path.DirectorySeparatorChar));

            return file.Format == TweakFileFormat.Yaml
                ? Read(path, file.RelativePath)
                : new TweakDocument(
                    file.RelativePath,
                    [new TweakUnhandled(1, file.RelativePath, "a file in the other tweak language, which this engine does not replay")],
                    IsReadable: true);
        }).ToArray();
    }

    /// <summary>
    /// Read a tweak file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="relativePath">
    /// What to call the file in a report - its path within the layer, never the
    /// machine path.
    /// </param>
    /// <returns>What the file says, in the order it says it.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TweakDocument Read(string path, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(relativePath);

        // The layer is written by hand in many editors and a byte order mark is
        // ordinary in it. Detected rather than assumed either way, because a
        // mark read as content turns the first record's name into one nothing
        // else in the layer can match.
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var stream = new YamlStream();
        try
        {
            stream.Load(reader);
        }
        catch (YamlException exception)
        {
            return new TweakDocument(
                relativePath,
                [new TweakUnhandled((int)exception.Start.Line, relativePath, $"the file could not be parsed: {exception.Message}")],
                IsReadable: false);
        }

        var statements = new List<TweakStatement>();

        if (stream.Documents.Count == 0)
        {
            return new TweakDocument(relativePath, statements, IsReadable: true);
        }

        // Only the first document is read, and a second is named rather than
        // dropped: the framework loads one document per file, so a file with
        // more is a file whose later content never reaches the game.
        for (var index = 1; index < stream.Documents.Count; index++)
        {
            statements.Add(new TweakUnhandled(
                (int)stream.Documents[index].RootNode.Start.Line,
                relativePath,
                "a second document in the same file, which the framework does not read"));
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            statements.Add(new TweakUnhandled(
                (int)stream.Documents[0].RootNode.Start.Line,
                relativePath,
                "the top level of the file is not a mapping, so the framework reads nothing from it"));

            return new TweakDocument(relativePath, statements, IsReadable: true);
        }

        foreach (var (keyNode, valueNode) in root.Children)
        {
            var name = Scalar(keyNode);
            if (name is null || name.Length == 0 || name[0] == AttributeMarker)
            {
                continue;
            }

            ReadTopLevel(name, valueNode, statements);
        }

        return new TweakDocument(relativePath, statements, IsReadable: true);
    }

    private static void ReadTopLevel(string name, YamlNode node, List<TweakStatement> statements)
    {
        if (node is not YamlMappingNode mapping)
        {
            statements.Add(WriteFor(name, node, owningRecordName: null));
            return;
        }

        if (mapping.Children.Keys.Any(key => Scalar(key) == InstancesAttribute))
        {
            // A template stands for a set of records whose names are built from
            // its own substitutions. Naming it is not the same as replaying it,
            // and a name guessed wrong would be reported as a value nothing
            // writes - so it is carried as unreplayed and says so.
            statements.Add(new TweakUnhandled(
                (int)node.Start.Line,
                name,
                "a template, whose records this engine does not expand"));
            return;
        }

        var typeName = Attribute(mapping, TypeAttribute);
        var baseName = Attribute(mapping, BaseAttribute);

        // Asked for by presence, not by content. A value given this way can be
        // a sequence or a mapping as easily as a scalar, and a test that only
        // recognises a scalar drops the whole write - the write vanishes from
        // the resolved state, from any contest over it and from the schema
        // reading, with nothing saying so.
        if (typeName is not null && Node(mapping, ValueAttribute) is { } valueNode)
        {
            statements.Add(WriteFor(name, valueNode, owningRecordName: null));
            return;
        }

        if (typeName is not null || baseName is not null)
        {
            statements.Add(new TweakRecordDeclaration((int)node.Start.Line, name, typeName, baseName));
        }

        ReadProperties(name, mapping, statements);
    }

    // A mapping with no declaration is read as a record's properties. The
    // framework decides this against the shipped database, which a run without
    // one cannot consult; the reading taken here is the one that holds for
    // every record, and the alternative - a value of a structured type written
    // in this form - would produce property names under a value rather than a
    // record. Such a name matches nothing else in the layer, so it cannot
    // manufacture a contest; it can only fail to join one.
    private static void ReadProperties(string recordName, YamlMappingNode mapping, List<TweakStatement> statements)
    {
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            var key = Scalar(keyNode);
            if (key is null || key.Length == 0 || key[0] == AttributeMarker)
            {
                continue;
            }

            var flatName = recordName + PropertySeparator + key;

            if (valueNode is YamlMappingNode)
            {
                // A structured value under a property is either a record
                // created in place or a value of a structured type. Either way
                // its name carries the file it came from, so no other file can
                // write it and it cannot be contested. Recorded as a write of
                // the property that holds it, which is the part that can be.
                statements.Add(new TweakFlatWrite(
                    (int)valueNode.Start.Line,
                    flatName,
                    "<defined in place>",
                    TweakWriteKind.Assignment,
                    recordName));
                continue;
            }

            statements.Add(WriteFor(flatName, valueNode, recordName));
        }
    }

    private static TweakStatement WriteFor(string flatName, YamlNode node, string? owningRecordName)
    {
        if (!TryRender(node, [], out var valueText))
        {
            // There is no text for a value that contains itself, and the
            // attempt to find one is what makes this worth refusing rather
            // than guessing at: it does not fail, it does not return, and the
            // process is gone before anything can say which file did it.
            // Named here instead, in the same list every other construct this
            // reader declines to replay goes into.
            return new TweakUnhandled(
                (int)node.Start.Line,
                flatName,
                "a value that contains itself through an alias, which this engine does not render");
        }

        var kind = IsMutation(node) ? TweakWriteKind.Mutation : TweakWriteKind.Assignment;

        return new TweakFlatWrite((int)node.Start.Line, flatName, valueText, kind, owningRecordName);
    }

    // A sequence mutates rather than replaces when any of its items carries an
    // operator tag. One tagged item is enough: the framework applies the tagged
    // items and warns that the untagged ones are ignored, so a sequence with
    // both is a mutation with a mistake in it rather than a replacement.
    private static bool IsMutation(YamlNode node) =>
        node is YamlSequenceNode sequence
        && sequence.Children.Any(item => MutationTags.Contains(item.Tag.ToString(), StringComparer.Ordinal));

    // A value is a graph rather than a tree: an alias naming the anchor it
    // sits inside makes it circular. What is tracked is the path currently
    // being walked and not everything seen, because one anchor aliased more
    // than once alongside itself is an ordinary way to write a file - a value
    // the game applies, and refusing it would report the layer as less read
    // than it is. Identity is by reference: comparing these nodes by value
    // walks their children, which on a circular value is the same
    // non-return this guard exists to prevent. The path is a list scanned by
    // reference rather than a set, because a set would compare by value to
    // find its bucket; it holds one entry per level of nesting, not one per
    // item, so scanning it costs nothing worth naming.
    private static bool TryRender(YamlNode node, List<YamlNode> walking, out string text)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                text = scalar.Value ?? string.Empty;
                return true;

            case YamlMappingNode:
                text = "<defined in place>";
                return true;

            case YamlSequenceNode sequence:
                text = string.Empty;

                if (walking.Any(entered => ReferenceEquals(entered, sequence)))
                {
                    return false;
                }

                walking.Add(sequence);

                var items = new List<string>(sequence.Children.Count);
                foreach (var item in sequence.Children)
                {
                    if (!TryRender(item, walking, out var rendered))
                    {
                        walking.RemoveAt(walking.Count - 1);
                        return false;
                    }

                    items.Add(rendered);
                }

                walking.RemoveAt(walking.Count - 1);
                text = "[" + string.Join(", ", items) + "]";
                return true;

            default:
                text = "<unreadable>";
                return true;
        }
    }

    private static string? Attribute(YamlMappingNode mapping, string attribute) =>
        Scalar(Node(mapping, attribute));

    private static YamlNode? Node(YamlMappingNode mapping, string attribute) => mapping.Children
        .Where(pair => Scalar(pair.Key) == attribute)
        .Select(pair => pair.Value)
        .FirstOrDefault();

    private static string? Scalar(YamlNode? node) => (node as YamlScalarNode)?.Value;
}

/// <summary>
/// One tweak file, as what it says.
/// </summary>
/// <param name="RelativePath">The file's path within the layer.</param>
/// <param name="Statements">What it says, in the order it says it.</param>
/// <param name="IsReadable">
/// False where the file could not be parsed at all, in which case the only
/// statement is the reason.
/// </param>
public sealed record TweakDocument(
    string RelativePath,
    IReadOnlyList<TweakStatement> Statements,
    bool IsReadable)
{
    /// <summary>Every value this file writes, in order.</summary>
    public IEnumerable<TweakFlatWrite> Writes => Statements.OfType<TweakFlatWrite>();

    /// <summary>Every record this file declares, in order.</summary>
    public IEnumerable<TweakRecordDeclaration> Declarations => Statements.OfType<TweakRecordDeclaration>();

    /// <summary>Everything in the file this reader did not replay.</summary>
    public IEnumerable<TweakUnhandled> Unhandled => Statements.OfType<TweakUnhandled>();
}
