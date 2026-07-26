using IronworksTranslator.Models.Enums;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Helpers
{
    internal sealed class LegacyV1SettingsYamlTypeConverter : IYamlTypeConverter
    {
        internal const string StableTranslatorEngineValue = "MiLLMT";
        internal const string StableModelSizePrefix = "MiLLMT_";

        public bool Accepts(Type type)
        {
            return type == typeof(TranslatorEngine) || type == typeof(MiLMMTModelSize);
        }

        public object ReadYaml(
            IParser parser,
            Type type,
            ObjectDeserializer rootDeserializer)
        {
            var value = parser.Consume<Scalar>().Value;
            var normalizedValue = NormalizeWireValue(type, value);

            if (Enum.TryParse(type, normalizedValue, ignoreCase: false, out var parsedValue) &&
                parsedValue != null)
            {
                return parsedValue;
            }

            throw new YamlException(
                $"Unsupported {type.Name} settings value '{value}'.");
        }

        public void WriteYaml(
            IEmitter emitter,
            object? value,
            Type type,
            ObjectSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(value);

            var wireValue = value switch
            {
                TranslatorEngine.MiLMMT => StableTranslatorEngineValue,
                MiLMMTModelSize modelSize => ToStableModelSizeValue(modelSize),
                _ => value.ToString()!,
            };

            emitter.Emit(new Scalar(wireValue));
        }

        private static string NormalizeWireValue(Type type, string value)
        {
            if (type == typeof(TranslatorEngine) &&
                string.Equals(value, StableTranslatorEngineValue, StringComparison.Ordinal))
            {
                return nameof(TranslatorEngine.MiLMMT);
            }

            if (type == typeof(MiLMMTModelSize) &&
                value.StartsWith(StableModelSizePrefix, StringComparison.Ordinal))
            {
                return $"MiLMMT_{value[StableModelSizePrefix.Length..]}";
            }

            return value;
        }

        private static string ToStableModelSizeValue(MiLMMTModelSize modelSize)
        {
            return modelSize switch
            {
                MiLMMTModelSize.MiLMMT_1B => $"{StableModelSizePrefix}1B",
                MiLMMTModelSize.MiLMMT_4B => $"{StableModelSizePrefix}4B",
                MiLMMTModelSize.MiLMMT_12B => $"{StableModelSizePrefix}12B",
                _ => $"{StableModelSizePrefix}1B",
            };
        }
    }
}
