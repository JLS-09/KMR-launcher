# KMR-launcher

A Kerbal Space Program (KSP 1) launcher and mod manager built with C# and Avalonia. It manages separate KSP instances and installs mods from the official [CKAN](https://github.com/KSP-CKAN/CKAN) metadata, following the [CKAN spec](https://github.com/KSP-CKAN/CKAN/blob/master/Spec.md).

> [!WARNING]
> **Early development.** KMR-launcher is developed by one person (JLS-09) on Linux. Mods can't be installed yet, and Windows and macOS are not supported for now. See the [roadmap](#roadmap) below.

## Features

### Instances
- Add a KSP zip and have an instance created automatically, with the KSP version detected from the build
- Create and delete instances
- Launch an instance through Wine (Linux only)
- Instance stats: mod count, playtime, last played and install size

### Mods
- Mod list built from [CKAN-meta](https://github.com/KSP-CKAN/CKAN-meta), cloned and cached locally with git
- Full local caching of the mod list, with a refresh on demand
- Search by mod name and author
- Select several mods at once, and choose a version per mod directly in the list
- Install wizard (in progress):
  - choose the target instance
  - automatic dependency resolution
  - recommendations, suggestions and supported mods
  - choosing between alternatives (`any_of` and virtual modules)

## Roadmap

The full plan is split into milestones:

| Milestone | Goal | Status |
|---|---|---|
| **M1** | Choose between alternative dependencies (`any_of` / virtual modules) in the install wizard | ✅ Done |
| **M2** | Correct dependency resolution:<br>• version ranges<br>• per-instance compatible KSP versions (like CKAN)<br>• `ksp_version_strict`<br>• conflicts and `provides`<br>• error handling and unit tests | 🚧 In progress |
| **M3** | Finished install wizard:<br>• recommendations with version choice<br>• skip empty steps<br>• summary / confirm step | ⏳ Planned |
| **M4** | Mod installation pipeline:<br>• download with mirrors and cache<br>• hash check<br>• CKAN install directives<br>• rollback on failure | ⏳ Planned |
| **M5** | Instance management:<br>• remove and update mods<br>• `replaced_by`<br>• currently-playing card<br>• create an instance from inside the wizard<br>• import an existing KSP install (with CKAN registry)<br>• offline mode | ⏳ Planned |
| **M6** | Extras:<br>• sorting the mod list<br>• instance export/import (`.ckan` metapackage)<br>• backup & restore<br>• extra metadata repos<br>• Windows and macOS support | ⏳ Planned |

## Building from source

**Requirements**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Linux, with Wine at `/usr/bin/wine` (needed to launch instances)
- A KSP 1 zip to create an instance from

```bash
git clone https://github.com/JLS-09/KMR-launcher.git
cd KMR-launcher
dotnet run
```

On first start, the launcher clones CKAN-meta. This can take a few minutes.

## Where data is stored

Everything lives under your application-data folder, which is `~/.config/kmrLauncher/` on Linux:

| Path | Contents |
|---|---|
| `settings.json` | Instances and app settings |
| `mods.json` | Cached, parsed mod list |
| `cache/ckan-meta/` | Local clone of CKAN-meta |
| `prefixes/<instance>/` | Wine prefix for each instance |

## Built with

- [Avalonia UI](https://avaloniaui.net/) 12 for the user interface
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for the MVVM structure
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) to clone and update CKAN-meta
- [Humanizer](https://github.com/Humanizr/Humanizer) to format times and sizes
- `Microsoft.Extensions.DependencyInjection` for dependency injection

## Acknowledgements

Mod metadata comes from the [CKAN](https://github.com/KSP-CKAN) project and its contributors. KMR-launcher is not affiliated with CKAN or with Squad / Private Division.