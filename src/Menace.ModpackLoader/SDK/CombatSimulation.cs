using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for combat simulation - hit chance calculation, damage prediction.
/// Calls the game's actual combat calculation code for accurate results.
///
/// Based on reverse engineering findings:
/// - Skill.GetHitchance(from, targetTile, properties, defenderProperties, includeDropoff, overrideTargetEntity, forImmediateUse)
///   @ 0x1806dba90
/// - Returns HitChanceResult struct with FinalValue, Accuracy, CoverMult, DefenseMult, AccuracyDropoff, IncludeDropoff, AlwaysHits
/// </summary>
public static class CombatSimulation
{
    // Cached types
    private static GameType _skillType;
    private static GameType _tileType;
    private static GameType _actorType;

    /// <summary>
    /// Result from hit chance calculation.
    /// </summary>
    public class HitChanceResult
    {
        public float FinalValue { get; set; }
        public float Accuracy { get; set; }
        public float CoverMult { get; set; }
        public float DefenseMult { get; set; }
        public float AccuracyDropoff { get; set; }
        public bool IncludeDropoff { get; set; }
        public bool AlwaysHits { get; set; }
        public string SkillName { get; set; }
        public float Distance { get; set; }
    }

    /// <summary>
    /// Calculate hit chance for an attacker hitting a target with their primary attack skill.
    /// </summary>
    public static HitChanceResult GetHitChance(GameObj attacker, GameObj target)
    {
        if (attacker.IsNull || target.IsNull)
            return new HitChanceResult { FinalValue = -1 };

        // Get attacker's primary attack skill
        var skills = EntityCombat.GetSkills(attacker);
        var attackSkill = skills.Find(s => s.IsAttack);
        if (attackSkill == null)
            return new HitChanceResult { FinalValue = -1 };

        return GetHitChance(attacker, target, attackSkill.Name);
    }

    /// <summary>
    /// Calculate hit chance for a specific skill.
    /// </summary>
    public static HitChanceResult GetHitChance(GameObj attacker, GameObj target, string skillName)
    {
        var result = new HitChanceResult { SkillName = skillName };

        if (attacker.IsNull || target.IsNull)
        {
            result.FinalValue = -1;
            return result;
        }

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            var tileType = _tileType?.ManagedType;
            var skillType = _skillType?.ManagedType;

            if (actorType == null || tileType == null || skillType == null)
            {
                result.FinalValue = -1;
                return result;
            }

            var attackerProxy = GetManagedProxy(attacker, actorType);
            var targetProxy = GetManagedProxy(target, actorType);
            if (attackerProxy == null || targetProxy == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Get tiles
            var getTileMethod = actorType.GetMethod("GetTile", BindingFlags.Public | BindingFlags.Instance);
            var sourceTile = getTileMethod?.Invoke(attackerProxy, null);
            var targetTile = getTileMethod?.Invoke(targetProxy, null);
            if (sourceTile == null || targetTile == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Get distance
            var getDistMethod = tileType.GetMethod("GetDistanceTo", BindingFlags.Public | BindingFlags.Instance);
            if (getDistMethod != null)
            {
                var distObj = getDistMethod.Invoke(sourceTile, new[] { targetTile });
                result.Distance = Convert.ToSingle(distObj);
            }

            // Get skills via GetSkills() method
            var getSkillsMethod = actorType.GetMethod("GetSkills", BindingFlags.Public | BindingFlags.Instance);
            var skillContainer = getSkillsMethod?.Invoke(attackerProxy, null);
            if (skillContainer == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Find the skill
            object skill = null;
            var skillsField = skillContainer.GetType().GetField("m_Skills", BindingFlags.NonPublic | BindingFlags.Instance);
            if (skillsField != null)
            {
                var skillsList = skillsField.GetValue(skillContainer);
                if (skillsList != null)
                {
                    var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
                    if (enumerator != null)
                    {
                        var moveNext = enumerator.GetType().GetMethod("MoveNext");
                        var current = enumerator.GetType().GetProperty("Current");

                        while ((bool)moveNext.Invoke(enumerator, null))
                        {
                            var s = current.GetValue(enumerator);
                            if (s != null)
                            {
                                var nameProp = s.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                                var name = nameProp?.GetValue(s)?.ToString();
                                if (name == skillName)
                                {
                                    skill = s;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (skill == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Call GetHitchance on the skill
            var getHitchanceMethod = skill.GetType().GetMethod("GetHitchance", BindingFlags.Public | BindingFlags.Instance);
            if (getHitchanceMethod == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Call with null for attackProps/defenseProps to let the game build them
            // Signature: GetHitchance(from, targetTile, properties, defenderProperties, includeDropoff, overrideTargetEntity, forImmediateUse)
            var hitChanceResult = getHitchanceMethod.Invoke(skill, new object[]
            {
                sourceTile,
                targetTile,
                null,  // properties - game will build
                null,  // defenderProperties - game will build
                true,  // includeDropoff
                targetProxy,  // overrideTargetEntity
                false  // forImmediateUse
            });

            if (hitChanceResult == null)
            {
                result.FinalValue = -1;
                return result;
            }

            // Extract fields from HitChanceResult struct
            var resultType = hitChanceResult.GetType();

            var finalValueField = resultType.GetField("FinalValue", BindingFlags.Public | BindingFlags.Instance);
            var accuracyField = resultType.GetField("Accuracy", BindingFlags.Public | BindingFlags.Instance);
            var coverMultField = resultType.GetField("CoverMult", BindingFlags.Public | BindingFlags.Instance);
            var defenseMultField = resultType.GetField("DefenseMult", BindingFlags.Public | BindingFlags.Instance);
            var accuracyDropoffField = resultType.GetField("AccuracyDropoff", BindingFlags.Public | BindingFlags.Instance);
            var includeDropoffField = resultType.GetField("IncludeDropoff", BindingFlags.Public | BindingFlags.Instance);
            var alwaysHitsField = resultType.GetField("AlwaysHits", BindingFlags.Public | BindingFlags.Instance);

            if (finalValueField != null)
                result.FinalValue = Convert.ToSingle(finalValueField.GetValue(hitChanceResult));
            if (accuracyField != null)
                result.Accuracy = Convert.ToSingle(accuracyField.GetValue(hitChanceResult));
            if (coverMultField != null)
                result.CoverMult = Convert.ToSingle(coverMultField.GetValue(hitChanceResult));
            if (defenseMultField != null)
                result.DefenseMult = Convert.ToSingle(defenseMultField.GetValue(hitChanceResult));
            if (accuracyDropoffField != null)
                result.AccuracyDropoff = Convert.ToSingle(accuracyDropoffField.GetValue(hitChanceResult));
            if (includeDropoffField != null)
                result.IncludeDropoff = (bool)includeDropoffField.GetValue(hitChanceResult);
            if (alwaysHitsField != null)
                result.AlwaysHits = (bool)alwaysHitsField.GetValue(hitChanceResult);

            return result;
        }
        catch (Exception ex)
        {
            ModError.Report("Menace.SDK", "CombatSimulation.GetHitChance failed", ex, ErrorSeverity.Error);
            result.FinalValue = -1;
            return result;
        }
    }

    /// <summary>
    /// Get hit chances from an attacker to all potential targets.
    /// </summary>
    public static List<(string targetName, HitChanceResult result)> GetAllHitChances(GameObj attacker)
    {
        var results = new List<(string, HitChanceResult)>();

        if (attacker.IsNull)
            return results;

        var attackerInfo = EntitySpawner.GetEntityInfo(attacker);
        var allActors = EntitySpawner.ListEntities();

        foreach (var target in allActors)
        {
            var targetInfo = EntitySpawner.GetEntityInfo(target);
            if (targetInfo == null || !targetInfo.IsAlive) continue;
            if (targetInfo.FactionIndex == attackerInfo?.FactionIndex) continue; // Same faction

            var hitChance = GetHitChance(attacker, target);
            if (hitChance.FinalValue >= 0)
            {
                results.Add((targetInfo.Name, hitChance));
            }
        }

        return results;
    }

    /// <summary>
    /// Register console commands.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("hitchance", "<target_name>", "Calculate hit chance against target", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull)
                return "No actor selected";

            if (args.Length == 0)
                return "Usage: hitchance <target_name>";

            var targetName = string.Join(" ", args);
            var target = GameQuery.FindByName("Actor", targetName);
            if (target.IsNull)
                return $"Target '{targetName}' not found";

            var result = GetHitChance(actor, target);
            if (result.FinalValue < 0)
                return "Could not calculate hit chance";

            return $"Hit chance vs {targetName}: {result.FinalValue:F0}%\n" +
                   $"Accuracy: {result.Accuracy:F1}, Cover: {result.CoverMult:F2}, Defense: {result.DefenseMult:F2}\n" +
                   $"Distance: {result.Distance:F1}, Dropoff: {result.AccuracyDropoff:F1}";
        });

        DevConsole.RegisterCommand("hitchances", "", "Show hit chances against all enemies", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull)
                return "No actor selected";

            var results = GetAllHitChances(actor);
            if (results.Count == 0)
                return "No valid targets";

            var lines = new List<string> { "Hit chances:" };
            foreach (var (name, result) in results)
            {
                lines.Add($"  {name}: {result.FinalValue:F0}% (dist: {result.Distance:F1})");
            }
            return string.Join("\n", lines);
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _skillType ??= GameType.Find("Menace.Tactical.Skills.BaseSkill");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
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
