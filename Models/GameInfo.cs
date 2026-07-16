namespace SteamBibliotekRydder.Models;

public sealed class GameInfo
{
    public uint AppId { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsInstalled { get; init; }

    public int PlaytimeMinutes { get; init; }

    public DateTimeOffset? LastPlayed { get; init; }

    public HashSet<int> GenreIds { get; init; } = [];

    public HashSet<int> TagIds { get; init; } = [];

    public HashSet<int> FeatureCategoryIds { get; init; } = [];

    public double PlaytimeHours => Math.Round(PlaytimeMinutes / 60d, 1);
}
