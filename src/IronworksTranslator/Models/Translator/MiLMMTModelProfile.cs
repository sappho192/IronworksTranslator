using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using System.IO;

namespace IronworksTranslator.Models.Translator
{
    public sealed record MiLMMTModelProfile(
        MiLMMTModelSize Size,
        MiLMMTQuantization Quantization,
        string Repository,
        string FileName,
        long FileSize,
        string Sha256,
        double EstimatedMemoryGb,
        string NoteKey)
    {
        public string DisplayName => $"{SizeLabel} {Quantization}";
        public string SizeLabel => Size switch
        {
            MiLMMTModelSize.MiLLMT_1B => "MiLLMT 1B",
            MiLMMTModelSize.MiLLMT_4B => "MiLLMT 4B",
            MiLMMTModelSize.MiLLMT_12B => "MiLLMT 12B",
            _ => Size.ToString(),
        };
        public string DirectoryPath => AppPaths.GetMiLMMTModelDirectory(Size);
        public string FilePath => Path.Combine(DirectoryPath, FileName);
        public string DownloadUrl => $"https://huggingface.co/{Repository}/resolve/main/{FileName}";
    }

    public static class MiLMMTModelProfiles
    {
        private static readonly MiLMMTModelProfile[] Profiles =
        [
            new(
                MiLMMTModelSize.MiLLMT_1B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-1B-v0.1-GGUF",
                "MiLMMT-46-1B-v0.1.Q4_K_M.gguf",
                1013675392,
                "9d5c10855eb2688d453e3069e7b6dee1756fc834d738d2dc04318511993fd54f",
                1.4,
                "settings.translator.engine.milmmt.note.1b.q4"),
            new(
                MiLMMTModelSize.MiLLMT_1B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-1B-v0.1-GGUF",
                "MiLMMT-46-1B-v0.1.Q8_0.gguf",
                1390169728,
                "2d5a99eafb172e7fe13a606ce57ef45eecabb919dbea7c757827da3e8dc03e1e",
                1.8,
                "settings.translator.engine.milmmt.note.1b.q8"),
            new(
                MiLMMTModelSize.MiLLMT_4B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q4_K_M.gguf",
                2867472640,
                "9888198d9f1cbac935f6428a2a4aead1272f55c1d5ebacd395ab1575bd09b1ec",
                3.5,
                "settings.translator.engine.milmmt.note.4b.q4"),
            new(
                MiLMMTModelSize.MiLLMT_4B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q8_0.gguf",
                4843607040,
                "f97bca9c5e1e221568c87ed0e71d7869418b728e07469187c46b708c4f6b148f",
                5.8,
                "settings.translator.engine.milmmt.note.4b.q8"),
            new(
                MiLMMTModelSize.MiLLMT_12B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-12B-v0.1-GGUF",
                "MiLMMT-46-12B-v0.1.Q4_K_M.gguf",
                7867146656,
                "c9ccc4ae361c83aa63d2c0995851f4bb1981609959ed184727c1d135d81cd28f",
                9.5,
                "settings.translator.engine.milmmt.note.12b.q4"),
        ];

        public static IReadOnlyList<MiLMMTModelProfile> All => Profiles;

        public static MiLMMTModelProfile Get(MiLMMTModelSize size, MiLMMTQuantization quantization)
        {
            return Profiles.First(profile => profile.Size == size && profile.Quantization == quantization);
        }

        public static bool IsSupported(MiLMMTModelSize size, MiLMMTQuantization quantization)
        {
            return Profiles.Any(profile => profile.Size == size && profile.Quantization == quantization);
        }

        public static MiLMMTQuantization GetDefaultQuantization(MiLMMTModelSize size)
        {
            return Profiles.First(profile => profile.Size == size).Quantization;
        }

        public static MiLMMTModelProfile GetCurrent()
        {
            var settings = Models.Settings.IronworksSettings.Instance?.TranslatorSettings;
            var size = settings?.MiLMMTModelSize ?? MiLMMTModelSize.MiLLMT_1B;
            var quantization = settings?.MiLMMTQuantization ?? MiLMMTQuantization.Q8_0;
            if (!IsSupported(size, quantization))
            {
                quantization = GetDefaultQuantization(size);
            }

            return Get(
                size,
                quantization);
        }
    }
}
