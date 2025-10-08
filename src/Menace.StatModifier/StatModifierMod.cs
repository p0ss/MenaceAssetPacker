using MelonLoader;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.StatModifier.StatModifierMod), "Menace Stat Modifier", "1.0.0", "MenaceModkit")]
[assembly: MelonGame(null, null)]

namespace Menace.StatModifier
{
    public class StatModifierMod : MelonMod
    {
        private static Dictionary<string, JObject> _modifiedWeapons = new Dictionary<string, JObject>();
        private static Dictionary<string, JObject> _modifiedArmor = new Dictionary<string, JObject>();
        private static Dictionary<string, JObject> _modifiedAccessories = new Dictionary<string, JObject>();
        private static Dictionary<string, JObject> _modifiedEntities = new Dictionary<string, JObject>();

        private string _modDataPath = "";

        public override void OnInitializeMelon()
        {
            var modsDir = Path.GetDirectoryName(typeof(StatModifierMod).Assembly.Location) ?? "";
            var rootDir = Directory.GetParent(modsDir)?.FullName ?? "";
            _modDataPath = Path.Combine(rootDir, "UserData", "ModifiedData");

            LoggerInstance.Msg("==========================================");
            LoggerInstance.Msg("Menace Stat Modifier v1.0.0");
            LoggerInstance.Msg($"Modified data path: {_modDataPath}");
            LoggerInstance.Msg("==========================================");

            LoadModifiedData();
            ApplyHarmonyPatches();
        }

        private void LoadModifiedData()
        {
            if (!Directory.Exists(_modDataPath))
            {
                Directory.CreateDirectory(_modDataPath);
                LoggerInstance.Msg("Created ModifiedData directory - place your modified JSON files here");
                return;
            }

            _modifiedWeapons = LoadTemplateData("WeaponTemplate.json");
            _modifiedArmor = LoadTemplateData("ArmorTemplate.json");
            _modifiedAccessories = LoadTemplateData("AccessoryTemplate.json");
            _modifiedEntities = LoadTemplateData("EntityTemplate.json");

            int totalMods = _modifiedWeapons.Count + _modifiedArmor.Count + _modifiedAccessories.Count + _modifiedEntities.Count;
            LoggerInstance.Msg($"Loaded {totalMods} modified templates:");
            LoggerInstance.Msg($"  - {_modifiedWeapons.Count} weapons");
            LoggerInstance.Msg($"  - {_modifiedArmor.Count} armor");
            LoggerInstance.Msg($"  - {_modifiedAccessories.Count} accessories");
            LoggerInstance.Msg($"  - {_modifiedEntities.Count} entities");
        }

        private Dictionary<string, JObject> LoadTemplateData(string fileName)
        {
            var result = new Dictionary<string, JObject>();
            var filePath = Path.Combine(_modDataPath, fileName);

            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var array = JsonConvert.DeserializeObject<JArray>(json);

                if (array != null)
                {
                    foreach (var item in array)
                    {
                        var obj = item as JObject;
                        if (obj != null && obj["name"] != null)
                        {
                            string name = obj["name"].ToString();
                            result[name] = obj;
                        }
                    }
                }

                LoggerInstance.Msg($"✓ Loaded {result.Count} modified {fileName.Replace(".json", "")}s");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to load {fileName}: {ex.Message}");
            }

            return result;
        }

        private void ApplyHarmonyPatches()
        {
            try
            {
                var harmony = new HarmonyLib.Harmony("com.menacemodkit.statmodifier");
                harmony.PatchAll();
                LoggerInstance.Msg("✓ Applied Harmony patches");
                LoggerInstance.Msg("==========================================");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to apply Harmony patches: {ex.Message}");
            }
        }

        // Helper methods for patches to access modified data
        public static bool TryGetModifiedWeapon(string name, out JObject data)
        {
            return _modifiedWeapons.TryGetValue(name, out data);
        }

        public static bool TryGetModifiedArmor(string name, out JObject data)
        {
            return _modifiedArmor.TryGetValue(name, out data);
        }

        public static bool TryGetModifiedAccessory(string name, out JObject data)
        {
            return _modifiedAccessories.TryGetValue(name, out data);
        }

        public static bool TryGetModifiedEntity(string name, out JObject data)
        {
            return _modifiedEntities.TryGetValue(name, out data);
        }

        // Utility to read from modified data or memory
        public static T ReadModifiedOrOriginal<T>(IntPtr objPtr, int offset, string name, string fieldName, JObject modData)
        {
            if (modData != null && modData[fieldName] != null)
            {
                return modData[fieldName].Value<T>();
            }

            // Fall back to reading original memory
            if (typeof(T) == typeof(int))
            {
                return (T)(object)Marshal.ReadInt32(objPtr + offset);
            }
            else if (typeof(T) == typeof(float))
            {
                return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(objPtr + offset)), 0);
            }

            return default(T);
        }
    }

    // ============================================================================
    // WEAPON TEMPLATE PATCHES
    // ============================================================================

    [HarmonyPatch]
    public class WeaponTemplate_Patches
    {
        [HarmonyPatch("WeaponTemplate", "get_MinRange")]
        [HarmonyPrefix]
        public static bool GetMinRange(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["MinRange"] != null)
            {
                __result = data["MinRange"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_IdealRange")]
        [HarmonyPrefix]
        public static bool GetIdealRange(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["IdealRange"] != null)
            {
                __result = data["IdealRange"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_MaxRange")]
        [HarmonyPrefix]
        public static bool GetMaxRange(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["MaxRange"] != null)
            {
                __result = data["MaxRange"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_AccuracyBonus")]
        [HarmonyPrefix]
        public static bool GetAccuracyBonus(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["AccuracyBonus"] != null)
            {
                __result = data["AccuracyBonus"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_AccuracyDropoff")]
        [HarmonyPrefix]
        public static bool GetAccuracyDropoff(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["AccuracyDropoff"] != null)
            {
                __result = data["AccuracyDropoff"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_Damage")]
        [HarmonyPrefix]
        public static bool GetDamage(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["Damage"] != null)
            {
                __result = data["Damage"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_DamageDropoff")]
        [HarmonyPrefix]
        public static bool GetDamageDropoff(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["DamageDropoff"] != null)
            {
                __result = data["DamageDropoff"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_ArmorPenetration")]
        [HarmonyPrefix]
        public static bool GetArmorPenetration(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["ArmorPenetration"] != null)
            {
                __result = data["ArmorPenetration"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("WeaponTemplate", "get_ArmorPenetrationDropoff")]
        [HarmonyPrefix]
        public static bool GetArmorPenetrationDropoff(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedWeapon(__instance.name, out var data) && data["ArmorPenetrationDropoff"] != null)
            {
                __result = data["ArmorPenetrationDropoff"].Value<float>();
                return false;
            }
            return true;
        }
    }

    // ============================================================================
    // ARMOR TEMPLATE PATCHES
    // ============================================================================

    [HarmonyPatch]
    public class ArmorTemplate_Patches
    {
        [HarmonyPatch("ArmorTemplate", "get_Armor")]
        [HarmonyPrefix]
        public static bool GetArmor(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["Armor"] != null)
            {
                __result = data["Armor"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_DurabilityPerElement")]
        [HarmonyPrefix]
        public static bool GetDurabilityPerElement(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["DurabilityPerElement"] != null)
            {
                __result = data["DurabilityPerElement"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_DamageResistance")]
        [HarmonyPrefix]
        public static bool GetDamageResistance(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["DamageResistance"] != null)
            {
                __result = data["DamageResistance"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_HitpointsPerElement")]
        [HarmonyPrefix]
        public static bool GetHitpointsPerElement(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["HitpointsPerElement"] != null)
            {
                __result = data["HitpointsPerElement"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_Accuracy")]
        [HarmonyPrefix]
        public static bool GetAccuracy(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["Accuracy"] != null)
            {
                __result = data["Accuracy"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_AccuracyMult")]
        [HarmonyPrefix]
        public static bool GetAccuracyMult(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["AccuracyMult"] != null)
            {
                __result = data["AccuracyMult"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_DefenseMult")]
        [HarmonyPrefix]
        public static bool GetDefenseMult(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["DefenseMult"] != null)
            {
                __result = data["DefenseMult"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_Discipline")]
        [HarmonyPrefix]
        public static bool GetDiscipline(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["Discipline"] != null)
            {
                __result = data["Discipline"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_Vision")]
        [HarmonyPrefix]
        public static bool GetVision(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["Vision"] != null)
            {
                __result = data["Vision"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("ArmorTemplate", "get_Detection")]
        [HarmonyPrefix]
        public static bool GetDetection(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedArmor(__instance.name, out var data) && data["Detection"] != null)
            {
                __result = data["Detection"].Value<int>();
                return false;
            }
            return true;
        }
    }

    // ============================================================================
    // ACCESSORY TEMPLATE PATCHES (same structure as armor)
    // ============================================================================

    [HarmonyPatch]
    public class AccessoryTemplate_Patches
    {
        [HarmonyPatch("AccessoryTemplate", "get_Armor")]
        [HarmonyPrefix]
        public static bool GetArmor(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["Armor"] != null)
            {
                __result = data["Armor"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_DurabilityPerElement")]
        [HarmonyPrefix]
        public static bool GetDurabilityPerElement(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["DurabilityPerElement"] != null)
            {
                __result = data["DurabilityPerElement"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_DamageResistance")]
        [HarmonyPrefix]
        public static bool GetDamageResistance(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["DamageResistance"] != null)
            {
                __result = data["DamageResistance"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_HitpointsPerElement")]
        [HarmonyPrefix]
        public static bool GetHitpointsPerElement(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["HitpointsPerElement"] != null)
            {
                __result = data["HitpointsPerElement"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_Accuracy")]
        [HarmonyPrefix]
        public static bool GetAccuracy(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["Accuracy"] != null)
            {
                __result = data["Accuracy"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_AccuracyMult")]
        [HarmonyPrefix]
        public static bool GetAccuracyMult(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["AccuracyMult"] != null)
            {
                __result = data["AccuracyMult"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_DefenseMult")]
        [HarmonyPrefix]
        public static bool GetDefenseMult(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["DefenseMult"] != null)
            {
                __result = data["DefenseMult"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_Discipline")]
        [HarmonyPrefix]
        public static bool GetDiscipline(UnityEngine.Object __instance, ref float __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["Discipline"] != null)
            {
                __result = data["Discipline"].Value<float>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_Vision")]
        [HarmonyPrefix]
        public static bool GetVision(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["Vision"] != null)
            {
                __result = data["Vision"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("AccessoryTemplate", "get_Detection")]
        [HarmonyPrefix]
        public static bool GetDetection(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedAccessory(__instance.name, out var data) && data["Detection"] != null)
            {
                __result = data["Detection"].Value<int>();
                return false;
            }
            return true;
        }
    }

    // ============================================================================
    // ENTITY TEMPLATE PATCHES
    // ============================================================================

    [HarmonyPatch]
    public class EntityTemplate_Patches
    {
        [HarmonyPatch("EntityTemplate", "get_ElementsMin")]
        [HarmonyPrefix]
        public static bool GetElementsMin(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedEntity(__instance.name, out var data) && data["ElementsMin"] != null)
            {
                __result = data["ElementsMin"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("EntityTemplate", "get_ElementsMax")]
        [HarmonyPrefix]
        public static bool GetElementsMax(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedEntity(__instance.name, out var data) && data["ElementsMax"] != null)
            {
                __result = data["ElementsMax"].Value<int>();
                return false;
            }
            return true;
        }

        [HarmonyPatch("EntityTemplate", "get_ArmyPointCost")]
        [HarmonyPrefix]
        public static bool GetArmyPointCost(UnityEngine.Object __instance, ref int __result)
        {
            if (StatModifierMod.TryGetModifiedEntity(__instance.name, out var data) && data["ArmyPointCost"] != null)
            {
                __result = data["ArmyPointCost"].Value<int>();
                return false;
            }
            return true;
        }
    }

    // Note: EntityProperties patches would require patching the Properties object's getters,
    // which is more complex. For MVP, we can modify EntityTemplate base properties first.
}
