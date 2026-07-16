namespace SteamBibliotekRydder.Models;

public sealed record SteamContext(
    string SteamPath,
    string SteamExePath,
    SteamAccount Account);
