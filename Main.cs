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

[assembly: Plugin("VehicleExtrasConsole", Description = "Console + menu for vehicle extras", Author = "blackbyrd")]

public class EntryPoint
{
    private static MenuPool menuPool;
    private static UIMenu extrasMenu;
    private static List<UIMenuCheckboxItem> extraCheckboxes = new List<UIMenuCheckboxItem>();
    private static Keys openMenuKey = Keys.F10;

    public static void Main()
    {
        Game.Console.Print("VehicleExtrasConsole plugin loaded.");

        string iniPath = Path.Combine(Directory.GetCurrentDirectory(), "plugins", "VehicleExtras.ini");
        if (File.Exists(iniPath))
        {
            foreach (string line in File.ReadAllLines(iniPath))
            {
                if (line.StartsWith("OpenMenuKey=", System.StringComparison.OrdinalIgnoreCase))
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
        menuPool.Add(extrasMenu);

        SetupExtrasMenu();

        GameFiber.StartNew(() =>
        {
            while (true)
            {
                menuPool.ProcessMenus();

                if (Game.IsKeyDown(openMenuKey) && !extrasMenu.Visible)
                {
                    UpdateExtrasMenu();
                    extrasMenu.Visible = true;
                }

                GameFiber.Yield();
            }
        });
    }

    private static void SetupExtrasMenu()
    {
        for (int i = 0; i <= 13; i++)
        {
            int extraId = i; // Capture the current value of i
            var checkbox = new UIMenuCheckboxItem($"Extra {extraId}", false, $"Toggle Extra {extraId}");
            checkbox.CheckboxEvent += (item, checkedState) =>
            {
                Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
                if (vehicle != null && vehicle.Exists())
                {
                    //NativeFunction.CallByName<uint>("SET_VEHICLE_MOD_KIT", vehicle, 0);
                    NativeFunction.CallByName<uint>("SET_VEHICLE_FIXED", vehicle);
                    //NativeFunction.CallByName<bool>("SET_VEHICLE_EXTRA", vehicle, extraId, !checkedState); // false = enable
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
                // Determine current majority state
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
}

public static class VehicleExtrasCommands
{
    [ConsoleCommand]
    public static void ToggleExtra(int extraId, bool enable)
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle == null || !vehicle.Exists())
        {
            Game.Console.Print("Player is not in a vehicle.");
            return;
        }

        Game.Console.Print($"Toggling Extra {extraId} to {(enable ? "ON" : "OFF")}");
        NativeFunction.CallByName<bool>("SET_VEHICLE_EXTRA", vehicle, extraId, !enable);
    }

    [ConsoleCommand]
    public static void ToggleAllExtras(bool enable)
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle == null || !vehicle.Exists())
        {
            Game.Console.Print("Player is not in a vehicle.");
            return;
        }

        Game.Console.Print($"Toggling all extras {(enable ? "ON" : "OFF")}");
        for (int i = 0; i <= 13; i++)
        {
            bool exists = NativeFunction.CallByName<bool>("DOES_EXTRA_EXIST", vehicle, i);
            if (exists)
            {
                NativeFunction.CallByName<bool>("SET_VEHICLE_EXTRA", vehicle, i, !enable);
                Game.Console.Print($"Extra {i}: {(enable ? "Enabled" : "Disabled")}");
            }
        }
    }

    [ConsoleCommand]
    public static void ListExtras()
    {
        Vehicle vehicle = Game.LocalPlayer.Character.CurrentVehicle;
        if (vehicle == null || !vehicle.Exists())
        {
            Game.Console.Print("Player is not in a vehicle.");
            return;
        }

        Game.Console.Print("Listing extras for current vehicle:");
        for (int i = 0; i <= 13; i++)
        {
            bool exists = NativeFunction.CallByName<bool>("DOES_EXTRA_EXIST", vehicle, i);
            bool enabled = exists && NativeFunction.CallByName<bool>("IS_VEHICLE_EXTRA_TURNED_ON", vehicle, i);

            string status = exists ? (enabled ? "Enabled" : "Disabled") : "Missing";
            Game.Console.Print($"Extra {i}: {status}");
        }
    }
}