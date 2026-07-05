using IronworksTranslator.Utils;
using System.IO;

namespace IronworksTranslator.Models.Translator
{
    public sealed record MiLMMTModelStorageItem(
        MiLMMTModelProfile Profile,
        string InstalledSize,
        string Status)
    {
        public string DisplayName => Profile.DisplayName;
        public string DirectoryPath => Profile.DirectoryPath;
        public string FilePath => Profile.FilePath;
        public bool IsDownloaded => File.Exists(Profile.FilePath);

        public static MiLMMTModelStorageItem FromProfile(MiLMMTModelProfile profile)
        {
            var fileInfo = File.Exists(profile.FilePath)
                ? new FileInfo(profile.FilePath)
                : null;

            return new MiLMMTModelStorageItem(
                profile,
                fileInfo == null
                    ? Localizer.GetString("settings.translator.engine.milmmt.storage.empty")
                    : FormatBytes((ulong)fileInfo.Length),
                fileInfo == null
                    ? Localizer.GetString("settings.translator.engine.milmmt.status.not_downloaded")
                    : Localizer.GetString("settings.translator.engine.milmmt.status.downloaded"));
        }

        private static string FormatBytes(ulong bytes)
        {
            return $"{bytes / 1024d / 1024d / 1024d:N2} GB";
        }
    }
}
