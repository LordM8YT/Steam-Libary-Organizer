namespace SteamBibliotekRydder.Models;

public sealed record ScanResult(
    SteamContext Context,
    IReadOnlyList<GameInfo> Games,
    IReadOnlyDictionary<int, string> TagNames,
    int CandidateCount,
    int AppInfoCount);
