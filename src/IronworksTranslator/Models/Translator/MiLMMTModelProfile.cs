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
        IReadOnlyList<TranslationLanguageCode> SupportedLanguages,
        string NoteKey,
        string? DisplayNameOverride = null)
    {
        public string DisplayName => DisplayNameOverride ?? $"{SizeLabel} {Quantization}";
        public string SizeLabel => Size switch
        {
            MiLMMTModelSize.MiLMMT_1B => "MiLMMT 1B",
            MiLMMTModelSize.MiLMMT_4B => "MiLMMT 4B",
            MiLMMTModelSize.MiLMMT_12B => "MiLMMT 12B",
            MiLMMTModelSize.MiLMMT_1B_Compact => "MiLMMT 1B Compact (Debug)",
            _ => Size.ToString(),
        };
        public string DirectoryPath => AppPaths.GetMiLMMTModelDirectory(Size);
        public string FilePath => Path.Combine(DirectoryPath, FileName);
        public string DownloadUrl => $"https://huggingface.co/{Repository}/resolve/main/{FileName}";
        public bool Supports(TranslationLanguageCode language) => SupportedLanguages.Contains(language);
        public string SupportedLanguageNames => string.Join(", ", SupportedLanguages.Select(language => language switch
        {
            TranslationLanguageCode.Japanese => "日本語",
            TranslationLanguageCode.English => "English",
            TranslationLanguageCode.German => "Deutsch",
            TranslationLanguageCode.French => "Français",
            TranslationLanguageCode.Korean => "한국어",
            _ => language.ToString(),
        }));
    }

    public static class MiLMMTModelProfiles
    {
        private static readonly TranslationLanguageCode[] AllLanguages =
        [
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.English,
            TranslationLanguageCode.German,
            TranslationLanguageCode.French,
            TranslationLanguageCode.Korean,
        ];

        private static readonly TranslationLanguageCode[] KoEnJaLanguages =
        [
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
        ];

        private static readonly MiLMMTModelProfile[] Profiles =
        [
            new(
                MiLMMTModelSize.MiLMMT_1B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-1B-v0.1-GGUF",
                "MiLMMT-46-1B-v0.1.Q4_K_M.gguf",
                1013675392,
                "9d5c10855eb2688d453e3069e7b6dee1756fc834d738d2dc04318511993fd54f",
                1.4,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.q4"),
            new(
                MiLMMTModelSize.MiLMMT_1B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-1B-v0.1-GGUF",
                "MiLMMT-46-1B-v0.1.Q8_0.gguf",
                1390169728,
                "2d5a99eafb172e7fe13a606ce57ef45eecabb919dbea7c757827da3e8dc03e1e",
                1.8,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.q8"),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q4_K_M.gguf",
                2867472640,
                "9888198d9f1cbac935f6428a2a4aead1272f55c1d5ebacd395ab1575bd09b1ec",
                3.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.4b.q4"),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q8_0.gguf",
                4843607040,
                "f97bca9c5e1e221568c87ed0e71d7869418b728e07469187c46b708c4f6b148f",
                5.8,
                AllLanguages,
                "settings.translator.engine.milmmt.note.4b.q8"),
            new(
                MiLMMTModelSize.MiLMMT_12B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-12B-v0.1-GGUF",
                "MiLMMT-46-12B-v0.1.Q4_K_M.gguf",
                7867146656,
                "c9ccc4ae361c83aa63d2c0995851f4bb1981609959ed184727c1d135d81cd28f",
                9.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.12b.q4"),
#if DEBUG
            new(
                MiLMMTModelSize.MiLMMT_1B_Compact,
                MiLMMTQuantization.Q4_K_M,
                "sappho192/MiLMMT-46-1B-v0.1-ko-en-ja-pruned-130k-imatrix-Q4_K_M-GGUF",
                "milmmt-pruned-130k-bf16-imatrix-mix-late16-25-ffngateupq8-Q4_K_M.gguf",
                803547328,
                "5f781fdc9a685212dba3244b7cf2df39625776066043408f769549195f018b0d",
                1.2,
                KoEnJaLanguages,
                "settings.translator.engine.milmmt.note.1b.compact",
                "MiLMMT 1B Compact (Debug)"),
#endif
        ];

        private static readonly MiLMMTModelProfile[] RetiredProfiles =
        [
#if !DEBUG
            new(
                MiLMMTModelSize.MiLMMT_1B_Compact,
                MiLMMTQuantization.Q4_K_M,
                "sappho192/MiLMMT-46-1B-v0.1-ko-en-ja-pruned-130k-imatrix-Q4_K_M-GGUF",
                "milmmt-pruned-130k-bf16-imatrix-mix-late16-25-ffngateupq8-Q4_K_M.gguf",
                803547328,
                "5f781fdc9a685212dba3244b7cf2df39625776066043408f769549195f018b0d",
                1.2,
                KoEnJaLanguages,
                "settings.translator.engine.milmmt.note.retired.1b.compact",
                "MiLMMT 1B Compact (Debug)"),
#endif
        ];

        public static IReadOnlyList<MiLMMTModelProfile> All => Profiles;
        public static IReadOnlyList<MiLMMTModelProfile> Retired => RetiredProfiles;
        public static IEnumerable<MiLMMTModelProfile> AllKnown => Profiles.Concat(RetiredProfiles);
        public static IReadOnlyList<MiLMMTModelSize> SelectableModelSizes { get; } =
        [
            MiLMMTModelSize.MiLMMT_1B,
            MiLMMTModelSize.MiLMMT_4B,
            MiLMMTModelSize.MiLMMT_12B,
#if DEBUG
            MiLMMTModelSize.MiLMMT_1B_Compact,
#endif
        ];

        public static IReadOnlyList<MiLMMTModelProfile> GetDownloadedRetiredProfiles(
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(isAvailable);

            return RetiredProfiles.Where(isAvailable).ToArray();
        }

        public static MiLMMTModelProfile Get(MiLMMTModelSize size, MiLMMTQuantization quantization)
        {
            return Profiles.First(profile => profile.Size == size && profile.Quantization == quantization);
        }

        public static bool IsSupported(MiLMMTModelSize size, MiLMMTQuantization quantization)
        {
            return Profiles.Any(profile => profile.Size == size && profile.Quantization == quantization);
        }

        public static MiLMMTModelSize GetFallbackModelSize(MiLMMTModelSize size)
        {
            return Profiles.Any(profile => profile.Size == size)
                ? size
                : MiLMMTModelSize.MiLMMT_1B;
        }

        public static MiLMMTQuantization GetDefaultQuantization(MiLMMTModelSize size)
        {
            var fallbackSize = GetFallbackModelSize(size);
            return Profiles.First(profile => profile.Size == fallbackSize).Quantization;
        }

        public static MiLMMTModelProfile? FindPreferredAvailableProfile(
            MiLMMTModelSize size,
            MiLMMTQuantization preferredQuantization,
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(isAvailable);

            size = GetFallbackModelSize(size);
            var defaultQuantization = GetDefaultQuantization(size);
            return Profiles
                .Where(profile => profile.Size == size)
                .OrderBy(profile => profile.Quantization == preferredQuantization ? 0 : 1)
                .ThenBy(profile => profile.Quantization == defaultQuantization ? 0 : 1)
                .FirstOrDefault(isAvailable);
        }

        public static MiLMMTModelProfile GetCurrent()
        {
            var settings = Models.Settings.IronworksSettings.Instance?.TranslatorSettings;
            var size = settings?.MiLMMTModelSize ?? MiLMMTModelSize.MiLMMT_1B;
            var quantization = settings?.MiLMMTQuantization ?? MiLMMTQuantization.Q8_0;
            size = GetFallbackModelSize(size);
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
