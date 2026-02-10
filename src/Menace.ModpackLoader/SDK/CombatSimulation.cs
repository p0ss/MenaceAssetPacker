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
/// - Skill.GetHitchance(sourceTile, targetTile, attackProps, defenseProps, target, includeDistance)
///   @ 0x1806dba90
/// - Returns HitChanceResult struct with FinalHitChance, Accuracy, CoverMult, DodgeMult
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
        public float FinalHitChance { get; set; }
        public float Accuracy { get; set; }
        public float CoverMult { get; set; }
        public float DodgeMult { get; set; }
        public float DistancePenalty { get; set; }
        public bool IncludesDistance { get; set; }
        public string SkillName { get; set; }
        public float Distance { get; set; }
    }

    /// <summary>
    /// Calculate hit chance for an attacker hitting a target with their primary attack skill.
    /// </summary>
    public static HitChanceResult GetHitChance(GameObj attacker, GameObj target)
    {
        if (attacker.IsNull || target.IsNull)
            return new HitChanceResult { FinalHitChance = -1 };

        // Get attacker's primary attack skill
        var skills = EntityCombat.GetSkills(attacker);
        var attackSkill = skills.Find(s => s.IsAttack);
        if (attackSkill == null)
            return new HitChanceResult { FinalHitChance = -1 };

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
            result.FinalHitChance = -1;
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
                result.FinalHitChance = -1;
                return result;
            }

            var attackerProxy = GetManagedProxy(attacker, actorType);
            var targetProxy = GetManagedProxy(target, actorType);
            if (attackerProxy == null || targetProxy == null)
            {
                result.FinalHitChance = -1;
                return result;
            }

            // Get tiles
            var getTileMethod = actorType.GetMethod("GetTile", BindingFlags.Public | BindingFlags.Instance);
            var sourceTile = getTileMethod?.Invoke(attackerProxy, null);
            var targetTile = getTileMethod?.Invoke(targetProxy, null);
            if (sourceTile == null || targetTile == null)
            {
                result.FinalHitChance = -1;
                return result;
            }

            // Get distance
            var getDistMethod = tileType.GetMethod("GetDistanceTo", BindingFlags.Public | BindingFlags.Instance);
            if (getDistMethod != null)
            {
                var distObj = getDistMethod.Invoke(sourceTile, new[] { targetTile });
                result.Distance = Convert.ToSingle(distObj);
            }

            // Get skill container
            var skillContainerProp = actorType.GetProperty("SkillContainer", BindingFlags.Public | BindingFlags.Instance);
            var skillContainer = skillContainerProp?.GetValue(attackerProxy);
            if (skillContainer == null)
            {
                result.FinalHitChance = -1;
                return result;
            }

            // Find the skill
            object skill = null;
            var skillsProp = skillContainer.GetType().GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance);
            if (skillsProp != null)
            {
                var skillsList = skillsProp.GetValue(skillContainer);
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
                result.FinalHitChance = -1;
                return result;
            }

            // Call GetHitchance on the skill
            // Signature: GetHitchance(Tile sourceTile, Tile targetTile, EntityProperties attackProps,
            //                         EntityProperties defenseProps, Entity target, bool includeDistance)
            var getHitchanceMethod = skill.GetType().GetMethod("GetHitchance", BindingFlags.Public | BindingFlags.Instance);
            if (getHitchanceMethod == null)
            {
                result.FinalHitChance = -1;
                return result;
            }

            // Call with null for attackProps/defenseProps to let the game build them
            var hitChanceResult = getHitchanceMethod.Invoke(skill, new object[]
            {
                sourceTile,
                targetTile,
                null,  // attackProps - game will build
                null,  // defenseProps - game will build
                targetProxy,
                true   // includeDistance
            });

            if (hitChanceResult == null)
            {
                result.FinalHitChance = -1;
                return result;
            }

            // Extract fields from HitChanceResult struct
            var resultType = hitChanceResult.GetType();

            var finalHitChanceField = resultType.GetField("FinalHitChance", BindingFlags.Public | BindingFlags.Instance);
            var accuracyField = resultType.GetField("Accuracy", BindingFlags.Public | BindingFlags.Instance);
            var coverMultField = resultType.GetField("CoverMult", BindingFlags.Public | BindingFlags.Instance);
            var dodgeMultField = resultType.GetField("DodgeMult", BindingFlags.Public | BindingFlags.Instance);
            var distPenaltyField = resultType.GetField("DistancePenalty", BindingFlags.Public | BindingFlags.Instance);
            var includesDistField = resultType.GetField("IncludesDistance", BindingFlags.Public | BindingFlags.Instance);

            if (finalHitChanceField != null)
                result.FinalHitChance = Convert.ToSingle(finalHitChanceField.GetValue(hitChanceResult));
            if (accuracyField != null)
                result.Accuracy = Convert.ToSingle(accuracyField.GetValue(hitChanceResult));
            if (coverMultField != null)
                result.CoverMult = Convert.ToSingle(coverMultField.GetValue(hitChanceResult));
            if (dodgeMultField != null)
                result.DodgeMult = Convert.ToSingle(dodgeMultField.GetValue(hitChanceResult));
            if (distPenaltyField != null)
                result.DistancePenalty = Convert.ToSingle(distPenaltyField.GetValue(hitChanceResult));
            if (includesDistField != null)
                result.IncludesDistance = (bool)includesDistField.GetValue(hitChanceResult);

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CombatSimulation.GetHitChance", "Failed", ex);
            result.FinalHitChance = -1;
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
            if (hitChance.FinalHitChance >= 0)
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
            if (result.FinalHitChance < 0)
                return "Could not calculate hit chance";

            return $"Hit chance vs {targetName}: {result.FinalHitChance:F0}%\n" +
                   $"Accuracy: {result.Accuracy:F1}, Cover: {result.CoverMult:F2}, Dodge: {result.DodgeMult:F2}\n" +
                   $"Distance: {result.Distance:F1}, Penalty: {result.DistancePenalty:F1}";
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
                lines.Add($"  {name}: {result.FinalHitChance:F0}% (dist: {result.Distance:F1})");
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
