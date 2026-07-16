# Steam-bibliotekrydder

Et lokalt Windows-program som leser Steam-biblioteket ditt, foreslår statiske
samlinger og skriver valgte samlinger tilbake til Steam-klienten.

Grensesnittet har en moderne mørk stil med bibliotekoversikt, søk,
nøkkeltall og raske valg for alle eller ingen samlinger.

Programmet:

- finner den sist brukte Steam-kontoen automatisk
- leser spill, sjangre, Steam-tagger, funksjoner og spilletid lokalt
- viser alle forslag før noe endres
- oppretter samlinger med prefikset `Auto •`
- kan erstatte eksisterende egendefinerte Steam-samlinger for å unngå duplikater
- tar sikkerhetskopi av Steam-filene før skriving
- kan oppdatere egne samlinger uten å lage duplikater

## Last ned

Den ferdige Windows-versjonen ligger under
[Releases](https://github.com/LordM8YT/Steam-Libary-Organizer/releases/latest).
Åpne den nyeste utgivelsen og last ned `Steam-Bibliotek-Rydder.exe` under
**Assets**.

EXE-filen ligger som et Release-vedlegg fordi den selvstendige utgaven
inneholder .NET og derfor er større enn GitHubs filgrense for vanlig kildekode.

## Bruk

1. Start `Steam Bibliotek Rydder.exe`.
2. Vent mens biblioteket skannes.
3. Huk av samlingene du vil opprette.
4. La **Unngå duplikater** være aktivert dersom programmets samlinger skal
   overta for de gamle.
5. Klikk **Organiser samlinger**.
6. Tillat at Steam avsluttes kontrollert.
7. Start Steam igjen når programmet spør.

Samlingene vises under **Bibliotek → Samlinger** i Steam.

## Sikkerhet og personvern

Programmet trenger ikke Steam-passord eller Steam Web API-nøkkel. Biblioteket
leses fra Steams lokale filer og sendes ikke til en tredjepart.

Sikkerhetskopier lagres her:

`%LOCALAPPDATA%\SteamBibliotekRydder\Backups`

Når erstatningsvalget er aktivt, fjernes eksisterende egendefinerte samlinger
før de valgte `Auto •`-samlingene beholdes eller opprettes. Steams innebygde
Favoritter og Skjulte påvirkes ikke.

## Begrensninger

- Steam må være helt avsluttet når samlingsfilen skrives.
- Endringene er laget for Steam-klienten på denne PC-en. Steam kan selv velge
  hvordan og når klientdata synkroniseres til andre maskiner.
- Ikke-Steam-spill kategoriseres ikke i denne versjonen.

## Bygg fra kildekode

Krever .NET 8 SDK eller nyere:

```powershell
dotnet build SteamBibliotekRydder.csproj -c Release
dotnet publish SteamBibliotekRydder.csproj -c Release -r win-x64 --self-contained true
```
