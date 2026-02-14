using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for item and inventory operations.
/// Provides safe access to items, containers, equipment, and trade values.
///
/// Based on reverse engineering findings:
/// - Item.Template @ +0x18
/// - Item.Container @ +0x28
/// - Item.Skills @ +0x30
/// - ItemContainer.SlotLists[11] @ +0x10
/// - ItemContainer.Owner @ +0x18
/// </summary>
public static class Inventory
{
    // Cached types
    private static GameType _itemType;
    private static GameType _baseItemType;
    private static GameType _itemContainerType;
    private static GameType _itemTemplateType;
    private static GameType _strategyStateType;
    private static GameType _ownedItemsType;

    // Slot type constants
    public const int SLOT_WEAPON1 = 0;
    public const int SLOT_WEAPON2 = 1;
    public const int SLOT_ARMOR = 2;
    public const int SLOT_ACCESSORY1 = 3;
    public const int SLOT_ACCESSORY2 = 4;
    public const int SLOT_CONSUMABLE1 = 5;
    public const int SLOT_CONSUMABLE2 = 6;
    public const int SLOT_GRENADE = 7;
    public const int SLOT_VEHICLE_WEAPON = 8;
    public const int SLOT_VEHICLE_ARMOR = 9;
    public const int SLOT_VEHICLE_ACCESSORY = 10;
    public const int SLOT_TYPE_COUNT = 11;

    /// <summary>
    /// Item information structure.
    /// </summary>
    public class ItemInfo
    {
        public string GUID { get; set; }
        public string TemplateName { get; set; }
        public int SlotType { get; set; }
        public string SlotTypeName { get; set; }
        public int TradeValue { get; set; }
        public string Rarity { get; set; }
        public int SkillCount { get; set; }
        public bool IsTemporary { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Container information structure.
    /// </summary>
    public class ContainerInfo
    {
        public int TotalItems { get; set; }
        public int[] SlotCounts { get; set; }
        public bool HasModularVehicle { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the global OwnedItems manager.
    /// </summary>
    public static GameObj GetOwnedItems()
    {
        try
        {
            // Try direct approach: find StrategyState type in game assembly
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                SdkLogger.Warning("GetOwnedItems: Assembly-CSharp not found");
                return GameObj.Null;
            }

            var ssType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "Menace.States.StrategyState" ||
                                     t.Name == "StrategyState");

            if (ssType == null)
            {
                SdkLogger.Warning("GetOwnedItems: StrategyState type not found");
                return GameObj.Null;
            }

            // Use Get() static method to get StrategyState singleton
            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            if (getMethod == null)
            {
                SdkLogger.Warning("GetOwnedItems: StrategyState.Get() method not found");
                return GameObj.Null;
            }

            var ss = getMethod.Invoke(null, null);
            if (ss == null)
            {
                // This is normal when not on strategy map
                return GameObj.Null;
            }

            // Access OwnedItems at offset +0x80 (verified via REPL)
            var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
            var ownedItemsPtr = ssObj.ReadPtr(0x80);
            if (ownedItemsPtr == IntPtr.Zero)
            {
                SdkLogger.Warning("GetOwnedItems: OwnedItems at +0x80 is null");
                return GameObj.Null;
            }

            return new GameObj(ownedItemsPtr);
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"GetOwnedItems failed: {ex.Message}");
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the item container for an entity.
    /// </summary>
    public static GameObj GetContainer(GameObj entity)
    {
        if (entity.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            // Access IHasItemContainer.ItemContainer property
            // Actor implements IHasItemContainer interface which has ItemContainer property
            var hasContainerType = GameType.Find("Menace.Items.IHasItemContainer")?.ManagedType;
            if (hasContainerType != null)
            {
                var proxy = GetManagedProxy(entity, hasContainerType);
                if (proxy != null)
                {
                    var containerProp = hasContainerType.GetProperty("ItemContainer",
                        BindingFlags.Public | BindingFlags.Instance);
                    var container = containerProp?.GetValue(proxy);
                    if (container != null)
                        return new GameObj(((Il2CppObjectBase)container).Pointer);
                }
            }

            // Fallback: try direct field access via m_ItemContainer
            var containerPtr = entity.ReadPtr("m_ItemContainer");
            if (containerPtr != IntPtr.Zero)
                return new GameObj(containerPtr);

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetContainer", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all items in a container.
    /// </summary>
    public static List<ItemInfo> GetAllItems(GameObj container)
    {
        var result = new List<ItemInfo>();
        if (container.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return result;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return result;

            var getAllMethod = containerType.GetMethod("GetAllItems",
                BindingFlags.Public | BindingFlags.Instance);
            var items = getAllMethod?.Invoke(proxy, null);
            if (items == null) return result;

            var listType = items.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(items);
            for (int i = 0; i < count; i++)
            {
                var item = indexer.Invoke(items, new object[] { i });
                if (item == null) continue;

                var info = GetItemInfo(new GameObj(((Il2CppObjectBase)item).Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetAllItems", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get items in a specific slot type.
    /// </summary>
    public static List<ItemInfo> GetItemsInSlot(GameObj container, int slotType)
    {
        var result = new List<ItemInfo>();
        if (container.IsNull || slotType < 0 || slotType >= SLOT_TYPE_COUNT) return result;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return result;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return result;

            var getSlotMethod = containerType.GetMethod("GetAllItemsAtSlot",
                BindingFlags.Public | BindingFlags.Instance);
            var items = getSlotMethod?.Invoke(proxy, new object[] { slotType });
            if (items == null) return result;

            var listType = items.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(items);
            for (int i = 0; i < count; i++)
            {
                var item = indexer.Invoke(items, new object[] { i });
                if (item == null) continue;

                var info = GetItemInfo(new GameObj(((Il2CppObjectBase)item).Pointer));
                if (info != null)
                    result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemsInSlot", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get the item at a specific slot and index.
    /// </summary>
    public static GameObj GetItemAt(GameObj container, int slotType, int index)
    {
        if (container.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return GameObj.Null;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return GameObj.Null;

            var getItemMethod = containerType.GetMethod("GetItemAtSlot",
                BindingFlags.Public | BindingFlags.Instance);
            var item = getItemMethod?.Invoke(proxy, new object[] { slotType, index });
            if (item == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)item).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get item information.
    /// </summary>
    public static ItemInfo GetItemInfo(GameObj item)
    {
        if (item.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var itemType = _itemType?.ManagedType;
            if (itemType == null) return null;

            var proxy = GetManagedProxy(item, itemType);
            if (proxy == null) return null;

            var info = new ItemInfo { Pointer = item.Pointer };

            // Get GUID
            var getIdMethod = itemType.GetMethod("GetID", BindingFlags.Public | BindingFlags.Instance);
            if (getIdMethod != null)
                info.GUID = getIdMethod.Invoke(proxy, null)?.ToString();

            // Get template
            var getTemplateMethod = itemType.GetMethod("GetTemplate", BindingFlags.Public | BindingFlags.Instance);
            var template = getTemplateMethod?.Invoke(proxy, null);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();

                // Get slot type from template using m_SlotType field at offset +0xe8
                info.SlotType = templateObj.ReadInt(0xe8);
                info.SlotTypeName = GetSlotTypeName(info.SlotType);
            }

            // Get trade value
            var baseItemType = _baseItemType?.ManagedType;
            if (baseItemType != null)
            {
                var baseProxy = GetManagedProxy(item, baseItemType);
                if (baseProxy != null)
                {
                    var getTradeValueMethod = baseItemType.GetMethod("GetTradeValue",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getTradeValueMethod != null)
                        info.TradeValue = (int)getTradeValueMethod.Invoke(baseProxy, null);

                    var getRarityMethod = baseItemType.GetMethod("GetHighestRarity",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getRarityMethod != null)
                        info.Rarity = getRarityMethod.Invoke(baseProxy, null)?.ToString();

                    var isTempMethod = baseItemType.GetMethod("IsTemporary",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (isTempMethod != null)
                        info.IsTemporary = (bool)isTempMethod.Invoke(baseProxy, null);
                }
            }

            // Get skill count using m_Skills field (Item.Skills @ +0x30)
            var skillsPtr = item.ReadPtr("m_Skills");
            if (skillsPtr != IntPtr.Zero)
            {
                var skillsList = new GameList(skillsPtr);
                info.SkillCount = skillsList.Count;
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get container information.
    /// </summary>
    public static ContainerInfo GetContainerInfo(GameObj container)
    {
        if (container.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return null;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return null;

            var info = new ContainerInfo
            {
                Pointer = container.Pointer,
                SlotCounts = new int[SLOT_TYPE_COUNT]
            };

            // Get slot counts
            var getSlotCountMethod = containerType.GetMethod("GetItemSlotCount",
                BindingFlags.Public | BindingFlags.Instance);

            for (int slot = 0; slot < SLOT_TYPE_COUNT; slot++)
            {
                if (getSlotCountMethod != null)
                {
                    info.SlotCounts[slot] = (int)getSlotCountMethod.Invoke(proxy, new object[] { slot });
                    info.TotalItems += info.SlotCounts[slot];
                }
            }

            // Check for modular vehicle using field at offset +0x20
            var modVehiclePtr = container.ReadPtr(0x20);
            info.HasModularVehicle = modVehiclePtr != IntPtr.Zero;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetContainerInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Find an item by GUID.
    /// </summary>
    public static GameObj FindByGUID(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return GameObj.Null;

        try
        {
            var ownedItems = GetOwnedItems();
            if (ownedItems.IsNull) return GameObj.Null;

            EnsureTypesLoaded();

            var ownedType = _ownedItemsType?.ManagedType;
            if (ownedType == null) return GameObj.Null;

            var proxy = GetManagedProxy(ownedItems, ownedType);
            if (proxy == null) return GameObj.Null;

            var getByGuidMethod = ownedType.GetMethod("GetItemByGuid",
                BindingFlags.Public | BindingFlags.Instance);
            var item = getByGuidMethod?.Invoke(proxy, new object[] { guid });
            if (item == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)item).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Check if a container has an item with a specific tag.
    /// </summary>
    public static bool HasItemWithTag(GameObj container, string tag)
    {
        if (container.IsNull || string.IsNullOrEmpty(tag)) return false;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return false;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return false;

            var containsTagMethod = containerType.GetMethod("ContainsTag",
                BindingFlags.Public | BindingFlags.Instance);
            if (containsTagMethod != null)
            {
                return (bool)containsTagMethod.Invoke(proxy, new object[] { tag });
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get items with a specific tag.
    /// Note: GetItemsWithTag in game returns count (int), so we iterate all items and filter by tag.
    /// </summary>
    public static List<ItemInfo> GetItemsWithTag(GameObj container, string tag)
    {
        var result = new List<ItemInfo>();
        if (container.IsNull || string.IsNullOrEmpty(tag)) return result;

        try
        {
            // Get all items and filter by tag
            // The game's GetItemsWithTag returns a count, not a list
            var allItems = GetAllItems(container);

            foreach (var itemInfo in allItems)
            {
                if (itemInfo.Pointer == IntPtr.Zero) continue;

                var itemObj = new GameObj(itemInfo.Pointer);

                // Check if item has the tag using HasTag method on BaseItem
                EnsureTypesLoaded();
                var baseItemType = _baseItemType?.ManagedType;
                if (baseItemType != null)
                {
                    var proxy = GetManagedProxy(itemObj, baseItemType);
                    if (proxy != null)
                    {
                        var hasTagMethod = baseItemType.GetMethod("HasTag",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (hasTagMethod != null)
                        {
                            var hasTag = (bool)hasTagMethod.Invoke(proxy, new object[] { tag });
                            if (hasTag)
                                result.Add(itemInfo);
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetItemsWithTag", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get equipped weapons for an entity.
    /// </summary>
    public static List<ItemInfo> GetEquippedWeapons(GameObj entity)
    {
        var result = new List<ItemInfo>();
        var container = GetContainer(entity);
        if (container.IsNull) return result;

        result.AddRange(GetItemsInSlot(container, SLOT_WEAPON1));
        result.AddRange(GetItemsInSlot(container, SLOT_WEAPON2));

        return result;
    }

    /// <summary>
    /// Get equipped armor for an entity.
    /// </summary>
    public static ItemInfo GetEquippedArmor(GameObj entity)
    {
        var container = GetContainer(entity);
        if (container.IsNull) return null;

        var items = GetItemsInSlot(container, SLOT_ARMOR);
        return items.Count > 0 ? items[0] : null;
    }

    /// <summary>
    /// Get total trade value of all items in a container.
    /// </summary>
    public static int GetTotalTradeValue(GameObj container)
    {
        var items = GetAllItems(container);
        int total = 0;
        foreach (var item in items)
        {
            total += item.TradeValue;
        }
        return total;
    }

    /// <summary>
    /// Get slot type name.
    /// </summary>
    public static string GetSlotTypeName(int slotType)
    {
        return slotType switch
        {
            0 => "Weapon1",
            1 => "Weapon2",
            2 => "Armor",
            3 => "Accessory1",
            4 => "Accessory2",
            5 => "Consumable1",
            6 => "Consumable2",
            7 => "Grenade",
            8 => "VehicleWeapon",
            9 => "VehicleArmor",
            10 => "VehicleAccessory",
            _ => $"Slot{slotType}"
        };
    }

    /// <summary>
    /// Register console commands for Inventory SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // inventory - List items for selected entity
        DevConsole.RegisterCommand("inventory", "", "List inventory for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var container = GetContainer(actor);
            if (container.IsNull) return "No inventory container";

            var items = GetAllItems(container);
            if (items.Count == 0) return "Inventory empty";

            var lines = new List<string> { $"Inventory ({items.Count} items):" };
            foreach (var item in items)
            {
                var temp = item.IsTemporary ? " [TEMP]" : "";
                lines.Add($"  [{item.SlotTypeName}] {item.TemplateName} (${item.TradeValue}){temp}");
            }
            return string.Join("\n", lines);
        });

        // weapons - List equipped weapons
        DevConsole.RegisterCommand("weapons", "", "List equipped weapons for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var weapons = GetEquippedWeapons(actor);
            if (weapons.Count == 0) return "No weapons equipped";

            var lines = new List<string> { "Equipped Weapons:" };
            foreach (var w in weapons)
            {
                lines.Add($"  {w.TemplateName} ({w.Rarity ?? "Common"}) - {w.SkillCount} skills");
            }
            return string.Join("\n", lines);
        });

        // armor - Show equipped armor
        DevConsole.RegisterCommand("armor", "", "Show equipped armor for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var armor = GetEquippedArmor(actor);
            if (armor == null) return "No armor equipped";

            return $"Armor: {armor.TemplateName}\n" +
                   $"Rarity: {armor.Rarity ?? "Common"}\n" +
                   $"Trade Value: ${armor.TradeValue}\n" +
                   $"Skills: {armor.SkillCount}";
        });

        // slot <type> - List items in slot
        DevConsole.RegisterCommand("slot", "<type>", "List items in slot (0-10 or name)", args =>
        {
            if (args.Length == 0)
                return "Usage: slot <type>\nTypes: 0-10 or Weapon1/Weapon2/Armor/Accessory1/Accessory2/Consumable1/Consumable2/Grenade";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            int slotType;
            if (!int.TryParse(args[0], out slotType))
            {
                // Try parsing by name
                slotType = args[0].ToLower() switch
                {
                    "weapon1" => 0,
                    "weapon2" => 1,
                    "armor" => 2,
                    "accessory1" => 3,
                    "accessory2" => 4,
                    "consumable1" => 5,
                    "consumable2" => 6,
                    "grenade" => 7,
                    _ => -1
                };
            }

            if (slotType < 0 || slotType >= SLOT_TYPE_COUNT)
                return "Invalid slot type";

            var container = GetContainer(actor);
            if (container.IsNull) return "No inventory container";

            var items = GetItemsInSlot(container, slotType);
            if (items.Count == 0)
                return $"No items in {GetSlotTypeName(slotType)}";

            var lines = new List<string> { $"{GetSlotTypeName(slotType)} ({items.Count} items):" };
            foreach (var item in items)
            {
                lines.Add($"  {item.TemplateName} (${item.TradeValue})");
            }
            return string.Join("\n", lines);
        });

        // itemvalue - Get total trade value
        DevConsole.RegisterCommand("itemvalue", "", "Get total trade value of inventory", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var container = GetContainer(actor);
            if (container.IsNull) return "No inventory container";

            var total = GetTotalTradeValue(container);
            var items = GetAllItems(container);
            return $"Total Trade Value: ${total} ({items.Count} items)";
        });

        // spawn <template> - Spawn an item by template name
        DevConsole.RegisterCommand("spawn", "<template>", "Spawn an item by template name (strategy map only)", args =>
        {
            if (args.Length == 0)
                return "Usage: spawn <template_name>\nExample: spawn weapon.laser_smg\nNote: Must be on strategy map (world map), not in tactical combat or menus.";

            var templateName = args[0];
            var result = SpawnItem(templateName);
            return result;
        });

        // give <template> - Give item to selected actor (works in tactical)
        DevConsole.RegisterCommand("give", "<template>", "Give item to selected actor (tactical mode)", args =>
        {
            if (args.Length == 0)
                return "Usage: give <template_name>\nExample: give weapon.laser_smg";

            var templateName = args[0];
            var result = GiveItemToActor(templateName);
            return result;
        });

        // spawnlist [filter] - List available item templates
        DevConsole.RegisterCommand("spawnlist", "[filter]", "List item templates (optionally filtered)", args =>
        {
            var filter = args.Length > 0 ? args[0] : null;
            var templates = GetItemTemplates(filter);

            // Also search by partial match if exact search found nothing
            if (templates.Count == 0 && filter != null)
            {
                // Try broader search
                templates = GetItemTemplates(null)
                    .Where(t => t.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (templates.Count == 0)
                return filter != null ? $"No templates matching '{filter}'" : "No item templates found";

            var lines = new List<string> { $"Item Templates ({templates.Count}):" };
            foreach (var t in templates.Take(50))
            {
                lines.Add($"  {t}");
            }
            if (templates.Count > 50)
                lines.Add($"  ... and {templates.Count - 50} more (use filter to narrow down)");
            return string.Join("\n", lines);
        });

        // spawninfo - Debug info about spawn system
        DevConsole.RegisterCommand("spawninfo", "", "Show spawn system debug info", args =>
        {
            EnsureTypesLoaded();
            var lines = new List<string> { "Spawn System Info:" };

            // Check types
            lines.Add($"  WeaponTemplate type: {(_weaponTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  ArmorTemplate type: {(_armorTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  BaseItemTemplate type: {(_itemTemplateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  StrategyState type: {(_strategyStateType != null ? "Found" : "NOT FOUND")}");
            lines.Add($"  OwnedItems type: {(_ownedItemsType != null ? "Found" : "NOT FOUND")}");

            // Check strategy state using Get() method
            if (_strategyStateType?.ManagedType != null)
            {
                var getMethod = _strategyStateType.ManagedType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ss = getMethod?.Invoke(null, null);
                lines.Add($"  StrategyState.Get(): {(ss != null ? "Available" : "NULL")}");

                if (ss != null)
                {
                    var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
                    var ownedItemsPtr = ssObj.ReadPtr("m_OwnedItems");
                    lines.Add($"  StrategyState.m_OwnedItems: {(ownedItemsPtr != IntPtr.Zero ? "Available" : "NULL")}");
                }
            }

            // Count templates
            if (_weaponTemplateType?.ManagedType != null)
            {
                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(_weaponTemplateType.ManagedType);
                var weapons = Resources.FindObjectsOfTypeAll(il2cppType);
                lines.Add($"  WeaponTemplate count: {weapons?.Length ?? 0}");
            }

            return string.Join("\n", lines);
        });

        // hastag <tag> - Check for item with tag
        DevConsole.RegisterCommand("hastag", "<tag>", "Check if inventory has item with tag", args =>
        {
            if (args.Length == 0)
                return "Usage: hastag <tag>";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var container = GetContainer(actor);
            if (container.IsNull) return "No inventory container";

            var tag = args[0];
            var hasTag = HasItemWithTag(container, tag);
            if (hasTag)
            {
                var items = GetItemsWithTag(container, tag);
                return $"Has tag '{tag}': Yes ({items.Count} items)";
            }
            return $"Has tag '{tag}': No";
        });
    }

    /// <summary>
    /// Give an item to the selected actor in tactical mode.
    /// </summary>
    public static string GiveItemToActor(string templateName)
    {
        try
        {
            // Get selected actor
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull)
                return "No actor selected. Select a unit first.";

            // Find the template
            var template = FindItemTemplate(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found. Use 'spawnlist {templateName}' to search.";

            // Get actor's container
            var container = GetContainer(actor);
            if (container.IsNull)
                return "Actor has no item container";

            // Find BaseItemTemplate.CreateItem method
            EnsureTypesLoaded();
            var templateType = _itemTemplateType?.ManagedType;
            if (templateType == null)
                return "BaseItemTemplate type not found";

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null)
                return "Failed to get template proxy";

            // Call CreateItem on the template
            var createItemMethod = templateType.GetMethod("CreateItem",
                BindingFlags.Public | BindingFlags.Instance);

            if (createItemMethod == null)
                return "CreateItem method not found";

            // CreateItem takes a GUID string
            var guid = Guid.NewGuid().ToString();
            var item = createItemMethod.Invoke(templateProxy, new object[] { guid });

            if (item == null)
                return "CreateItem returned null";

            // Add to container
            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null)
                return "ItemContainer type not found";

            var containerProxy = GetManagedProxy(container, containerType);
            if (containerProxy == null)
                return "Failed to get container proxy";

            // Use Place() method to add item to container
            // ItemContainer.Place(BaseItem item) - adds item to appropriate slot
            var placeMethod = containerType.GetMethod("Place",
                BindingFlags.Public | BindingFlags.Instance);

            if (placeMethod != null)
            {
                placeMethod.Invoke(containerProxy, new object[] { item });
                return $"Gave {templateName} to {actor.GetName()}";
            }

            return "Could not find Place() method on ItemContainer";
        }
        catch (Exception ex)
        {
            return $"Give failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Spawn an item by template name and add it to owned items.
    /// </summary>
    public static string SpawnItem(string templateName)
    {
        try
        {
            EnsureTypesLoaded();

            // Find the template - try multiple template types
            var template = FindItemTemplate(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found. Use 'spawnlist {templateName}' to search.";

            // Get OwnedItems with detailed diagnostics
            var ownedItems = GetOwnedItems();
            if (ownedItems.IsNull)
            {
                // Provide more diagnostic info
                var ssType = _strategyStateType?.ManagedType;
                if (ssType == null)
                    return "Error: StrategyState type not found";

                var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var ss = getMethod?.Invoke(null, null);
                if (ss == null)
                    return "Error: StrategyState.Get() returned null (are you on the strategy map?)";

                var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
                var ownedItemsPtr = ssObj.ReadPtr(0x80);
                if (ownedItemsPtr == IntPtr.Zero)
                    return "Error: StrategyState OwnedItems at +0x80 is null";

                return "Error: Could not get OwnedItems";
            }

            var ownedType = _ownedItemsType?.ManagedType;
            if (ownedType == null)
                return "OwnedItems type not found";

            var ownedProxy = GetManagedProxy(ownedItems, ownedType);
            if (ownedProxy == null)
                return "Failed to get OwnedItems proxy";

            // Find the AddItem method - AddItem(BaseItemTemplate, bool showReward)
            var addItemMethod = ownedType.GetMethod("AddItem",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { _itemTemplateType.ManagedType, typeof(bool) },
                null);

            if (addItemMethod == null)
            {
                // Try alternate signature
                var methods = ownedType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "AddItem")
                    .ToList();

                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[1].ParameterType == typeof(bool))
                    {
                        addItemMethod = m;
                        break;
                    }
                }
            }

            if (addItemMethod == null)
                return "AddItem method not found on OwnedItems";

            // Get template proxy
            var templateProxy = GetManagedProxy(template, _itemTemplateType.ManagedType);
            if (templateProxy == null)
                return "Failed to get template proxy";

            // Call AddItem(template, false) - false = don't show reward UI
            var item = addItemMethod.Invoke(ownedProxy, new object[] { templateProxy, false });

            if (item != null)
            {
                var itemObj = new GameObj(((Il2CppObjectBase)item).Pointer);
                return $"Spawned: {templateName} (ID: {itemObj.GetName()})";
            }
            else
            {
                return $"Spawned: {templateName} (item added to inventory)";
            }
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            return $"Failed to spawn item: {inner.Message}";
        }
    }

    // Additional template types to search
    private static GameType _weaponTemplateType;
    private static GameType _armorTemplateType;

    /// <summary>
    /// Find an item template by name. Searches multiple template types.
    /// </summary>
    public static GameObj FindItemTemplate(string templateName)
    {
        try
        {
            EnsureTypesLoaded();

            // Try multiple template types
            var typesToSearch = new[]
            {
                _weaponTemplateType?.ManagedType,
                _armorTemplateType?.ManagedType,
                _itemTemplateType?.ManagedType
            };

            foreach (var templateType in typesToSearch)
            {
                if (templateType == null) continue;

                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (obj != null && obj.name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                        {
                            return new GameObj(((Il2CppObjectBase)obj).Pointer);
                        }
                    }
                }
            }

            return GameObj.Null;
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all item template names, optionally filtered. Searches multiple template types.
    /// </summary>
    public static List<string> GetItemTemplates(string filter = null)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            EnsureTypesLoaded();

            // Search multiple template types
            var typesToSearch = new[]
            {
                _weaponTemplateType?.ManagedType,
                _armorTemplateType?.ManagedType,
                _itemTemplateType?.ManagedType
            };

            foreach (var templateType in typesToSearch)
            {
                if (templateType == null) continue;

                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (obj == null || string.IsNullOrEmpty(obj.name)) continue;

                        if (filter == null ||
                            obj.name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(obj.name);
                        }
                    }
                }
            }

            var sorted = result.ToList();
            sorted.Sort();
            return sorted;
        }
        catch
        {
            return new List<string>();
        }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _itemType ??= GameType.Find("Menace.Items.Item");
        _baseItemType ??= GameType.Find("Menace.Items.BaseItem");
        _itemContainerType ??= GameType.Find("Menace.Items.ItemContainer");
        _itemTemplateType ??= GameType.Find("Menace.Items.BaseItemTemplate");
        _weaponTemplateType ??= GameType.Find("Menace.Items.WeaponTemplate");
        _armorTemplateType ??= GameType.Find("Menace.Items.ArmorTemplate");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
        _ownedItemsType ??= GameType.Find("Menace.Strategy.OwnedItems");
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
