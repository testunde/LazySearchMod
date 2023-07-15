# LazySearchMod
A lazy search mod for blocks in VintageStory

## Development/Building Prerequisites
- Vintage Story game installation
- .NET SDK
- IDE: Visual Studio Code with C# extension

## Environment Variables
Those env. variables are used in `.csproj` and `launch.json`.
- `VINTAGE_STORY`: points to the where the game is installed (thus i.e. `/usr/share/vintagestory` or `%AppData%\Vintagestory`)
- `VINTAGE_STORY_DATA`: points to the user data directory (thus i.e. `$HOME/.config/VintagestoryData` or `%AppData%\VintagestoryData`)

## In-Game Usage
Syntax: `/lz [radius from player position] [string which is searched in block path]` - searches for blocks in the world; set radius `< 0` to clear all markings

Syntax: `/lz_mb [optional:maxBlocks]` - get/set maximal blocks to highlight

! Be aware that a too-high used `radius` at a too-high set `maxBlock` may freeze a game for the time it needs to search, as it runs on the main thread. ETA depends on your PC specifications.

## Acknowledgement
Base project setup from `copygirl` (https://github.com/copygirl/howto-example-mod), thanks to You!

## Known Issues
- may draw over chatbox, in-world huds (drawn over blocks) and inventories if standing too near (as they appear to be not rendered in the dedicated "GUI" rendering step)
- may freeze the game if search radius is too high as search is done in main thread
