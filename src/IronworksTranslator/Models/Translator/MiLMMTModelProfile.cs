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
        string? DisplayNameOverride = null,
        string? DirectoryNameOverride = null,
        bool AllowTemporaryFallback = false,
        bool SupportsGlossaryPrompt = false)
    {
        public string DisplayName => DisplayNameOverride ?? $"{SizeLabel} v1.0 {Quantization}";
        public string SizeLabel => Size switch
        {
            MiLMMTModelSize.MiLMMT_1B => "MiLMMT 1B",
            MiLMMTModelSize.MiLMMT_4B => "MiLMMT 4B",
            MiLMMTModelSize.MiLMMT_12B => "MiLMMT 12B",
            MiLMMTModelSize.MiLMMT_1B_Compact => "MiLMMT 1B Compact (Debug)",
            MiLMMTModelSize.Ironworks_1B => "Ironworks Translator 1B v0.1 (Debug)",
            _ => Size.ToString(),
        };
        public string DirectoryPath => DirectoryNameOverride != null
            ? Path.Combine(AppPaths.ModelDirectory, DirectoryNameOverride)
            : AppPaths.GetMiLMMTModelDirectory(Size);
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
                "mradermacher/MiLMMT-46-1B-v1.0-GGUF",
                "MiLMMT-46-1B-v1.0.Q4_K_M.gguf",
                806057408,
                "74d38ba75108d455326e9deeaf9ab01bb266dfa665eae9c4aa84e84485d4fdf9",
                1.2,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.q4"),
            new(
                MiLMMTModelSize.MiLMMT_1B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-1B-v1.0-GGUF",
                "MiLMMT-46-1B-v1.0.Q8_0.gguf",
                1069305536,
                "e813418992ee1eae0d2859ca1a610b31ad19577078803aed4124dc9518f84b22",
                1.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.q8"),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-4B-v1.0-GGUF",
                "MiLMMT-46-4B-v1.0.Q4_K_M.gguf",
                2489893152,
                "25424cbb5423afaa22b1b53bfb3d8aa116cfd84b5f8dcfa82363bfb3c4221ddf",
                3.2,
                AllLanguages,
                "settings.translator.engine.milmmt.note.4b.q4"),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-4B-v1.0-GGUF",
                "MiLMMT-46-4B-v1.0.Q8_0.gguf",
                4130401312,
                "92796e263e22461c273c9f964ba4c1454d8dc764a482af60bc1ad9c269d9e7d0",
                5.0,
                AllLanguages,
                "settings.translator.engine.milmmt.note.4b.q8"),
            new(
                MiLMMTModelSize.MiLMMT_12B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-12B-v1.0-GGUF",
                "MiLMMT-46-12B-v1.0.Q4_K_M.gguf",
                7300777440,
                "8c2d878d8daf8722faf1a42eef02ce046411ede17bf3115fb8b2cce491d5fb03",
                8.8,
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
                "MiLMMT 1B Compact (Debug)",
                "milmmt-46-1b-v0.1"),
            new(
                MiLMMTModelSize.Ironworks_1B,
                MiLMMTQuantization.Q8_0,
                "sappho192/ironworks-translator-1b-v0.1",
                "ironworks-translator-1b-v0.1.Q8_0.gguf",
                1069305088,
                "cb1040e81d29dd7308d3956d7742007398f001170ee5c22bbcb32ae41f180932",
                1.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.ffxiv",
                "Ironworks Translator 1B v0.1 Q8_0 (Debug)",
                "ironworks-translator-1b-v0.1",
                SupportsGlossaryPrompt: true),
            new(
                MiLMMTModelSize.Ironworks_1B,
                MiLMMTQuantization.Q4_K_M,
                "sappho192/ironworks-translator-1b-v0.1",
                "ironworks-translator-1b-v0.1.Q4_K_M.gguf",
                806056960,
                "d33e366de2f29de6eff976f5bed4c21286a7ddcca40df6783fcf9e56e8534542",
                1.2,
                AllLanguages,
                "settings.translator.engine.milmmt.note.1b.ffxiv",
                "Ironworks Translator 1B v0.1 Q4_K_M (Debug)",
                "ironworks-translator-1b-v0.1",
                SupportsGlossaryPrompt: true),
#endif
        ];

        private static readonly MiLMMTModelProfile[] RetiredProfiles =
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
                "settings.translator.engine.milmmt.note.retired.v0_1",
                "MiLMMT 1B v0.1 Q4_K_M",
                "milmmt-46-1b-v0.1",
                AllowTemporaryFallback: true),
            new(
                MiLMMTModelSize.MiLMMT_1B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-1B-v0.1-GGUF",
                "MiLMMT-46-1B-v0.1.Q8_0.gguf",
                1390169728,
                "2d5a99eafb172e7fe13a606ce57ef45eecabb919dbea7c757827da3e8dc03e1e",
                1.8,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.v0_1",
                "MiLMMT 1B v0.1 Q8_0",
                "milmmt-46-1b-v0.1",
                AllowTemporaryFallback: true),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q4_K_M.gguf",
                2867472640,
                "9888198d9f1cbac935f6428a2a4aead1272f55c1d5ebacd395ab1575bd09b1ec",
                3.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.v0_1",
                "MiLMMT 4B v0.1 Q4_K_M",
                "milmmt-46-4b-v0.1",
                AllowTemporaryFallback: true),
            new(
                MiLMMTModelSize.MiLMMT_4B,
                MiLMMTQuantization.Q8_0,
                "mradermacher/MiLMMT-46-4B-v0.1-GGUF",
                "MiLMMT-46-4B-v0.1.Q8_0.gguf",
                4843607040,
                "f97bca9c5e1e221568c87ed0e71d7869418b728e07469187c46b708c4f6b148f",
                5.8,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.v0_1",
                "MiLMMT 4B v0.1 Q8_0",
                "milmmt-46-4b-v0.1",
                AllowTemporaryFallback: true),
            new(
                MiLMMTModelSize.MiLMMT_12B,
                MiLMMTQuantization.Q4_K_M,
                "mradermacher/MiLMMT-46-12B-v0.1-GGUF",
                "MiLMMT-46-12B-v0.1.Q4_K_M.gguf",
                7867146656,
                "c9ccc4ae361c83aa63d2c0995851f4bb1981609959ed184727c1d135d81cd28f",
                9.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.v0_1",
                "MiLMMT 12B v0.1 Q4_K_M",
                "milmmt-46-12b-v0.1",
                AllowTemporaryFallback: true),
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
                "MiLMMT 1B Compact (Debug)",
                "milmmt-46-1b-v0.1"),
            new(
                MiLMMTModelSize.Ironworks_1B,
                MiLMMTQuantization.Q8_0,
                "sappho192/ironworks-translator-1b-v0.1",
                "ironworks-translator-1b-v0.1.Q8_0.gguf",
                1069305088,
                "cb1040e81d29dd7308d3956d7742007398f001170ee5c22bbcb32ae41f180932",
                1.5,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.1b.ffxiv",
                "Ironworks Translator 1B v0.1 Q8_0 (Debug)",
                "ironworks-translator-1b-v0.1"),
            new(
                MiLMMTModelSize.Ironworks_1B,
                MiLMMTQuantization.Q4_K_M,
                "sappho192/ironworks-translator-1b-v0.1",
                "ironworks-translator-1b-v0.1.Q4_K_M.gguf",
                806056960,
                "d33e366de2f29de6eff976f5bed4c21286a7ddcca40df6783fcf9e56e8534542",
                1.2,
                AllLanguages,
                "settings.translator.engine.milmmt.note.retired.1b.ffxiv",
                "Ironworks Translator 1B v0.1 Q4_K_M (Debug)",
                "ironworks-translator-1b-v0.1"),
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
            MiLMMTModelSize.Ironworks_1B,
#endif
        ];

        public static IReadOnlyList<MiLMMTModelProfile> GetDownloadedRetiredProfiles(
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(isAvailable);

            return RetiredProfiles.Where(isAvailable).ToArray();
        }

        public static IReadOnlyList<MiLMMTModelProfile> GetDownloadedFallbackProfiles(
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(isAvailable);

            return RetiredProfiles
                .Where(profile => profile.AllowTemporaryFallback && isAvailable(profile))
                .ToArray();
        }

        public static MiLMMTModelProfile? FindTemporaryFallbackProfile(
            MiLMMTModelProfile targetProfile,
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(targetProfile);
            ArgumentNullException.ThrowIfNull(isAvailable);

            return RetiredProfiles
                .Where(profile => profile.AllowTemporaryFallback && isAvailable(profile))
                .OrderBy(profile =>
                    profile.Size == targetProfile.Size && profile.Quantization == targetProfile.Quantization ? 0 : 1)
                .ThenBy(profile => profile.Size == targetProfile.Size ? 0 : 1)
                .ThenBy(profile => profile.FileSize)
                .FirstOrDefault();
        }

        public static MiLMMTModelProfile? ResolveEffectiveProfile(
            MiLMMTModelProfile targetProfile,
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(targetProfile);
            ArgumentNullException.ThrowIfNull(isAvailable);

            if (isAvailable(targetProfile))
            {
                return targetProfile;
            }

            return FindTemporaryFallbackProfile(targetProfile, isAvailable);
        }

        public static MiLMMTModelProfile GetEffectiveCurrent()
        {
            var current = GetCurrent();
            return ResolveEffectiveProfile(current, profile => File.Exists(profile.FilePath))
                ?? current;
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
