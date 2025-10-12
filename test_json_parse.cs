using System;
using System.Text.Json;
using System.IO;

var menuPath = "/home/poss/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData/menu.json";
var menuJson = File.ReadAllText(menuPath);
var menuRoot = JsonDocument.Parse(menuJson).RootElement;

// Navigate to weapon
var itemTemplate = menuRoot.GetProperty("ItemTemplate");
var weaponTemplate = itemTemplate.GetProperty("WeaponTemplate");
var modularVehicle = weaponTemplate.GetProperty("ModularVehicleWeaponTemplate");
var modWeapon = modularVehicle.GetProperty("mod_weapon");
var heavy = modWeapon.GetProperty("heavy");
var cannonLong = heavy.GetProperty("cannon_long");

Console.WriteLine($"ValueKind: {cannonLong.ValueKind}");
Console.WriteLine($"Has template_type: {cannonLong.TryGetProperty("template_type", out _)}");
Console.WriteLine($"Has data: {cannonLong.TryGetProperty("data", out var dataElement)}");

if (cannonLong.TryGetProperty("data", out dataElement))
{
    Console.WriteLine($"Data ValueKind: {dataElement.ValueKind}");
    Console.WriteLine("Data properties:");
    int count = 0;
    foreach (var prop in dataElement.EnumerateObject())
    {
        count++;
        Console.WriteLine($"  {prop.Name}: {prop.Value}");
        if (count >= 5) break;
    }
}
