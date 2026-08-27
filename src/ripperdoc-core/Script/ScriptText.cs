namespace Ripperdoc.Core.Script;

/// <summary>
/// The one lexical pass this engine makes over script source.
/// </summary>
/// <remarks>
/// <para>
/// Annotations are found by reading text, not by parsing the language. What
/// text alone gets wrong is the places where an annotation can appear without
/// being one: inside a comment, and inside a string. Blanking both is lexical
/// rather than syntactic - it needs no grammar, no types, and no knowledge of
/// anything the language does - and it is the whole of what stands between a
/// search for <c>@replaceMethod</c> and a commented-out line counted as a live
/// conflict.
/// </para>
/// <para>
/// A string is not a flat span. An interpolation opens with <c>\(</c> and its
/// contents are ordinary code, which may open strings of its own. Reading the
/// whole literal as one span gets both directions wrong: the interpolation's
/// real code is hidden, and a string nested inside it is handed back as live
/// code - which is enough to fabricate an annotation out of a message. The
/// compiler was asked directly: a nested string carrying a closing brace and
/// annotation-shaped text compiles clean, so both are string to it.
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
        ScanCode(source, result, ref index, insideInterpolation: false);
        return new string(result);
    }

    /// <summary>
    /// Copies code through, blanking the comments and strings it meets.
    /// </summary>
    /// <remarks>
    /// Inside an interpolation this returns with the index resting on the
    /// parenthesis that closes it, which the caller consumes.
    /// </remarks>
    private static void ScanCode(string source, char[] result, ref int index, bool insideInterpolation)
    {
        var depth = 0;

        while (index < source.Length)
        {
            var c = source[index];

            if (insideInterpolation && c == ')' && depth == 0)
            {
                return;
            }

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
                ScanString(source, result, ref index);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }

            result[index] = c;
            index++;
        }
    }

    /// <summary>
    /// Blanks a string literal, entering its interpolations as code.
    /// </summary>
    /// <remarks>
    /// The index arrives on the opening quote and leaves past the closing one,
    /// or at the end of the source when the literal does not close.
    /// </remarks>
    private static void ScanString(string source, char[] result, ref int index)
    {
        result[index] = ' ';
        index++;

        while (index < source.Length)
        {
            if (source[index] == '\\' && index + 1 < source.Length)
            {
                if (source[index + 1] == '(')
                {
                    result[index] = ' ';
                    index++;

                    // The parentheses are kept so that a brace-matching or
                    // paren-matching pass over the blanked text still sees a
                    // balanced interpolation rather than a stray opener.
                    result[index] = source[index];
                    index++;

                    ScanCode(source, result, ref index, insideInterpolation: true);

                    if (index < source.Length)
                    {
                        result[index] = source[index];
                        index++;
                    }

                    continue;
                }

                result[index] = ' ';

                // The escaped character is blanked like any other, but a
                // newline stays a newline: every span this pass blanks keeps
                // its line breaks, and an escape is not an exception to that.
                result[index + 1] = source[index + 1] == '\n' ? '\n' : ' ';
                index += 2;
                continue;
            }

            var closing = source[index] == '"';
            result[index] = source[index] == '\n' ? '\n' : ' ';
            index++;

            if (closing)
            {
                return;
            }
        }
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

    /// <summary>
    /// The index just past the parenthesised group opening at
    /// <paramref name="openParen" />, or -1 when it does not close.
    /// </summary>
    /// <remarks>
    /// Counted rather than matched to the first close, because a gate's
    /// condition carries calls of its own and their parentheses nest. Strings
    /// are already blanked when this runs, so no parenthesis inside one is
    /// counted.
    /// </remarks>
    internal static int EndOfParenthesised(string blanked, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < blanked.Length; i++)
        {
            if (blanked[i] == '(')
            {
                depth++;
            }
            else if (blanked[i] == ')')
            {
                depth--;
                if (depth == 0)
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
