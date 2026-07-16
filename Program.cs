using System.Text.Json;
using SteamBibliotekRydder.Models;
using SteamBibliotekRydder.Services;

namespace SteamBibliotekRydder;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (TryRunCommandLine(args))
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static bool TryRunCommandLine(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return false;
        }

        if (args[0].Equals("--diagnose", StringComparison.OrdinalIgnoreCase))
        {
            var outputPath = args.Count > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Environment.CurrentDirectory, "diagnose.json");

            var locator = new SteamLocator();
            var scanner = new SteamLibraryScanner(locator);
            var result = scanner.Scan();
            var suggestions = new CollectionCategorizer().BuildSuggestions(result.Games);

            WriteJson(outputPath, new
            {
                steamPath = result.Context.SteamPath,
                account = result.Context.Account.ToString(),
                result.CandidateCount,
                result.AppInfoCount,
                gameCount = result.Games.Count,
                installedCount = result.Games.Count(game => game.IsInstalled),
                playedCount = result.Games.Count(game => game.PlaytimeMinutes > 0),
                tagNameCount = result.TagNames.Count,
                categories = suggestions.Select(item => new
                {
                    item.Name,
                    count = item.AppIds.Count
                }),
                sampleGames = result.Games.Take(25).Select(game => new
                {
                    game.AppId,
                    game.Name,
                    game.IsInstalled,
                    game.PlaytimeMinutes,
                    game.LastPlayed,
                    genres = game.GenreIds.Order(),
                    tags = game.TagIds.Order().Take(20),
                    features = game.FeatureCategoryIds.Order()
                })
            });
            return true;
        }

        if (args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
        {
            var outputPath = args.Count > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Environment.CurrentDirectory, "self-test.json");
            RunSelfTest(outputPath);
            return true;
        }

        if (args[0].Equals("--dry-write", StringComparison.OrdinalIgnoreCase))
        {
            var outputDirectory = args.Count > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Environment.CurrentDirectory, "dry-write");
            RunDryWrite(outputDirectory);
            return true;
        }

        if (args[0].Equals("--apply-replace", StringComparison.OrdinalIgnoreCase))
        {
            var outputPath = args.Count > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(Environment.CurrentDirectory, "apply-replace.json");
            ApplyReplacement(outputPath);
            return true;
        }

        return false;
    }

    private static void ApplyReplacement(string outputPath)
    {
        var locator = new SteamLocator();
        if (locator.IsSteamRunning())
        {
            throw new InvalidOperationException(
                "Steam må være helt avsluttet før --apply-replace kan brukes.");
        }

        var scanner = new SteamLibraryScanner(locator);
        var result = scanner.Scan();
        var selected = new CollectionCategorizer()
            .BuildSuggestions(result.Games)
            .Where(item => item.IsCheckedByDefault)
            .ToArray();
        var writeResult = new SteamCollectionWriter(locator).Write(
            result.Context,
            selected,
            replaceExistingCustomCollections: true);

        WriteJson(outputPath, new
        {
            success = true,
            selectedCount = selected.Length,
            gameCount = result.Games.Count,
            writeResult
        });
    }

    private static void RunDryWrite(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var locator = new SteamLocator();
        var scanner = new SteamLibraryScanner(locator);
        var result = scanner.Scan();
        var selected = new CollectionCategorizer()
            .BuildSuggestions(result.Games)
            .Where(item => item.IsCheckedByDefault)
            .ToArray();

        var copyPath = Path.Combine(
            outputDirectory,
            "cloud-storage-namespace-1.modified.json");
        File.Copy(result.Context.Account.CloudStoragePath, copyPath, true);
        var localConfigCopyPath = Path.Combine(
            outputDirectory,
            "localconfig.modified.vdf");
        File.Copy(
            result.Context.Account.LocalConfigPath,
            localConfigCopyPath,
            true);

        var writer = new SteamCollectionWriter(locator);
        var writeResult = writer.WriteToFile(
            copyPath,
            localConfigCopyPath,
            selected,
            true,
            Path.Combine(outputDirectory, "backups"));

        using var validation = JsonDocument.Parse(File.ReadAllText(copyPath));
        WriteJson(Path.Combine(outputDirectory, "result.json"), new
        {
            success = validation.RootElement.ValueKind == JsonValueKind.Array,
            selectedCount = selected.Length,
            gameCount = result.Games.Count,
            writeResult
        });
    }

    private static void RunSelfTest(string outputPath)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "SteamBibliotekRydderTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var cloudPath = Path.Combine(root, "cloud-storage-namespace-1.json");
            File.WriteAllText(
                cloudPath,
                """
                [["collection-bootstrap-complete",{"key":"collection-bootstrap-complete","timestamp":1,"value":"true","version":"9"}]]
                """);

            var writer = new SteamCollectionWriter(new SteamLocator());
            var first = writer.WriteToFile(
                cloudPath,
                null,
                [
                    Suggestion("Auto • Action", 10, 20),
                    Suggestion("Auto • Eventyr", 30)
                ],
                false,
                Path.Combine(root, "backups"));

            var second = writer.WriteToFile(
                cloudPath,
                null,
                [Suggestion("Auto • Action", 10, 40)],
                true,
                Path.Combine(root, "backups"));

            using var document = JsonDocument.Parse(File.ReadAllText(cloudPath));
            var live = 0;
            var deleted = 0;
            foreach (var pair in document.RootElement.EnumerateArray())
            {
                var record = pair[1];
                if (!record.GetProperty("key").GetString()!.StartsWith(
                        "user-collections.",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (record.TryGetProperty("is_deleted", out var isDeleted) &&
                    isDeleted.ValueKind == JsonValueKind.True)
                {
                    deleted++;
                }
                else
                {
                    live++;
                }
            }

            if (first.Created != 2 ||
                second.Updated != 1 ||
                second.Removed != 1 ||
                live != 1 ||
                deleted != 1)
            {
                throw new InvalidOperationException(
                    "Selvtesten ga uventede telleverdier.");
            }

            WriteJson(outputPath, new
            {
                success = true,
                first,
                second,
                live,
                deleted
            });
        }
        catch (Exception exception)
        {
            WriteJson(outputPath, new
            {
                success = false,
                error = exception.ToString()
            });
            throw;
        }
        finally
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "SteamBibliotekRydderTests");
            var resolvedRoot = Path.GetFullPath(root);
            if (resolvedRoot.StartsWith(
                    Path.GetFullPath(tempRoot),
                    StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolvedRoot))
            {
                Directory.Delete(resolvedRoot, true);
            }
        }
    }

    private static CategorySuggestion Suggestion(string name, params uint[] appIds) =>
        new()
        {
            Name = name,
            Description = "Test",
            AppIds = appIds
        };

    private static void WriteJson(string path, object value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }
}
