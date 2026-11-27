using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Cloudflare;
using IronworksTranslator.Utils.Translator;
using Serilog;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IronworksTranslator.Services
{
    /// <summary>
    /// Manages local caching and background synchronization of the MiLMMT runtime glossary.
    /// Safe to use offline: loads the last cached snapshot from disk and silently keeps it on sync failures.
    /// </summary>
    public sealed class MiLMMTGlossaryService : IDisposable
    {
        private readonly BiggsWorker _biggsWorker;
        private readonly string _cacheFilePath;
        private readonly string _metaFilePath;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private MiLMMTGlossaryMatcher _matcher = MiLMMTGlossaryMatcher.Empty;
        private string? _cachedETag;
        private bool _disposed;

        public MiLMMTGlossaryService(BiggsWorker biggsWorker)
            : this(biggsWorker, AppPaths.GlossaryCacheFilePath, AppPaths.GlossaryMetaFilePath)
        {
        }

        internal MiLMMTGlossaryService(
            BiggsWorker biggsWorker,
            string cacheFilePath,
            string metaFilePath)
        {
            _biggsWorker = biggsWorker ?? throw new ArgumentNullException(nameof(biggsWorker));
            _cacheFilePath = cacheFilePath;
            _metaFilePath = metaFilePath;
            LoadFromDiskCache();
        }

        public MiLMMTGlossaryMatcher CurrentMatcher => Volatile.Read(ref _matcher);

        public string CurrentVersion => CurrentMatcher.Version;

        public int TermCount => CurrentMatcher.TermCount;

        public int NpcNameCount => CurrentMatcher.NpcNameCount;

        public int NpcStyleCount => CurrentMatcher.NpcStyleCount;

        public IReadOnlyList<MiLMMTGlossaryTerm> Match(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? input,
            int maxTerms = MiLMMTGlossaryMatcher.DefaultMaxTerms)
        {
            return CurrentMatcher.Match(sourceLanguage, targetLanguage, input, maxTerms);
        }

        public bool TryResolveNpcName(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? speaker,
            out string officialName)
        {
            return CurrentMatcher.TryResolveNpcName(sourceLanguage, targetLanguage, speaker, out officialName);
        }

        public bool TryResolveNpcStyle(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? speaker,
            out string speechStyle)
        {
            return CurrentMatcher.TryResolveNpcStyle(sourceLanguage, targetLanguage, speaker, out speechStyle);
        }

        public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return false;
            }

            var lockAcquired = false;
            try
            {
                await _refreshLock.WaitAsync(cancellationToken);
                lockAcquired = true;

                var result = await _biggsWorker.FetchGlossaryAsync(_cachedETag, cancellationToken);
                if (result.Status == BiggsGlossaryFetchStatus.NotModified)
                {
                    if (!string.IsNullOrWhiteSpace(result.ETag))
                    {
                        _cachedETag = result.ETag;
                    }

                    Log.Debug(
                        "MiLMMT glossary is already up to date. Version: {Version}, ETag: {ETag}",
                        CurrentVersion,
                        _cachedETag);
                    return true;
                }

                if (result.Status != BiggsGlossaryFetchStatus.Updated
                    || string.IsNullOrWhiteSpace(result.JsonContent))
                {
                    Log.Warning(
                        "MiLMMT glossary refresh skipped; keeping cached version '{Version}'. Reason: {Error}",
                        CurrentVersion,
                        result.ErrorMessage ?? "Unknown error");
                    return false;
                }

                if (!MiLMMTGlossaryMatcher.TryFromJson(result.JsonContent, out var parsedMatcher))
                {
                    Log.Warning("Fetched MiLMMT glossary payload was invalid JSON; keeping cached version '{Version}'.", CurrentVersion);
                    return false;
                }

                SaveToDiskCache(result.JsonContent, result.ETag, parsedMatcher.Version);
                _cachedETag = result.ETag;
                Volatile.Write(ref _matcher, parsedMatcher);
                Log.Information(
                    "MiLMMT glossary updated to version '{Version}' ({TermCount} terms, {NpcNameCount} NPC names, {NpcStyleCount} NPC styles).",
                    parsedMatcher.Version,
                    parsedMatcher.TermCount,
                    parsedMatcher.NpcNameCount,
                    parsedMatcher.NpcStyleCount);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unexpected error while refreshing MiLMMT glossary; keeping cached snapshot.");
                return false;
            }
            finally
            {
                if (lockAcquired)
                {
                    _refreshLock.Release();
                }
            }
        }

        private void LoadFromDiskCache()
        {
            try
            {
                if (File.Exists(_metaFilePath))
                {
                    var metaJson = File.ReadAllText(_metaFilePath, Encoding.UTF8);
                    var meta = JsonSerializer.Deserialize<GlossaryCacheMetadata>(metaJson);
                    if (!string.IsNullOrWhiteSpace(meta?.ETag))
                    {
                        _cachedETag = meta.ETag;
                    }
                }

                if (!File.Exists(_cacheFilePath))
                {
                    return;
                }

                var rawJson = File.ReadAllText(_cacheFilePath, Encoding.UTF8);
                if (MiLMMTGlossaryMatcher.TryFromJson(rawJson, out var parsedMatcher))
                {
                    Volatile.Write(ref _matcher, parsedMatcher);
                    Log.Information(
                        "Loaded cached MiLMMT glossary version '{Version}' ({TermCount} terms, {NpcNameCount} NPC names, {NpcStyleCount} NPC styles).",
                        parsedMatcher.Version,
                        parsedMatcher.TermCount,
                        parsedMatcher.NpcNameCount,
                        parsedMatcher.NpcStyleCount);
                }
                else
                {
                    Log.Warning("Cached MiLMMT glossary at {Path} could not be parsed; ignoring cache.", _cacheFilePath);
                    _cachedETag = null;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load cached MiLMMT glossary from {Path}; continuing with empty glossary.", _cacheFilePath);
                _cachedETag = null;
            }
        }

        private void SaveToDiskCache(string rawJson, string? etag, string version)
        {
            try
            {
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempCachePath = $"{_cacheFilePath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(tempCachePath, rawJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tempCachePath, _cacheFilePath, overwrite: true);

                var meta = new GlossaryCacheMetadata
                {
                    Version = version,
                    ETag = etag,
                    UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                };
                var tempMetaPath = $"{_metaFilePath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(
                    tempMetaPath,
                    JsonSerializer.Serialize(meta),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tempMetaPath, _metaFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to persist MiLMMT glossary cache to {Path}.", _cacheFilePath);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _refreshLock.Dispose();
            _disposed = true;
        }

        private sealed class GlossaryCacheMetadata
        {
            public string? Version { get; set; }
            public string? ETag { get; set; }
            public string? UpdatedUtc { get; set; }
        }
    }
}
