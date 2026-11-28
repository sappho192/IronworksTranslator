using System.Text;

namespace IronworksTranslator.Services.FFXIV
{
    /// <summary>
    /// Decodes FFXIV SeString binary control payloads (0x02 ... 0x03) and game icon glyphs
    /// into clean plain text suitable for display, glossary matching, and translation.
    /// </summary>
    public static class FfxivTextSanitizer
    {
        private const char PayloadStart = '\u0002';
        private const char PayloadEnd = '\u0003';
        private const char PayloadNewLine = '\u0010';
        private const char PayloadNonBreakingSpace = '\u001D';
        private const char PayloadHyphen = '\u001F';
        private const int MaxPayloadSpanLength = 64;

        public static string SanitizeDialogueText(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);
            var previousWasSpace = false;

            for (var i = 0; i < input.Length; i++)
            {
                var ch = input[i];

                if (ch == PayloadStart)
                {
                    if (TryConsumePayload(input, i, out var nextIndex, out var replacement))
                    {
                        if (replacement == '-')
                        {
                            sb.Append('-');
                            previousWasSpace = false;
                        }
                        else if (replacement == ' ')
                        {
                            AppendCollapsedSpace(sb, ref previousWasSpace);
                        }

                        i = nextIndex - 1;
                        continue;
                    }

                    // Stray unclosed STX byte: drop without swallowing subsequent text.
                    continue;
                }

                if (ch == PayloadEnd || ch == '\uE0BB' || ch == '\uFFFD')
                {
                    continue;
                }

                if (ch == '\uE03C')
                {
                    sb.Append("[HQ]");
                    previousWasSpace = false;
                    continue;
                }

                if (ch == '\uE06F')
                {
                    sb.Append('⇒');
                    previousWasSpace = false;
                    continue;
                }

                if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                {
                    AppendCollapsedSpace(sb, ref previousWasSpace);
                    continue;
                }

                if (ch < '\u0020')
                {
                    // Drop stray C0 control characters.
                    continue;
                }

                sb.Append(ch);
                previousWasSpace = false;
            }

            return sb.ToString().Trim();
        }

        public static string SanitizeSpeaker(string? input)
        {
            return SanitizeDialogueText(input);
        }

        private static bool TryConsumePayload(
            string input,
            int startIndex,
            out int nextIndex,
            out char replacement)
        {
            nextIndex = startIndex + 1;
            replacement = '\0';

            if (startIndex + 2 >= input.Length)
            {
                return false;
            }

            var payloadType = input[startIndex + 1];
            var searchLimit = Math.Min(input.Length - 1, startIndex + MaxPayloadSpanLength);
            for (var j = startIndex + 2; j <= searchLimit; j++)
            {
                if (input[j] == PayloadStart)
                {
                    // Unterminated payload before another STX.
                    return false;
                }

                if (input[j] == PayloadEnd)
                {
                    nextIndex = j + 1;
                    replacement = payloadType switch
                    {
                        PayloadHyphen => '-',
                        PayloadNewLine or PayloadNonBreakingSpace => ' ',
                        _ => '\0',
                    };
                    return true;
                }
            }

            return false;
        }

        private static void AppendCollapsedSpace(StringBuilder sb, ref bool previousWasSpace)
        {
            if (sb.Length == 0 || previousWasSpace)
            {
                return;
            }

            sb.Append(' ');
            previousWasSpace = true;
        }
    }
}
