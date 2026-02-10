using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

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
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return GameObj.Null;

            var instanceProp = ssType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var ss = instanceProp?.GetValue(null);
            if (ss == null) return GameObj.Null;

            var ownedItemsProp = ssType.GetProperty("OwnedItems", BindingFlags.Public | BindingFlags.Instance);
            var ownedItems = ownedItemsProp?.GetValue(ss);
            if (ownedItems == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)ownedItems).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Inventory.GetOwnedItems", "Failed", ex);
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

            var actorType = GameType.Find("Menace.Tactical.Actor")?.ManagedType;
            if (actorType == null) return GameObj.Null;

            var proxy = GetManagedProxy(entity, actorType);
            if (proxy == null) return GameObj.Null;

            // Try GetItemContainer method
            var getContainerMethod = actorType.GetMethod("GetItemContainer",
                BindingFlags.Public | BindingFlags.Instance);
            var container = getContainerMethod?.Invoke(proxy, null);
            if (container == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)container).Pointer);
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

                // Get slot type from template
                var slotTypeProp = template.GetType().GetProperty("SlotType", BindingFlags.Public | BindingFlags.Instance);
                if (slotTypeProp != null)
                {
                    info.SlotType = Convert.ToInt32(slotTypeProp.GetValue(template));
                    info.SlotTypeName = GetSlotTypeName(info.SlotType);
                }
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

            // Get skill count
            var skillsProp = itemType.GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance);
            var skills = skillsProp?.GetValue(proxy);
            if (skills != null)
            {
                var countProp = skills.GetType().GetProperty("Count");
                info.SkillCount = (int)(countProp?.GetValue(skills) ?? 0);
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

            // Check for modular vehicle
            var modVehicleProp = containerType.GetProperty("ModularVehicle",
                BindingFlags.Public | BindingFlags.Instance);
            info.HasModularVehicle = modVehicleProp?.GetValue(proxy) != null;

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
    /// </summary>
    public static List<ItemInfo> GetItemsWithTag(GameObj container, string tag)
    {
        var result = new List<ItemInfo>();
        if (container.IsNull || string.IsNullOrEmpty(tag)) return result;

        try
        {
            EnsureTypesLoaded();

            var containerType = _itemContainerType?.ManagedType;
            if (containerType == null) return result;

            var proxy = GetManagedProxy(container, containerType);
            if (proxy == null) return result;

            var getTaggedMethod = containerType.GetMethod("GetItemsWithTag",
                BindingFlags.Public | BindingFlags.Instance);
            var items = getTaggedMethod?.Invoke(proxy, new object[] { tag });
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

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _itemType ??= GameType.Find("Menace.Items.Item");
        _baseItemType ??= GameType.Find("Menace.Items.BaseItem");
        _itemContainerType ??= GameType.Find("Menace.Items.ItemContainer");
        _itemTemplateType ??= GameType.Find("Menace.Items.BaseItemTemplate");
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
