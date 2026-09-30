# Release workflow

This repository prepares separate update payloads for GitHub, Nexus Mods, and Thunderstore from one
tested DLL. Preparing the payloads does not publish, tag, commit, or upload anything.

## Source of truth

Before preparing a release, update these files to the same semantic version:

- `manifest.json`
- `src/VoidCrestMovesetCompat/VoidCrestMovesetCompat.csproj`
- `src/VoidCrestMovesetCompat/Properties/AssemblyInfo.cs`
- the `BepInPlugin` declaration in `src/VoidCrestMovesetCompat/Plugin.cs`
- `CHANGELOG.md`
- `docs/releases/<version>.md`

Use the exact DLL that completed gameplay verification. Do not silently rebuild between gameplay
approval and packaging unless that rebuilt binary is tested again.

## Prepare all platform payloads

From the repository root:

```powershell
.\tools\Prepare-Release.ps1 `
  -Version 1.1.0 `
  -TestedDllPath "<testing-profile>\BepInEx\plugins\ManthisCo-Void_Crest_Compatibility\VoidCrestMovesetCompat.dll"
```

The command creates this ignored local structure:

```text
release/updates/<version>/
├── github/
│   ├── RELEASE_NOTES.md
│   ├── SHA256SUMS.txt
│   ├── ManthisCo-Void_Crest_Compatibility-<version>.zip
│   └── Void-Crest-Compatibility-<version>-Nexus.zip
├── nexus/
│   ├── package/
│   │   ├── BepInEx/plugins/ManthisCo-Void_Crest_Compatibility/VoidCrestMovesetCompat.dll
│   │   ├── CHANGELOG.md
│   │   ├── INSTALL.md
│   │   └── README.md
│   └── Void-Crest-Compatibility-<version>-Nexus.zip
├── thunderstore/
│   ├── package/
│   │   ├── CHANGELOG.md
│   │   ├── icon.png
│   │   ├── manifest.json
│   │   ├── README.md
│   │   └── VoidCrestMovesetCompat.dll
│   └── ManthisCo-Void_Crest_Compatibility-<version>.zip
└── release-plan.json
```

The script validates the semantic version, assembly version, Thunderstore description length,
256×256 PNG icon, required package entries, canonical release notes, and the immutable published
1.0.0 DLL before creating any output.

## GitHub

1. Merge the approved release changes before tagging.
2. Draft a release using tag `v<version>` and title `Void Crest Compatibility <version>`.
3. Paste `github/RELEASE_NOTES.md` into the release body.
4. Attach both ZIP files and `github/SHA256SUMS.txt`.
5. Save it as a draft until publication is authorized.

GitHub releases are tag-based and support binary assets. Drafting first keeps the artifacts and
notes reviewable before publication:
https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository

The repository's `.github/release.yml` also groups automatically generated notes for later updates.

## Nexus Mods

1. Open the existing mod page and add the Nexus ZIP as the new main file.
2. Use `<version>` as the file version and make it unambiguous that it supersedes the older main
   file.
3. Paste the canonical release notes into the file changelog/description.
4. Keep Void Crest, Needleforge, and ModMenu listed as requirements.
5. Do not bundle any dependency or game assembly.
6. Leave the update unpublished or hidden until publication is authorized.

Nexus recommends semantic versions, clear current-file labeling, requirements, and installation
instructions:
https://help.nexusmods.com/article/136-best-practices-for-mod-authors

## Thunderstore

1. Upload only the ZIP from `thunderstore/`.
2. Confirm `manifest.json`, `README.md`, and `icon.png` are at the archive root.
3. Confirm the package version is higher than the currently published version and all dependency
   strings exist in the target community.
4. Preview the README using Thunderstore's Markdown preview.
5. Upload only after publication is authorized; Thunderstore updates are created by incrementing the
   manifest version and uploading the new package.

Thunderstore's current package requirements are documented here:
https://wiki.thunderstore.io/mods/creating-a-package

## Final cross-platform gate

- Gameplay verification completed with the packaged DLL.
- Version declarations and release notes agree.
- `tests/ReleaseVersion.Tests.ps1` passes.
- `tests/VoidCrestBaseCrestNames.Tests.ps1` passes for this update.
- `tests/DistributionStructure.Tests.ps1 -Version <version>` passes after preparation.
- GitHub, Nexus, and Thunderstore use the same tested DLL.
- SHA-256 values are recorded before upload.
- No third-party or game assemblies are present.
- Publication is explicitly authorized.
