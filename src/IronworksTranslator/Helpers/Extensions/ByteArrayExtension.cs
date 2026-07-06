using IronworksTranslator.Models.Enums;
using IronworksTranslator.Services.FFXIV;
using Serilog;
using Sharlayan.Core;
using Sharlayan.Utilities;
using System.Text;

namespace IronworksTranslator.Helpers.Extensions
{
    public static class ByteArrayExtension
    {
        public static ChatLogItem DecodeAutoTranslate(this byte[] rawMessage)
        {
            return DecodeAutoTranslateInternal(
                rawMessage,
                _ => AutoTranslateDictionary.FallbackText,
                null,
                null,
                ClientLanguage.English,
                false).DecodedChat;
        }

        internal static AutoTranslateDecodeResult DecodeAutoTranslate(
            this byte[] rawMessage,
            ClientLanguage replacementLanguage,
            ClientLanguage sourceLanguage,
            ClientLanguage targetLanguage,
            ClientLanguage fallbackLanguage)
        {
            return DecodeAutoTranslateInternal(
                rawMessage,
                key => ResolveReplacement(key, replacementLanguage, fallbackLanguage),
                sourceLanguage,
                targetLanguage,
                fallbackLanguage,
                true);
        }

        internal static AutoTranslateDecodeResult DecodeAutoTranslateMarker(
            this byte[] rawMessage,
            ClientLanguage sourceLanguage,
            ClientLanguage targetLanguage)
        {
            return DecodeAutoTranslateInternal(
                rawMessage,
                _ => AutoTranslateDictionary.FallbackText,
                sourceLanguage,
                targetLanguage,
                sourceLanguage,
                false);
        }

        public static List<byte[]> ExtractAutoTranslate(this byte[] rawMessage)
        {
            List<byte[]> result = [];

            /*
             * \u0002 \002E \u0004 \u0002 \u00F0 \u00CF \u0003
             * \u0002 \002E \u0003 \u0002 \u00CA \u0003
             * \u0002 \002E \u0005 \u0004 \u00F2 \u0001 \u0095 \u0003
             */
            for (int i = 0; i < rawMessage.Length; i++)
            {
                if (!TryReadAutoTranslateBlock(rawMessage, i, out var payload, out var endIndex))
                {
                    continue;
                }

                result.Add(payload);
                i = endIndex;
            }

            return result;
        }

        internal static bool TryGetAutoTranslatePayloadKey(byte[] payload, out ulong key)
        {
            key = 0;
            if (payload.Length == 0 || payload.Length > sizeof(ulong))
            {
                return false;
            }

            foreach (var value in payload)
            {
                key = (key << 8) | value;
            }

            return true;
        }

        private static AutoTranslateDecodeResult DecodeAutoTranslateInternal(
            byte[] rawMessage,
            Func<ulong, string> replacementResolver,
            ClientLanguage? sourceLanguage,
            ClientLanguage? targetLanguage,
            ClientLanguage fallbackLanguage,
            bool logUnsupportedPayload)
        {
            List<byte> rawResult = [];
            List<AutoTranslateBlock> blocks = [];

            for (int i = 0; i < rawMessage.Length; i++)
            {
                if (!TryReadAutoTranslateBlock(rawMessage, i, out var payload, out var endIndex))
                {
                    rawResult.Add(rawMessage[i]);
                    continue;
                }

                var block = CreateBlock(
                    payload,
                    sourceLanguage,
                    targetLanguage,
                    fallbackLanguage,
                    logUnsupportedPayload);
                blocks.Add(block);
                rawResult.AddRange(Encoding.UTF8.GetBytes(replacementResolver(block.PayloadKey)));
                i = endIndex;
            }

            return new AutoTranslateDecodeResult(ChatEntry.Process(rawResult.ToArray()), blocks);
        }

        private static bool TryReadAutoTranslateBlock(
            byte[] rawMessage,
            int startIndex,
            out byte[] payload,
            out int endIndex)
        {
            payload = [];
            endIndex = startIndex;

            if (!rawMessage[startIndex].Equals(0x02)) // STX
            {
                return false;
            }

            if (startIndex + 2 >= rawMessage.Length)
            {
                return false;
            }

            if (!rawMessage[startIndex + 1].Equals(0x2E)) // ASCII '.'
            {
                return false;
            }

            byte range = rawMessage[startIndex + 2];
            endIndex = startIndex + range + 2;
            if (range < 1 || endIndex >= rawMessage.Length)
            {
                return false;
            }

            if (!rawMessage[endIndex].Equals(0x03)) // ETX
            {
                return false;
            }

            payload = new byte[range - 1];
            Array.Copy(rawMessage, startIndex + 3, payload, 0, range - 1);
            return true;
        }

        private static AutoTranslateBlock CreateBlock(
            byte[] payload,
            ClientLanguage? sourceLanguage,
            ClientLanguage? targetLanguage,
            ClientLanguage fallbackLanguage,
            bool logUnsupportedPayload)
        {
            var hasKey = TryGetAutoTranslatePayloadKey(payload, out var key);
            if (!hasKey && logUnsupportedPayload)
            {
                Log.Warning("Unsupported auto-translate payload length: {Length}", payload.Length);
            }

            var sourceText = ResolveBlockText(key, sourceLanguage, fallbackLanguage, out var sourceResolved);
            var targetText = ResolveBlockText(key, targetLanguage, fallbackLanguage, out var targetResolved);

            return new AutoTranslateBlock(
                key,
                AutoTranslateDictionary.FallbackText,
                sourceText,
                targetText,
                sourceResolved,
                targetResolved);
        }

        private static string ResolveBlockText(
            ulong key,
            ClientLanguage? language,
            ClientLanguage fallbackLanguage,
            out bool resolved)
        {
            if (language.HasValue &&
                AutoTranslateDictionary.TryResolveWithFallback(
                    key,
                    language.Value,
                    fallbackLanguage,
                    out var text,
                    out _))
            {
                resolved = true;
                return text;
            }

            resolved = false;
            return AutoTranslateDictionary.FallbackText;
        }

        private static string ResolveReplacement(
            ulong key,
            ClientLanguage language,
            ClientLanguage fallbackLanguage)
        {
            if (AutoTranslateDictionary.TryResolveWithFallback(
                key,
                language,
                fallbackLanguage,
                out var replacement,
                out var resolvedLanguage))
            {
                if (resolvedLanguage != language)
                {
                    Log.Warning(
                        "Auto-translate key 0x{Key:X} is missing for {Language}; using {FallbackLanguage} text.",
                        key,
                        language,
                        resolvedLanguage);
                }

                return replacement;
            }

            Log.Warning("Unknown auto-translate key: 0x{Key:X}", key);
            return AutoTranslateDictionary.FallbackText;
        }
    }
}
