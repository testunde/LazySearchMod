# LazySearchMod
A lazy search mod for blocks in VintageStory

## Prerequisites
- Vintage Story game installation
- .NET SDK
- IDE: Visual Studio Code with C# extension

## Environment Variables
Those env. variables are used in `.csproj` and `launch.json`.
- `VINTAGE_STORY`: points to the where the game is installed (thus i.e. `/usr/share/vintagestory` or `%AppData%\Vintagestory`)
- `VINTAGE_STORY_DATA`: points to the user data directory (thus i.e. `$HOME/.config/VintagestoryData` or `%AppData%\VintagestoryData`)

## Usage
Syntax: `/lz [radius from player position] [string which is searched in block path]`

Set radius `<0` to clear all markings.

## Acknowledgement
Base project setup from `copygirl` (https://github.com/copygirl/howto-example-mod), thanks to You!
