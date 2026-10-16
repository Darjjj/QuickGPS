# QuickGPS — GTA IV

Hold **Tab** for 1 second and a panel opens in the top-left corner. It lists:

- **Mission givers**: every contact blip on the map (Roman, Little Jacob, Brucie, strangers, assassination jobs and so on), nearest first.
- **Nearest services**: the closest one of each kind (Hospital, Gun Shop, Pay 'n' Spray, Clothes Shop, Burger Shot, Internet Cafe, Safehouse, Train Station, Taxi Rank, Bar, Bowling and more).

Pick an entry and a yellow destination blip appears with a GPS route to it. The route clears itself when you arrive.

## Controls

| Input | Action |
|---|---|
| Hold Tab (1 s) | Open the menu (a bar fills while you hold; press Tab again to close) |
| Mouse wheel | Move the selection |
| Left click | Set a route to the selected entry |
| Right click | Close the menu |

Niko keeps moving while the menu is open. QuickGPS stops the game from seeing your clicks and scroll wheel while it's open, so he won't shoot, aim or switch weapons.

**Changing the hotkey in game:** open the menu, scroll to **SETTINGS > Change hotkey** at the bottom, left click it, then press the key you want. It's saved to `QuickGPS.ini` straight away. Click again to cancel. GTA IV's own pause-menu Settings screen can't be extended by scripts, which is why this option lives in the QuickGPS menu. Every other option (buttons, scroll direction and more) is in `QuickGPS.ini`.

## Requirements

- GTA IV: The Complete Edition **1.2.0.59** (your install), or 1.0.7.0 / 1.0.8.0
- An ASI loader (already installed: `dinput8.dll`)
- **ScriptHookDotNet with Complete Edition support.** Use **Tomasak's fork**:
  https://github.com/Tomasak/gta4_scripthookdotnet/releases
  (Priler's fork, https://www.nexusmods.com/gta4/mods/1217, also works.)
- .NET Framework 4.x (preinstalled on Windows 10)

## Install

1. Back up your saves: `Documents\Rockstar Games\GTA IV\savegames`.
2. Install ScriptHookDotNet from the release zip into `...\Grand Theft Auto IV\GTAIV\`. That folder holds `GTAIV.exe`. Follow the release's own readme, since it may also include `ScriptHook.dll` and `aCompleteEditionHook.asi`.
3. Copy `scripts\QuickGPS.net.dll` and `scripts\QuickGPS.ini` into `...\GTAIV\scripts\`. Create the folder if it doesn't exist.
4. Start the game, load a save, and hold Tab.

QuickGPS ships precompiled, because compiling a `.cs` script at startup can crash ScriptHookDotNet's native hook. Don't put `QuickGPS.cs` in the game's `scripts` folder as well, or it will load twice. If nothing shows up, open `ScriptHookDotNet.log` in the GTAIV folder.

## Building from source

The source is `src\QuickGPS.cs`. Run `build.cmd` (optionally pass your GTAIV folder) to rebuild `scripts\QuickGPS.net.dll`. It compiles against the game's own `ScriptHookDotNet.asi` with the .NET Framework compiler that ships with Windows.

## Notes and limits

- GTA IV has no script function for placing the player's own map waypoint. QuickGPS adds its own destination blip and turns on the GPS route for it, which looks and works like a waypoint.
- Only blips the game has actually placed are listed. Story contacts appear as they unlock, and some services only show on the map after certain missions.
- Contacts from The Lost and Damned and Ballad of Gay Tony use icons that aren't in the script's name table. They still show up, under the blip's name or as "Contact #id".
- Single-player only.
