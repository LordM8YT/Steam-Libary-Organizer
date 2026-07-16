using SteamBibliotekRydder.Models;

namespace SteamBibliotekRydder.Services;

public sealed class CollectionCategorizer
{
    public const string CollectionPrefix = "Auto • ";

    private static readonly IReadOnlyList<CategoryRule> Rules =
    [
        Genre("Action", "Actionspill", 1),
        Genre("Eventyr", "Eventyrspill", 25),
        Genre("Rollespill", "Rollespill og action-rollespill", 3),
        Genre("Strategi", "Strategi- og taktikkspill", 2),
        Genre("Simulator", "Simuleringsspill", 28),
        Genre("Racing", "Racing- og kjørespill", 9),
        Genre("Sport", "Sportsspill", 18),
        Genre("Casual", "Lettbeinte og avslappede spill", 4),
        Genre("Indie", "Indiespill", 23),
        Genre("MMO", "Massivt flerspillerspill", 29),
        Tag("FPS", "Førstepersons- og arenaskytespill", 1663, 5547, 1023537),
        Tag("Hjernetrim", "Hjernetrim, logikk og puzzle", 1664, 5537, 6129, 769306),
        Tag("Skrekk", "Skrekk og psykologisk skrekk", 1667, 1721, 3978, 42089),
        Tag("Overlevelse", "Overlevelse og overlevelsesbygging", 1662, 3978, 1100689),
        Tag("Åpen verden", "Spill med åpen verden", 1695),
        Tag("Bygging og sandkasse", "Bygging, sandkasse og automatisering",
            1643, 1702, 3810, 4328, 7332, 255534),
        Tag("Sniking", "Sniking og spionasje", 1687, 1776, 97070),
        Custom(
            "Enkeltspiller",
            "Spill med støtte for enkeltspiller",
            game => game.FeatureCategoryIds.Contains(2)),
        Custom(
            "Flerspiller",
            "Spill med flerspiller eller PvP",
            game => game.FeatureCategoryIds.Overlaps([1, 20, 27, 36, 37])),
        Custom(
            "Samarbeid",
            "Spill med samarbeid lokalt eller på nett",
            game => game.FeatureCategoryIds.Overlaps([9, 24, 38, 39, 40])),
        Custom(
            "VR",
            "Spill med VR-støtte",
            game => game.TagIds.Contains(21978) ||
                    game.TagIds.Contains(856791) ||
                    game.FeatureCategoryIds.Overlaps([52, 53, 54])),
        Custom(
            "Installert",
            "Spill som er installert lokalt akkurat nå",
            game => game.IsInstalled),
        Custom(
            "Ikke spilt",
            "Spill uten registrert spilletid",
            game => game.PlaytimeMinutes <= 0),
        Custom(
            "Nylig spilt",
            "Spill startet i løpet av de siste 180 dagene",
            game => game.LastPlayed >= DateTimeOffset.Now.AddDays(-180))
    ];

    public IReadOnlyList<CategorySuggestion> BuildSuggestions(IReadOnlyList<GameInfo> games)
    {
        var suggestions = new List<CategorySuggestion>();

        foreach (var rule in Rules)
        {
            var matches = games
                .Where(rule.Predicate)
                .Select(game => game.AppId)
                .Distinct()
                .OrderBy(appId => appId)
                .ToArray();

            if (matches.Length == 0)
            {
                continue;
            }

            suggestions.Add(new CategorySuggestion
            {
                Name = CollectionPrefix + rule.Name,
                Description = rule.Description,
                AppIds = matches,
                IsCheckedByDefault = matches.Length >= 2 ||
                                     rule.Name is "Installert" or "Ikke spilt"
            });
        }

        var mostPlayed = games
            .Where(game => game.PlaytimeMinutes > 0)
            .OrderByDescending(game => game.PlaytimeMinutes)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(20)
            .Select(game => game.AppId)
            .ToArray();

        if (mostPlayed.Length > 0)
        {
            suggestions.Add(new CategorySuggestion
            {
                Name = CollectionPrefix + "Mest spilt",
                Description = "De 20 spillene med mest registrert spilletid",
                AppIds = mostPlayed,
                IsCheckedByDefault = true
            });
        }

        return suggestions
            .OrderBy(suggestion => suggestion.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static CategoryRule Genre(string name, string description, int genreId) =>
        Custom(name, description, game => game.GenreIds.Contains(genreId));

    private static CategoryRule Tag(
        string name,
        string description,
        params int[] tagIds) =>
        Custom(name, description, game => game.TagIds.Overlaps(tagIds));

    private static CategoryRule Custom(
        string name,
        string description,
        Func<GameInfo, bool> predicate) =>
        new(name, description, predicate);

    private sealed record CategoryRule(
        string Name,
        string Description,
        Func<GameInfo, bool> Predicate);
}
