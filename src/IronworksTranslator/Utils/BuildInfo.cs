namespace IronworksTranslator.Utils
{
    public static class BuildInfo
    {
        public static string ReleaseVersion { get; } = GetReleaseVersion(
            typeof(BuildInfo).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion,
            typeof(BuildInfo).Assembly.GetName().Version);

        internal static string GetReleaseVersion(string? informationalVersion, Version? assemblyVersion)
        {
            var version = informationalVersion?.Split('+', 2)[0].Trim();
            return !string.IsNullOrWhiteSpace(version)
                ? version
                : assemblyVersion?.ToString(3) ?? string.Empty;
        }

#if IRONWORKS_BETA
        public static ReleaseChannelInfo ReleaseChannel { get; } = new("Beta", "beta", includePrereleases: true);
#else
        public static ReleaseChannelInfo ReleaseChannel { get; } = new("Stable", "win", includePrereleases: false);
#endif
    }
}
