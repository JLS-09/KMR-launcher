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
- Choose per instance which KSP versions count as compatible (like CKAN), including "all 1.12.x"

### Mods
- Mod list built from [CKAN-meta](https://github.com/KSP-CKAN/CKAN-meta), cloned and cached locally with git
- Full local caching of the mod list, with a refresh on demand
- Search by mod name and author
- Select several mods at once, and choose a version per mod directly in the list
- Install wizard (it plans the install; actually installing comes with [M4](#roadmap)):
  - choose the target instance
  - automatic dependency resolution following the CKAN spec:
    - version ranges (`version`, `min_version`, `max_version`) with CKAN's version ordering
    - KSP compatibility (`ksp_version`, `ksp_version_min`/`max`, `ksp_version_strict`) against the instance's compatible versions
    - conflicts with new and installed mods, `provides` and `suppress_recommendations`
  - problems are shown in the wizard and block the next step, instead of crashing
  - recommendations (ticked by default), suggestions and supported mods, each with a version picker that respects the relationship's version limits; `any_of` and virtual recommendations are offered per option
  - choosing between alternatives (`any_of` and virtual modules), with a version picker per option
  - steps with nothing to show are skipped
  - summary of what will be installed, replaced and removed, with the total download size and any problems; Install is only enabled when there are none

## Roadmap

The full plan is split into milestones:

| Milestone | Goal | Status |
|---|---|---|
| **M1** | Choose between alternative dependencies (`any_of` / virtual modules) in the install wizard | ✅ Done |
| **M2** | Correct dependency resolution:<br>• version ranges<br>• per-instance compatible KSP versions (like CKAN)<br>• `ksp_version_strict`<br>• conflicts and `provides`<br>• error handling and unit tests | ✅ Done |
| **M3** | Finished install wizard:<br>• recommendations with version choice<br>• skip empty steps<br>• summary / confirm step | ✅ Done |
| **M4** | Mod installation pipeline:<br>• download with mirrors and cache<br>• hash check<br>• CKAN install directives<br>• rollback on failure | 🚧 Next |
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

**Running the tests**

The resolver logic (version ordering, KSP compatibility, conflicts, `provides`, `suppress_recommendations`) and the install wizard's steps (recommendations, module choices, skipping and the summary) are covered by xUnit tests in `KMRLauncherMvvm.Tests`:

```bash
dotnet test
```

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
- [xUnit.net](https://xunit.net/) for the unit tests

## Acknowledgements

Mod metadata comes from the [CKAN](https://github.com/KSP-CKAN) project and its contributors. KMR-launcher is not affiliated with CKAN or with Squad / Private Division.