# LazySearchMod
A lazy search mod for blocks in VintageStory

## Development/Building Prerequisites
- Vintage Story game installation
- .NET SDK 7.0 (or whatever the current game verison requires currently)
- IDE: Visual Studio Code with C# extension

## Environment Variables
Those env. variables are used in `.csproj` and `launch.json`.
- `VINTAGE_STORY`: points to the where the game is installed (thus i.e. `/usr/share/vintagestory` or `%AppData%\Vintagestory`)
- `VINTAGE_STORY_DATA`: points to the user data directory (thus i.e. `$HOME/.config/VintagestoryData` or `%AppData%\VintagestoryData`)

## In-Game Usage
Syntax: `/lz <radius from player position> <string (word) searched in block path>` - searches for blocks in the world

Syntax: `/lzd <radius from player position> <string (word) searched in block path> [height above player head = 0]` - searches for blocks in the world, but below the players head (optional starting above player head)

Syntax: `/lz_mb [max blocks to uncover]` - get/set maximal block-highlights as criterion for searching

Syntax: `/lz_cl` - clears any visible highlights

Syntax: `/lz_st` - stops the currently running search

## Acknowledgement
Base project setup from `copygirl` (https://github.com/copygirl/howto-example-mod), thanks to You!

## Known Issues
-
