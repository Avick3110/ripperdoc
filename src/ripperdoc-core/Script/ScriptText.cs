namespace Ripperdoc.Core.Script;

/// <summary>
/// The one lexical pass this engine makes over script source.
/// </summary>
/// <remarks>
/// <para>
/// Annotations are found by reading text, not by parsing the language. What
/// text alone gets wrong is the two places where an annotation can appear
/// without being one: inside a comment, and inside a string. Blanking both is
/// lexical rather than syntactic - it needs no grammar, no types, and no
/// knowledge of anything the language does - and it is the whole of what stands
/// between a search for <c>@replaceMethod</c> and a commented-out line counted
/// as a live conflict.
/// </para>
/// <para>
/// Blanked spans keep their length and their newlines, so a position in the
/// blanked text is the same position in the original and reported line numbers
/// are the file's own.
/// </para>
/// </remarks>
internal static class ScriptText
{
    /// <summary>
    /// Returns <paramref name="source" /> with comment and string spans
    /// replaced by spaces, preserving length and line breaks.
    /// </summary>
    internal static string Blanked(string source)
    {
        var result = new char[source.Length];
        var index = 0;

        while (index < source.Length)
        {
            var c = source[index];

            if (c == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                while (index < source.Length && source[index] != '\n')
                {
                    result[index] = ' ';
                    index++;
                }

                continue;
            }

            if (c == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                result[index] = ' ';
                result[index + 1] = ' ';
                index += 2;

                while (index < source.Length)
                {
                    if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index += 2;
                        break;
                    }

                    // Newlines survive so that a line number taken after this
                    // pass is still the line number in the file.
                    result[index] = source[index] == '\n' ? '\n' : ' ';
                    index++;
                }

                continue;
            }

            if (c == '"')
            {
                result[index] = ' ';
                index++;

                while (index < source.Length)
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        result[index] = ' ';
                        result[index + 1] = ' ';
                        index += 2;
                        continue;
                    }

                    var closing = source[index] == '"';
                    result[index] = source[index] == '\n' ? '\n' : ' ';
                    index++;

                    if (closing)
                    {
                        break;
                    }
                }

                continue;
            }

            result[index] = c;
            index++;
        }

        return new string(result);
    }

    /// <summary>
    /// The index just past the body that opens at the first brace at or after
    /// <paramref name="from" />, or -1 when the braces do not close.
    /// </summary>
    internal static int EndOfBody(string blanked, int from)
    {
        var depth = 0;
        var opened = false;

        for (var i = from; i < blanked.Length; i++)
        {
            if (blanked[i] == '{')
            {
                depth++;
                opened = true;
            }
            else if (blanked[i] == '}')
            {
                depth--;
                if (opened && depth == 0)
                {
                    return i + 1;
                }
            }
        }

        return -1;
    }

    /// <summary>The one-based line number of <paramref name="index" />.</summary>
    internal static int LineAt(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
