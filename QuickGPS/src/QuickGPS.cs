// QuickGPS for GTA IV (ScriptHookDotNet)
// Hold the hotkey (default Tab, 1 s) to open a list of every mission giver on the map and the
// closest shop/service of each kind. Pick one and a GPS route is drawn to it.
//
// Controls while the menu is open (Niko can keep walking/driving):
//   mouse wheel      move the selection
//   left click       set a route to the selection (or use a SETTINGS row)
//   right click      close the menu
//   hotkey again     close the menu
//
// While the menu is open the clicks and wheel are filtered out of DirectInput so the game
// never sees them: Niko doesn't shoot, aim or switch weapons.
//
// SETTINGS > "Change hotkey" lets you rebind the hotkey in game; it's saved to QuickGPS.ini.
// Settings live in QuickGPS.ini next to this file.
// Written for the old CodeDom compiler that ScriptHookDotNet uses: keep to C# 3 syntax.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using GTA;

namespace QuickGPS
{
    public class QuickGPS : Script
    {
        const string SectionMissions = "MISSION GIVERS";
        const string SectionServices = "NEAREST SERVICES";
        const string SectionSettings = "SETTINGS";

        class Entry
        {
            public string Label;
            public string Section;
            public Vector3 Position;
            public float Distance;
            public bool IsHotkeySetting;
            public Blip Source;   // the game's own blip for this destination
        }

        class Line
        {
            public string Text;
            public bool IsHeader;
            public int EntryIndex;
        }

        // GET_FIRST/NEXT_BLIP_INFO_ID take a blip *sprite* id, not a blip type (ScriptHookDotNet's
        // Blip.GetAllBlipsOfType is misnamed). So every sprite id is scanned and blips are classified
        // by icon. EFLC sprites go past the base-game enum, hence the generous upper bound.

        static readonly Dictionary<BlipIcon, string> MissionLabels = new Dictionary<BlipIcon, string>();
        static readonly Dictionary<BlipIcon, string> ServiceLabels = new Dictionary<BlipIcon, string>();

        static QuickGPS()
        {
            MissionLabels[BlipIcon.Person_Vlad] = "Vlad";
            MissionLabels[BlipIcon.Person_Manny] = "Manny";
            MissionLabels[BlipIcon.Person_LittleJacob] = "Little Jacob";
            MissionLabels[BlipIcon.Person_Roman] = "Roman";
            MissionLabels[BlipIcon.Person_Faustin] = "Faustin";
            MissionLabels[BlipIcon.Person_Bernie] = "Bernie";
            MissionLabels[BlipIcon.Person_Brucie] = "Brucie";
            MissionLabels[BlipIcon.Person_Unknown] = "Unknown Contact";
            MissionLabels[BlipIcon.Person_Dwayne] = "Dwayne";
            MissionLabels[BlipIcon.Person_Dwayne_Red] = "Dwayne";
            MissionLabels[BlipIcon.Person_Elizabeta] = "Elizabeta";
            MissionLabels[BlipIcon.Person_Gambetti] = "Jon Gravelli";
            MissionLabels[BlipIcon.Person_JimmyPegorino] = "Jimmy Pegorino";
            MissionLabels[BlipIcon.Person_Derrick] = "Derrick";
            MissionLabels[BlipIcon.Person_Francis] = "Francis";
            MissionLabels[BlipIcon.Person_Gerry] = "Gerry";
            MissionLabels[BlipIcon.Person_Katie] = "Kate";
            MissionLabels[BlipIcon.Person_Packie] = "Packie";
            MissionLabels[BlipIcon.Person_PhilBell] = "Phil Bell";
            MissionLabels[BlipIcon.Person_PlayboyX] = "Playboy X";
            MissionLabels[BlipIcon.Person_PlayboyX_Red] = "Playboy X";
            MissionLabels[BlipIcon.Person_RayBoccino] = "Ray Boccino";
            MissionLabels[BlipIcon.Person_Dimitri] = "Dimitri";
            MissionLabels[BlipIcon.Person_Michelle] = "Michelle";
            MissionLabels[BlipIcon.Person_Male] = "Stranger (male)";
            MissionLabels[BlipIcon.Person_Female] = "Stranger (female)";
            MissionLabels[BlipIcon.Person_Assassin] = "Assassination job";
            MissionLabels[BlipIcon.Person_Lawyer] = "Lawyer";
            MissionLabels[BlipIcon.Person_UnitedLibertyPaper] = "United Liberty Paper";
            MissionLabels[BlipIcon.Misc_Boss] = "Boss";
            MissionLabels[BlipIcon.Misc_Deal] = "Drug deal";
            MissionLabels[BlipIcon.Misc_Ransom] = "Ransom";
            MissionLabels[BlipIcon.Misc_Revenge] = "Revenge";

            ServiceLabels[BlipIcon.Building_BurgerShot] = "Burger Shot";
            ServiceLabels[BlipIcon.Building_CluckinBell] = "Cluckin' Bell";
            ServiceLabels[BlipIcon.Building_Internet] = "Internet Cafe";
            ServiceLabels[BlipIcon.Building_Safehouse] = "Safehouse";
            ServiceLabels[BlipIcon.Building_ClothShop] = "Clothes Shop";
            ServiceLabels[BlipIcon.Building_TrainStation] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStation3] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStation8] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationA] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationB] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationC] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationE] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationJ] = "Train Station";
            ServiceLabels[BlipIcon.Building_TrainStationK] = "Train Station";
            ServiceLabels[BlipIcon.Building_WeaponShop] = "Gun Shop";
            ServiceLabels[BlipIcon.Building_PoliceStation] = "Police Station";
            ServiceLabels[BlipIcon.Building_FireStation] = "Fire Station";
            ServiceLabels[BlipIcon.Building_Hospital] = "Hospital";
            ServiceLabels[BlipIcon.Building_PayNSpray] = "Pay 'n' Spray";
            ServiceLabels[BlipIcon.Building_Garage] = "Garage";
            ServiceLabels[BlipIcon.Building_CarWash] = "Car Wash";
            ServiceLabels[BlipIcon.Misc_TaxiRank] = "Taxi Rank";
            ServiceLabels[BlipIcon.Misc_8BALL] = "8-Ball (explosives)";
            ServiceLabels[BlipIcon.Misc_ConsoleGame] = "Arcade";
            ServiceLabels[BlipIcon.Activity_Bar] = "Bar";
            ServiceLabels[BlipIcon.Activity_BoatTour] = "Boat Tour";
            ServiceLabels[BlipIcon.Activity_Bowling] = "Bowling";
            ServiceLabels[BlipIcon.Activity_Club] = "Club";
            ServiceLabels[BlipIcon.Activity_Darts] = "Darts";
            ServiceLabels[BlipIcon.Activity_Date] = "Date";
            ServiceLabels[BlipIcon.Activity_HeliTour] = "Helicopter Tour";
            ServiceLabels[BlipIcon.Activity_Restaurant] = "Restaurant";
            ServiceLabels[BlipIcon.Activity_StripClub] = "Strip Club";
            ServiceLabels[BlipIcon.Activity_ComedyClub] = "Comedy Club";
            ServiceLabels[BlipIcon.Activity_CabaretClub] = "Cabaret Club";
        }

        // Settings
        Keys hotkey, selectButton, closeButton, dumpKey;
        int maxSprite;
        bool freezeWhileOpen, holsterWhileOpen, invertScroll, imperial, useMouseHook;
        int maxRows;
        float arriveDistance;
        float holdSeconds;
        bool showRouteHint;
        string routeHint;      // e.g. "Little Jacob  756 m  ahead-left", built in the tick

        // State
        bool menuOpen;
        bool draining;         // menu closed, but a mouse button pressed in it is still held
        int drainFrames;
        DateTime drainStarted;
        bool capturingHotkey;  // waiting for the player to press a new hotkey
        bool hotkeyWasDown;
        bool holdUsed;         // this press already opened or closed the menu
        DateTime holdStart;
        bool selectWasDown;    // edge detection when the mouse hook isn't available
        bool closeWasDown;
        bool frozeControl;
        bool holstered;
        GTA.Weapon savedWeapon = GTA.Weapon.Unarmed;
        bool wheelBroken;
        int wheelAccum;
        int selected;
        List<Entry> entries = new List<Entry>();
        DateTime lastRefresh = DateTime.MinValue;
        MouseBlocker mouse;

        Blip routeBlip;
        bool ownsRouteBlip;    // true only for the fallback marker QuickGPS created itself
        string routeLabel;
        Vector3 routeTarget;

        GTA.Font font;
        GTA.Font titleFont;
        GTA.Font hintFont;
        int fontForHeight = -1;

        public QuickGPS()
        {
            hotkey = Settings.GetValueKey("Hotkey", "Settings", Keys.Tab);
            selectButton = Settings.GetValueKey("SelectButton", "Settings", Keys.LButton);
            closeButton = Settings.GetValueKey("CloseButton", "Settings", Keys.RButton);
            dumpKey = Settings.GetValueKey("DebugDumpKey", "Settings", Keys.F9);
            maxSprite = Math.Max(100, Settings.GetValueInteger("MaxBlipSprite", "Settings", 255));
            invertScroll = Settings.GetValueBool("InvertScroll", "Settings", false);
            useMouseHook = Settings.GetValueBool("BlockMouseFromGame", "Settings", true);
            freezeWhileOpen = Settings.GetValueBool("FreezePlayerWhileOpen", "Settings", false);
            holsterWhileOpen = Settings.GetValueBool("HolsterWeaponWhileOpen", "Settings", false);
            imperial = Settings.GetValueBool("UseMiles", "Settings", false);
            maxRows = Math.Max(5, Settings.GetValueInteger("MaxVisibleRows", "Settings", 14));
            arriveDistance = Settings.GetValueFloat("ArriveDistance", "Settings", 25.0f);
            holdSeconds = Math.Max(0.0f, Settings.GetValueFloat("HotkeyHoldSeconds", "Settings", 1.0f));
            showRouteHint = Settings.GetValueBool("ShowRouteHint", "Settings", true);

            mouse = new MouseBlocker(selectButton, closeButton);

            Interval = 0;   // every frame, so input is never missed
            this.Tick += new EventHandler(OnTick);
            this.KeyDown += new GTA.KeyEventHandler(OnKeyDown);
            this.PerFrameDrawing += new GraphicsEventHandler(OnDraw);
        }

        // ---------------------------------------------------------------- tick / input

        void OnTick(object sender, EventArgs e)
        {
            UpdateDrain();

            Ped me = Player.Character;
            if (!Game.Exists(me))
            {
                if (menuOpen) CloseMenu(false);
                return;
            }

            // The hotkey toggles the menu: press to open, press again (or right click) to close.
            bool hotkeyDown = isKeyPressed(hotkey);
            // Hold the hotkey to open the menu (HotkeyHoldSeconds); a press while it's open closes it.
            if (hotkeyDown && !hotkeyWasDown)
            {
                holdStart = DateTime.Now;
                holdUsed = capturingHotkey;
                if (menuOpen && !capturingHotkey)
                {
                    CloseMenu(false);
                    holdUsed = true;
                }
            }
            if (hotkeyDown && !holdUsed && !menuOpen && HoldProgress() >= 1.0f)
            {
                OpenMenu();
                holdUsed = true;
            }
            hotkeyWasDown = hotkeyDown;

            if (menuOpen)
            {
                HandleMouse();
                if (menuOpen && (DateTime.Now - lastRefresh).TotalMilliseconds > 500)
                    Rebuild();
            }

            UpdateRoute(me.Position);
        }

        bool HookActive()
        {
            return useMouseHook && !mouse.Failed;
        }

        void HandleMouse()
        {
            int wheel;
            bool select, close;

            if (HookActive())
            {
                // The hook eats the clicks and wheel before the game sees them, so Niko
                // doesn't shoot, aim or switch weapons. It removes itself if pings stop.
                mouse.Ping();
                mouse.Start();
                wheel = mouse.TakeWheel();
                select = mouse.TakeSelect();
                close = mouse.TakeClose();
            }
            else
            {
                wheel = ReadMouseWheel();
                if (wheel != 0 && Math.Abs(wheel) < 120) wheel = wheel > 0 ? 120 : -120;
                bool sDown = isKeyPressed(selectButton);
                bool cDown = isKeyPressed(closeButton);
                select = sDown && !selectWasDown;
                close = cDown && !closeWasDown;
                selectWasDown = sDown;
                closeWasDown = cDown;
            }

            if (capturingHotkey)
            {
                if (close || select)
                {
                    capturingHotkey = false;
                    Notify("QuickGPS: hotkey unchanged (" + hotkey + ")");
                }
                return;
            }
            if (close)
            {
                CloseMenu(false);
                return;
            }
            if (select)
            {
                if (selected >= 0 && selected < entries.Count && entries[selected].IsHotkeySetting)
                    capturingHotkey = true;
                else
                    CloseMenu(true);
                return;
            }

            if (entries.Count > 0 && wheel != 0)
            {
                // 120 per notch; smooth-scrolling mice send smaller steps, so accumulate.
                wheelAccum += wheel;
                while (Math.Abs(wheelAccum) >= 120)
                {
                    int step = wheelAccum > 0 ? -1 : 1;   // wheel up = move up the list
                    wheelAccum += wheelAccum > 0 ? -120 : 120;
                    if (invertScroll) step = -step;
                    selected = (selected + step + entries.Count) % entries.Count;
                }
            }
        }

        // The game's own wheel reading (GET_MOUSE_WHEEL); used only when the mouse hook can't be installed.
        int ReadMouseWheel()
        {
            if (wheelBroken) return 0;
            try
            {
                GTA.Native.Pointer p = new GTA.Native.Pointer(typeof(int));
                GTA.Native.Function.Call("GET_MOUSE_WHEEL", p);
                return (int)p;
            }
            catch (Exception ex)
            {
                wheelBroken = true;
                Game.DisplayText("QuickGPS: mouse wheel unavailable (" + ex.Message + ")", 5000);
                return 0;
            }
        }

        void OnKeyDown(object sender, GTA.KeyEventArgs e)
        {
            if (capturingHotkey)
            {
                CaptureHotkey(e.Key);
                return;
            }
            if (e.Key == dumpKey)
            {
                DumpBlips();
                return;
            }
        }

        void CaptureHotkey(Keys k)
        {
            switch (k)
            {
                // Modifiers alone and the keys QuickGPS already uses can't be the hotkey.
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey:
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey:
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu:
                case Keys.Escape:
                    return;
            }
            if (k == dumpKey)
            {
                Notify("QuickGPS: " + k + " is already used by QuickGPS, pick another key");
                return;
            }

            hotkey = k;
            hotkeyWasDown = true;   // the new key is held right now; don't let it toggle the menu
            holdUsed = true;
            capturingHotkey = false;
            Notify(SaveHotkey(k) ? "QuickGPS: hotkey set to " + k : "QuickGPS: hotkey set to " + k + " (could not save to QuickGPS.ini)");
            Rebuild();
        }

        // Rewrites only the Hotkey= line so the comments in QuickGPS.ini survive.
        bool SaveHotkey(Keys k)
        {
            try
            {
                string path = System.IO.Path.Combine(System.IO.Path.Combine(Environment.CurrentDirectory, "scripts"), "QuickGPS.ini");
                if (System.IO.File.Exists(path))
                {
                    string[] lines = System.IO.File.ReadAllLines(path);
                    bool found = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string t = lines[i].Trim();
                        if (t.StartsWith("Hotkey", StringComparison.OrdinalIgnoreCase) && t.Substring(6).TrimStart().StartsWith("="))
                        {
                            lines[i] = "Hotkey=" + k;
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        System.IO.File.WriteAllLines(path, lines);
                        return true;
                    }
                }
                Settings.SetValue("Hotkey", "Settings", k);
                return Settings.Save();
            }
            catch
            {
                return false;
            }
        }

        void OpenMenu()
        {
            draining = false;
            menuOpen = true;
            capturingHotkey = false;
            selected = 0;
            wheelAccum = 0;
            // Buttons already held when the menu opens shouldn't count as a click.
            selectWasDown = isKeyPressed(selectButton);
            closeWasDown = isKeyPressed(closeButton);
            if (HookActive())
            {
                mouse.Ping();
                mouse.Start();
                mouse.Reset();
            }
            else
            {
                ReadMouseWheel();   // throw away wheel movement from before the menu opened
            }
            Rebuild();

            if (freezeWhileOpen && Player.CanControlCharacter)
            {
                Player.CanControlCharacter = false;
                frozeControl = true;
            }
            // Without the hook the clicks reach the game, so put the gun away instead.
            if (!holstered && (holsterWhileOpen || !HookActive())) Holster();
        }

        void CloseMenu(bool confirm)
        {
            if (confirm && selected >= 0 && selected < entries.Count)
                SetRoute(entries[selected]);

            menuOpen = false;
            capturingHotkey = false;
            BeginDrain();
            if (frozeControl)
            {
                Player.CanControlCharacter = true;
                frozeControl = false;
            }
        }

        // The menu reacts to the button press, so the button is usually still down when the menu
        // closes. Keep it hidden from the game (and the weapon holstered) until it's let go,
        // otherwise Niko attacks with the click that picked the destination.
        void BeginDrain()
        {
            mouse.BeginDrain();
            draining = true;
            drainFrames = 0;
            drainStarted = DateTime.Now;
        }

        void UpdateDrain()
        {
            if (!draining) return;
            mouse.Ping();
            bool held = isKeyPressed(selectButton) || isKeyPressed(closeButton);
            drainFrames = held ? 0 : drainFrames + 1;
            if (drainFrames >= 3 || (DateTime.Now - drainStarted).TotalSeconds > 10)
            {
                draining = false;
                mouse.Stop();
                if (holstered) Unholster();
            }
        }

        // Put the weapon away and block switching so the wheel and click only drive the menu.
        void Holster()
        {
            try
            {
                Ped me = Player.Character;
                savedWeapon = me.Weapons.CurrentType;
                me.Weapons.Select(GTA.Weapon.Unarmed);
                me.CanSwitchWeapons = false;
                holstered = true;
            }
            catch { holstered = false; }
        }

        void Unholster()
        {
            holstered = false;
            try
            {
                Ped me = Player.Character;
                if (!Game.Exists(me)) return;
                me.CanSwitchWeapons = true;
                if (savedWeapon != GTA.Weapon.Unarmed && savedWeapon != GTA.Weapon.None)
                    me.Weapons.Select(savedWeapon);
            }
            catch { }
        }

        // ---------------------------------------------------------------- blip scan

        void Rebuild()
        {
            lastRefresh = DateTime.Now;

            string keepLabel = null;
            string keepSection = null;
            if (selected >= 0 && selected < entries.Count)
            {
                keepLabel = entries[selected].Label;
                keepSection = entries[selected].Section;
            }

            Vector3 me = Player.Character.Position;
            List<Entry> missions = new List<Entry>();
            Dictionary<string, Entry> services = new Dictionary<string, Entry>();

            List<Blip> blips = ScanAllBlips();
            for (int i = 0; i < blips.Count; i++)
            {
                Blip b = blips[i];
                if (ownsRouteBlip && object.ReferenceEquals(b, routeBlip)) continue;

                string label;
                bool isMission;
                if (!Classify(b, out label, out isMission)) continue;

                Vector3 pos = b.Position;
                float dist = Distance(me, pos);

                if (isMission)
                {
                    if (IsDuplicate(missions, label, pos)) continue;
                    Entry m = new Entry();
                    m.Label = label;
                    m.Section = SectionMissions;
                    m.Position = pos;
                    m.Distance = dist;
                    m.Source = b;
                    missions.Add(m);
                }
                else
                {
                    Entry cur;
                    if (services.TryGetValue(label, out cur) && cur.Distance <= dist) continue;
                    Entry s = new Entry();
                    s.Label = label;
                    s.Section = SectionServices;
                    s.Position = pos;
                    s.Distance = dist;
                    s.Source = b;
                    services[label] = s;
                }
            }

            missions.Sort(CompareDistance);
            List<Entry> serviceList = new List<Entry>(services.Values);
            serviceList.Sort(CompareDistance);

            List<Entry> result = new List<Entry>();
            result.AddRange(missions);
            result.AddRange(serviceList);

            Entry hk = new Entry();
            hk.Label = "Change hotkey (now: " + hotkey + ")";
            hk.Section = SectionSettings;
            hk.Position = me;
            hk.IsHotkeySetting = true;
            result.Add(hk);
            entries = result;

            // Keep the highlight on the same destination when distances reshuffle the list.
            selected = Math.Min(selected, Math.Max(0, entries.Count - 1));
            if (keepLabel != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Label == keepLabel && entries[i].Section == keepSection)
                    {
                        selected = i;
                        break;
                    }
                }
            }
        }

        // Every blip on the map, one entry per blip.
        List<Blip> ScanAllBlips()
        {
            List<Blip> all = new List<Blip>();
            Dictionary<Blip, bool> seen = new Dictionary<Blip, bool>();
            for (int sprite = 0; sprite <= maxSprite; sprite++)
            {
                Blip[] found;
                try { found = Blip.GetAllBlipsOfType((BlipType)sprite); }
                catch { continue; }
                for (int i = 0; i < found.Length; i++)
                {
                    Blip b = found[i];
                    if (b == null || seen.ContainsKey(b) || !Game.Exists(b)) continue;
                    seen[b] = true;
                    all.Add(b);
                }
            }
            return all;
        }

        static bool Classify(Blip b, out string label, out bool isMission)
        {
            label = null;
            isMission = false;

            BlipIcon icon = b.Icon;
            switch (icon)
            {
                case BlipIcon.Misc_Player:
                case BlipIcon.Misc_North:
                case BlipIcon.Misc_Waypoint:
                    return false;
            }

            if (MissionLabels.TryGetValue(icon, out label))
            {
                isMission = true;
                return true;
            }
            if (ServiceLabels.TryGetValue(icon, out label))
                return true;

            // Story contacts that aren't in the icon table (e.g. TLAD / TBoGT characters).
            BlipType type;
            try { type = b.Type; }
            catch { return false; }
            if (type == BlipType.Contact)
            {
                label = "Contact #" + (int)icon;
                isMission = true;
                return true;
            }
            return false;
        }

        // Writes every blip the game reports to scripts\QuickGPS_blips.txt, for troubleshooting.
        void DumpBlips()
        {
            try
            {
                Vector3 me = Player.Character.Position;
                List<Blip> blips = ScanAllBlips();
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("QuickGPS blip dump " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", " + blips.Count + " blips");
                const string T = "\t";
                sb.AppendLine("Current QuickGPS route: " + (routeLabel ?? "(none)"));
                sb.AppendLine("sprite" + T + "icon" + T + "type" + T + "display" + T + "rawDisplay" + T + "address" + T + "distance" + T + "x" + T + "y" + T + "z" + T + "quickgps");
                for (int i = 0; i < blips.Count; i++)
                {
                    Blip b = blips[i];
                    string label, kind;
                    bool isMission;
                    if (Classify(b, out label, out isMission))
                        kind = (isMission ? "mission: " : "service: ") + label;
                    else
                        kind = "-";
                    string type, display;
                    try { type = b.Type.ToString(); } catch { type = "?"; }
                    try { display = b.Display.ToString(); } catch { display = "?"; }
                    string rawDisplay, address;
                    try { rawDisplay = RawDisplay(b).ToString(); } catch { rawDisplay = "?"; }
                    try { address = "0x" + b.MemoryAddress.ToString("X8"); } catch { address = "?"; }
                    Vector3 p = b.Position;
                    sb.AppendLine((int)b.Icon + T + b.Icon + T + type + T + display + T + rawDisplay + T + address + T +
                        ((int)Distance(me, p)) + T + p.X.ToString("0") + T + p.Y.ToString("0") + T +
                        p.Z.ToString("0") + T + kind);
                }
                string path = System.IO.Path.Combine(System.IO.Path.Combine(Environment.CurrentDirectory, "scripts"), "QuickGPS_blips.txt");
                System.IO.File.WriteAllText(path, sb.ToString());
                Notify("QuickGPS: wrote " + blips.Count + " blips to scripts\\QuickGPS_blips.txt");
            }
            catch (Exception ex)
            {
                Notify("QuickGPS: blip dump failed (" + ex.Message + ")");
            }
        }

        // GET_BLIP_INFO_ID_DISPLAY straight from the game, without ScriptHookDotNet's conversion.
        static int RawDisplay(Blip b)
        {
            return GTA.Native.Function.Call<int>("GET_BLIP_INFO_ID_DISPLAY", b);
        }

        static bool IsDuplicate(List<Entry> list, string label, Vector3 pos)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Label == label && Distance(list[i].Position, pos) < 30.0f) return true;
            return false;
        }

        static int CompareDistance(Entry a, Entry b)
        {
            return a.Distance.CompareTo(b.Distance);
        }

        static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // ---------------------------------------------------------------- route

        // GTA IV ignores SET_ROUTE on blips owned by the game's mission scripts (contacts etc.), so the
        // route goes on a destination marker QuickGPS creates at the blip's position. The game only
        // draws GPS routes while you're in a vehicle; on foot the marker plus the on-screen hint guide you.
        void SetRoute(Entry target)
        {
            ClearRoute();
            Vector3 pos = target.Position;
            if (!PlausiblePosition(pos) && target.Source != null && Game.Exists(target.Source))
                pos = target.Source.Position;
            if (!PlausiblePosition(pos))
            {
                Notify("QuickGPS: couldn't read where " + target.Label + " is");
                return;
            }

            routeBlip = Blip.AddBlip(pos);
            if (routeBlip == null)
            {
                Notify("QuickGPS: could not set a route");
                return;
            }
            ownsRouteBlip = true;
            routeBlip.Icon = BlipIcon.Misc_Destination;
            routeBlip.Color = BlipColor.Yellow;
            routeBlip.RouteActive = true;
            routeLabel = target.Label;
            routeTarget = pos;

            bool inVehicle = false;
            try { inVehicle = Player.Character.isInVehicle(); } catch { }
            Notify("GPS set: " + target.Label + " (" + FormatDistance(target.Distance) + ")" +
                (inVehicle ? "" : " - the route line shows once you're in a vehicle"));
        }

        void ClearRoute()
        {
            if (routeBlip != null && Game.Exists(routeBlip))
            {
                routeBlip.RouteActive = false;
                // Never delete the game's own blips, only the fallback marker.
                if (ownsRouteBlip) routeBlip.Delete();
            }
            routeBlip = null;
            ownsRouteBlip = false;
            routeLabel = null;
        }

        void UpdateRoute(Vector3 me)
        {
            routeHint = null;
            if (routeBlip == null) return;
            if (!Game.Exists(routeBlip))
            {
                // The game removed the blip (e.g. the mission started).
                routeBlip = null;
                ownsRouteBlip = false;
                return;
            }
            Vector3 target = ownsRouteBlip ? routeTarget : routeBlip.Position;
            if (!PlausiblePosition(target)) return;
            if (Distance(me, target) < arriveDistance)
            {
                Notify("QuickGPS: arrived at " + routeLabel);
                ClearRoute();
                return;
            }
            if (showRouteHint) routeHint = routeLabel + "  " + FormatDistance(Distance(me, target)) + "  " + DirectionTo(me, target);
        }

        // Where the target is relative to the camera: ahead, ahead-right, right, behind-right, ...
        static string DirectionTo(Vector3 me, Vector3 target)
        {
            float cx, cy;
            try
            {
                Vector3 d = Game.CurrentCamera.Direction;
                cx = d.X; cy = d.Y;
            }
            catch { return ""; }
            float tx = target.X - me.X, ty = target.Y - me.Y;
            if ((cx == 0 && cy == 0) || (tx == 0 && ty == 0)) return "";
            double angle = Math.Atan2(cx * ty - cy * tx, cx * tx + cy * ty) * 180.0 / Math.PI;   // + = to the left
            string[] names = { "ahead", "ahead-left", "left", "behind-left", "behind", "behind-right", "right", "ahead-right" };
            int sector = (int)Math.Round(angle / 45.0);
            return names[((sector % 8) + 8) % 8];
        }

        // Rejects the zero / NaN / far-off-map positions a bad blip read produces.
        static bool PlausiblePosition(Vector3 v)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z)) return false;
            if (Math.Abs(v.X) < 1.0f && Math.Abs(v.Y) < 1.0f) return false;
            return Math.Abs(v.X) < 10000.0f && Math.Abs(v.Y) < 10000.0f;
        }

        static void Notify(string text)
        {
            Game.DisplayText(text, 3000);
        }

        string FormatDistance(float meters)
        {
            if (imperial)
            {
                float feet = meters * 3.28084f;
                if (feet < 1000.0f) return ((int)feet).ToString() + " ft";
                return (meters / 1609.344f).ToString("0.0") + " mi";
            }
            if (meters < 1000.0f) return ((int)meters).ToString() + " m";
            return (meters / 1000.0f).ToString("0.0") + " km";
        }

        // ---------------------------------------------------------------- drawing

        // Small filling bar in the top-left while the hotkey is held (not shown for quick taps).
        // Small "GPS: <target>  <distance>  <direction>" line in the top-left while a route is set.
        void DrawRouteHint(GTA.Graphics g)
        {
            string hint = routeHint;
            if (hint == null) return;
            g.Scaling = FontScaling.Pixel;
            Size res = Game.Resolution;
            float s = res.Height / 1080.0f;
            EnsureFonts(res.Height, s);
            float x = 24.0f * s, y = 24.0f * s, h = 30.0f * s, pad = 10.0f * s;
            float w = (12 + hint.Length * 11) * s;
            g.DrawRectangle(x + w / 2, y + h / 2, w, h, Color.FromArgb(150, 0, 0, 0));
            g.DrawText("GPS: " + hint, x + pad, y + 5.0f * s, Color.FromArgb(255, 240, 200, 80), hintFont);
        }

        void EnsureFonts(int height, float s)
        {
            if (fontForHeight == height) return;
            font = new GTA.Font(22.0f * s, FontScaling.Pixel);
            titleFont = new GTA.Font(28.0f * s, FontScaling.Pixel, true, false);
            hintFont = new GTA.Font(17.0f * s, FontScaling.Pixel);
            fontForHeight = height;
        }

        bool DrawHoldBar(GTA.Graphics g)
        {
            if (!hotkeyWasDown || holdUsed || holdSeconds <= 0.0f) return false;
            if ((DateTime.Now - holdStart).TotalSeconds < 0.15) return false;

            g.Scaling = FontScaling.Pixel;
            Size res = Game.Resolution;
            float s = res.Height / 1080.0f;
            if (fontForHeight != res.Height)
            {
                font = new GTA.Font(22.0f * s, FontScaling.Pixel);
                titleFont = new GTA.Font(28.0f * s, FontScaling.Pixel, true, false);
                hintFont = new GTA.Font(17.0f * s, FontScaling.Pixel);
                fontForHeight = res.Height;
            }

            float x = 24.0f * s, y = 24.0f * s, w = 220.0f * s, h = 40.0f * s, pad = 10.0f * s;
            float barH = 6.0f * s;
            g.DrawRectangle(x + w / 2, y + h / 2, w, h, Color.FromArgb(170, 0, 0, 0));
            g.DrawText("QUICK GPS", x + pad, y + 4.0f * s, Color.FromArgb(255, 240, 200, 80), hintFont);
            float fill = (w - pad * 2) * HoldProgress();
            g.DrawRectangle(x + w / 2, y + h - pad, w - pad * 2, barH, Color.FromArgb(200, 60, 60, 60));
            if (fill > 0.5f)
                g.DrawRectangle(x + pad + fill / 2, y + h - pad, fill, barH, Color.FromArgb(255, 240, 200, 80));
            return true;
        }

        // 0..1 while the hotkey is being held to open the menu.
        float HoldProgress()
        {
            if (holdSeconds <= 0.0f) return 1.0f;
            return (float)Math.Min(1.0, (DateTime.Now - holdStart).TotalSeconds / holdSeconds);
        }

        void OnDraw(object sender, GraphicsEventArgs e)
        {
            if (!menuOpen)
            {
                if (!DrawHoldBar(e.Graphics)) DrawRouteHint(e.Graphics);
                return;
            }

            GTA.Graphics g = e.Graphics;
            g.Scaling = FontScaling.Pixel;

            Size res = Game.Resolution;
            float s = res.Height / 1080.0f;
            if (fontForHeight != res.Height)
            {
                font = new GTA.Font(22.0f * s, FontScaling.Pixel);
                titleFont = new GTA.Font(28.0f * s, FontScaling.Pixel, true, false);
                hintFont = new GTA.Font(17.0f * s, FontScaling.Pixel);
                fontForHeight = res.Height;
            }

            float x = 24.0f * s;
            float y = 24.0f * s;
            float w = 460.0f * s;
            float pad = 12.0f * s;
            float rowH = 28.0f * s;
            float titleH = 40.0f * s;

            // Flatten into lines with section headers, then window around the selection.
            List<Line> lines = new List<Line>();
            string lastSection = null;
            int selectedLine = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Section != lastSection)
                {
                    lastSection = entries[i].Section;
                    Line h = new Line();
                    h.Text = lastSection;
                    h.IsHeader = true;
                    h.EntryIndex = -1;
                    lines.Add(h);
                }
                Line l = new Line();
                l.Text = entries[i].Label;
                l.EntryIndex = i;
                if (i == selected) selectedLine = lines.Count;
                lines.Add(l);
            }

            int shown = Math.Min(maxRows, lines.Count);
            int first = Math.Max(0, Math.Min(selectedLine - shown / 2, lines.Count - shown));
            bool noBlips = entries.Count <= 1;
            int rowsDrawn = shown + (noBlips ? 1 : 0);
            float h2 = titleH + rowsDrawn * rowH + rowH + pad * 2;

            g.DrawRectangle(x + w / 2, y + h2 / 2, w, h2, Color.FromArgb(190, 0, 0, 0));
            g.DrawRectangle(x + w / 2, y + 2.0f * s, w, 4.0f * s, Color.FromArgb(255, 240, 200, 80));
            g.DrawText("QUICK GPS", x + pad, y + pad, Color.FromArgb(255, 240, 200, 80), titleFont);

            float cy = y + pad + titleH;
            if (noBlips)
            {
                g.DrawText("No mission givers or services on the map.", x + pad, cy, Color.LightGray, font);
                cy += rowH;
            }
            for (int i = first; i < first + shown; i++)
            {
                Line l = lines[i];
                if (l.IsHeader)
                {
                    g.DrawText(l.Text, x + pad, cy + 4.0f * s, Color.FromArgb(255, 140, 180, 255), font);
                }
                else
                {
                    Entry en = entries[l.EntryIndex];
                    bool sel = l.EntryIndex == selected;
                    if (sel)
                        g.DrawRectangle(x + w / 2, cy + rowH / 2, w - pad, rowH, Color.FromArgb(200, 240, 200, 80));
                    Color c = sel ? Color.Black : Color.White;
                    string text = en.Label;
                    if (en.IsHotkeySetting && capturingHotkey) text = "Press a new key...  (click to cancel)";
                    g.DrawText((sel ? "> " : "  ") + text, x + pad, cy + 2.0f * s, c, font);
                    if (!en.IsHotkeySetting)
                        g.DrawText(PlausiblePosition(en.Position) ? FormatDistance(en.Distance) : "--",
                            new RectangleF(x, cy + 2.0f * s, w - pad, rowH), GTA.TextAlignment.Right, c, font);
                }
                cy += rowH;
            }

            if (first > 0)
                g.DrawText("^", x + w - pad * 2, y + pad + titleH - rowH * 0.6f, Color.Gray, font);
            if (first + shown < lines.Count)
                g.DrawText("v", x + w - pad * 2, cy - 4.0f * s, Color.Gray, font);

            g.DrawText("Scroll: select   Left click: go   Right click: close" + (HookActive() ? "" : "   [fallback input]"),
                x + pad, cy + pad / 2, Color.FromArgb(255, 170, 170, 170), hintFont);
        }
    }

    // Hides the mouse buttons and wheel from the game while the menu is open.
    //
    // GTA IV reads the mouse through DirectInput 8, which a Windows low-level mouse hook can't
    // block. Instead, while the menu is open, the two DirectInput functions the game uses to read
    // the mouse (IDirectInputDevice8::GetDeviceState and GetDeviceData) are redirected through
    // filters that pick up the wheel/buttons for the menu and hand the game a copy with them zeroed.
    // Mouse movement is left alone, so the camera still works. The original functions are put back
    // as soon as the menu closes, or by a watchdog if the script stops pinging (paused, reloaded).
    class MouseBlocker
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetDeviceStateFn(IntPtr self, int cbData, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetDeviceDataFn(IntPtr self, int cbObjectData, IntPtr rgdod, IntPtr pdwInOut, int flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetCapabilitiesFn(IntPtr self, IntPtr caps);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateDeviceFn(IntPtr self, ref Guid guid, out IntPtr device, IntPtr outer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ReleaseFn(IntPtr self);

        [DllImport("dinput8.dll")]
        static extern int DirectInput8Create(IntPtr hinst, int version, ref Guid riid, out IntPtr ppvOut, IntPtr outer);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr addr, IntPtr size, int newProtect, out int oldProtect);

        const int PAGE_READWRITE = 0x04;
        const int SlotRelease = 2, SlotCreateDevice = 3;
        const int SlotGetCapabilities = 3, SlotGetDeviceState = 9, SlotGetDeviceData = 10;
        const int MouseZ = 8, MouseButtons = 12;   // DIMOUSESTATE(2) offsets; also DIMOFS_* for buffered data
        const long StaleTicks = 15000000;          // 1.5 s without a ping = the script isn't running

        // The ANSI and Unicode device interfaces have separate vtables; filter both.
        class Table
        {
            public IntPtr StateSlot, DataSlot;
            public IntPtr OrigState, OrigData;
            public GetDeviceStateFn CallState;
            public GetDeviceDataFn CallData;
        }

        readonly int selectButton, closeButton;
        readonly List<Table> tables = new List<Table>();
        readonly Dictionary<IntPtr, bool> isMouse = new Dictionary<IntPtr, bool>();
        readonly Dictionary<IntPtr, byte[]> lastButtons = new Dictionary<IntPtr, byte[]>();
        readonly object gate = new object();

        // Kept in fields so the GC never collects the thunks the game is calling.
        readonly GetDeviceStateFn stateFilter;
        readonly GetDeviceDataFn dataFilter;
        readonly IntPtr stateThunk, dataThunk;

        System.Threading.Timer watchdog;
        bool initialised, patched;
        public volatile bool Failed;
        volatile bool collecting;   // false = menu closed, just keep the buttons hidden
        long lastPing;
        int wheel, selects, closes;

        public MouseBlocker(Keys select, Keys close)
        {
            selectButton = ButtonIndex(select);
            closeButton = ButtonIndex(close);
            stateFilter = new GetDeviceStateFn(StateFilter);
            dataFilter = new GetDeviceDataFn(DataFilter);
            stateThunk = Marshal.GetFunctionPointerForDelegate(stateFilter);
            dataThunk = Marshal.GetFunctionPointerForDelegate(dataFilter);
        }

        static int ButtonIndex(Keys k)
        {
            switch (k)
            {
                case Keys.RButton: return 1;
                case Keys.MButton: return 2;
                case Keys.XButton1: return 3;
                case Keys.XButton2: return 4;
                default: return 0;
            }
        }

        public void Ping() { Interlocked.Exchange(ref lastPing, DateTime.UtcNow.Ticks); }
        bool Alive() { return DateTime.UtcNow.Ticks - Interlocked.Read(ref lastPing) < StaleTicks; }

        public void Reset()
        {
            Interlocked.Exchange(ref wheel, 0);
            Interlocked.Exchange(ref selects, 0);
            Interlocked.Exchange(ref closes, 0);
        }

        public int TakeWheel() { return Interlocked.Exchange(ref wheel, 0); }
        public bool TakeSelect() { return Interlocked.Exchange(ref selects, 0) > 0; }
        public bool TakeClose() { return Interlocked.Exchange(ref closes, 0) > 0; }

        public void Start()
        {
            lock (gate)
            {
                collecting = true;
                if (Failed || patched) return;
                try
                {
                    if (!initialised) Initialise();
                    for (int i = 0; i < tables.Count; i++)
                    {
                        WriteSlot(tables[i].StateSlot, stateThunk);
                        WriteSlot(tables[i].DataSlot, dataThunk);
                    }
                    lastButtons.Clear();
                    patched = true;
                    if (watchdog == null)
                        watchdog = new System.Threading.Timer(new TimerCallback(CheckStale), null, 500, 500);
                }
                catch
                {
                    Failed = true;
                    Restore();
                }
            }
        }

        public void BeginDrain()
        {
            collecting = false;
        }

        public void Stop()
        {
            lock (gate)
            {
                Interlocked.Exchange(ref lastPing, 0);
                Restore();
            }
        }

        void Restore()
        {
            for (int i = 0; i < tables.Count; i++)
            {
                try
                {
                    WriteSlot(tables[i].StateSlot, tables[i].OrigState);
                    WriteSlot(tables[i].DataSlot, tables[i].OrigData);
                }
                catch { }
            }
            patched = false;
        }

        void CheckStale(object state)
        {
            if (patched && !Alive()) Stop();
        }

        // Create a throwaway mouse device of each interface flavour to find the shared vtables.
        void Initialise()
        {
            initialised = true;
            Guid[] iids = new Guid[] {
                new Guid("BF798030-483A-4DA2-AA99-5D64ED369700"),   // IID_IDirectInput8A
                new Guid("BF798031-483A-4DA2-AA99-5D64ED369700")    // IID_IDirectInput8W
            };
            Guid sysMouse = new Guid("6F1D2B60-D5A0-11CF-BFC7-444553540000");
            IntPtr hinst = GetModuleHandle(null);

            for (int i = 0; i < iids.Length; i++)
            {
                IntPtr di;
                Guid iid = iids[i];
                if (DirectInput8Create(hinst, 0x0800, ref iid, out di, IntPtr.Zero) < 0 || di == IntPtr.Zero) continue;
                try
                {
                    CreateDeviceFn create = (CreateDeviceFn)Marshal.GetDelegateForFunctionPointer(Slot(di, SlotCreateDevice), typeof(CreateDeviceFn));
                    IntPtr dev;
                    Guid g = sysMouse;
                    if (create(di, ref g, out dev, IntPtr.Zero) < 0 || dev == IntPtr.Zero) continue;
                    try
                    {
                        IntPtr vtable = Marshal.ReadIntPtr(dev);
                        Table t = new Table();
                        t.StateSlot = new IntPtr(vtable.ToInt64() + SlotGetDeviceState * IntPtr.Size);
                        t.DataSlot = new IntPtr(vtable.ToInt64() + SlotGetDeviceData * IntPtr.Size);
                        // If an earlier copy of this script was cut off while patched, the slot may still hold
                        // its filter; the real function address is remembered process-wide for that case.
                        t.OrigState = Original("QUICKGPS_DI_STATE_" + vtable.ToInt64(), Marshal.ReadIntPtr(t.StateSlot));
                        t.OrigData = Original("QUICKGPS_DI_DATA_" + vtable.ToInt64(), Marshal.ReadIntPtr(t.DataSlot));
                        t.CallState = (GetDeviceStateFn)Marshal.GetDelegateForFunctionPointer(t.OrigState, typeof(GetDeviceStateFn));
                        t.CallData = (GetDeviceDataFn)Marshal.GetDelegateForFunctionPointer(t.OrigData, typeof(GetDeviceDataFn));
                        bool dup = false;
                        for (int k = 0; k < tables.Count; k++) if (tables[k].StateSlot == t.StateSlot) dup = true;
                        if (!dup) tables.Add(t);
                    }
                    finally { Release(dev); }
                }
                finally { Release(di); }
            }
            if (tables.Count == 0) throw new InvalidOperationException("no DirectInput mouse vtable");
        }

        static IntPtr Original(string key, IntPtr current)
        {
            string saved = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(saved)) return new IntPtr(long.Parse(saved));
            Environment.SetEnvironmentVariable(key, current.ToInt64().ToString());
            return current;
        }

        static IntPtr Slot(IntPtr obj, int index)
        {
            return Marshal.ReadIntPtr(new IntPtr(Marshal.ReadIntPtr(obj).ToInt64() + index * IntPtr.Size));
        }

        static void Release(IntPtr obj)
        {
            ReleaseFn r = (ReleaseFn)Marshal.GetDelegateForFunctionPointer(Slot(obj, SlotRelease), typeof(ReleaseFn));
            r(obj);
        }

        static void WriteSlot(IntPtr slot, IntPtr value)
        {
            int old;
            if (!VirtualProtect(slot, new IntPtr(IntPtr.Size), PAGE_READWRITE, out old))
                throw new InvalidOperationException("VirtualProtect failed");
            Marshal.WriteIntPtr(slot, value);
            int ignored;
            VirtualProtect(slot, new IntPtr(IntPtr.Size), old, out ignored);
        }

        Table FindTable(IntPtr self)
        {
            IntPtr vtable = Marshal.ReadIntPtr(self);
            IntPtr slot = new IntPtr(vtable.ToInt64() + SlotGetDeviceState * IntPtr.Size);
            for (int i = 0; i < tables.Count; i++) if (tables[i].StateSlot == slot) return tables[i];
            return tables[0];
        }

        // Keyboards and joysticks share the vtable, so only touch devices that are mice.
        bool IsMouse(IntPtr self)
        {
            lock (isMouse)
            {
                bool m;
                if (isMouse.TryGetValue(self, out m)) return m;
                m = false;
                IntPtr caps = Marshal.AllocHGlobal(64);
                try
                {
                    for (int i = 0; i < 64; i += 4) Marshal.WriteInt32(caps, i, 0);
                    Marshal.WriteInt32(caps, 0, 44);   // DIDEVCAPS.dwSize
                    GetCapabilitiesFn getCaps = (GetCapabilitiesFn)Marshal.GetDelegateForFunctionPointer(Slot(self, SlotGetCapabilities), typeof(GetCapabilitiesFn));
                    if (getCaps(self, caps) >= 0)
                        m = (Marshal.ReadInt32(caps, 8) & 0xFF) == 0x12;   // DI8DEVTYPE_MOUSE
                }
                catch { }
                finally { Marshal.FreeHGlobal(caps); }
                isMouse[self] = m;
                return m;
            }
        }

        int StateFilter(IntPtr self, int cbData, IntPtr data)
        {
            Table t = FindTable(self);
            int hr = t.CallState(self, cbData, data);
            if (hr < 0 || data == IntPtr.Zero || !Alive() || cbData < MouseButtons + 1 || !IsMouse(self)) return hr;

            int buttons = Math.Min(cbData - MouseButtons, 8);
            if (collecting) Interlocked.Add(ref wheel, Marshal.ReadInt32(data, MouseZ));
            Marshal.WriteInt32(data, MouseZ, 0);

            byte[] prev;
            lock (lastButtons)
            {
                if (!lastButtons.TryGetValue(self, out prev))
                {
                    // First read after opening: buttons already held don't count as clicks.
                    prev = new byte[8];
                    for (int i = 0; i < buttons; i++) prev[i] = Marshal.ReadByte(data, MouseButtons + i);
                    lastButtons[self] = prev;
                }
            }
            for (int i = 0; i < buttons; i++)
            {
                byte now = Marshal.ReadByte(data, MouseButtons + i);
                bool down = (now & 0x80) != 0, was = (prev[i] & 0x80) != 0;
                if (down && !was && collecting)
                {
                    if (i == selectButton) Interlocked.Increment(ref selects);
                    else if (i == closeButton) Interlocked.Increment(ref closes);
                }
                prev[i] = now;
                Marshal.WriteByte(data, MouseButtons + i, 0);
            }
            return hr;
        }

        int DataFilter(IntPtr self, int cbObjectData, IntPtr rgdod, IntPtr pdwInOut, int flags)
        {
            Table t = FindTable(self);
            int hr = t.CallData(self, cbObjectData, rgdod, pdwInOut, flags);
            if (hr < 0 || rgdod == IntPtr.Zero || pdwInOut == IntPtr.Zero || !Alive() || !IsMouse(self)) return hr;

            int count = Marshal.ReadInt32(pdwInOut);
            for (int i = 0; i < count; i++)
            {
                IntPtr item = new IntPtr(rgdod.ToInt64() + (long)i * cbObjectData);
                int ofs = Marshal.ReadInt32(item, 0);    // DIDEVICEOBJECTDATA.dwOfs
                int val = Marshal.ReadInt32(item, 4);    // .dwData
                if (ofs == MouseZ)
                {
                    if (collecting) Interlocked.Add(ref wheel, val);
                    Marshal.WriteInt32(item, 4, 0);
                }
                else if (ofs >= MouseButtons && ofs < MouseButtons + 8)
                {
                    int b = ofs - MouseButtons;
                    if ((val & 0x80) != 0 && collecting)
                    {
                        if (b == selectButton) Interlocked.Increment(ref selects);
                        else if (b == closeButton) Interlocked.Increment(ref closes);
                    }
                    Marshal.WriteInt32(item, 4, 0);   // the game only ever sees "released"
                }
            }
            return hr;
        }
    }
}
