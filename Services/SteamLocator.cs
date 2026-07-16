using System.Diagnostics;
using Microsoft.Win32;
using SteamBibliotekRydder.Models;

namespace SteamBibliotekRydder.Services;

public sealed class SteamLocator
{
    private const ulong SteamIdBase = 76561197960265728UL;

    public SteamContext DetectMostRecent()
    {
        var steamPath = DetectSteamPath();
        var steamExe = Path.Combine(steamPath, "steam.exe");
        var accounts = DetectAccounts(steamPath);
        var account = accounts
            .OrderByDescending(item => item.IsMostRecent)
            .ThenByDescending(item => Directory.GetLastWriteTimeUtc(item.UserDataPath))
            .FirstOrDefault();

        if (account is null)
        {
            throw new InvalidOperationException(
                "Fant Steam, men ingen innlogget konto i config\\loginusers.vdf.");
        }

        return new SteamContext(steamPath, steamExe, account);
    }

    public IReadOnlyList<SteamAccount> DetectAccounts(string steamPath)
    {
        var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath))
        {
            return [];
        }

        var root = VdfTextParser.ParseFile(loginUsersPath);
        var users = root["users"];
        if (users is null)
        {
            return [];
        }

        var accounts = new List<SteamAccount>();
        foreach (var user in users.Children)
        {
            if (!ulong.TryParse(user.Name, out var steamId64) || steamId64 < SteamIdBase)
            {
                continue;
            }

            var accountId = checked((uint)(steamId64 - SteamIdBase));
            var userDataPath = Path.Combine(steamPath, "userdata", accountId.ToString());
            if (!Directory.Exists(userDataPath))
            {
                continue;
            }

            accounts.Add(new SteamAccount(
                steamId64,
                accountId,
                user["AccountName"]?.Value ?? string.Empty,
                user["PersonaName"]?.Value ?? string.Empty,
                userDataPath,
                user["MostRecent"]?.Value == "1"));
        }

        return accounts;
    }

    public bool IsSteamRunning() => Process.GetProcessesByName("steam").Length > 0;

    public async Task<bool> ShutdownSteamAsync(
        SteamContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!IsSteamRunning())
        {
            return true;
        }

        if (!File.Exists(context.SteamExePath))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = context.SteamExePath,
            Arguments = "-shutdown",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSteamRunning())
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return !IsSteamRunning();
    }

    public void StartSteam(SteamContext context)
    {
        if (!File.Exists(context.SteamExePath))
        {
            throw new FileNotFoundException("Fant ikke steam.exe.", context.SteamExePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = context.SteamExePath,
            UseShellExecute = true
        });
    }

    private static string DetectSteamPath()
    {
        var candidates = new List<string?>();

        using (var currentUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
        {
            candidates.Add(currentUser?.GetValue("SteamPath") as string);
            candidates.Add(Path.GetDirectoryName(currentUser?.GetValue("SteamExe") as string));
        }

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var localMachine = RegistryKey
                .OpenBaseKey(RegistryHive.LocalMachine, view)
                .OpenSubKey(@"SOFTWARE\Valve\Steam");
            candidates.Add(localMachine?.GetValue("InstallPath") as string);
        }

        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam"));

        var path = candidates
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => Path.GetFullPath(item!))
            .FirstOrDefault(item =>
                File.Exists(Path.Combine(item, "steam.exe")) &&
                File.Exists(Path.Combine(item, "appcache", "appinfo.vdf")));

        return path ?? throw new DirectoryNotFoundException(
            "Steam-installasjonen ble ikke funnet automatisk.");
    }
}
