using System.Globalization;
using System.Text.Json;
using SteamAppInfo.Steam;
using SteamAppInfo.Steam.Enums;
using SteamAppInfo.Steam.Models;
using SteamBibliotekRydder.Models;
using ValveKeyValue;

namespace SteamBibliotekRydder.Services;

public sealed class SteamLibraryScanner
{
    private readonly SteamLocator _locator;

    public SteamLibraryScanner(SteamLocator locator)
    {
        _locator = locator;
    }

    public ScanResult Scan(IProgress<string>? progress = null)
    {
        progress?.Report("Finner Steam-konto …");
        var context = _locator.DetectMostRecent();

        progress?.Report("Leser lokale Steam-innstillinger …");
        var localConfig = File.Exists(context.Account.LocalConfigPath)
            ? VdfTextParser.ParseFile(context.Account.LocalConfigPath)
            : new VdfNode("__root__");

        var playtime = ReadPlaytime(localConfig);
        var tagNames = ReadTagNames(localConfig);

        progress?.Report("Finner spill som tilhører biblioteket …");
        var candidateIds = ReadCandidateAppIds(context, localConfig);

        progress?.Report("Leser Steams lokale spillmetadata …");
        var steamClient = new SteamClient(context.SteamPath);
        var appInfo = steamClient.GetApps().ToList();
        var installedIds = steamClient.Libraries
            .SelectMany(library => library.Apps)
            .ToHashSet();

        var games = new List<GameInfo>();
        foreach (var app in appInfo)
        {
            if (app.AppType != AppType.Game || !candidateIds.Contains(app.AppId))
            {
                continue;
            }

            var common = app.Data["common"];
            if (common is null)
            {
                continue;
            }

            var usage = playtime.GetValueOrDefault(app.AppId) ??
                        new UsageInfo(0, null);
            games.Add(new GameInfo
            {
                AppId = app.AppId,
                Name = string.IsNullOrWhiteSpace(app.Name)
                    ? $"Ukjent spill {app.AppId}"
                    : app.Name.Trim(),
                IsInstalled = installedIds.Contains(app.AppId),
                PlaytimeMinutes = usage.PlaytimeMinutes,
                LastPlayed = usage.LastPlayed,
                GenreIds = ReadIdsFromValues(common["genres"]),
                TagIds = ReadIdsFromValues(common["store_tags"]),
                FeatureCategoryIds = ReadFeatureCategoryIds(common["category"])
            });
        }

        games.Sort((left, right) =>
            StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));

        progress?.Report($"Fant {games.Count} spill.");
        return new ScanResult(
            context,
            games,
            tagNames,
            candidateIds.Count,
            appInfo.Count);
    }

    private static HashSet<uint> ReadCandidateAppIds(
        SteamContext context,
        VdfNode localConfig)
    {
        var ids = new HashSet<uint>();

        var libraryCache = Path.Combine(context.SteamPath, "appcache", "librarycache");
        if (Directory.Exists(libraryCache))
        {
            foreach (var directory in Directory.EnumerateDirectories(libraryCache))
            {
                if (uint.TryParse(Path.GetFileName(directory), out var appId))
                {
                    ids.Add(appId);
                }
            }
        }

        var apps = localConfig["UserLocalConfigStore"]?["Software"]?["Valve"]?["Steam"]?["apps"];
        if (apps is not null)
        {
            foreach (var app in apps.Children)
            {
                if (uint.TryParse(app.Name, out var appId))
                {
                    ids.Add(appId);
                }
            }
        }

        var libraryFoldersPath = Path.Combine(
            context.SteamPath,
            "steamapps",
            "libraryfolders.vdf");
        if (File.Exists(libraryFoldersPath))
        {
            var libraryFolders = VdfTextParser.ParseFile(libraryFoldersPath)["libraryfolders"];
            if (libraryFolders is not null)
            {
                foreach (var library in libraryFolders.Children)
                {
                    var installedApps = library["apps"];
                    if (installedApps is null)
                    {
                        continue;
                    }

                    foreach (var app in installedApps.Children)
                    {
                        if (uint.TryParse(app.Name, out var appId))
                        {
                            ids.Add(appId);
                        }
                    }
                }
            }
        }

        ReadCollectionAppIds(context.Account.CloudStoragePath, ids);
        return ids;
    }

    private static void ReadCollectionAppIds(string cloudStoragePath, ISet<uint> ids)
    {
        if (!File.Exists(cloudStoragePath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(cloudStoragePath));
            foreach (var pair in document.RootElement.EnumerateArray())
            {
                if (pair.GetArrayLength() < 2)
                {
                    continue;
                }

                var record = pair[1];
                if (!record.TryGetProperty("key", out var keyElement) ||
                    !keyElement.GetString()!.StartsWith(
                        "user-collections.",
                        StringComparison.OrdinalIgnoreCase) ||
                    !record.TryGetProperty("value", out var valueElement))
                {
                    continue;
                }

                using var value = JsonDocument.Parse(valueElement.GetString() ?? "{}");
                ReadAppIdArray(value.RootElement, "added", ids);
                ReadAppIdArray(value.RootElement, "removed", ids);
            }
        }
        catch (JsonException)
        {
            // The writer performs strict validation before any modification.
        }
    }

    private static void ReadAppIdArray(
        JsonElement value,
        string propertyName,
        ISet<uint> ids)
    {
        if (!value.TryGetProperty(propertyName, out var appIds) ||
            appIds.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in appIds.EnumerateArray())
        {
            if (element.TryGetUInt32(out var appId))
            {
                ids.Add(appId);
            }
        }
    }

    private static Dictionary<uint, UsageInfo> ReadPlaytime(VdfNode localConfig)
    {
        var result = new Dictionary<uint, UsageInfo>();
        var apps = localConfig["UserLocalConfigStore"]?["Software"]?["Valve"]?["Steam"]?["apps"];
        if (apps is null)
        {
            return result;
        }

        foreach (var app in apps.Children)
        {
            if (!uint.TryParse(app.Name, out var appId))
            {
                continue;
            }

            _ = int.TryParse(
                app["Playtime"]?.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var playtimeMinutes);

            DateTimeOffset? lastPlayed = null;
            if (long.TryParse(
                    app["LastPlayed"]?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var lastPlayedUnix) &&
                lastPlayedUnix > 0)
            {
                try
                {
                    lastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayedUnix);
                }
                catch (ArgumentOutOfRangeException)
                {
                    lastPlayed = null;
                }
            }

            result[appId] = new UsageInfo(Math.Max(0, playtimeMinutes), lastPlayed);
        }

        return result;
    }

    private static Dictionary<int, string> ReadTagNames(VdfNode localConfig)
    {
        var result = new Dictionary<int, string>();
        var tagCache = FindFirst(
            localConfig,
            node => node.Name.StartsWith(
                "LocalizedTagNames2_",
                StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(tagCache?.Value))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(tagCache.Value);
            if (!document.RootElement.TryGetProperty("tags", out var tags))
            {
                return result;
            }

            foreach (var tag in tags.EnumerateArray())
            {
                if (tag.TryGetProperty("tagid", out var idElement) &&
                    idElement.TryGetInt32(out var id) &&
                    tag.TryGetProperty("name", out var nameElement))
                {
                    result[id] = nameElement.GetString() ?? id.ToString();
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static VdfNode? FindFirst(VdfNode node, Func<VdfNode, bool> predicate)
    {
        foreach (var child in node.Children)
        {
            if (predicate(child))
            {
                return child;
            }

            var nested = FindFirst(child, predicate);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static HashSet<int> ReadIdsFromValues(KVValue? node)
    {
        var result = new HashSet<int>();
        if (node is not IEnumerable<KVObject> children)
        {
            return result;
        }

        foreach (var child in children)
        {
            if (int.TryParse(
                    child.Value.ToString(CultureInfo.InvariantCulture),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static HashSet<int> ReadFeatureCategoryIds(KVValue? node)
    {
        var result = new HashSet<int>();
        if (node is not IEnumerable<KVObject> children)
        {
            return result;
        }

        foreach (var child in children)
        {
            var digits = new string(child.Name.Where(char.IsDigit).ToArray());
            if (int.TryParse(
                    digits,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private sealed record UsageInfo(int PlaytimeMinutes, DateTimeOffset? LastPlayed);
}
