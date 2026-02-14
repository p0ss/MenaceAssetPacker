using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the BlackMarket shop system.
/// Provides safe access to purchasable items, item stacks, and shop management.
///
/// Based on reverse engineering findings:
/// - BlackMarket.Stacks @ +0x10 (List of BlackMarketItemStack)
/// - BlackMarketItemStack.Template @ +0x10
/// - BlackMarketItemStack.OperationsRemaining @ +0x18
/// - BlackMarketItemStack.Items @ +0x20
/// - BlackMarketItemStack.Type @ +0x28
/// - StrategyState.BlackMarket @ +0x88 (field, not property)
/// - StrategyConfig.BlackMarket @ +0x198 (BlackMarketConfig sub-object)
///   - BlackMarketConfig.Items @ +0x78 (BaseItemTemplate[] item pool)
///   - BlackMarketConfig.MinItems/MaxItems @ +0x80
///   - BlackMarketConfig.OperationsTimeout @ +0xac (range)
/// </summary>
public static class BlackMarket
{
    // Cached types
    private static GameType _blackMarketType;
    private static GameType _blackMarketItemStackType;
    private static GameType _strategyStateType;
    private static GameType _strategyConfigType;
    private static GameType _baseItemTemplateType;
    private static GameType _baseItemType;

    /// <summary>
    /// Stack type enumeration matching game's BlackMarketStackType.
    /// </summary>
    public enum StackType
    {
        /// <summary>Normal shop item generated during FillUp.</summary>
        Generated = 0,
        /// <summary>Item that never expires (OperationsRemaining ignored).</summary>
        Permanent = 1,
        /// <summary>Special one-time unique item.</summary>
        Unique = 2,
        /// <summary>Item added as a mission or event reward.</summary>
        Reward = 3,
        /// <summary>Special offer item (IsSpecialOffer returns true when type == 4).</summary>
        SpecialOffer = 4
    }

    // Default configuration values from StrategyConfig
    /// <summary>Default minimum items to generate during FillUp.</summary>
    public const int DEFAULT_MIN_ITEMS = 3;
    /// <summary>Default maximum items to generate during FillUp.</summary>
    public const int DEFAULT_MAX_ITEMS = 6;
    /// <summary>Default number of operations before item removal.</summary>
    public const int DEFAULT_TIMEOUT = 3;

    // Field offsets from reverse engineering
    private const int OFFSET_STACKS = 0x10;
    private const int OFFSET_TEMPLATE = 0x10;
    private const int OFFSET_OPERATIONS_REMAINING = 0x18;
    private const int OFFSET_ITEMS = 0x20;
    private const int OFFSET_TYPE = 0x28;
    // StrategyState.BlackMarket field offset
    private const int OFFSET_SS_BLACKMARKET = 0x88;
    // StrategyConfig.BlackMarket (BlackMarketConfig sub-object)
    private const int OFFSET_CONFIG_BM = 0x198;
    // BlackMarketConfig field offsets (relative to BlackMarketConfig)
    private const int OFFSET_BMC_ITEMS = 0x78;
    private const int OFFSET_BMC_MIN_MAX = 0x80;
    private const int OFFSET_BMC_TIMEOUT = 0xAC;

    /// <summary>
    /// BlackMarket information structure containing shop state and configuration.
    /// </summary>
    public class BlackMarketInfo
    {
        /// <summary>Number of item stacks currently available in the shop.</summary>
        public int StackCount { get; set; }
        /// <summary>Total number of individual items across all stacks.</summary>
        public int TotalItemCount { get; set; }
        /// <summary>Minimum items generated per FillUp from config.</summary>
        public int MinItems { get; set; }
        /// <summary>Maximum items generated per FillUp from config.</summary>
        public int MaxItems { get; set; }
        /// <summary>Operations until item removal from config.</summary>
        public int ItemTimeout { get; set; }
        /// <summary>Number of templates available in the item pool.</summary>
        public int ItemPoolSize { get; set; }
        /// <summary>Current campaign progress level affecting item generation.</summary>
        public int CampaignProgress { get; set; }
        /// <summary>Pointer to the BlackMarket instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Item stack information representing a purchasable item entry in the shop.
    /// </summary>
    public class ItemStackInfo
    {
        /// <summary>Name of the item template.</summary>
        public string TemplateName { get; set; }
        /// <summary>Operations remaining before this stack is removed.</summary>
        public int OperationsRemaining { get; set; }
        /// <summary>Number of item instances available in this stack.</summary>
        public int ItemCount { get; set; }
        /// <summary>Category type of this stack.</summary>
        public StackType Type { get; set; }
        /// <summary>Display name for the stack type.</summary>
        public string TypeName { get; set; }
        /// <summary>Trade value of items in this stack.</summary>
        public int TradeValue { get; set; }
        /// <summary>Rarity of items in this stack.</summary>
        public string Rarity { get; set; }
        /// <summary>Whether this stack will expire (Type != Permanent).</summary>
        public bool WillExpire { get; set; }
        /// <summary>Pointer to the BlackMarketItemStack instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Individual item information for items within a stack.
    /// </summary>
    public class ItemInfo
    {
        /// <summary>Unique identifier for this item instance.</summary>
        public string GUID { get; set; }
        /// <summary>Name of the item template.</summary>
        public string TemplateName { get; set; }
        /// <summary>Trade value of this item.</summary>
        public int TradeValue { get; set; }
        /// <summary>Rarity tier of this item.</summary>
        public string Rarity { get; set; }
        /// <summary>Number of skills on this item.</summary>
        public int SkillCount { get; set; }
        /// <summary>Pointer to the BaseItem instance.</summary>
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the BlackMarket instance from StrategyState.
    /// </summary>
    /// <returns>GameObj representing the BlackMarket, or GameObj.Null if unavailable.</returns>
    public static GameObj GetBlackMarket()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return GameObj.Null;

            // Use Get() static method instead of s_Singleton property
            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return GameObj.Null;

            // Access BlackMarket via field at offset 0x88, not property
            var ssObj = new GameObj(((Il2CppObjectBase)ss).Pointer);
            var blackMarketPtr = ssObj.ReadPtr((uint)OFFSET_SS_BLACKMARKET);
            if (blackMarketPtr == IntPtr.Zero) return GameObj.Null;

            return new GameObj(blackMarketPtr);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetBlackMarket", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get detailed information about the BlackMarket state.
    /// </summary>
    /// <returns>BlackMarketInfo containing shop state, or null if unavailable.</returns>
    public static BlackMarketInfo GetBlackMarketInfo()
    {
        var bm = GetBlackMarket();
        if (bm.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var info = new BlackMarketInfo { Pointer = bm.Pointer };

            // Get stacks list using direct offset access (Stacks property doesn't exist)
            var (stacks, listType, count) = GetStacksList(bm);
            info.StackCount = count;

            if (stacks != null && listType != null)
            {
                // Count total items across all stacks
                var indexer = listType.GetMethod("get_Item");
                for (int i = 0; i < info.StackCount; i++)
                {
                    var stack = indexer?.Invoke(stacks, new object[] { i });
                    if (stack != null)
                    {
                        var stackInfo = GetItemStackInfoInternal(stack);
                        if (stackInfo != null)
                            info.TotalItemCount += stackInfo.ItemCount;
                    }
                }
            }

            // Get config values
            var config = GetStrategyConfig();
            if (config != null)
            {
                var configType = _strategyConfigType?.ManagedType;
                if (configType != null)
                {
                    var configProxy = GetManagedProxy(config.Value, configType);
                    if (configProxy != null)
                    {
                        var minProp = configType.GetProperty("BlackMarketMinItems", BindingFlags.Public | BindingFlags.Instance);
                        var maxProp = configType.GetProperty("BlackMarketMaxItems", BindingFlags.Public | BindingFlags.Instance);
                        var timeoutProp = configType.GetProperty("BlackMarketItemTimeout", BindingFlags.Public | BindingFlags.Instance);
                        var itemsProp = configType.GetProperty("BlackMarketItems", BindingFlags.Public | BindingFlags.Instance);

                        if (minProp != null) info.MinItems = (int)minProp.GetValue(configProxy);
                        if (maxProp != null) info.MaxItems = (int)maxProp.GetValue(configProxy);
                        if (timeoutProp != null) info.ItemTimeout = (int)timeoutProp.GetValue(configProxy);

                        var itemPool = itemsProp?.GetValue(configProxy);
                        if (itemPool != null)
                        {
                            var poolCount = itemPool.GetType().GetProperty("Count");
                            info.ItemPoolSize = (int)(poolCount?.GetValue(itemPool) ?? 0);
                        }
                    }
                }
            }

            // Get campaign progress
            info.CampaignProgress = GetCampaignProgress();

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetBlackMarketInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get all available item stacks in the BlackMarket.
    /// </summary>
    /// <returns>List of ItemStackInfo for each available stack.</returns>
    public static List<ItemStackInfo> GetAvailableStacks()
    {
        var result = new List<ItemStackInfo>();

        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return result;

            EnsureTypesLoaded();

            // Get stacks list using direct offset access (Stacks property doesn't exist)
            var (stacks, listType, count) = GetStacksList(bm);
            if (stacks == null || listType == null) return result;

            var indexer = listType.GetMethod("get_Item");

            for (int i = 0; i < count; i++)
            {
                var stack = indexer.Invoke(stacks, new object[] { i });
                if (stack == null) continue;

                var stackInfo = GetItemStackInfoInternal(stack);
                if (stackInfo != null)
                    result.Add(stackInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetAvailableStacks", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get information about a specific item stack by index.
    /// </summary>
    /// <param name="index">Index of the stack in the Stacks list.</param>
    /// <returns>ItemStackInfo for the stack, or null if invalid.</returns>
    public static ItemStackInfo GetStackInfo(int index)
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return null;

            EnsureTypesLoaded();

            // Get stacks list using direct offset access (Stacks property doesn't exist)
            var (stacks, listType, count) = GetStacksList(bm);
            if (stacks == null || listType == null) return null;

            if (index < 0 || index >= count) return null;

            var indexer = listType.GetMethod("get_Item");
            var stack = indexer.Invoke(stacks, new object[] { index });
            if (stack == null) return null;

            return GetItemStackInfoInternal(stack);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetStackInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the GameObj for a stack by index.
    /// </summary>
    /// <param name="index">Index of the stack in the Stacks list.</param>
    /// <returns>GameObj representing the stack, or GameObj.Null if invalid.</returns>
    public static GameObj GetStackAt(int index)
    {
        try
        {
            var bm = GetBlackMarket();
            if (bm.IsNull) return GameObj.Null;

            EnsureTypesLoaded();

            // Get stacks list using direct offset access (Stacks property doesn't exist)
            var (stacks, listType, count) = GetStacksList(bm);
            if (stacks == null || listType == null) return GameObj.Null;

            if (index < 0 || index >= count) return GameObj.Null;

            var indexer = listType.GetMethod("get_Item");
            var stack = indexer.Invoke(stacks, new object[] { index });
            if (stack == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)stack).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all items within a specific stack.
    /// </summary>
    /// <param name="stack">The stack GameObj to inspect.</param>
    /// <returns>List of ItemInfo for items in the stack.</returns>
    public static List<ItemInfo> GetItemsInStack(GameObj stack)
    {
        var result = new List<ItemInfo>();
        if (stack.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var stackType = _blackMarketItemStackType?.ManagedType;
            if (stackType == null) return result;

            var proxy = GetManagedProxy(stack, stackType);
            if (proxy == null) return result;

            var itemsProp = stackType.GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
            var items = itemsProp?.GetValue(proxy);
            if (items == null) return result;

            var listType = items.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(items);
            for (int i = 0; i < count; i++)
            {
                var item = indexer.Invoke(items, new object[] { i });
                if (item == null) continue;

                var itemInfo = GetItemInfoInternal(item);
                if (itemInfo != null)
                    result.Add(itemInfo);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemsInStack", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Find a stack by template name.
    /// </summary>
    /// <param name="templateName">Name of the item template to find.</param>
    /// <returns>ItemStackInfo for the matching stack, or null if not found.</returns>
    public static ItemStackInfo FindStackByTemplate(string templateName)
    {
        if (string.IsNullOrEmpty(templateName)) return null;

        try
        {
            var stacks = GetAvailableStacks();
            foreach (var stack in stacks)
            {
                if (stack.TemplateName != null &&
                    stack.TemplateName.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                {
                    return stack;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if the BlackMarket contains a specific item template.
    /// </summary>
    /// <param name="templateName">Name of the item template to check.</param>
    /// <returns>True if the template is available for purchase.</returns>
    public static bool HasTemplate(string templateName)
    {
        return FindStackByTemplate(templateName) != null;
    }

    /// <summary>
    /// Get the total number of available stacks.
    /// </summary>
    /// <returns>Number of stacks in the BlackMarket.</returns>
    public static int GetStackCount()
    {
        var info = GetBlackMarketInfo();
        return info?.StackCount ?? 0;
    }

    /// <summary>
    /// Get stacks that are expiring soon (1 operation remaining).
    /// </summary>
    /// <returns>List of ItemStackInfo for stacks about to expire.</returns>
    public static List<ItemStackInfo> GetExpiringStacks()
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var stacks = GetAvailableStacks();
            foreach (var stack in stacks)
            {
                if (stack.WillExpire && stack.OperationsRemaining <= 1)
                {
                    result.Add(stack);
                }
            }
            return result;
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// Get permanent stacks that never expire.
    /// </summary>
    /// <returns>List of ItemStackInfo for permanent stacks.</returns>
    public static List<ItemStackInfo> GetPermanentStacks()
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var stacks = GetAvailableStacks();
            foreach (var stack in stacks)
            {
                if (stack.Type == StackType.Permanent)
                {
                    result.Add(stack);
                }
            }
            return result;
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// Get stacks of a specific type.
    /// </summary>
    /// <param name="type">Stack type to filter by.</param>
    /// <returns>List of ItemStackInfo matching the type.</returns>
    public static List<ItemStackInfo> GetStacksByType(StackType type)
    {
        var result = new List<ItemStackInfo>();
        try
        {
            var stacks = GetAvailableStacks();
            foreach (var stack in stacks)
            {
                if (stack.Type == type)
                {
                    result.Add(stack);
                }
            }
            return result;
        }
        catch
        {
            return result;
        }
    }

    /// <summary>
    /// Get the total trade value of all items in the BlackMarket.
    /// </summary>
    /// <returns>Sum of trade values for all available items.</returns>
    public static int GetTotalTradeValue()
    {
        try
        {
            var stacks = GetAvailableStacks();
            int total = 0;
            foreach (var stack in stacks)
            {
                total += stack.TradeValue * stack.ItemCount;
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get the stack type name from a StackType value.
    /// </summary>
    /// <param name="type">StackType value to convert.</param>
    /// <returns>Human-readable name for the stack type.</returns>
    public static string GetStackTypeName(StackType type)
    {
        return type switch
        {
            StackType.Generated => "Generated",
            StackType.Permanent => "Permanent",
            StackType.Unique => "Unique",
            StackType.Reward => "Reward",
            StackType.SpecialOffer => "SpecialOffer",
            _ => $"Type{(int)type}"
        };
    }

    /// <summary>
    /// Get the stack type name from an integer value.
    /// </summary>
    /// <param name="typeValue">Integer stack type value.</param>
    /// <returns>Human-readable name for the stack type.</returns>
    public static string GetStackTypeName(int typeValue)
    {
        return GetStackTypeName((StackType)typeValue);
    }

    /// <summary>
    /// Register console commands for BlackMarket SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // blackmarket - Show BlackMarket overview
        DevConsole.RegisterCommand("blackmarket", "", "Show BlackMarket overview", args =>
        {
            var info = GetBlackMarketInfo();
            if (info == null)
                return "BlackMarket not available (strategy layer not active?)";

            return $"BlackMarket Status:\n" +
                   $"  Stacks: {info.StackCount} ({info.TotalItemCount} total items)\n" +
                   $"  Config: {info.MinItems}-{info.MaxItems} items, {info.ItemTimeout} ops timeout\n" +
                   $"  Item Pool: {info.ItemPoolSize} templates\n" +
                   $"  Campaign Progress: {info.CampaignProgress}";
        });

        // bmdebug - Debug StrategyState access
        DevConsole.RegisterCommand("bmdebug", "", "Debug StrategyState/BlackMarket access", args =>
        {
            var lines = new List<string> { "BlackMarket Debug:" };

            try
            {
                // Step 1: Find StrategyState type via GameType
                EnsureTypesLoaded();
                var gtType = _strategyStateType;
                lines.Add($"  GameType.Find(StrategyState): {(gtType != null ? "Found" : "NULL")}");

                if (gtType != null)
                {
                    lines.Add($"    FullName: {gtType.FullName}");
                    lines.Add($"    IsValid: {gtType.IsValid}");

                    var managed = gtType.ManagedType;
                    lines.Add($"    ManagedType: {(managed != null ? managed.FullName : "NULL")}");

                    if (managed != null)
                    {
                        // Use Get() static method instead of s_Singleton property
                        var getMethod = managed.GetMethod("Get",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        lines.Add($"    Get() method: {(getMethod != null ? "Found" : "NULL")}");

                        if (getMethod != null)
                        {
                            try
                            {
                                var instance = getMethod.Invoke(null, null);
                                lines.Add($"    Instance value: {(instance != null ? "EXISTS" : "NULL")}");

                                if (instance != null)
                                {
                                    // Access BlackMarket via field offset 0x88
                                    var ssObj = new GameObj(((Il2CppObjectBase)instance).Pointer);
                                    var bmPtr = ssObj.ReadPtr((uint)OFFSET_SS_BLACKMARKET);
                                    lines.Add($"    BlackMarket @ +0x88: {(bmPtr != IntPtr.Zero ? $"0x{bmPtr:X}" : "NULL")}");
                                }
                            }
                            catch (Exception ex)
                            {
                                lines.Add($"    Instance.GetValue error: {ex.Message}");
                            }
                        }
                    }
                }

                // Step 2: Try direct assembly search
                lines.Add("");
                lines.Add("  Direct assembly search:");
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                lines.Add($"    Assembly-CSharp: {(gameAssembly != null ? "Found" : "NULL")}");

                if (gameAssembly != null)
                {
                    var ssType = gameAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == "StrategyState");
                    lines.Add($"    StrategyState type: {(ssType != null ? ssType.FullName : "NULL")}");

                    if (ssType != null)
                    {
                        var getMethod = ssType.GetMethod("Get",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        lines.Add($"    Get() method: {(getMethod != null ? "Found" : "NULL")}");

                        if (getMethod != null)
                        {
                            try
                            {
                                var instance = getMethod.Invoke(null, null);
                                lines.Add($"    Instance value: {(instance != null ? "EXISTS" : "NULL")}");
                            }
                            catch (Exception ex)
                            {
                                lines.Add($"    Instance error: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add($"  Error: {ex.Message}");
            }

            return string.Join("\n", lines);
        });

        // bmitems - List all BlackMarket items
        DevConsole.RegisterCommand("bmitems", "", "List all BlackMarket items", args =>
        {
            var stacks = GetAvailableStacks();
            if (stacks.Count == 0)
                return "No items in BlackMarket";

            var lines = new List<string> { $"BlackMarket Items ({stacks.Count} stacks):" };
            for (int i = 0; i < stacks.Count; i++)
            {
                var s = stacks[i];
                var expiry = s.WillExpire ? $" ({s.OperationsRemaining} ops)" : " [PERM]";
                var typeTag = s.Type != StackType.Generated ? $" [{s.TypeName}]" : "";
                lines.Add($"  {i}. {s.TemplateName} x{s.ItemCount} - ${s.TradeValue}{expiry}{typeTag}");
            }
            return string.Join("\n", lines);
        });

        // bmstack <index> - Show stack details
        DevConsole.RegisterCommand("bmstack", "<index>", "Show BlackMarket stack details", args =>
        {
            if (args.Length == 0)
                return "Usage: bmstack <index>";

            if (!int.TryParse(args[0], out int index))
                return "Invalid index";

            var stack = GetStackInfo(index);
            if (stack == null)
                return $"Stack {index} not found";

            var expiryInfo = stack.WillExpire
                ? $"{stack.OperationsRemaining} operations remaining"
                : "Never expires (Permanent)";

            return $"Stack {index}: {stack.TemplateName}\n" +
                   $"  Type: {stack.TypeName}\n" +
                   $"  Items: {stack.ItemCount}\n" +
                   $"  Trade Value: ${stack.TradeValue} each\n" +
                   $"  Rarity: {stack.Rarity ?? "Common"}\n" +
                   $"  Expiry: {expiryInfo}";
        });

        // bmexpiring - List items expiring soon
        DevConsole.RegisterCommand("bmexpiring", "", "List BlackMarket items expiring soon", args =>
        {
            var expiring = GetExpiringStacks();
            if (expiring.Count == 0)
                return "No items expiring soon";

            var lines = new List<string> { $"Expiring Items ({expiring.Count}):" };
            foreach (var s in expiring)
            {
                lines.Add($"  {s.TemplateName} x{s.ItemCount} - ${s.TradeValue} ({s.OperationsRemaining} op left)");
            }
            lines.Add("\nThese items will be removed after the next operation!");
            return string.Join("\n", lines);
        });

        // bmpermanent - List permanent items
        DevConsole.RegisterCommand("bmpermanent", "", "List permanent BlackMarket items", args =>
        {
            var permanent = GetPermanentStacks();
            if (permanent.Count == 0)
                return "No permanent items in BlackMarket";

            var lines = new List<string> { $"Permanent Items ({permanent.Count}):" };
            foreach (var s in permanent)
            {
                lines.Add($"  {s.TemplateName} x{s.ItemCount} - ${s.TradeValue}");
            }
            return string.Join("\n", lines);
        });

        // bmfind <name> - Search for item by name
        DevConsole.RegisterCommand("bmfind", "<name>", "Search for BlackMarket item by name", args =>
        {
            if (args.Length == 0)
                return "Usage: bmfind <name>";

            var searchTerm = string.Join(" ", args).ToLowerInvariant();
            var stacks = GetAvailableStacks();
            var matches = new List<ItemStackInfo>();

            foreach (var s in stacks)
            {
                if (s.TemplateName != null &&
                    s.TemplateName.ToLowerInvariant().Contains(searchTerm))
                {
                    matches.Add(s);
                }
            }

            if (matches.Count == 0)
                return $"No items matching '{searchTerm}' found";

            var lines = new List<string> { $"Found {matches.Count} matching items:" };
            foreach (var s in matches)
            {
                var expiry = s.WillExpire ? $" ({s.OperationsRemaining} ops)" : " [PERM]";
                lines.Add($"  {s.TemplateName} x{s.ItemCount} - ${s.TradeValue}{expiry}");
            }
            return string.Join("\n", lines);
        });

        // bmstock <template> - Add item to BlackMarket for testing
        DevConsole.RegisterCommand("bmstock", "<template>", "Stock an item in BlackMarket (for testing)", args =>
        {
            if (args.Length == 0)
                return "Usage: bmstock <template_name>\nExample: bmstock weapon.laser_smg";

            var templateName = args[0];
            return StockItemInBlackMarket(templateName);
        });

        // bmvalue - Show total BlackMarket value
        DevConsole.RegisterCommand("bmvalue", "", "Show total BlackMarket trade value", args =>
        {
            var total = GetTotalTradeValue();
            var stacks = GetAvailableStacks();
            int itemCount = 0;
            foreach (var s in stacks)
                itemCount += s.ItemCount;

            return $"Total BlackMarket Value: ${total}\n" +
                   $"Items: {itemCount} across {stacks.Count} stacks";
        });

        // bmbytype <type> - Filter by stack type
        DevConsole.RegisterCommand("bmbytype", "<type>", "Filter BlackMarket by type (Generated/Permanent/Unique/Reward/SpecialOffer)", args =>
        {
            if (args.Length == 0)
                return "Usage: bmbytype <type>\nTypes: Generated, Permanent, Unique, Reward, SpecialOffer (or 0-4)";

            StackType type;
            if (int.TryParse(args[0], out int typeInt))
            {
                type = (StackType)typeInt;
            }
            else
            {
                type = args[0].ToLowerInvariant() switch
                {
                    "generated" => StackType.Generated,
                    "permanent" => StackType.Permanent,
                    "unique" => StackType.Unique,
                    "reward" => StackType.Reward,
                    "specialoffer" => StackType.SpecialOffer,
                    _ => (StackType)(-1)
                };
            }

            if ((int)type < 0 || (int)type > 4)
                return "Invalid type. Use: Generated, Permanent, Unique, Reward, SpecialOffer (or 0-4)";

            var stacks = GetStacksByType(type);
            if (stacks.Count == 0)
                return $"No {GetStackTypeName(type)} items in BlackMarket";

            var lines = new List<string> { $"{GetStackTypeName(type)} Items ({stacks.Count}):" };
            foreach (var s in stacks)
            {
                var expiry = s.WillExpire ? $" ({s.OperationsRemaining} ops)" : "";
                lines.Add($"  {s.TemplateName} x{s.ItemCount} - ${s.TradeValue}{expiry}");
            }
            return string.Join("\n", lines);
        });
    }

    /// <summary>
    /// Stock an item in the BlackMarket by template name.
    /// </summary>
    public static string StockItemInBlackMarket(string templateName)
    {
        try
        {
            EnsureTypesLoaded();

            // Get BlackMarket
            var bm = GetBlackMarket();
            if (bm.IsNull)
                return "BlackMarket not available. Are you on the strategy map?";

            // Find the template
            var template = Inventory.FindItemTemplate(templateName);
            if (template.IsNull)
                return $"Template '{templateName}' not found";

            // Get template proxy
            var templateType = _baseItemTemplateType?.ManagedType;
            if (templateType == null)
                return "BaseItemTemplate type not found";

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null)
                return "Failed to get template proxy";

            // Create an item from the template
            var createMethod = templateType.GetMethod("CreateItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (createMethod == null)
                return "CreateItem method not found on template";

            var guid = System.Guid.NewGuid().ToString();
            var item = createMethod.Invoke(templateProxy, new object[] { guid });
            if (item == null)
                return "CreateItem returned null";

            // Get BlackMarket proxy
            var bmType = _blackMarketType?.ManagedType;
            if (bmType == null)
                return "BlackMarket type not found";

            var bmProxy = GetManagedProxy(bm, bmType);
            if (bmProxy == null)
                return "Failed to get BlackMarket proxy";

            // Call AddItem(BaseItem item, int operationsRemaining)
            var addMethod = bmType.GetMethod("AddItem",
                BindingFlags.Public | BindingFlags.Instance);
            if (addMethod == null)
                return "AddItem method not found on BlackMarket";

            // AddItem takes (BaseItem, int operationsRemaining)
            addMethod.Invoke(bmProxy, new object[] { item, 99 });

            return $"Stocked '{templateName}' in BlackMarket";
        }
        catch (Exception ex)
        {
            return $"Failed to stock item: {ex.Message}";
        }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _blackMarketType ??= GameType.Find("Menace.Strategy.BlackMarket");
        _blackMarketItemStackType ??= GameType.Find("Menace.Strategy.BlackMarket.BlackMarketItemStack");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
        _strategyConfigType ??= GameType.Find("Menace.Strategy.StrategyConfig");
        _baseItemTemplateType ??= GameType.Find("Menace.Items.BaseItemTemplate");
        _baseItemType ??= GameType.Find("Menace.Items.BaseItem");
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

    /// <summary>
    /// Get the stacks list from BlackMarket using direct offset access.
    /// The "Stacks" property doesn't exist - we need to read at offset +0x10.
    /// </summary>
    private static (object list, Type listType, int count) GetStacksList(GameObj bm)
    {
        if (bm.IsNull) return (null, null, 0);

        try
        {
            EnsureTypesLoaded();

            // Read stacks list at offset +0x10
            var stacksPtr = bm.ReadPtr(0x10);
            if (stacksPtr == IntPtr.Zero) return (null, null, 0);

            // Get typed list using explicit generic type construction
            // Use concrete type directly since GameType.Find doesn't handle nested types well
            var stackType = typeof(Il2CppMenace.Strategy.BlackMarket.BlackMarketItemStack);
            if (stackType == null) return (null, null, 0);

            var listGenericType = typeof(Il2CppSystem.Collections.Generic.List<>);
            var listTyped = listGenericType.MakeGenericType(stackType);
            var ptrCtor = listTyped.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null) return (null, null, 0);

            var list = ptrCtor.Invoke(new object[] { stacksPtr });
            if (list == null) return (null, null, 0);

            var countProp = listTyped.GetProperty("Count");
            int count = (int)(countProp?.GetValue(list) ?? 0);

            return (list, listTyped, count);
        }
        catch
        {
            return (null, null, 0);
        }
    }

    private static GameObj? GetStrategyConfig()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return null;

            // Use Get() static method instead of s_Singleton property
            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return null;

            var configProp = ssType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
            var config = configProp?.GetValue(ss);
            if (config == null) return null;

            return new GameObj(((Il2CppObjectBase)config).Pointer);
        }
        catch
        {
            return null;
        }
    }

    private static int GetCampaignProgress()
    {
        try
        {
            EnsureTypesLoaded();

            var ssType = _strategyStateType?.ManagedType;
            if (ssType == null) return 0;

            // Use Get() static method instead of s_Singleton property
            var getMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var ss = getMethod?.Invoke(null, null);
            if (ss == null) return 0;

            var getProgressMethod = ssType.GetMethod("GetCampaignProgress",
                BindingFlags.Public | BindingFlags.Instance);
            if (getProgressMethod != null)
            {
                return (int)getProgressMethod.Invoke(ss, null);
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static ItemStackInfo GetItemStackInfoInternal(object stack)
    {
        if (stack == null) return null;

        try
        {
            EnsureTypesLoaded();

            var stackType = _blackMarketItemStackType?.ManagedType;
            if (stackType == null) return null;

            var info = new ItemStackInfo
            {
                Pointer = ((Il2CppObjectBase)stack).Pointer
            };

            // Get template
            var templateProp = stackType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(stack);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();

                // Get trade value and rarity from template
                var baseItemTemplateType = _baseItemTemplateType?.ManagedType;
                if (baseItemTemplateType != null)
                {
                    var templateProxy = GetManagedProxy(templateObj, baseItemTemplateType);
                    if (templateProxy != null)
                    {
                        var getValueMethod = baseItemTemplateType.GetMethod("GetTradeValue",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (getValueMethod != null)
                            info.TradeValue = (int)getValueMethod.Invoke(templateProxy, null);

                        var getRarityMethod = baseItemTemplateType.GetMethod("GetHighestRarity",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (getRarityMethod != null)
                            info.Rarity = getRarityMethod.Invoke(templateProxy, null)?.ToString();
                    }
                }
            }

            // Get operations remaining
            var opsRemainingProp = stackType.GetProperty("OperationsRemaining",
                BindingFlags.Public | BindingFlags.Instance);
            if (opsRemainingProp != null)
                info.OperationsRemaining = (int)opsRemainingProp.GetValue(stack);

            // Get items list and count
            var itemsProp = stackType.GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
            var items = itemsProp?.GetValue(stack);
            if (items != null)
            {
                var countProp = items.GetType().GetProperty("Count");
                info.ItemCount = (int)(countProp?.GetValue(items) ?? 0);
            }

            // Get stack type
            var typeProp = stackType.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance);
            if (typeProp != null)
            {
                var typeValue = Convert.ToInt32(typeProp.GetValue(stack));
                info.Type = (StackType)typeValue;
                info.TypeName = GetStackTypeName(info.Type);
            }

            // Determine if will expire
            info.WillExpire = info.Type != StackType.Permanent;

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemStackInfoInternal", "Failed", ex);
            return null;
        }
    }

    private static ItemInfo GetItemInfoInternal(object item)
    {
        if (item == null) return null;

        try
        {
            EnsureTypesLoaded();

            var baseItemType = _baseItemType?.ManagedType;
            if (baseItemType == null) return null;

            var info = new ItemInfo
            {
                Pointer = ((Il2CppObjectBase)item).Pointer
            };

            // Get GUID
            var getIdMethod = baseItemType.GetMethod("GetID", BindingFlags.Public | BindingFlags.Instance);
            if (getIdMethod != null)
                info.GUID = getIdMethod.Invoke(item, null)?.ToString();

            // Get template name
            var getTemplateMethod = baseItemType.GetMethod("GetTemplate", BindingFlags.Public | BindingFlags.Instance);
            var template = getTemplateMethod?.Invoke(item, null);
            if (template != null)
            {
                var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                info.TemplateName = templateObj.GetName();
            }

            // Get trade value
            var getTradeValueMethod = baseItemType.GetMethod("GetTradeValue",
                BindingFlags.Public | BindingFlags.Instance);
            if (getTradeValueMethod != null)
                info.TradeValue = (int)getTradeValueMethod.Invoke(item, null);

            // Get rarity
            var getRarityMethod = baseItemType.GetMethod("GetHighestRarity",
                BindingFlags.Public | BindingFlags.Instance);
            if (getRarityMethod != null)
                info.Rarity = getRarityMethod.Invoke(item, null)?.ToString();

            // Get skill count
            var skillsProp = baseItemType.GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance);
            var skills = skillsProp?.GetValue(item);
            if (skills != null)
            {
                var countProp = skills.GetType().GetProperty("Count");
                info.SkillCount = (int)(countProp?.GetValue(skills) ?? 0);
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("BlackMarket.GetItemInfoInternal", "Failed", ex);
            return null;
        }
    }
}
