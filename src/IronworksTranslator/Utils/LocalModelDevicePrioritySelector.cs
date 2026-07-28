using IronworksTranslator.Models.Enums;
using Serilog;

namespace IronworksTranslator.Utils
{
    public static class LocalModelDevicePrioritySelector
    {
        public static LocalModelDevicePriority GetDefaultPriority()
        {
            try
            {
                return GetRecommendedPriority(SystemResourceMonitor.GetSnapshot().VramAdapterName)
                    ?? LocalModelDevicePriority.Cuda;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to determine default local model device priority.");
                return LocalModelDevicePriority.Cuda;
            }
        }

        public static LocalModelDevicePriority ResolveStartupPriority(
            LocalModelDevicePriority configuredPriority,
            bool userSelectedPriority)
        {
            return ResolveStartupPriority(
                configuredPriority,
                userSelectedPriority,
                GetDefaultPriority());
        }

        internal static LocalModelDevicePriority ResolveStartupPriority(
            LocalModelDevicePriority configuredPriority,
            bool userSelectedPriority,
            LocalModelDevicePriority? recommendedPriority)
        {
            if (!Enum.IsDefined(typeof(LocalModelDevicePriority), configuredPriority))
            {
                return recommendedPriority ?? LocalModelDevicePriority.Cuda;
            }

            if (userSelectedPriority || configuredPriority != LocalModelDevicePriority.Cuda)
            {
                return configuredPriority;
            }

            return recommendedPriority ?? configuredPriority;
        }

        public static LocalModelDevicePriority? GetRecommendedPriority(string? adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
            {
                return null;
            }

            if (adapterName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                return LocalModelDevicePriority.Cuda;
            }

            if (adapterName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                || adapterName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                return LocalModelDevicePriority.Vulkan;
            }

            return null;
        }
    }
}
