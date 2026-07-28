namespace IronworksTranslator.Utils
{
    public static class BuildInfo
    {
#if IRONWORKS_BETA
        public static ReleaseChannelInfo ReleaseChannel { get; } = new("Beta", "beta", includePrereleases: true);
#else
        public static ReleaseChannelInfo ReleaseChannel { get; } = new("Stable", "win", includePrereleases: false);
#endif
    }
}
