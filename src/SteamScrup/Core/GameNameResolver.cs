using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SteamScrup.Core;

/// <summary>
/// Resolves a Steam appid to a game name.
///
/// Lookup order: names already known from app manifests, then a local cache, and finally
/// Steam's public store endpoint for anything still unknown. The result is cached so a
/// machine only ever pays for the lookup once per appid.
///
/// This is the only place in the application that touches the network. It is deliberately
/// confined to a single batch call at startup and never blocks the UI: the scan shows
/// names it already knows and the bubbles fill in when the answer arrives.
/// </summary>
public sealed class GameNameResolver
{
    /// <summary>
    /// Single-appid endpoint. The public store API rejects a comma-separated list with
    /// HTTP 400 (verified against the live endpoint), so requests are issued one at a time
    /// with a small delay between them to stay under the rate limit.
    /// </summary>
    private const string StoreEndpoint =
        "https://store.steampowered.com/api/appdetails?appids={0}&l={1}&filters=basic";

    /// <summary>Delay between lookups; the endpoint is public and not ours to hammer.</summary>
    private const int RequestDelayMs = 200;

    private readonly OperationLog _log;
    private readonly string _cachePath;
    private readonly Dictionary<int, string> _names = new();
    private readonly HashSet<int> _knownMissing = new();

    private static readonly HttpClient Http = CreateClient();

    public GameNameResolver(OperationLog log, string? cachePath = null)
    {
        _log = log;
        _cachePath = cachePath ?? Path.Combine(AppPaths.DataDirectory, "names.json");
        LoadCache();
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            // Use the machine's proxy settings when one is configured; many users need it
            // to reach Steam at all, and the default handler ignores it in some setups.
            UseProxy = true,
            Proxy = WebRequest.GetSystemWebProxy(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SteamScrup/1.1 (portable; local cleanup tool)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    /// <summary>Flattens an exception chain so the log says why a lookup failed.</summary>
    private static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");
        return string.Join(" <- ", parts);
    }

    /// <summary>Cached or resolved name, or null when it could not be determined.</summary>
    public string? TryGet(int appId) =>
        _names.TryGetValue(appId, out var name) ? name : null;

    /// <summary>Trimmed label for the UI: a real name, or an explicit AppID marker.</summary>
    public string Describe(int appId) =>
        TryGet(appId) is { Length: > 0 } name ? name : $"AppID {appId}";

    public int CachedCount => _names.Count;

    /// <summary>Seeds names gathered from app manifests, which need no network lookup.</summary>
    public void SeedFromManifests(IEnumerable<ManifestInfo> manifests)
    {
        foreach (var m in manifests)
        {
            if (m.Name is null || m.Name.Length == 0) continue;
            _names[m.AppId] = m.Name;
        }
    }

    /// <summary>
    /// Resolves every appid that is neither known from a manifest nor already cached as
    /// missing. Never throws: a network failure leaves the bubbles showing "AppID n".
    ///
    /// Requests are sequential because the endpoint only accepts one appid at a time, so a
    /// progress callback is offered and callers are expected to run this in the background.
    /// </summary>
    public async Task<IReadOnlyList<int>> ResolveAsync(
        IEnumerable<int> appIds,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var wanted = appIds
            .Where(id => id > 0)
            .Distinct()
            .Where(id => !_names.ContainsKey(id) && !_knownMissing.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (wanted.Count == 0)
        {
            _log.Info($"name resolution: nothing to look up ({_names.Count} name(s) already cached)");
            return Array.Empty<int>();
        }

        _log.Info($"name resolution: querying Steam one appid at a time for {wanted.Count} id(s)");

        var resolved = new List<int>();
        var language = Localizer.Instance.IsChinese ? "schinese" : "english";
        var done = 0;
        var consecutiveFailures = 0;

        foreach (var appId in wanted)
        {
            ct.ThrowIfCancellationRequested();

            var url = string.Format(StoreEndpoint, appId, language);

            try
            {
                using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
                consecutiveFailures = 0;

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (ApplyResponse(json, appId)) resolved.Add(appId);
                }
                else
                {
                    _log.Warn($"name resolution: HTTP {(int)response.StatusCode} for appid {appId}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Offline or blocked: back off quickly rather than retrying every id.
                consecutiveFailures++;
                _log.Warn($"name resolution failed for appid {appId}: {Describe(ex)}");

                if (consecutiveFailures == 1)
                {
                    _log.Warn("name resolution: network unavailable or TLS blocked; " +
                              "candidate titles will show \"AppID n\" instead of game names");
                }

                if (consecutiveFailures >= 3)
                {
                    _log.Warn("name resolution: three consecutive failures, giving up for this run");
                    break;
                }
            }

            done++;
            progress?.Report((done, wanted.Count));

            await Task.Delay(RequestDelayMs, ct).ConfigureAwait(false);
        }

        SaveCache();

        var stillMissing = wanted.Count - resolved.Count;
        _log.Info($"name resolution: resolved {resolved.Count}, unresolved {stillMissing} " +
                  $"(cache now holds {_names.Count} name(s))");

        return resolved;
    }

    /// <summary>
    /// Reads a single-appid store response into the cache.
    /// Returns true when the appid gained a name.
    /// </summary>
    private bool ApplyResponse(string json, int appId)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _log.Warn($"name resolution: unusable response for {appId} ({ex.Message})");
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty(appId.ToString(), out var entry))
            {
                _knownMissing.Add(appId);
                return false;
            }

            var ok = entry.TryGetProperty("success", out var success) &&
                     success.ValueKind == JsonValueKind.True;

            // A delisted app reports success=false and carries no data block. Those are
            // permanent, so they are remembered as missing rather than retried every run.
            if (!ok ||
                !entry.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                _knownMissing.Add(appId);
                return false;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                _knownMissing.Add(appId);
                return false;
            }

            _names[appId] = name!;
            return true;
        }
    }

    // --------------------------------------------------------------- cache

    private sealed class CacheModel
    {
        public Dictionary<string, string> Names { get; set; } = new();
        public List<int> Missing { get; set; } = new();
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;

            var model = JsonSerializer.Deserialize<CacheModel>(File.ReadAllText(_cachePath));
            if (model is null) return;

            foreach (var pair in model.Names)
                if (int.TryParse(pair.Key, out var id) && !string.IsNullOrWhiteSpace(pair.Value))
                    _names[id] = pair.Value;

            foreach (var id in model.Missing) _knownMissing.Add(id);

            _log.Info($"name cache loaded: {_names.Count} name(s), {_knownMissing.Count} known-missing");
        }
        catch (Exception ex)
        {
            _log.Warn($"name cache could not be read ({ex.Message}); starting empty");
        }
    }

    private void SaveCache()
    {
        try
        {
            var model = new CacheModel
            {
                Names = _names.ToDictionary(p => p.Key.ToString(), p => p.Value),
                Missing = _knownMissing.OrderBy(id => id).ToList(),
            };

            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(_cachePath,
                JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _log.Warn($"name cache could not be written ({ex.Message})");
        }
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }
}