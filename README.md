# KMR-launcher

A Kerbal Space Program (KSP 1) launcher and mod manager built with C# and Avalonia. It manages separate KSP instances and installs mods from the official [CKAN](https://github.com/KSP-CKAN/CKAN) metadata, following the [CKAN spec](https://github.com/KSP-CKAN/CKAN/blob/master/Spec.md).

> [!WARNING]
> **Early development.** KMR-launcher is developed by one person (JLS-09) on Linux. Mods can be installed, updated and removed per instance, but the Home page and offline mode aren't there yet, and Windows and macOS are not supported for now. See the [roadmap](#roadmap) below.

## Features

### Instances
- New instance dialog (also from inside the install flow when you have no instance yet):
  - from a KSP zip: name, KSP version (with the version detected from the build), primary instance and compatible KSP versions; the zip is unpacked into `~/.local/share/KMR/instances/<name>` (or a folder you pick, which is remembered), with progress, and Cancel removes the half-made folder
  - from a KSP folder you already have (for example Steam's): copy it into KMR or use it in place. Mods installed by CKAN are read from its registry (picked mods and dependencies, the files each one owns) and CKAN's compatible KSP versions are taken over, so they can be updated and removed like any other mod. Folders in GameData that no mod owns are left alone and never overwritten. A folder that was already a KMR instance comes back with its own data.
- Instances page: every instance with its KSP version, mod count and when it was last played; sort by last played or name
- Instance stats: mods (picked / dependencies), playtime, last played and install size
- Launch an instance through Wine (Linux only), from the Instances page or the primary instance in the sidebar
- While KSP runs, the sidebar shows which instance is running and for how long, with a Stop button
- Settings per instance:
  - name, primary instance, and the Wine prefix folder (a separate prefix per instance)
  - launch arguments (e.g. `-force-glcore`)
  - which KSP versions count as compatible (like CKAN), including "all 1.12.x"; the game's own version is always included
  - forget the instance (it's taken off the list and nothing on disk changes; add it again with Import), or delete it together with its prefix

### Finding mods (Discover)
- Mod list built from [CKAN-meta](https://github.com/KSP-CKAN/CKAN-meta), cloned and cached locally with git, with a refresh on demand
- Search by name, identifier and author; filter by tag and kind, "only compatible" and "hide installed"
- Every mod is marked against an instance of your choice: fits, installed, update available, or not for that KSP version
- Details panel: description, authors, license, tags, links, KSP compatibility, dependencies, how many mods need it, download counts, and every version with the ones that fit the instance highlighted
- Pick several mods (and a version per mod) and install them together

### Installing, updating and removing mods
- Install dialog inside the main window: instance → extras → choices → review → install, with a side panel that shows what the change does (new, updated and removed mods, download size, what's already cached, problems)
- Dependency resolution following the CKAN spec:
  - version ranges (`version`, `min_version`, `max_version`) with CKAN's version ordering
  - KSP compatibility (`ksp_version`, `ksp_version_min`/`max`, `ksp_version_strict`) against the instance's compatible versions
  - conflicts with new and installed mods, `provides` and `suppress_recommendations`
  - installed mods keep their own dependencies: an update that would break one is flagged
  - mods installed by hand count as installed, like in CKAN: a `.dll` in GameData that no mod owns (outside `Squad`) stands for the mod it's named after (`ModuleManager.4.2.3.dll` → `ModuleManager`), at any version. It isn't installed again as a dependency, it can be a conflict, and a mod you pick yourself is installed over it
  - problems are shown in the dialog and block the next step, instead of crashing
- Recommendations (ticked by default), suggestions and supported mods, each with a version picker; choices between alternatives (`any_of` and virtual modules); steps with nothing to show are skipped
- Installing works like CKAN:
  - several downloads at the same time (a setting, 4 by default), and each mod is installed as soon as its download is done
  - download mirrors are tried in order, every download is checked against its `download_hash`, and downloads are cached so reinstalls and other instances don't download again
  - CKAN install directives (`file`, `find`, `find_regexp`, `install_to`, `as`, filters and `include_only`), matched the way CKAN does
  - metapackages are supported; DLC can't be installed but is detected
  - the files of every installed mod are recorded, and a mod that would overwrite a file it doesn't own stops the install
  - if anything fails, or you cancel, everything is rolled back and the instance is left unchanged; the failure screen shows what went wrong, where each mod got to, and lets you retry (cached downloads aren't downloaded again)
- Mods tab of an instance: every installed mod with why it's there (picked by you, recommended by…, dependency of…), its version, KSP compatibility and size
  - mods that don't fit the instance's KSP versions are flagged
  - update one mod, a selection or everything that has a newer fitting version
  - mods that CKAN marks as replaced (`replaced_by`) can be replaced in one step
  - removing a mod also removes the mods that need it and dependencies nothing uses anymore, and says so first

### Launcher settings
- KSP zips that new instances are created from, with their version and size
- CKAN-meta: when it was last fetched and at which commit, fetch now, and whether to fetch when the launcher starts (with it off, the cached mod list is used)
- Downloads: how many run at the same time, and the download cache with how much of it installed mods use; remove unused downloads or clear the cache
- Launching: where `wine` (and `umu-run`) were found, and how much Wine prints (`WINEDEBUG`: off, errors only, or Wine's default)
- Accent colour (red, orange, green or blue), applied right away
- Version and commit of the build

## Roadmap

The full plan is split into milestones:

| Milestone | Goal | Status |
|---|---|---|
| **M1** | Choose between alternative dependencies (`any_of` / virtual modules) in the install wizard | ✅ Done |
| **M2** | Correct dependency resolution:<br>• version ranges<br>• per-instance compatible KSP versions (like CKAN)<br>• `ksp_version_strict`<br>• conflicts and `provides`<br>• error handling and unit tests | ✅ Done |
| **M3** | Finished install wizard:<br>• recommendations with version choice<br>• skip empty steps<br>• summary / confirm step | ✅ Done |
| **M4** | Mod installation pipeline:<br>• download with mirrors and cache<br>• hash check<br>• CKAN install directives<br>• rollback on failure | ✅ Done |
| **M5** | First public release, in a new design:<br>• ✅ new design for Discover, Instances and the install flow<br>• ✅ remove and update mods, `replaced_by`<br>• ✅ currently-playing card<br>• ✅ launcher Settings page<br>• ✅ new instance dialog, also from inside the install flow<br>• ✅ import an existing KSP install (with CKAN registry)<br>• code clean-up | 🚧 In progress |
| **M6** | Home page, startup window and offline mode | ⏳ Planned |
| **M7** | Extras:<br>• sorting the mod list<br>• instance export/import (`.ckan` metapackage)<br>• backup & restore<br>• extra metadata repos<br>• launching through Proton<br>• Windows and macOS support | ⏳ Planned |

## Building from source

**Requirements**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Linux, with Wine at `/usr/bin/wine` (needed to launch instances; Stop also uses `/usr/bin/wineserver`)
- A KSP 1 zip to create an instance from

```bash
git clone https://github.com/JLS-09/KMR-launcher.git
cd KMR-launcher
dotnet run
```

On first start, the launcher clones CKAN-meta. This can take a few minutes.

If a heavily modded instance (such as RP-1) crashes while loading under Wine, add `-force-glcore` to its launch arguments (the instance's Settings tab on the Instances page). It makes KSP use OpenGL directly instead of Wine's Direct3D 11 layer.

If the app still shows an old screen after pulling changes, run `dotnet clean` and build again: Avalonia can keep old compiled XAML around.

**Running the tests**

The resolver logic (version ordering, KSP compatibility, conflicts, `provides`, `suppress_recommendations`, `replaced_by`), the install flow (recommendations, choices, review, progress and failure texts), the install pipeline (install directives, downloads with mirrors and hash checks, installing and rollback) and instance management (new instances from a zip, importing a folder with a CKAN registry, removal plans, updates, settings, Wine prefixes) are covered by xUnit tests in `KMRLauncherMvvm.Tests`:

```bash
dotnet test
```

## Where data is stored

On Linux:

| Path | Contents |
|---|---|
| `~/.config/kmrLauncher/settings.json` | App settings, KSP zips, the list of instance folders, which one is primary, and the folder new instances go in |
| `<instance>/.kmr/instance.json` | The instance's name, KSP version, compatible KSP versions, launch arguments, Wine prefix path, playtime and last played |
| `<instance>/.kmr/mods.json` | The instance's installed mods and the files each one owns |
| `~/.local/share/kmrLauncher/mods.json` | Cached, parsed mod list |
| `~/.local/share/kmrLauncher/cache/ckan-meta/` | Local clone of CKAN-meta |
| `~/.local/share/kmrLauncher/cache/downloads/` | Verified mod downloads, shared by all instances |
| `~/.local/share/KMR/instances/` | New instances, unless you pick another folder in the New instance dialog |
| `~/.config/kmrLauncher/prefixes/<instance folder>/` | Wine prefix for each instance (the path is saved per instance and can be changed in its Settings tab; prefixes made before 2026-11 stay in `~/.config/prefixes/<instance name>/`) |
| `~/.local/share/kmrLauncher/sync.json` | Time and commit of the last CKAN-meta fetch, and the last fetch error |

An instance folder carries its own mod information, so it can be moved, copied or backed up with its mods. If a folder is moved or deleted, the instance shows as missing on the Instances page, where you can locate the folder or remove the instance from the list. Don't edit `.kmr/` by hand.

During an install, files of mods that are being removed or replaced are kept in `.kmr/staging/` inside the instance folder until the install has finished.

Versions from before November 2026 kept everything in `settings.json`. On the first start of a newer version, each instance's data is moved into its `.kmr/` folder and the old file is kept as `settings.json.bak`. If that fails, the launcher keeps using the old file, shows why, and tries again at the next start.

## Built with

- [Avalonia UI](https://avaloniaui.net/) 12 for the user interface
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for the MVVM structure
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) to clone and update CKAN-meta
- [Humanizer](https://github.com/Humanizr/Humanizer) to format times and sizes
- `Microsoft.Extensions.DependencyInjection` for dependency injection
- [xUnit.net](https://xunit.net/) for the unit tests

## Acknowledgements

Mod metadata comes from the [CKAN](https://github.com/KSP-CKAN) project and its contributors. KMR-launcher is not affiliated with CKAN or with Squad / Private Division.