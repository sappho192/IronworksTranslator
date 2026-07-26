namespace IronworksTranslator.Models.ReleaseNotes
{
    public sealed class GitHubReleaseNote
    {
        public GitHubReleaseNote(
            string tagName,
            string name,
            string body,
            string htmlUrl,
            DateTimeOffset publishedAt,
            bool isPrerelease)
        {
            TagName = tagName;
            Name = name;
            Body = body;
            HtmlUrl = htmlUrl;
            PublishedAt = publishedAt;
            IsPrerelease = isPrerelease;
        }

        public string TagName { get; }

        public string Name { get; }

        public string Body { get; }

        public string HtmlUrl { get; }

        public DateTimeOffset PublishedAt { get; }

        public bool IsPrerelease { get; }

        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(Name)
            || string.Equals(Name.Trim(), TagName, StringComparison.OrdinalIgnoreCase)
                ? TagName
                : $"{TagName} — {Name.Trim()}";

        public string PublishedDateText =>
            PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");
    }
}
