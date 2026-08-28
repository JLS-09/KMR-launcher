# KMR-launcher

## Disclaimer

This project is under heavy development, a lot of the features are not yet implemented. This project is currently only being developed by me (JLS-09) on Linux, so the chances that this project currently works on Windows or macOS are very slim. Official support for Windows and macOS will come at a later date.

## Implemented features

- Adding zips of ksp to the launcher for automatic instance creation (with version detection)
- Creating new instances
- Deleting instances
- Playing instance (only on Linux through wine)
- Instance stats tracking (mod count, playtime, last played and instance size)
- Browsing mods
- searching mods on name and authors
- Full caching of mod list
- Refreshing mod list on demand
- Multi mod select in mod list
- Direct version selection in mod list

## Planned features (In no particular order)

- No internet handling (git request step gets skipped and modlist straight from cache)
- Sorting mod list
- Apply changes button on mod list
- Entire mod installation pipeline
- Currently playing instance card above settings
- Manage instances (with installing, removing and updating mods per instance)
- Export/import instances (with ckan support)
- Integrated back-up and restore system
- Ability to add other repos

## Personal notes

- check exception catching in InstallModsSelectInstanceStepViewModel for the compatibilityService
- ToString methods for models like AnyOf and Relationship (primarily for exception messages)
- make SuppressRecommendations flag in Relationship actually do its thing
- Create extra model around version to keep extra information like the mods that depend on it


- anyOf dependency gets handled like a provides module, not in the dependency resolution but after recommendations, suggestions,... Where the user is able to choose
    - gets ignored when any one of the mods is installed
    - gets skipped in first step when there is only one option
- check compatibility between versions -> conflict checking
- also check compatibility with ksp version
- check for duplicates, already installed mods and already queued mods in the recommended/suggested/...
- implement supports list
- fix gap when there are no recommendations
- handling of user wanting to install mods without instance
- handling of user wanting to create an instance without version added
- add already existing instance to instances + ckan support
- no internet handling