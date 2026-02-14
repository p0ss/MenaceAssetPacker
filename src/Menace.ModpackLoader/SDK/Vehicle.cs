using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for vehicle operations.
/// Provides safe access to vehicle health, armor, modular equipment, and twin-fire detection.
///
/// Based on reverse engineering findings:
/// - Vehicle.m_HitpointsPct @ +0x20
/// - Vehicle.m_ArmorDurabilityPct @ +0x24
/// - Vehicle.EquipmentSkills @ +0x28
/// - ItemsModularVehicle.Slots @ +0x18
/// - ItemsModularVehicle.IsTwinFire @ +0x20
/// </summary>
public static class Vehicle
{
    // Cached types
    private static GameType _vehicleType;
    private static GameType _modularVehicleType;
    private static GameType _slotType;
    private static GameType _vehicleTemplateType;

    // Modular slot types
    public const int MODULAR_WEAPON = 0;
    public const int MODULAR_ARMOR = 1;
    public const int MODULAR_ACCESSORY = 2;

    /// <summary>
    /// Vehicle information structure.
    /// </summary>
    public class VehicleInfo
    {
        public string TemplateName { get; set; }
        public float HitpointsPct { get; set; }
        public float ArmorDurabilityPct { get; set; }
        public int BaseHp { get; set; }
        public int MaxHp { get; set; }
        public int Armor { get; set; }
        public int EquippedSlots { get; set; }
        public bool HasTwinFire { get; set; }
        public List<SlotInfo> Slots { get; set; } = new();
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Slot information structure.
    /// </summary>
    public class SlotInfo
    {
        public int SlotType { get; set; }
        public string SlotTypeName { get; set; }
        public bool IsEnabled { get; set; }
        public string EquippedItem { get; set; }
        public bool HasItem { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get vehicle information for an entity.
    /// </summary>
    public static VehicleInfo GetVehicleInfo(GameObj entity)
    {
        if (entity.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var vehicleType = _vehicleType?.ManagedType;
            if (vehicleType == null) return null;

            var proxy = GetManagedProxy(entity, vehicleType);
            if (proxy == null) return null;

            var info = new VehicleInfo { Pointer = entity.Pointer };

            // Get template
            var templateProp = vehicleType.GetProperty("EntityTemplate", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(proxy);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get health
            var hpPctProp = vehicleType.GetProperty("m_HitpointsPct", BindingFlags.Public | BindingFlags.Instance);
            if (hpPctProp != null)
                info.HitpointsPct = (float)hpPctProp.GetValue(proxy);

            var armorPctProp = vehicleType.GetProperty("m_ArmorDurabilityPct", BindingFlags.Public | BindingFlags.Instance);
            if (armorPctProp != null)
                info.ArmorDurabilityPct = (float)armorPctProp.GetValue(proxy);

            var getBaseHpMethod = vehicleType.GetMethod("GetBaseHp", BindingFlags.Public | BindingFlags.Instance);
            if (getBaseHpMethod != null)
                info.BaseHp = (int)getBaseHpMethod.Invoke(proxy, null);

            var getMaxHpMethod = vehicleType.GetMethod("GetBaseMaxHp", BindingFlags.Public | BindingFlags.Instance);
            if (getMaxHpMethod != null)
                info.MaxHp = (int)getMaxHpMethod.Invoke(proxy, null);

            var getArmorMethod = vehicleType.GetMethod("GetArmor", BindingFlags.Public | BindingFlags.Instance);
            if (getArmorMethod != null)
                info.Armor = (int)getArmorMethod.Invoke(proxy, null);

            // Get modular vehicle info
            var modVehicle = GetModularVehicle(entity);
            if (modVehicle != null)
            {
                info.HasTwinFire = modVehicle.HasTwinFire;
                info.EquippedSlots = modVehicle.EquippedCount;
                info.Slots = modVehicle.Slots;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Vehicle.GetVehicleInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Modular vehicle wrapper info.
    /// </summary>
    public class ModularVehicleInfo
    {
        public bool HasTwinFire { get; set; }
        public int EquippedCount { get; set; }
        public List<SlotInfo> Slots { get; set; } = new();
    }

    /// <summary>
    /// Get modular vehicle information.
    /// </summary>
    public static ModularVehicleInfo GetModularVehicle(GameObj entity)
    {
        if (entity.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            // Get ItemContainer first
            var container = Inventory.GetContainer(entity);
            if (container.IsNull) return null;

            var containerType = GameType.Find("Menace.Items.ItemContainer")?.ManagedType;
            if (containerType == null) return null;

            var containerProxy = GetManagedProxy(container, containerType);
            if (containerProxy == null) return null;

            var modVehicleProp = containerType.GetProperty("m_ModularVehicle",
                BindingFlags.Public | BindingFlags.Instance);
            var modVehicle = modVehicleProp?.GetValue(containerProxy);
            if (modVehicle == null) return null;

            var modType = _modularVehicleType?.ManagedType;
            if (modType == null) return null;

            var info = new ModularVehicleInfo();

            // Get IsTwinFire
            var twinFireProp = modType.GetProperty("IsTwinFire", BindingFlags.Public | BindingFlags.Instance);
            if (twinFireProp != null)
                info.HasTwinFire = (bool)twinFireProp.GetValue(modVehicle);

            // Get slots
            var slotsProp = modType.GetProperty("Slots", BindingFlags.Public | BindingFlags.Instance);
            var slots = slotsProp?.GetValue(modVehicle) as Array;
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot == null) continue;
                    var slotInfo = GetSlotInfo(new GameObj(((Il2CppObjectBase)slot).Pointer));
                    if (slotInfo != null)
                    {
                        info.Slots.Add(slotInfo);
                        if (slotInfo.HasItem)
                            info.EquippedCount++;
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Vehicle.GetModularVehicle", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get slot information.
    /// </summary>
    public static SlotInfo GetSlotInfo(GameObj slot)
    {
        if (slot.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var slotType = _slotType?.ManagedType;
            if (slotType == null) return null;

            var proxy = GetManagedProxy(slot, slotType);
            if (proxy == null) return null;

            var info = new SlotInfo { Pointer = slot.Pointer };

            // Slot is always enabled (no IsEnabled property)
            info.IsEnabled = true;

            // Get Data for slot type (returns ModularVehicleSlot with SlotType)
            var dataProp = slotType.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
            var data = dataProp?.GetValue(proxy);
            if (data != null)
            {
                var slotTypeProp = data.GetType().GetProperty("SlotType",
                    BindingFlags.Public | BindingFlags.Instance);
                if (slotTypeProp != null)
                {
                    info.SlotType = (int)slotTypeProp.GetValue(data);
                    info.SlotTypeName = GetSlotTypeName(info.SlotType);
                }
            }

            // Get mounted weapon
            var mountedProp = slotType.GetProperty("MountedWeapon", BindingFlags.Public | BindingFlags.Instance);
            var mounted = mountedProp?.GetValue(proxy);
            if (mounted != null)
            {
                info.HasItem = true;
                var itemObj = new GameObj(((Il2CppObjectBase)mounted).Pointer);
                var itemInfo = Inventory.GetItemInfo(itemObj);
                info.EquippedItem = itemInfo?.TemplateName ?? "Unknown";
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Vehicle.GetSlotInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if entity is a vehicle.
    /// </summary>
    public static bool IsVehicle(GameObj entity)
    {
        if (entity.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var vehicleType = _vehicleType?.ManagedType;
            if (vehicleType == null) return false;

            var proxy = GetManagedProxy(entity, vehicleType);
            return proxy != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get slot type name.
    /// </summary>
    public static string GetSlotTypeName(int slotType)
    {
        return slotType switch
        {
            0 => "Weapon",
            1 => "Armor",
            2 => "Accessory",
            _ => $"Type{slotType}"
        };
    }

    /// <summary>
    /// Register console commands for Vehicle SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // vehicle - Show vehicle info for selected actor
        DevConsole.RegisterCommand("vehicle", "", "Show vehicle info for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            if (!IsVehicle(actor))
                return "Selected actor is not a vehicle";

            var info = GetVehicleInfo(actor);
            if (info == null)
                return "Could not get vehicle info";

            var lines = new List<string>
            {
                $"Vehicle: {info.TemplateName}",
                $"HP: {info.BaseHp}/{info.MaxHp} ({info.HitpointsPct:P0})",
                $"Armor: {info.Armor} (Durability: {info.ArmorDurabilityPct:P0})",
                $"Equipped Slots: {info.EquippedSlots}",
                $"Twin-Fire: {info.HasTwinFire}"
            };

            if (info.Slots.Count > 0)
            {
                lines.Add("Slots:");
                foreach (var slot in info.Slots)
                {
                    var item = slot.HasItem ? slot.EquippedItem : "(empty)";
                    var enabled = slot.IsEnabled ? "" : " [disabled]";
                    lines.Add($"  [{slot.SlotTypeName}] {item}{enabled}");
                }
            }

            return string.Join("\n", lines);
        });

        // twinfire - Check twin-fire status
        DevConsole.RegisterCommand("twinfire", "", "Check twin-fire status for selected vehicle", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            if (!IsVehicle(actor))
                return "Selected actor is not a vehicle";

            var modular = GetModularVehicle(actor);
            if (modular == null)
                return "No modular vehicle data";

            return $"Twin-Fire Active: {modular.HasTwinFire}\n" +
                   $"Equipped Slots: {modular.EquippedCount}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _vehicleType ??= GameType.Find("Menace.Strategy.Vehicle");
        _modularVehicleType ??= GameType.Find("Menace.Strategy.ItemsModularVehicle");
        _slotType ??= GameType.Find("Menace.Strategy.ModularVehicleSlot");
        _vehicleTemplateType ??= GameType.Find("Menace.Strategy.ModularVehicleTemplate");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { obj.Pointer });
        }
        catch
        {
            return null;
        }
    }
}
