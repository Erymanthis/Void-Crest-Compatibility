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

## Preparing distribution updates

After gameplay-testing the final DLL, prepare the GitHub, Nexus Mods, and Thunderstore payloads with:

```powershell
.\tools\Prepare-Release.ps1 `
  -Version 1.1.0 `
  -TestedDllPath "<testing-profile>\BepInEx\plugins\ManthisCo-Void_Crest_Compatibility\VoidCrestMovesetCompat.dll"

.\tests\DistributionStructure.Tests.ps1 -Version 1.1.0
```

This creates local, ignored artifacts only. It does not tag, upload, or publish. See
[`docs/RELEASING.md`](docs/RELEASING.md) for the complete cross-platform checklist.

## Experimental framework checks

The framework metadata, recipe validation, and rollback transaction can be tested without launching the game:

```powershell
./tests/CrestFrameworkFoundation.Tests.ps1
```

Validate that the released Void Crest switcher exposes in-game crest names while preserving its
legacy configuration migrations and internal provider mappings:

```powershell
./tests/VoidCrestBaseCrestNames.Tests.ps1
```

Check that all development version declarations agree and that the published 1.0.0 artifacts have
not changed:

```powershell
./tests/ReleaseVersion.Tests.ps1
```

To regenerate the managed-IL crest inventory, provide the installed game assembly and the `Mono.Cecil.dll` shipped with BepInEx:

```powershell
./tools/Inspect-CrestSystem.ps1 `
  -AssemblyCSharp "<game>/Hollow Knight Silksong_Data/Managed/Assembly-CSharp.dll" `
  -MonoCecil "<profile>/BepInEx/core/Mono.Cecil.dll" `
  -OutputPath ./docs/evidence/VANILLA_CREST_INVENTORY.md
```

An instrumented gameplay log can be reduced to its config-group timeline and relevant compatibility diagnostics with:

```powershell
./tools/Summarize-CrestRuntimeLog.ps1 `
  -LogPath "<profile>/BepInEx/LogOutput.log" `
  -OutputPath ./tmp/crest-runtime-summary.md
```

## Crest Component Lab

The clean-room lab project requires only Needleforge 0.9.0 and its dependencies in the selected
profile:

```powershell
dotnet build .\src\CrestComponentLab\CrestComponentLab.csproj -c Release `
  -p:ThunderstoreProfileDir="<clean-profile-path>"
```

Verify that the assembly has no Void Crest dependency or implementation strings:

```powershell
.\tests\CrestComponentLab.Static.Tests.ps1
```

Install it into the clean profile:

```powershell
.\tools\Install-CrestComponentLab.ps1 -ProfilePath "<clean-profile-path>"
```
