using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SteamBibliotekRydder.Models;

namespace SteamBibliotekRydder.Services;

public sealed class SteamCollectionWriter
{
    private const string CollectionKeyPrefix = "user-collections.";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly char[] IdAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly SteamLocator _locator;

    public SteamCollectionWriter(SteamLocator locator)
    {
        _locator = locator;
    }

    public CollectionWriteResult Write(
        SteamContext context,
        IReadOnlyList<CategorySuggestion> selectedSuggestions,
        bool replaceExistingCustomCollections)
    {
        if (_locator.IsSteamRunning())
        {
            throw new InvalidOperationException(
                "Steam kjører fortsatt. Avslutt Steam helt før samlingene skrives.");
        }

        return WriteToFile(
            context.Account.CloudStoragePath,
            context.Account.LocalConfigPath,
            selectedSuggestions,
            replaceExistingCustomCollections,
            CreateBackupRoot());
    }

    internal CollectionWriteResult WriteToFile(
        string cloudStoragePath,
        string? localConfigPath,
        IReadOnlyList<CategorySuggestion> selectedSuggestions,
        bool replaceExistingCustomCollections,
        string backupRoot)
    {
        if (!File.Exists(cloudStoragePath))
        {
            throw new FileNotFoundException(
                "Fant ikke Steams samlingsfil for denne kontoen.",
                cloudStoragePath);
        }

        var originalText = File.ReadAllText(cloudStoragePath, Encoding.UTF8);
        var root = JsonNode.Parse(originalText) as JsonArray
                   ?? throw new InvalidDataException(
                       "Steams samlingsfil har et ukjent JSON-format.");

        var records = ReadRecords(root);
        var maxVersion = records.Count == 0
            ? 0
            : records.Max(record => record.Version);
        var nextVersion = maxVersion + 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var activeCustomCollections = records
            .Where(record =>
                !record.IsDeleted &&
                record.Id?.StartsWith("uc-", StringComparison.Ordinal) == true &&
                !string.IsNullOrWhiteSpace(record.Name))
            .ToArray();

        var generated = activeCustomCollections
            .Where(record =>
                record.Name?.StartsWith(
                    CollectionCategorizer.CollectionPrefix,
                    StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(record => record.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.Version).First(),
                StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        var removed = 0;
        var keptRecords = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        var removedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var suggestion in selectedSuggestions)
        {
            if (generated.TryGetValue(suggestion.Name, out var existing))
            {
                UpdateCollectionRecord(
                    existing.Record,
                    existing.Id!,
                    suggestion,
                    now,
                    nextVersion++);
                keptRecords.Add(existing.Record);
                updated++;
                continue;
            }

            var id = GenerateCollectionId(records.Select(record => record.Id));
            root.Add(CreateCollectionPair(
                id,
                suggestion,
                now,
                nextVersion++));
            created++;
        }

        if (replaceExistingCustomCollections)
        {
            foreach (var existing in activeCustomCollections.Where(item =>
                         !keptRecords.Contains(item.Record)))
            {
                MarkDeleted(existing.Record, now, nextVersion++);
                if (!string.IsNullOrWhiteSpace(existing.Id))
                {
                    removedIds.Add(existing.Id);
                }

                removed++;
            }

            foreach (var showcase in records.Where(record =>
                         !record.IsDeleted &&
                         record.Key.StartsWith("showcases.", StringComparison.Ordinal) &&
                         record.ReferencedCollectionId is not null &&
                         removedIds.Contains(record.ReferencedCollectionId)))
            {
                MarkDeleted(showcase.Record, now, nextVersion++);
            }
        }

        var serialized = root.ToJsonString();

        _ = JsonDocument.Parse(serialized);

        var backupDirectory = CreateBackup(
            backupRoot,
            cloudStoragePath,
            localConfigPath);
        AtomicWrite(cloudStoragePath, serialized, validateJson: true);
        RemoveDeletedCollectionsFromLocalConfig(localConfigPath, removedIds);

        return new CollectionWriteResult(
            created,
            updated,
            removed,
            backupDirectory,
            cloudStoragePath);
    }

    private static List<CollectionRecord> ReadRecords(JsonArray root)
    {
        var result = new List<CollectionRecord>();

        foreach (var node in root)
        {
            if (node is not JsonArray pair ||
                pair.Count < 2 ||
                pair[1] is not JsonObject record)
            {
                continue;
            }

            var key = GetString(record["key"]);
            var version = int.TryParse(GetString(record["version"]), out var parsedVersion)
                ? parsedVersion
                : 0;

            string? id = null;
            string? name = null;
            string? referencedCollectionId = null;
            var valueText = GetString(record["value"]);
            if (!string.IsNullOrWhiteSpace(valueText))
            {
                try
                {
                    using var value = JsonDocument.Parse(valueText);
                    if (value.RootElement.ValueKind == JsonValueKind.Object &&
                        value.RootElement.TryGetProperty("id", out var idElement))
                    {
                        id = idElement.GetString();
                    }

                    if (value.RootElement.ValueKind == JsonValueKind.Object &&
                        value.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        name = nameElement.GetString();
                    }

                    if (value.RootElement.ValueKind == JsonValueKind.Object &&
                        value.RootElement.TryGetProperty(
                            "strCollectionId",
                            out var collectionIdElement))
                    {
                        referencedCollectionId = collectionIdElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Keep unrelated records byte-for-byte through JsonNode.
                }
            }

            result.Add(new CollectionRecord(
                pair,
                record,
                key,
                id,
                name,
                version,
                record["is_deleted"]?.GetValue<bool>() == true,
                referencedCollectionId));
        }

        return result;
    }

    private static JsonArray CreateCollectionPair(
        string id,
        CategorySuggestion suggestion,
        long timestamp,
        int version)
    {
        var key = CollectionKeyPrefix + id;
        return new JsonArray(
            key,
            CreateRecordObject(key, id, suggestion, timestamp, version));
    }

    private static JsonObject CreateRecordObject(
        string key,
        string id,
        CategorySuggestion suggestion,
        long timestamp,
        int version)
    {
        return new JsonObject
        {
            ["key"] = key,
            ["timestamp"] = timestamp,
            ["value"] = CreateCollectionValue(id, suggestion),
            ["version"] = version.ToString(),
            ["conflictResolutionMethod"] = "custom",
            ["strMethodId"] = "union-collections"
        };
    }

    private static void UpdateCollectionRecord(
        JsonObject record,
        string id,
        CategorySuggestion suggestion,
        long timestamp,
        int version)
    {
        var key = CollectionKeyPrefix + id;
        record["key"] = key;
        record["timestamp"] = timestamp;
        record["value"] = CreateCollectionValue(id, suggestion);
        record["version"] = version.ToString();
        record["conflictResolutionMethod"] = "custom";
        record["strMethodId"] = "union-collections";
        record.Remove("is_deleted");
    }

    private static string CreateCollectionValue(
        string id,
        CategorySuggestion suggestion)
    {
        var filterGroups = new JsonArray
        {
            CreateFilterGroup(true),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false),
            CreateFilterGroup(false)
        };

        var appIds = new JsonArray();
        foreach (var appId in suggestion.AppIds.Distinct().OrderBy(item => item))
        {
            appIds.Add(appId);
        }

        var value = new JsonObject
        {
            ["id"] = id,
            ["name"] = suggestion.Name,
            ["added"] = appIds,
            ["removed"] = new JsonArray(),
            ["filterSpec"] = new JsonObject
            {
                ["nFormatVersion"] = 2,
                ["strSearchText"] = string.Empty,
                ["filterGroups"] = filterGroups,
                ["setSuggestions"] = new JsonObject()
            }
        };

        return value.ToJsonString();
    }

    private static JsonObject CreateFilterGroup(bool acceptUnion) =>
        new()
        {
            ["rgOptions"] = new JsonArray(),
            ["bAcceptUnion"] = acceptUnion
        };

    private static void MarkDeleted(
        JsonObject record,
        long timestamp,
        int version)
    {
        record["timestamp"] = timestamp;
        record["version"] = version.ToString();
        record["is_deleted"] = true;
        record.Remove("value");
        record.Remove("conflictResolutionMethod");
        record.Remove("strMethodId");
    }

    private static string GenerateCollectionId(IEnumerable<string?> existingIds)
    {
        var existing = existingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        while (true)
        {
            var random = new byte[12];
            RandomNumberGenerator.Fill(random);

            var chars = new char[12];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = IdAlphabet[random[i] % IdAlphabet.Length];
            }

            var id = "uc-" + new string(chars);
            if (!existing.Contains(id))
            {
                return id;
            }
        }
    }

    private static string GetString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return node?.ToJsonString() ?? string.Empty;
        }
    }

    private static string CreateBackupRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamBibliotekRydder",
            "Backups");

    private static string CreateBackup(
        string backupRoot,
        string cloudStoragePath,
        string? localConfigPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupDirectory = Path.Combine(backupRoot, timestamp);
        var suffix = 1;
        while (Directory.Exists(backupDirectory))
        {
            backupDirectory = Path.Combine(backupRoot, $"{timestamp}-{suffix++}");
        }

        Directory.CreateDirectory(backupDirectory);
        File.Copy(
            cloudStoragePath,
            Path.Combine(backupDirectory, Path.GetFileName(cloudStoragePath)),
            true);

        if (!string.IsNullOrWhiteSpace(localConfigPath) &&
            File.Exists(localConfigPath))
        {
            File.Copy(
                localConfigPath,
                Path.Combine(backupDirectory, Path.GetFileName(localConfigPath)),
                true);
        }

        return backupDirectory;
    }

    private static void AtomicWrite(
        string path,
        string content,
        bool validateJson)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, content, Utf8WithoutBom);
            if (validateJson)
            {
                using var stream = File.OpenRead(tempPath);
                _ = JsonDocument.Parse(stream);
            }

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void RemoveDeletedCollectionsFromLocalConfig(
        string? localConfigPath,
        IReadOnlySet<string> removedIds)
    {
        if (removedIds.Count == 0 ||
            string.IsNullOrWhiteSpace(localConfigPath) ||
            !File.Exists(localConfigPath))
        {
            return;
        }

        var content = File.ReadAllText(localConfigPath, Encoding.UTF8);
        var pattern = new Regex(
            "^(?<prefix>\\s*\"user-collections\"\\s*)(?<quoted>\"(?:\\\\.|[^\"])*\")(?<suffix>\\s*)$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var match = pattern.Match(content);
        if (!match.Success)
        {
            return;
        }

        string? jsonText;
        try
        {
            jsonText = JsonSerializer.Deserialize<string>(match.Groups["quoted"].Value);
        }
        catch (JsonException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(jsonText) ||
            JsonNode.Parse(jsonText) is not JsonObject collections)
        {
            return;
        }

        var changed = false;
        foreach (var id in removedIds)
        {
            changed |= collections.Remove(id);
        }

        if (!changed)
        {
            return;
        }

        var replacement =
            match.Groups["prefix"].Value +
            JsonSerializer.Serialize(collections.ToJsonString()) +
            match.Groups["suffix"].Value;
        var updated =
            content[..match.Index] +
            replacement +
            content[(match.Index + match.Length)..];
        AtomicWrite(localConfigPath, updated, validateJson: false);
    }

    private sealed record CollectionRecord(
        JsonArray Pair,
        JsonObject Record,
        string Key,
        string? Id,
        string? Name,
        int Version,
        bool IsDeleted,
        string? ReferencedCollectionId);
}
