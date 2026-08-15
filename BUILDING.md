# Building from source

Void Crest Compatibility targets .NET Framework 4.7.2 and builds with the .NET SDK.

## Requirements

- Hollow Knight: Silksong installed through Steam;
- a Thunderstore profile containing BepInEx, Void Crest, Needleforge, and ModMenu;
- a .NET SDK capable of building `net472` projects.

The project does not redistribute the game assemblies or dependency DLLs. It references them from the local game installation and Thunderstore profile.

## Default paths

The project defaults to the standard Steam installation and the Thunderstore profile named `Void Playthrough`. Build from the repository root with:

```powershell
dotnet build .\src\VoidCrestMovesetCompat\VoidCrestMovesetCompat.csproj -c Release
```

If either location differs, override it without editing the project file:

```powershell
dotnet build .\src\VoidCrestMovesetCompat\VoidCrestMovesetCompat.csproj -c Release `
  -p:SilksongGameDir="D:\Games\Hollow Knight Silksong" `
  -p:ThunderstoreProfileDir="D:\Thunderstore\profiles\My Profile"
```

The compiled plugin is written to `src\VoidCrestMovesetCompat\bin\Release\net472\VoidCrestMovesetCompat.dll`.
