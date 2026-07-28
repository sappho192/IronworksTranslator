using IronworksTranslator.Utils;
using IronworksTranslator.Models.Enums;
using System.IO;

namespace IronworksTranslator.Models.Translator
{
    internal enum MiLMMTResourceCompatibility
    {
        Unknown,
        Insufficient,
        Tight,
        Comfortable,
    }

    public sealed record MiLMMTModelStorageItem(
        MiLMMTModelProfile Profile,
        string InstalledSize,
        string Status,
        bool IsSelected,
        string CompatibilityHint)
    {
        public string DisplayName => Profile.DisplayName;
        public string DirectoryPath => Profile.DirectoryPath;
        public string FilePath => Profile.FilePath;
        public bool IsDownloaded => File.Exists(Profile.FilePath);
        public string Note => Localizer.GetString(Profile.NoteKey);
        public string DownloadSizeLine => string.Format(
            Localizer.GetString("settings.translator.engine.milmmt.storage.download_size"),
            FormatBytes((ulong)Profile.FileSize));
        public string EstimatedMemoryLine => string.Format(
            Localizer.GetString("settings.translator.engine.milmmt.storage.estimated_memory"),
            Profile.EstimatedMemoryGb);
        public string InstalledSizeLine => string.Format(
            Localizer.GetString("settings.translator.engine.milmmt.storage.installed_size"),
            InstalledSize);
        public string StatusLine => IsSelected
            ? $"{Status} - {Localizer.GetString("settings.translator.engine.milmmt.selected")}"
            : Status;

        public static MiLMMTModelStorageItem FromProfile(
            MiLMMTModelProfile profile,
            MiLMMTModelProfile? selectedProfile = null,
            SystemResourceSnapshot? resourceSnapshot = null,
            LocalModelDevicePriority devicePriority = LocalModelDevicePriority.Cuda)
        {
            var fileInfo = File.Exists(profile.FilePath)
                ? new FileInfo(profile.FilePath)
                : null;
            var isSelected = profile == selectedProfile;
            var compatibilityHint = GetCompatibilityHint(profile, resourceSnapshot, devicePriority);

            return new MiLMMTModelStorageItem(
                profile,
                fileInfo == null
                    ? Localizer.GetString("settings.translator.engine.milmmt.storage.empty")
                    : FormatBytes((ulong)fileInfo.Length),
                fileInfo == null
                    ? Localizer.GetString("settings.translator.engine.milmmt.status.not_downloaded")
                    : Localizer.GetString("settings.translator.engine.milmmt.status.downloaded"),
                isSelected,
                compatibilityHint);
        }

        private static string FormatBytes(ulong bytes)
        {
            return $"{bytes / 1024d / 1024d / 1024d:N2} GB";
        }

        private static string GetCompatibilityHint(
            MiLMMTModelProfile profile,
            SystemResourceSnapshot? resourceSnapshot,
            LocalModelDevicePriority devicePriority)
        {
            if (resourceSnapshot == null)
            {
                return Localizer.GetString("settings.translator.engine.milmmt.compatibility.unknown");
            }

            var targetDevice = devicePriority == LocalModelDevicePriority.Cpu
                ? Localizer.GetString("settings.translator.engine.milmmt.compatibility.device.ram")
                : Localizer.GetString("settings.translator.engine.milmmt.compatibility.device.vram");
            var availableBytes = devicePriority == LocalModelDevicePriority.Cpu
                ? resourceSnapshot.AvailableRamBytes
                : resourceSnapshot.AvailableVramBytes;

            if (availableBytes == null)
            {
                return string.Format(
                    Localizer.GetString("settings.translator.engine.milmmt.compatibility.unknown_device"),
                    targetDevice);
            }

            var key = GetCompatibility(profile, resourceSnapshot, devicePriority) switch
            {
                MiLMMTResourceCompatibility.Comfortable => "settings.translator.engine.milmmt.compatibility.comfortable",
                MiLMMTResourceCompatibility.Tight => "settings.translator.engine.milmmt.compatibility.tight",
                _ => "settings.translator.engine.milmmt.compatibility.insufficient",
            };

            return string.Format(
                Localizer.GetString(key),
                targetDevice);
        }

        internal static MiLMMTResourceCompatibility GetCompatibility(
            MiLMMTModelProfile profile,
            SystemResourceSnapshot? resourceSnapshot,
            LocalModelDevicePriority devicePriority)
        {
            var availableBytes = devicePriority == LocalModelDevicePriority.Cpu
                ? resourceSnapshot?.AvailableRamBytes
                : resourceSnapshot?.AvailableVramBytes;
            if (availableBytes == null)
            {
                return MiLMMTResourceCompatibility.Unknown;
            }

            var requiredBytes = (ulong)(profile.EstimatedMemoryGb * 1024d * 1024d * 1024d);
            var comfortableReserveBytes = 4UL * 1024UL * 1024UL * 1024UL;
            return availableBytes.Value >= requiredBytes + comfortableReserveBytes
                ? MiLMMTResourceCompatibility.Comfortable
                : availableBytes.Value >= requiredBytes
                    ? MiLMMTResourceCompatibility.Tight
                    : MiLMMTResourceCompatibility.Insufficient;
        }

        internal static bool HasCompatibilityChanged(
            IEnumerable<MiLMMTModelProfile> profiles,
            SystemResourceSnapshot? previousSnapshot,
            SystemResourceSnapshot currentSnapshot,
            LocalModelDevicePriority devicePriority)
        {
            return previousSnapshot == null
                || profiles.Any(profile =>
                    GetCompatibility(profile, previousSnapshot, devicePriority)
                    != GetCompatibility(profile, currentSnapshot, devicePriority));
        }
    }
}
