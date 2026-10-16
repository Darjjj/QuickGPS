# QuickGPS (GTA IV) — mod log

## Target
- GTA IV: The Complete Edition, Steam 12210, `C:\Steam\steamapps\common\Grand Theft Auto IV\GTAIV`
- GTAIV.exe **1.2.0.59**. Already installed: ASI loader (`dinput8.dll`), FusionFix, DLSS-IV, XboxRainDroplets, LibertyCityPlates.
- No anti-cheat (single-player only).

## Idea
Hold a hotkey (default Tab) → panel at top-left listing every mission giver on the map plus the
closest shop/service of each kind. Pick one → GPS route to it.

## Route chosen
ScriptHookDotNet (C#), **Tomasak's CE fork** (github.com/Tomasak/gta4_scripthookdotnet), because:
- classic HazardX/arinc9 SHDN and IV-SDK .NET only support 1.0.7.0 / 1.0.8.0; Tomasak's fork supports CE 1.2.0.59
  (Priler's Nexus fork #1217 is an alternative; needs aCompleteEditionHook.asi).
- SHDN compiles plain `.cs` files in `GTAIV\scripts\` at runtime → no build toolchain needed.

## API facts (read from SHDN source, commit Aug 2024)
- `Blip.GetAllBlipsOfType(BlipType)` wraps GET_FIRST/NEXT_BLIP_INFO_ID. BlipType: Vehicle1 Ped2 Object3 Coordinate4 Contact5 Pickup6 Unknown7 Pickup2=8.
- Mission givers are `BlipType.Contact` blips (ADD_BLIP_FOR_CONTACT) + `Person_*` icons.
- `Blip.Icon` = GET_BLIP_SPRITE; enum BlipIcon 0..94 (Building_*, Activity_*, Person_*). EFLC sprites fall outside the enum.
- `Blip.RouteActive = true` = SET_ROUTE (draws GPS line). `Blip.AddBlip(Vector3)` makes a coord blip.
- No native to *place* the player's map waypoint in IV; using our own coord blip + route instead.
- Drawing: `PerFrameDrawing` event, `e.Graphics.Scaling = FontScaling.Pixel`, `DrawRectangle(cx, cy, w, h, color)` (centre-based), `DrawText(text, x, y, color, font)`.
- Settings: `Settings.GetValueKey("Hotkey","Settings",Keys.Tab)` reads `QuickGPS.ini` beside the script.
- Runtime compiler is CodeDom (old C#) → no `$""`, `?.`, `nameof`, `=>` members.

## Status / next
- [x] Script written: `QuickGPS/scripts/QuickGPS.cs` + `QuickGPS.ini`
- Controls changed to mouse wheel (GET_MOUSE_WHEEL native via GTA.Native.Function.Call + Pointer(typeof(int))) and left click (GetAsyncKeyState LButton, edge-detected). Player keeps control; weapon holstered + Ped.CanSwitchWeapons=false while open. Tick Interval=0 so wheel deltas are read every frame.
- [x] 2026-10-15: installed Tomasak SHDN v1.7.1.8 (zip sha256 5669e442…9058) into `GTAIV\`: ScriptHookDotNet.asi, ScriptHook.dll, aCompleteEditionHook.asi, ScriptHookDotNet.readme.txt. QuickGPS.cs + .ini into `GTAIV\scripts\`. Uninstall = delete those files.
- [x] Saves + settings backed up to `Documents\GTA IV Backups\2026-10-15_2232` (restore: copy `Documents_GTA IV` back to `Documents\Rockstar Games\GTA IV`).
- [x] First in-game test: menu drew but list was empty, no errors in log.
  Cause: GET_FIRST/NEXT_BLIP_INFO_ID take a blip SPRITE id, not a blip type. SHDN's `Blip.GetAllBlipsOfType(BlipType)` is misnamed
  (its own Game.GetWaypoint only works because it loops 0..9 and hits sprite 8). Fix: scan sprites 0..255 via `GetAllBlipsOfType((BlipType)sprite)`,
  dedupe, classify by `b.Icon`, fall back to `b.Type == Contact` (GET_BLIP_INFO_ID_TYPE) for unknown sprites. Dropped the Display filter.
  Added F9 dump → `GTAIV\scripts\QuickGPS_blips.txt`.
- [x] Retest after fix: list populated. New issue: the select click reached the game (Niko shot / punched when holstered).
- Fix: WH_MOUSE_LL hook (class MouseBlocker) on its own message-loop thread, only while the menu is open; swallows select/close
  button down+up and WM_MOUSEWHEEL (delta read from MSLLHOOKSTRUCT.mouseData). Hook procs check a ping timestamp from the script
  tick and pass through when stale (>1.5 s); a Threading.Timer posts WM_QUIT to unhook. Needed because SHDN has no script
  shutdown event (scripts get Thread.Abort on device lost). Verified out of game: hook installs + unhooks cleanly (x86 test harness).
  Fallback if the hook fails: GetAsyncKeyState + GET_MOUSE_WHEEL + holster weapon.
- Menu is now a toggle (Tab opens, Tab or right click closes). Hotkey rebind row under SETTINGS; saves by rewriting only the
  Hotkey= line of scripts\QuickGPS.ini (SettingsFile.Save would drop comments). The game's pause-menu settings can't be extended.
- [x] Retest: alt-tab fine, but clicks/wheel STILL reached the game. GTAIV.exe imports DirectInput8Create → mouse is read
  via DirectInput (raw input underneath), which WH_MOUSE_LL can't block. Dead end: LL hooks.
- Fix: replaced MouseBlocker internals with a DirectInput vtable filter. Create throwaway SysMouse devices via
  IDirectInput8A and 8W (separate vtables), patch slots 9 (GetDeviceState) and 10 (GetDeviceData) with managed thunks
  (VirtualProtect + WriteIntPtr) only while the menu is open. Filters call the original, then for mouse devices only
  (GetCapabilities dwDevType&0xFF == 0x12; keyboard shares the vtable) read lZ (offset 8) + buttons (12..19) for the menu and
  zero them. Originals saved in process env vars QUICKGPS_DI_STATE_/DATA_<vtable> so a reloaded script never chains onto a
  stale thunk. Watchdog restores if pings stop. Out-of-game test: 2 vtables found, patch → call through filter returns the
  original HRESULT → restore OK. Not yet verified with an acquired device in game.
- [x] Retest: wheel blocked (DirectInput filter confirmed working in game). Left click still attacked.
  Cause: the menu acts on button-down and closed + restored the vtable while the button was still held, so the game's next
  GetDeviceState saw it down. Fix: on close, filter enters "drain" (keeps zeroing buttons, stops collecting) until
  GetAsyncKeyState says select/close buttons are up for 3 ticks (10 s cap), then restores; fallback unholster waits too.
- [x] Retest: left click works. Then removed clear-route row/key; added hold-to-open (HotkeyHoldSeconds, now 1 s) with a fill bar.
- 2026-10-15 23:04 CRASH at startup: ScriptHookDotNet.log stops at "Loading dynamic scriptfile 'scripts\QuickGPS.cs'";
  WER: faulting module scripthook.dll 0.5.1.0 offset 0x20861, 0xc0000417 (invalid CRT parameter). Source was unchanged except
  2.0f→1.0f and compiled fine, so it's the hook crashing during runtime CodeDom compilation, before any QuickGPS code ran.
  Fix: ship precompiled. ScriptHookDotNet.asi is a .NET 4 mixed-mode assembly → copy it as ScriptHookDotNet.dll and use it as the
  csc reference (real API check, no stub). csc 4.0 fails on long output paths (CS2021) → build in %TEMP%\qgps-build.
  Layout now: src\QuickGPS.cs, scripts\QuickGPS.net.dll + .ini, build.cmd. Removed QuickGPS.cs from GTAIV\scripts (would load twice).
  Note: earlier crashes at 22:15 (before mod install) and 22:33 (ntdll, unknown module) are unrelated exit crashes.
- [x] Retest startup with the precompiled DLL: OK.
- Bug: route worked for Faustin but not Roman / Little Jacob (notification said set, no GPS line).
  Likely cause: SHDN Blip.Position for Contact/Coordinate blips reads memory (blip+0x30/34/38, offsets from old versions;
  GET_BLIP_COORDS never worked in SHDN, all variants commented out), so our copied coord blip landed in the wrong place.
  Fix: SET_ROUTE on the game's own blip (Entry.Source) instead of a copy; fallback marker only if the source is gone.
  ClearRoute never deletes game blips (ownsRouteBlip flag). Arrival uses the live blip position, skipped when implausible.
  Menu shows "--" for implausible positions. Unconfirmed until retest; F9 dump would show the bad coords.
- [x] Retest: Roman / LJ STILL no route even with SET_ROUTE on the game's own blip → position wasn't (only) the problem.
  F9 failed: scripthook.log "[ERROR] Native function named "GET_BLIP_NAME" is not available." → Blip.Name returns garbage →
  AccessViolationException in String..ctor (corrupted-state exception, catch{} doesn't catch it). Removed all Blip.Name get/set.
  Dump now adds rawDisplay (GET_BLIP_INFO_ID_DISPLAY via Function.Call<int>) and blip memory address.
  Hypothesis: the Roman/LJ entries are hidden blips (display filter was removed during the sprite fix).
- [x] F9 dump (23:18, player at 892,-500 Hove Beach): 13 blips. LJ = sprite 26 Contact rawDisplay 2 at (1332,115,31), 756 m;
  Faustin = sprite 28 Contact rawDisplay 2 at (1329,-847,7). Positions plausible; not hidden (contacts 2, coord blips 4).
  SHDN's Blip.Display reports "ArrowOnly" for everything (Invoke<b8> truncation) — don't trust it, use the raw native.
  No Roman blip present at that moment. Hidden-blip hypothesis is wrong.
  New hypothesis: GTA IV only draws the GPS route while the player is in a vehicle; Faustin may have been tested from a car.
- User: Faustin ALSO failed with the SET_ROUTE-on-game-blip build → that approach doesn't work for any contact
  (likely: SET_ROUTE ignored on blips owned by other scripts). The only success was the own-coord-blip build.
- Fix: back to our own coord blip (Misc_Destination, yellow) at the blip position (dump shows positions are correct).
  Notification says the route line appears once in a vehicle when on foot. Added on-screen hint (ShowRouteHint):
  "GPS: <place> <distance> <ahead/left/...>" from Game.CurrentCamera.Direction, computed in the tick, drawn top-left.
- [ ] Retest: pick LJ/Faustin, get in a car → route line? On foot → yellow marker on radar + hint.
- Back up saves first: `%USERPROFILE%\Documents\Rockstar Games\GTA IV\savegames`.

## v1.0.0 release prep (2026-10-16)
- User confirmed everything works in game (yellow marker routing, hint, hold-to-open 1 s, click blocking).
- Optimizations: full blip scan (256 sprite queries) only on open + every 3 s; 250 ms distance refresh from known blips;
  menu rows/distance strings precomputed into an immutable MenuView swapped in by the tick (draw reads only);
  route hint/arrival every 100 ms instead of every frame; Environment.TickCount instead of DateTime.Now;
  MouseBlocker.Start lock-free fast path, Stop no-op when unpatched; removed dead "route on game blip" code; fonts set up once.
- Tests outside game: DirectInput filter test with the real build; 14 logic checks (sorting, selection follow, line map,
  empty map, direction words, timer) against the stub API. All passed.
- Added AssemblyVersion 1.0.0, package.ps1 (zip with "/" entry paths), README rewritten for public release with credits.

## v1.1.0: controller support (2026-10-16)
- Request: hold D-pad left (1 s) to open, D-pad up/down to move, A select, B cancel.
- Research: GTA IV button IDs for IS_BUTTON_PRESSED (from ClonkAndre/ClonksCodingLib.GTAIV Enums.cs): DPAD_UP 8, DOWN 9,
  LEFT 10, RIGHT 11, A 16, B 17, X 14, Y 15. NativeUI-IV (ClonkAndre) reads IS_BUTTON_PRESSED and "blocks" by
  CanControlCharacter=false + TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME "spcellphone"; no IV mod found that truly hides pad input.
- GTAIV.exe does NOT import XInput statically (only DINPUT8 in the import table); it LoadLibrary's xinput9_1_0.dll and
  GetProcAddress's XInputGetState into a variable. Approach: scan GTAIV.exe writable sections for a DWORD equal to the
  XInputGetState address and point it at a managed filter (VirtualProtect + WriteIntPtr); retries every 2 s up to 15x in case
  the game resolves it late; slots/orig remembered in env vars QUICKGPS_XI_SLOTS / QUICKGPS_XI_ORIG for script reloads;
  watchdog restores when pings stop. Filter rewrites dwPacketNumber when it changes buttons (games skip unchanged packets).
- D-pad left is hidden while held; released before HotkeyHoldSeconds -> 120 ms replayed tap (weapon switch/radio on release).
  Menu open: hides UP/DOWN/LEFT/A/B, auto-repeat 400/110 ms. Close: held buttons hidden until released.
- Fallback if no slot found: poll XInputGetState directly (game sees buttons), footer "[fallback input]", holster.
- Tests outside game: 21 PadInput.Process checks pass; filter install/call-through/restore on a planted slot passes
  (no controller connected -> 1167 passthrough). NOT yet verified in game (slot discovery is the unknown).

## EFLC contact names (2026-10-16)
- F9 dumps from TLAD and TBoGT: unknown contact sprites TLAD 101 (-1718,361), 106 (-1838,281); TBoGT 97 (-74,-226),
  103 (-461,146). Coordinate sprites 110/112 inside the Lost MC clubhouse (activities, not contacts).
- No public sprite table covers EFLC (GTA Connected + GTAMods wikis and ClonkAndre's enum all stop at 94 BLIP_BASE).
- Map Genie (mapgenie.io/grand-theft-auto-4) mapData has TLAD/TBoGT mission categories with "Quest Giver" and lat/lng.
  Fitted affine lat/lng -> game XY on 11 shops from the dumps (max error 11 m; two held-out safehouses 5-9 m).
  Matches: 97 Tony Prince (4 m), 103 Tony Prince (10 m), 106 Jim Fitzgerald (6 m), 101 Billy Grey (47 m; next is
  Stubbs 75 m, who unlocks much later). 110/112 ambiguous between Pool/Arm Wrestling/Cards (26-36 m) -> left unnamed.
- Automatic naming: tools/mapgenie_points.txt (Map Genie TLAD/TBoGT quest-giver mission starts + 66 neighbourhood labels)
  -> tools/make_contact_table.py (affine fit, merges a giver's stacked markers within 40 m) -> src/KnownPlaces.cs
  (25 giver places, 66 areas). Unknown Contact blips: nearest same-episode giver within 60 m, else "Contact (<area>)".
  build.cmd now compiles src\*.cs in %TEMP%\qgps-build\src. 7 naming checks pass against the stub API.
- Main game too: Map Genie "Mission" (88 with quest giver, 22 characters) tagged G/GTAIV and "Random Encounter" (31,
  episode not given -> "ANY"). Generic icons get a specific name within 60 m: Person_Male/Female -> RandomCharacters,
  Person_Unknown/Misc_Boss -> MissionGivers; otherwise the generic label stays. Table: 70 giver places, 31 random, 66 areas.
  Real-data check: the "Stranger (male)" at (863,-558) from the first dump resolves to Brian Meech (early-game encounter).
