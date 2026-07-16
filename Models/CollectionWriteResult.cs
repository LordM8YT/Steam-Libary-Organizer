namespace SteamBibliotekRydder.Models;

public sealed record CollectionWriteResult(
    int Created,
    int Updated,
    int Removed,
    string BackupDirectory,
    string CloudStoragePath);
