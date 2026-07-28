namespace IronworksTranslator.Utils
{
    public sealed class ReleaseChannelInfo
    {
        public ReleaseChannelInfo(string displayName, string velopackChannel, bool includePrereleases)
        {
            DisplayName = displayName;
            VelopackChannel = velopackChannel;
            IncludePrereleases = includePrereleases;
        }

        public string DisplayName { get; }

        public string VelopackChannel { get; }

        public bool IncludePrereleases { get; }
    }
}
