namespace SteamBibliotekRydder.Models;

public sealed class CategorySuggestion
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<uint> AppIds { get; init; }

    public bool IsCheckedByDefault { get; init; } = true;

    public override string ToString() => $"{Name}  ({AppIds.Count})";
}
