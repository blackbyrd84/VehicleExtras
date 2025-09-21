using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rage;
using Rage.Attributes;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;

[assembly: Plugin("VehicleExtras", Description = "UI Menu for vehicle extras", Author = "blackbyrd")]

public class EntryPoint
{
    private static MenuPool menuPool;
    private static UIMenu extrasMenu;
    private static UIMenu performanceMenu;
    private static List<UIMenuCheckboxItem> extraCheckboxes = new List<UIMenuCheckboxItem>();
    private static Keys openMenuKey = Keys.F10;

    public static void Main()
    {
        Game.Console.Print("VehicleExtras plugin loaded.");

        string iniPath = Path.Combine(Directory.GetCurrentDirectory(), "plugins", "VehicleExtras.ini");
        if (File.Exists(iniPath))
        {
            foreach (string line in File.ReadAllLines(iniPath))
            {
                if (line.StartsWith("OpenMenuKey=", StringComparison.OrdinalIgnoreCase))
                {
                    string keyString = line.Split('=')[1].Trim();
                    if (Enum.TryParse(keyString, out Keys parsedKey))
                    {
                        openMenuKey = parsedKey;
                        Game.Console.Print($"[VehicleExtras] Menu key set to {openMenuKey}");
                    }
                    else
                    {
                        Game.Console.Print($"[VehicleExtras] Invalid key '{keyString}' in INI. Using default F10.");
                    }
                    break;
                }
            }
        }
        else
        {
            Game.Console.Print("[VehicleExtras] INI file not found. Using default F10.");
        }

        menuPool = new MenuPool();
        extrasMenu = new UIMenu("Vehicle Extras", "~b~Toggle Extras");
        performanceMenu = new UIMenu("Performance Mods", "~b~Tune Performance");

        menuPool.Add(extrasMenu);
        menuPool.Add(performanceMenu);

        SetupExtrasMenu();
        SetupPerformanceMenu();

        var performanceModsItem = new UIMenuItem("Performance Mods", "Tune vehicle performance");
        extrasMenu.AddItem(performanceModsItem);
        performanceModsItem.Activated += (menu, item) =>
        {
            UpdatePerformanceMenu();
            extrasMenu.Visible = false;
            performanceMenu.Visible = true;
        };

        var backItem = new UIMenuItem("← Back to Extras");
        performanceMenu.AddItem(backItem);
        backItem.Activated += (menu, item) =>
        {
            performanceMenu.Visible = false;
            extrasMenu.Visible = true;
        };

        GameFiber.StartNew(() =>
        {
            while (true)
            {
                menuPool.ProcessMenus();

                if (Game.IsKeyDown(openMenuKey) && !extrasMenu.Visible && !performanceMenu.Visible)
                {
                    Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
                    if (vehicle != null && vehicle.Exists())
                    {
                        UpdateExtrasMenu();
                        extrasMenu.Visible = true;
                    }
                    else
                    {
                        Game.Console.Print("[VehicleExtras] Menu not opened—player is not in a vehicle.");
                        Game.DisplayNotification("~r~Vehicle Extras menu unavailable.~w~ Enter a vehicle to customize.");
                    }
                }

                GameFiber.Yield();
            }
        });
    }

    private static void SetupExtrasMenu()
    {
        for (int i = 0; i <= 13; i++)
        {
            int extraId = i;
            var checkbox = new UIMenuCheckboxItem($"Extra {extraId}", false, $"Toggle Extra {extraId}");
            checkbox.CheckboxEvent += (item, checkedState) =>
            {
                Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
                if (vehicle != null && vehicle.Exists())
                {
                    NativeFunction.CallByName<bool>("SET_VEHICLE_EXTRA", vehicle, extraId, !checkedState);
                }
            };
            extrasMenu.AddItem(checkbox);
            extraCheckboxes.Add(checkbox);
        }

        var toggleAll = new UIMenuItem("Toggle All Extras");
        toggleAll.Activated += (menu, item) =>
        {
            Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
            if (vehicle != null && vehicle.Exists())
            {
                int enabledCount = extraCheckboxes.Count(cb => cb.Checked && cb.Enabled);
                int totalEnabled = extraCheckboxes.Count(cb => cb.Enabled);
                bool disableAll = enabledCount == totalEnabled;

                for (int i = 0; i <= 13; i++)
                {
                    bool exists = NativeFunction.CallByName<bool>("DOES_EXTRA_EXIST", vehicle, i);
                    if (exists)
                    {
                        NativeFunction.CallByName<bool>("SET_VEHICLE_EXTRA", vehicle, i, disableAll);
                        extraCheckboxes[i].Checked = !disableAll;
                    }
                }
            }
        };
        extrasMenu.AddItem(toggleAll);
    }

    private static void UpdateExtrasMenu()
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle == null || !vehicle.Exists())
            return;

        for (int i = 0; i <= 13; i++)
        {
            bool exists = NativeFunction.CallByName<bool>("DOES_EXTRA_EXIST", vehicle, i);
            bool enabled = exists && NativeFunction.CallByName<bool>("IS_VEHICLE_EXTRA_TURNED_ON", vehicle, i);

            extraCheckboxes[i].Enabled = exists;
            extraCheckboxes[i].Checked = enabled;
        }
    }

    private static void SetupPerformanceMenu()
    {
        // Placeholder items; actual lists will be populated in UpdatePerformanceMenu
        var engineMod = new UIMenuListItem("Engine", "Upgrade engine performance", new List<string> { "Loading..." });
        var brakesMod = new UIMenuListItem("Brakes", "Upgrade braking system", new List<dynamic> { "Loading..." });
        var transmissionMod = new UIMenuListItem("Transmission", "Upgrade gear shifting", new List<dynamic> { "Loading..." });
        var turboMod = new UIMenuCheckboxItem("Turbo", false, "Enable or disable turbo");

        performanceMenu.AddItem(engineMod);
        performanceMenu.AddItem(brakesMod);
        performanceMenu.AddItem(transmissionMod);
        performanceMenu.AddItem(turboMod);

        engineMod.OnListChanged += (item, index) => ApplyMod(11, index - 1);
        brakesMod.OnListChanged += (item, index) => ApplyMod(12, index - 1);
        transmissionMod.OnListChanged += (item, index) => ApplyMod(13, index - 1);
        turboMod.CheckboxEvent += (item, state) => ToggleTurbo(state);
    }

    private static void UpdatePerformanceMenu()
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle == null || !vehicle.Exists())
            return;

        NativeFunction.CallByName<int>("SET_VEHICLE_MOD_KIT", vehicle, 0);

        foreach (var item in performanceMenu.MenuItems)
        {
            if (item is UIMenuListItem listItem)
            {
                int modType = listItem.Text switch
                {
                    "Engine" => 11,
                    "Brakes" => 12,
                    "Transmission" => 13,
                    _ => -1
                };

                if (modType >= 0)
                {
                    int modCount = NativeFunction.CallByName<int>("GET_NUM_VEHICLE_MODS", vehicle, modType);
                    var levels = new List<string> { "Stock" };
                    for (int i = 0; i < modCount; i++)
                        levels.Add($"Level {i + 1}");

                    listItem.Collection.Clear();
                    foreach (string level in levels)
                    {
                        listItem.Collection.Add(level);
                    }

                    int currentMod = NativeFunction.CallByName<int>("GET_VEHICLE_MOD", vehicle, modType);
                    listItem.Index = currentMod + 1;
                }
            }
            else if (item is UIMenuCheckboxItem checkbox && checkbox.Text == "Turbo")
            {
                bool turboOn = NativeFunction.CallByName<bool>("IS_TOGGLE_MOD_ON", vehicle, 18);
                checkbox.Checked = turboOn;
            }
        }
    }

    private static void ApplyMod(int modType, int modIndex)
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle != null && vehicle.Exists())
        {
            NativeFunction.CallByName<int>("SET_VEHICLE_MOD_KIT", vehicle, 0);
            NativeFunction.CallByName<int>("SET_VEHICLE_MOD", vehicle, modType, modIndex, false);
            Game.Console.Print($"Applied mod {modType} level {modIndex}");
        }
    }

    private static void ToggleTurbo(bool enable)
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle != null && vehicle.Exists())
        {
            NativeFunction.CallByName<int>("SET_VEHICLE_MOD_KIT", vehicle, 0);
            NativeFunction.CallByName<bool>("TOGGLE_VEHICLE_MOD", vehicle, 18, enable);
            Game.Console.Print($"Turbo {(enable ? "enabled" : "disabled")}");
        }
    }
}