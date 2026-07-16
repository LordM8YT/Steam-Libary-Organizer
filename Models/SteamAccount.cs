namespace SteamBibliotekRydder.Models;

public sealed record SteamAccount(
    ulong SteamId64,
    uint AccountId,
    string AccountName,
    string PersonaName,
    string UserDataPath,
    bool IsMostRecent)
{
    public string LocalConfigPath => Path.Combine(UserDataPath, "config", "localconfig.vdf");

    public string CloudStoragePath =>
        Path.Combine(UserDataPath, "config", "cloudstorage", "cloud-storage-namespace-1.json");

    public override string ToString() =>
        string.IsNullOrWhiteSpace(PersonaName)
            ? $"{AccountName} ({SteamId64})"
            : $"{PersonaName} / {AccountName} ({SteamId64})";
}
