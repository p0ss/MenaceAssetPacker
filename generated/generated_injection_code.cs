// Auto-generated template injection code
// Writes modified template data back into IL2CPP memory

private void ApplyTemplateModifications(UnityEngine.Object obj, Type templateType, Dictionary<string, object> modifications)
{
    // Get IL2CPP pointer from the object
    IntPtr ptr = IntPtr.Zero;
    if (obj is Il2CppObjectBase il2cppObj)
    {
        ptr = il2cppObj.Pointer;
    }
    else
    {
        LoggerInstance.Error($"Cannot apply modifications: Object {obj.name} is not Il2CppObjectBase");
        return;
    }

    // Template-specific injection
            if (templateType.Name == "AIWeightsTemplate")
            {
                if (modifications.ContainsKey("BehaviorScorePOW"))
                {
                    Marshal.WriteInt32(ptr + 0x18, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["BehaviorScorePOW"])), 0));
                }
                if (modifications.ContainsKey("TTL_MAX"))
                {
                    Marshal.WriteInt32(ptr + 0x1C, Convert.ToInt32(modifications["TTL_MAX"]));
                }
                if (modifications.ContainsKey("UtilityPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x20, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UtilityPOW"])), 0));
                }
                if (modifications.ContainsKey("UtilityScale"))
                {
                    Marshal.WriteInt32(ptr + 0x24, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UtilityScale"])), 0));
                }
                if (modifications.ContainsKey("UtilityPostPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x28, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UtilityPostPOW"])), 0));
                }
                if (modifications.ContainsKey("UtilityPostScale"))
                {
                    Marshal.WriteInt32(ptr + 0x2C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UtilityPostScale"])), 0));
                }
                if (modifications.ContainsKey("SafetyPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x30, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyPOW"])), 0));
                }
                if (modifications.ContainsKey("SafetyScale"))
                {
                    Marshal.WriteInt32(ptr + 0x34, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyScale"])), 0));
                }
                if (modifications.ContainsKey("SafetyPostPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x38, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyPostPOW"])), 0));
                }
                if (modifications.ContainsKey("SafetyPostScale"))
                {
                    Marshal.WriteInt32(ptr + 0x3C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyPostScale"])), 0));
                }
                if (modifications.ContainsKey("DistanceScale"))
                {
                    Marshal.WriteInt32(ptr + 0x40, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceScale"])), 0));
                }
                if (modifications.ContainsKey("DistancePickScale"))
                {
                    Marshal.WriteInt32(ptr + 0x44, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistancePickScale"])), 0));
                }
                if (modifications.ContainsKey("ThreatLevelPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x48, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatLevelPOW"])), 0));
                }
                if (modifications.ContainsKey("OpportunityLevelPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x4C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["OpportunityLevelPOW"])), 0));
                }
                if (modifications.ContainsKey("PickingScoreMultPOW"))
                {
                    Marshal.WriteInt32(ptr + 0x50, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PickingScoreMultPOW"])), 0));
                }
                if (modifications.ContainsKey("DistanceToCurrentTile"))
                {
                    Marshal.WriteInt32(ptr + 0x54, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceToCurrentTile"])), 0));
                }
                if (modifications.ContainsKey("DistanceToZones"))
                {
                    Marshal.WriteInt32(ptr + 0x58, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceToZones"])), 0));
                }
                if (modifications.ContainsKey("DistanceToAdvanceZones"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceToAdvanceZones"])), 0));
                }
                if (modifications.ContainsKey("SafetyOutsideDefendZones"))
                {
                    Marshal.WriteInt32(ptr + 0x60, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyOutsideDefendZones"])), 0));
                }
                if (modifications.ContainsKey("SafetyOutsideDefendZonesVehicles"))
                {
                    Marshal.WriteInt32(ptr + 0x64, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SafetyOutsideDefendZonesVehicles"])), 0));
                }
                if (modifications.ContainsKey("OccupyZoneValue"))
                {
                    Marshal.WriteInt32(ptr + 0x68, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["OccupyZoneValue"])), 0));
                }
                if (modifications.ContainsKey("CaptureZoneValue"))
                {
                    Marshal.WriteInt32(ptr + 0x6C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CaptureZoneValue"])), 0));
                }
                if (modifications.ContainsKey("CoverAgainstOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x70, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CoverAgainstOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x74, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromTileEffects"))
                {
                    Marshal.WriteInt32(ptr + 0x78, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromTileEffects"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsDamage"))
                {
                    Marshal.WriteInt32(ptr + 0x7C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsDamage"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsArmorDamage"))
                {
                    Marshal.WriteInt32(ptr + 0x80, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsArmorDamage"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsSuppression"))
                {
                    Marshal.WriteInt32(ptr + 0x84, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsSuppression"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsStun"))
                {
                    Marshal.WriteInt32(ptr + 0x88, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsStun"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromPinnedDownOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromPinnedDownOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromSuppressedOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x90, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromSuppressedOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFrom2xStunnedOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x94, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFrom2xStunnedOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromFleeingOpponents"))
                {
                    Marshal.WriteInt32(ptr + 0x98, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromFleeingOpponents"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsAlreadyActed"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsAlreadyActed"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsButAlliesInControl"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsButAlliesInControl"])), 0));
                }
                if (modifications.ContainsKey("ThreatFromOpponentsAtHypotheticalPositionsMult"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ThreatFromOpponentsAtHypotheticalPositionsMult"])), 0));
                }
                if (modifications.ContainsKey("AllyMetascoreAgainstThreshold"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AllyMetascoreAgainstThreshold"])), 0));
                }
                if (modifications.ContainsKey("AvoidAlliesPOW"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AvoidAlliesPOW"])), 0));
                }
                if (modifications.ContainsKey("AvoidOpponentsPOW"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AvoidOpponentsPOW"])), 0));
                }
                if (modifications.ContainsKey("FleeFromOpponentsPOW"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["FleeFromOpponentsPOW"])), 0));
                }
                if (modifications.ContainsKey("ScalePositionWithTags"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ScalePositionWithTags"])), 0));
                }
                if (modifications.ContainsKey("IncludeAttacksAgainstAllOpponentsMult"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["IncludeAttacksAgainstAllOpponentsMult"])), 0));
                }
                if (modifications.ContainsKey("OppositeSideDistanceFromOpponentCap"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["OppositeSideDistanceFromOpponentCap"]));
                }
                if (modifications.ContainsKey("CullTilesDistances"))
                {
                    Marshal.WriteInt32(ptr + 0xC4, Convert.ToInt32(modifications["CullTilesDistances"]));
                }
                if (modifications.ContainsKey("DistanceToZoneDeployScore"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceToZoneDeployScore"])), 0));
                }
                if (modifications.ContainsKey("DistanceToAlliesScore"))
                {
                    Marshal.WriteInt32(ptr + 0xCC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceToAlliesScore"])), 0));
                }
                if (modifications.ContainsKey("CoverInEachDirectionBonus"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CoverInEachDirectionBonus"])), 0));
                }
                if (modifications.ContainsKey("InsideBuildingDuringDeployment"))
                {
                    Marshal.WriteInt32(ptr + 0xD4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InsideBuildingDuringDeployment"])), 0));
                }
                if (modifications.ContainsKey("DeploymentConcealmentMult"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DeploymentConcealmentMult"])), 0));
                }
                if (modifications.ContainsKey("InvisibleTargetValueMult"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InvisibleTargetValueMult"])), 0));
                }
                if (modifications.ContainsKey("TargetValueDamageScale"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueDamageScale"])), 0));
                }
                if (modifications.ContainsKey("TargetValueArmorScale"))
                {
                    Marshal.WriteInt32(ptr + 0xE4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueArmorScale"])), 0));
                }
                if (modifications.ContainsKey("TargetValueSuppressionScale"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueSuppressionScale"])), 0));
                }
                if (modifications.ContainsKey("TargetValueStunScale"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueStunScale"])), 0));
                }
                if (modifications.ContainsKey("TargetValueThreatScale"))
                {
                    Marshal.WriteInt32(ptr + 0xF0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueThreatScale"])), 0));
                }
                if (modifications.ContainsKey("TargetValueMaxThreatSuppressScale"))
                {
                    Marshal.WriteInt32(ptr + 0xF4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetValueMaxThreatSuppressScale"])), 0));
                }
                if (modifications.ContainsKey("ScoreThresholdWithLimitedUses"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ScoreThresholdWithLimitedUses"])), 0));
                }
                if (modifications.ContainsKey("FriendlyFirePenalty"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["FriendlyFirePenalty"])), 0));
                }
                if (modifications.ContainsKey("DamageBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x100, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageBaseScore"])), 0));
                }
                if (modifications.ContainsKey("DamageScoreMult"))
                {
                    Marshal.WriteInt32(ptr + 0x104, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageScoreMult"])), 0));
                }
                if (modifications.ContainsKey("InflictDamageFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x108, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InflictDamageFromTile"])), 0));
                }
                if (modifications.ContainsKey("SuppressionBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x10C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SuppressionBaseScore"])), 0));
                }
                if (modifications.ContainsKey("SuppressionScoreMult"))
                {
                    Marshal.WriteInt32(ptr + 0x110, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SuppressionScoreMult"])), 0));
                }
                if (modifications.ContainsKey("InflictSuppressionFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x114, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InflictSuppressionFromTile"])), 0));
                }
                if (modifications.ContainsKey("StunBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x118, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["StunBaseScore"])), 0));
                }
                if (modifications.ContainsKey("StunScoreMult"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["StunScoreMult"])), 0));
                }
                if (modifications.ContainsKey("StunFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x120, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["StunFromTile"])), 0));
                }
                if (modifications.ContainsKey("MoveBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x124, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MoveBaseScore"])), 0));
                }
                if (modifications.ContainsKey("MoveScoreMult"))
                {
                    Marshal.WriteInt32(ptr + 0x128, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MoveScoreMult"])), 0));
                }
                if (modifications.ContainsKey("NearTileLimit"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, Convert.ToInt32(modifications["NearTileLimit"]));
                }
                if (modifications.ContainsKey("TileScoreDifferenceMult"))
                {
                    Marshal.WriteInt32(ptr + 0x130, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TileScoreDifferenceMult"])), 0));
                }
                if (modifications.ContainsKey("TileScoreDifferencePow"))
                {
                    Marshal.WriteInt32(ptr + 0x134, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TileScoreDifferencePow"])), 0));
                }
                if (modifications.ContainsKey("UtilityThreshold"))
                {
                    Marshal.WriteInt32(ptr + 0x138, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UtilityThreshold"])), 0));
                }
                if (modifications.ContainsKey("PathfindingSafetyCostMult"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PathfindingSafetyCostMult"])), 0));
                }
                if (modifications.ContainsKey("PathfindingUnknownTileSafety"))
                {
                    Marshal.WriteInt32(ptr + 0x140, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PathfindingUnknownTileSafety"])), 0));
                }
                if (modifications.ContainsKey("PathfindingHiddenFromOpponentsBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["PathfindingHiddenFromOpponentsBonus"]));
                }
                if (modifications.ContainsKey("EntirePathScoreContribution"))
                {
                    Marshal.WriteInt32(ptr + 0x148, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EntirePathScoreContribution"])), 0));
                }
                if (modifications.ContainsKey("MoveIfNewTileIsBetterBy"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MoveIfNewTileIsBetterBy"])), 0));
                }
                if (modifications.ContainsKey("GetUpIfNewTileIsBetterBy"))
                {
                    Marshal.WriteInt32(ptr + 0x150, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["GetUpIfNewTileIsBetterBy"])), 0));
                }
                if (modifications.ContainsKey("DistanceTooFarForOneTurnMult"))
                {
                    Marshal.WriteInt32(ptr + 0x154, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DistanceTooFarForOneTurnMult"])), 0));
                }
                if (modifications.ContainsKey("ConsiderAlternativeIfBetterBy"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ConsiderAlternativeIfBetterBy"])), 0));
                }
                if (modifications.ContainsKey("ConsiderAlternativeToUltimateIfBetterBy"))
                {
                    Marshal.WriteInt32(ptr + 0x15C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ConsiderAlternativeToUltimateIfBetterBy"])), 0));
                }
                if (modifications.ContainsKey("EnoughAPToPerformSkillAfterwards"))
                {
                    Marshal.WriteInt32(ptr + 0x160, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnoughAPToPerformSkillAfterwards"])), 0));
                }
                if (modifications.ContainsKey("EnoughAPToPerformOnlySkillAfterwards"))
                {
                    Marshal.WriteInt32(ptr + 0x164, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnoughAPToPerformOnlySkillAfterwards"])), 0));
                }
                if (modifications.ContainsKey("EnoughAPToDeployAfterwards"))
                {
                    Marshal.WriteInt32(ptr + 0x168, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnoughAPToDeployAfterwards"])), 0));
                }
                if (modifications.ContainsKey("BuffBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x16C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["BuffBaseScore"])), 0));
                }
                if (modifications.ContainsKey("BuffTargetValueMult"))
                {
                    Marshal.WriteInt32(ptr + 0x170, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["BuffTargetValueMult"])), 0));
                }
                if (modifications.ContainsKey("BuffFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x174, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["BuffFromTile"])), 0));
                }
                if (modifications.ContainsKey("RemoveSuppressionMult"))
                {
                    Marshal.WriteInt32(ptr + 0x178, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RemoveSuppressionMult"])), 0));
                }
                if (modifications.ContainsKey("RemoveStunnedMult"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RemoveStunnedMult"])), 0));
                }
                if (modifications.ContainsKey("RestoreMoraleMult"))
                {
                    Marshal.WriteInt32(ptr + 0x180, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RestoreMoraleMult"])), 0));
                }
                if (modifications.ContainsKey("IncreaseMovementMult"))
                {
                    Marshal.WriteInt32(ptr + 0x184, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["IncreaseMovementMult"])), 0));
                }
                if (modifications.ContainsKey("IncreaseOffensiveStatsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x188, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["IncreaseOffensiveStatsMult"])), 0));
                }
                if (modifications.ContainsKey("IncreaseDefensiveStatsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["IncreaseDefensiveStatsMult"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x190, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoBaseScore"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoTargetValueMult"))
                {
                    Marshal.WriteInt32(ptr + 0x194, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoTargetValueMult"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoNoAmmoMult"))
                {
                    Marshal.WriteInt32(ptr + 0x198, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoNoAmmoMult"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoSpecialWeaponMult"))
                {
                    Marshal.WriteInt32(ptr + 0x19C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoSpecialWeaponMult"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoGoalThreshold"))
                {
                    Marshal.WriteInt32(ptr + 0x1A0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoGoalThreshold"])), 0));
                }
                if (modifications.ContainsKey("SupplyAmmoFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x1A4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SupplyAmmoFromTile"])), 0));
                }
                if (modifications.ContainsKey("TargetDesignatorBaseScore"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetDesignatorBaseScore"])), 0));
                }
                if (modifications.ContainsKey("TargetDesignatorScoreMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1AC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetDesignatorScoreMult"])), 0));
                }
                if (modifications.ContainsKey("TargetDesignatorFromTile"))
                {
                    Marshal.WriteInt32(ptr + 0x1B0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TargetDesignatorFromTile"])), 0));
                }
                if (modifications.ContainsKey("GainBonusTurnBaseMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1B4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["GainBonusTurnBaseMult"])), 0));
                }
            }

            else if (templateType.Name == "AccessoryTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("SlotType"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotType"]));
                }
                if (modifications.ContainsKey("ItemType"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["ItemType"]));
                }
                if (modifications.ContainsKey("ExclusiveCategory"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["ExclusiveCategory"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
            }

            else if (templateType.Name == "AnimationSequenceTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("HasRandomRotation"))
                {
                    Marshal.WriteByte(ptr + 0x80, Convert.ToByte(modifications["HasRandomRotation"]));
                }
                if (modifications.ContainsKey("MinRandomAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x84, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinRandomAngle"])), 0));
                }
                if (modifications.ContainsKey("MaxRandomAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x88, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxRandomAngle"])), 0));
                }
            }

            else if (templateType.Name == "AnimationSoundTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "AnimatorParameterNameTemplate")
            {
                if (modifications.ContainsKey("ParameterType"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["ParameterType"]));
                }
                if (modifications.ContainsKey("m_ParamHash"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["m_ParamHash"]));
                }
            }

            else if (templateType.Name == "ArmorTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("SlotType"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotType"]));
                }
                if (modifications.ContainsKey("ItemType"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["ItemType"]));
                }
                if (modifications.ContainsKey("ExclusiveCategory"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["ExclusiveCategory"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("HasSpecialFemaleModels"))
                {
                    Marshal.WriteByte(ptr + 0x128, Convert.ToByte(modifications["HasSpecialFemaleModels"]));
                }
                if (modifications.ContainsKey("SquadLeaderMode"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["SquadLeaderMode"]));
                }
                if (modifications.ContainsKey("OverrideScale"))
                {
                    Marshal.WriteByte(ptr + 0x180, Convert.ToByte(modifications["OverrideScale"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("Armor"))
                {
                    Marshal.WriteInt32(ptr + 0x190, Convert.ToInt32(modifications["Armor"]));
                }
                if (modifications.ContainsKey("DurabilityPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["DurabilityPerElement"]));
                }
                if (modifications.ContainsKey("DamageResistance"))
                {
                    Marshal.WriteInt32(ptr + 0x198, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageResistance"])), 0));
                }
                if (modifications.ContainsKey("HitpointsPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0x19C, Convert.ToInt32(modifications["HitpointsPerElement"]));
                }
                if (modifications.ContainsKey("HitpointsPerElementMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1A0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["HitpointsPerElementMult"])), 0));
                }
                if (modifications.ContainsKey("Accuracy"))
                {
                    Marshal.WriteInt32(ptr + 0x1A4, Convert.ToInt32(modifications["Accuracy"]));
                }
                if (modifications.ContainsKey("AccuracyMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyMult"])), 0));
                }
                if (modifications.ContainsKey("DefenseMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1AC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DefenseMult"])), 0));
                }
                if (modifications.ContainsKey("Discipline"))
                {
                    Marshal.WriteInt32(ptr + 0x1B0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["Discipline"])), 0));
                }
                if (modifications.ContainsKey("DisciplineMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1B4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DisciplineMult"])), 0));
                }
                if (modifications.ContainsKey("Vision"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["Vision"]));
                }
                if (modifications.ContainsKey("VisionMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1BC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["VisionMult"])), 0));
                }
                if (modifications.ContainsKey("Detection"))
                {
                    Marshal.WriteInt32(ptr + 0x1C0, Convert.ToInt32(modifications["Detection"]));
                }
                if (modifications.ContainsKey("DetectionMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1C4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DetectionMult"])), 0));
                }
                if (modifications.ContainsKey("Concealment"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["Concealment"]));
                }
                if (modifications.ContainsKey("ConcealmentMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1CC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ConcealmentMult"])), 0));
                }
                if (modifications.ContainsKey("SuppressionImpactMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1D0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SuppressionImpactMult"])), 0));
                }
                if (modifications.ContainsKey("GetDismemberedChanceBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x1D4, Convert.ToInt32(modifications["GetDismemberedChanceBonus"]));
                }
                if (modifications.ContainsKey("GetDismemberedChanceMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1D8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["GetDismemberedChanceMult"])), 0));
                }
                if (modifications.ContainsKey("ActionPoints"))
                {
                    Marshal.WriteInt32(ptr + 0x1DC, Convert.ToInt32(modifications["ActionPoints"]));
                }
                if (modifications.ContainsKey("ActionPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ActionPointsMult"])), 0));
                }
                if (modifications.ContainsKey("AdditionalMovementCost"))
                {
                    Marshal.WriteInt32(ptr + 0x1E4, Convert.ToInt32(modifications["AdditionalMovementCost"]));
                }
                if (modifications.ContainsKey("SoundOnMovementStep"))
                {
                    Marshal.WriteInt32(ptr + 0x1F0, Convert.ToInt32(modifications["SoundOnMovementStep"]));
                }
                if (modifications.ContainsKey("SoundOnMovementStepOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["SoundOnMovementStepOverrides2"]));
                }
                if (modifications.ContainsKey("SoundOnMovementSymbolic"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["SoundOnMovementSymbolic"]));
                }
                if (modifications.ContainsKey("SoundOnArmorHit"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["SoundOnArmorHit"]));
                }
                if (modifications.ContainsKey("SoundOnHitpointsHit"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["SoundOnHitpointsHit"]));
                }
                if (modifications.ContainsKey("SoundOnHitpointsHitFemale"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["SoundOnHitpointsHitFemale"]));
                }
                if (modifications.ContainsKey("SoundOnDeath"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["SoundOnDeath"]));
                }
                if (modifications.ContainsKey("SoundOnDeathFemale"))
                {
                    Marshal.WriteInt32(ptr + 0x228, Convert.ToInt32(modifications["SoundOnDeathFemale"]));
                }
            }

            else if (templateType.Name == "ArmyTemplate")
            {
                if (modifications.ContainsKey("FactionType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["FactionType"]));
                }
                if (modifications.ContainsKey("FactionTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["FactionTemplate"]));
                }
            }

            else if (templateType.Name == "BiomeTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("BiomeType"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["BiomeType"]));
                }
                if (modifications.ContainsKey("ShowInCheatMenu"))
                {
                    Marshal.WriteByte(ptr + 0x84, Convert.ToByte(modifications["ShowInCheatMenu"]));
                }
                if (modifications.ContainsKey("PropData"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["PropData"]));
                }
                if (modifications.ContainsKey("TextureArray"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TextureArray"]));
                }
                if (modifications.ContainsKey("PhysicMaterial"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["PhysicMaterial"]));
                }
                if (modifications.ContainsKey("HasGrass"))
                {
                    Marshal.WriteByte(ptr + 0xC0, Convert.ToByte(modifications["HasGrass"]));
                }
                if (modifications.ContainsKey("LightConditions"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["LightConditions"]));
                }
                if (modifications.ContainsKey("ElevationBlockLoS"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ElevationBlockLoS"])), 0));
                }
            }

            else if (templateType.Name == "BoolPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("DefaultValue"))
                {
                    Marshal.WriteByte(ptr + 0x8C, Convert.ToByte(modifications["DefaultValue"]));
                }
            }

            else if (templateType.Name == "ChunkTemplate")
            {
                if (modifications.ContainsKey("Width"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["Width"]));
                }
                if (modifications.ContainsKey("Height"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, Convert.ToInt32(modifications["Height"]));
                }
                if (modifications.ContainsKey("CoverConfig"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["CoverConfig"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("SpawnMode"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["SpawnMode"]));
                }
                if (modifications.ContainsKey("MaxSpawns"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["MaxSpawns"]));
                }
                if (modifications.ContainsKey("RandomlyRotateChildren"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["RandomlyRotateChildren"]));
                }
            }

            else if (templateType.Name == "CommodityTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
            }

            else if (templateType.Name == "ConversationEffectsTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "ConversationStageTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "ConversationTemplate")
            {
                if (modifications.ContainsKey("ConversationType"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["ConversationType"]));
                }
                if (modifications.ContainsKey("Active"))
                {
                    Marshal.WriteByte(ptr + 0x1C, Convert.ToByte(modifications["Active"]));
                }
                if (modifications.ContainsKey("LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x20, Convert.ToInt32(modifications["LocaState"]));
                }
                if (modifications.ContainsKey("TriggerTag"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["TriggerTag"]));
                }
                if (modifications.ContainsKey("Stage"))
                {
                    Marshal.WriteInt32(ptr + 0x48, Convert.ToInt32(modifications["Stage"]));
                }
                if (modifications.ContainsKey("Condition"))
                {
                    Marshal.WriteInt32(ptr + 0x50, Convert.ToInt32(modifications["Condition"]));
                }
                if (modifications.ContainsKey("Repeatable"))
                {
                    Marshal.WriteByte(ptr + 0x58, Convert.ToByte(modifications["Repeatable"]));
                }
                if (modifications.ContainsKey("Repetitions"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, Convert.ToInt32(modifications["Repetitions"]));
                }
                if (modifications.ContainsKey("Priority"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["Priority"]));
                }
                if (modifications.ContainsKey("PlayChance"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["PlayChance"]));
                }
                if (modifications.ContainsKey("Nodes"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Nodes"]));
                }
                if (modifications.ContainsKey("Version"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Version"]));
                }
                if (modifications.ContainsKey("m_DummyInstance"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["m_DummyInstance"]));
                }
            }

            else if (templateType.Name == "DecalTemplate")
            {
                if (modifications.ContainsKey("Index"))
                {
                    Marshal.WriteInt32(ptr + 0x10, Convert.ToInt32(modifications["Index"]));
                }
                if (modifications.ContainsKey("MinSize"))
                {
                    Marshal.WriteInt32(ptr + 0x20, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinSize"])), 0));
                }
                if (modifications.ContainsKey("MaxSize"))
                {
                    Marshal.WriteInt32(ptr + 0x24, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxSize"])), 0));
                }
                if (modifications.ContainsKey("MinRotation"))
                {
                    Marshal.WriteInt32(ptr + 0x28, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinRotation"])), 0));
                }
                if (modifications.ContainsKey("MaxRotation"))
                {
                    Marshal.WriteInt32(ptr + 0x2C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxRotation"])), 0));
                }
            }

            else if (templateType.Name == "DefectTemplate")
            {
                if (modifications.ContainsKey("DamageEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["DamageEffect"]));
                }
                if (modifications.ContainsKey("Severity"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["Severity"]));
                }
                if (modifications.ContainsKey("Chance"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["Chance"]));
                }
            }

            else if (templateType.Name == "DisplayIndexPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("DefaultValue"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["DefaultValue"]));
                }
                if (modifications.ContainsKey("MinValue"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["MinValue"]));
                }
                if (modifications.ContainsKey("MaxValue"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["MaxValue"]));
                }
            }

            else if (templateType.Name == "DossierItemTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("m_DossierType"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["m_DossierType"]));
                }
            }

            else if (templateType.Name == "ElementAnimatorTemplate")
            {
                if (modifications.ContainsKey("SpeedBlendTime"))
                {
                    Marshal.WriteInt32(ptr + 0x58, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SpeedBlendTime"])), 0));
                }
                if (modifications.ContainsKey("StanceDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["StanceDelay"])), 0));
                }
                if (modifications.ContainsKey("DisableDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x60, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DisableDelay"])), 0));
                }
                if (modifications.ContainsKey("MovementStanceDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x64, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MovementStanceDelay"])), 0));
                }
                if (modifications.ContainsKey("UnderAttackResetDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x70, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["UnderAttackResetDelay"])), 0));
                }
                if (modifications.ContainsKey("AnimatorInPlaceTurningSpeedCurve"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["AnimatorInPlaceTurningSpeedCurve"]));
                }
                if (modifications.ContainsKey("DeathBehaviour"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["DeathBehaviour"]));
                }
                if (modifications.ContainsKey("AdditionalRagdollKillImpulseArea"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["AdditionalRagdollKillImpulseArea"]));
                }
                if (modifications.ContainsKey("HitAnimations"))
                {
                    Marshal.WriteByte(ptr + 0xA4, Convert.ToByte(modifications["HitAnimations"]));
                }
                if (modifications.ContainsKey("DmgToHitAnimationStrength"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["DmgToHitAnimationStrength"]));
                }
                if (modifications.ContainsKey("RecoilOnHit"))
                {
                    Marshal.WriteByte(ptr + 0xB0, Convert.ToByte(modifications["RecoilOnHit"]));
                }
                if (modifications.ContainsKey("DisableAttachmentAnimatorsOnDeath"))
                {
                    Marshal.WriteByte(ptr + 0xB1, Convert.ToByte(modifications["DisableAttachmentAnimatorsOnDeath"]));
                }
                if (modifications.ContainsKey("ExhaustEffects"))
                {
                    Marshal.WriteByte(ptr + 0xB2, Convert.ToByte(modifications["ExhaustEffects"]));
                }
                if (modifications.ContainsKey("HumanIK"))
                {
                    Marshal.WriteByte(ptr + 0xC0, Convert.ToByte(modifications["HumanIK"]));
                }
                if (modifications.ContainsKey("LeftHandIKBlendTime"))
                {
                    Marshal.WriteInt32(ptr + 0xC4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["LeftHandIKBlendTime"])), 0));
                }
                if (modifications.ContainsKey("NegativeSpeedTurns"))
                {
                    Marshal.WriteByte(ptr + 0xD4, Convert.ToByte(modifications["NegativeSpeedTurns"]));
                }
                if (modifications.ContainsKey("SteeringDirection"))
                {
                    Marshal.WriteByte(ptr + 0xD5, Convert.ToByte(modifications["SteeringDirection"]));
                }
                if (modifications.ContainsKey("MaxClampSteeringAngle"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxClampSteeringAngle"])), 0));
                }
                if (modifications.ContainsKey("Aiming"))
                {
                    Marshal.WriteByte(ptr + 0xDC, Convert.ToByte(modifications["Aiming"]));
                }
                if (modifications.ContainsKey("AimSpeed"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AimSpeed"])), 0));
                }
                if (modifications.ContainsKey("TurnDelay180Degree"))
                {
                    Marshal.WriteInt32(ptr + 0xE4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["TurnDelay180Degree"])), 0));
                }
                if (modifications.ContainsKey("UseRootMotionAiming"))
                {
                    Marshal.WriteByte(ptr + 0xE8, Convert.ToByte(modifications["UseRootMotionAiming"]));
                }
                if (modifications.ContainsKey("AngleMapping"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["AngleMapping"]));
                }
            }

            else if (templateType.Name == "EmotionalStateTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("StateType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["StateType"]));
                }
                if (modifications.ContainsKey("Effect"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Effect"]));
                }
                if (modifications.ContainsKey("Category"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["Category"]));
                }
                if (modifications.ContainsKey("IsPositive"))
                {
                    Marshal.WriteByte(ptr + 0xCC, Convert.ToByte(modifications["IsPositive"]));
                }
                if (modifications.ContainsKey("IsSuperState"))
                {
                    Marshal.WriteByte(ptr + 0xCD, Convert.ToByte(modifications["IsSuperState"]));
                }
                if (modifications.ContainsKey("SuperState"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["SuperState"]));
                }
            }

            else if (templateType.Name == "EnemyAssetTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("DisableAfterMission"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["DisableAfterMission"]));
                }
            }

            else if (templateType.Name == "EntityTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("ActorType"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["ActorType"]));
                }
                if (modifications.ContainsKey("StructureType"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["StructureType"]));
                }
                if (modifications.ContainsKey("SurfaceType"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["SurfaceType"]));
                }
                if (modifications.ContainsKey("ElementsMin"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["ElementsMin"]));
                }
                if (modifications.ContainsKey("ElementsMax"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["ElementsMax"]));
                }
                if (modifications.ContainsKey("ChanceForFemaleElements"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["ChanceForFemaleElements"]));
                }
                if (modifications.ContainsKey("ArmyPointCost"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["ArmyPointCost"]));
                }
                if (modifications.ContainsKey("ProvidesCover"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["ProvidesCover"]));
                }
                if (modifications.ContainsKey("ProvidesCoverWhenDestroyed"))
                {
                    Marshal.WriteByte(ptr + 0xBC, Convert.ToByte(modifications["ProvidesCoverWhenDestroyed"]));
                }
                if (modifications.ContainsKey("UsesCover"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["UsesCover"]));
                }
                if (modifications.ContainsKey("IsContainableInEntities"))
                {
                    Marshal.WriteByte(ptr + 0xC4, Convert.ToByte(modifications["IsContainableInEntities"]));
                }
                if (modifications.ContainsKey("ContainerType"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["ContainerType"]));
                }
                if (modifications.ContainsKey("ContainedEntityOnSpawn"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["ContainedEntityOnSpawn"]));
                }
                if (modifications.ContainsKey("DespawnIfEmpty"))
                {
                    Marshal.WriteByte(ptr + 0xD8, Convert.ToByte(modifications["DespawnIfEmpty"]));
                }
                if (modifications.ContainsKey("ProvidesCoverInside"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["ProvidesCoverInside"]));
                }
                if (modifications.ContainsKey("EffectOnContained"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["EffectOnContained"]));
                }
                if (modifications.ContainsKey("IsIgnoredInActorCount"))
                {
                    Marshal.WriteByte(ptr + 0xF0, Convert.ToByte(modifications["IsIgnoredInActorCount"]));
                }
                if (modifications.ContainsKey("IsDestructible"))
                {
                    Marshal.WriteByte(ptr + 0xF1, Convert.ToByte(modifications["IsDestructible"]));
                }
                if (modifications.ContainsKey("IsSurfaceChangedOnDeath"))
                {
                    Marshal.WriteByte(ptr + 0xF2, Convert.ToByte(modifications["IsSurfaceChangedOnDeath"]));
                }
                if (modifications.ContainsKey("ChangeSurfaceOnDeath"))
                {
                    Marshal.WriteInt32(ptr + 0xF4, Convert.ToInt32(modifications["ChangeSurfaceOnDeath"]));
                }
                if (modifications.ContainsKey("IsTraversableByInfantry"))
                {
                    Marshal.WriteByte(ptr + 0xF8, Convert.ToByte(modifications["IsTraversableByInfantry"]));
                }
                if (modifications.ContainsKey("IsAffectedByFatalities"))
                {
                    Marshal.WriteByte(ptr + 0xF9, Convert.ToByte(modifications["IsAffectedByFatalities"]));
                }
                if (modifications.ContainsKey("DestroyPropsOnDeath"))
                {
                    Marshal.WriteByte(ptr + 0xFA, Convert.ToByte(modifications["DestroyPropsOnDeath"]));
                }
                if (modifications.ContainsKey("DestroyPropsRadius"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DestroyPropsRadius"])), 0));
                }
                if (modifications.ContainsKey("SpeakerTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["SpeakerTemplate"]));
                }
                if (modifications.ContainsKey("IsAligningWithTerrain"))
                {
                    Marshal.WriteByte(ptr + 0x118, Convert.ToByte(modifications["IsAligningWithTerrain"]));
                }
                if (modifications.ContainsKey("FixedRotationForDestroyedPrefab"))
                {
                    Marshal.WriteByte(ptr + 0x119, Convert.ToByte(modifications["FixedRotationForDestroyedPrefab"]));
                }
                if (modifications.ContainsKey("IsCompatibleWithCables"))
                {
                    Marshal.WriteByte(ptr + 0x11A, Convert.ToByte(modifications["IsCompatibleWithCables"]));
                }
                if (modifications.ContainsKey("HasExtendedRangeForCables"))
                {
                    Marshal.WriteByte(ptr + 0x11B, Convert.ToByte(modifications["HasExtendedRangeForCables"]));
                }
                if (modifications.ContainsKey("CameraAutoHeightOffset"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CameraAutoHeightOffset"])), 0));
                }
                if (modifications.ContainsKey("OverrideMissionPreviewColor"))
                {
                    Marshal.WriteByte(ptr + 0x120, Convert.ToByte(modifications["OverrideMissionPreviewColor"]));
                }
                if (modifications.ContainsKey("OverrideScaleForSquadLeader"))
                {
                    Marshal.WriteByte(ptr + 0x170, Convert.ToByte(modifications["OverrideScaleForSquadLeader"]));
                }
                if (modifications.ContainsKey("ScaleOffsetSquadLeader"))
                {
                    Marshal.WriteInt32(ptr + 0x174, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ScaleOffsetSquadLeader"])), 0));
                }
                if (modifications.ContainsKey("IsBlockingLineOfSight"))
                {
                    Marshal.WriteByte(ptr + 0x198, Convert.ToByte(modifications["IsBlockingLineOfSight"]));
                }
                if (modifications.ContainsKey("HudYOffsetScale"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["HudYOffsetScale"])), 0));
                }
                if (modifications.ContainsKey("FactionSpecificAnimation"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, Convert.ToInt32(modifications["FactionSpecificAnimation"]));
                }
                if (modifications.ContainsKey("AimWithVisualSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x1E4, Convert.ToInt32(modifications["AimWithVisualSlot"]));
                }
                if (modifications.ContainsKey("AnimatorTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x1E8, Convert.ToInt32(modifications["AnimatorTemplate"]));
                }
                if (modifications.ContainsKey("MinSkinQuality"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["MinSkinQuality"]));
                }
                if (modifications.ContainsKey("BloodDecals"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["BloodDecals"]));
                }
                if (modifications.ContainsKey("BloodDecalsOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["BloodDecalsOverride"]));
                }
                if (modifications.ContainsKey("BloodPool"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["BloodPool"]));
                }
                if (modifications.ContainsKey("BloodPoolOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["BloodPoolOverride"]));
                }
                if (modifications.ContainsKey("BloodPoolTriggerType"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["BloodPoolTriggerType"]));
                }
                if (modifications.ContainsKey("BloodPoolAnimation"))
                {
                    Marshal.WriteByte(ptr + 0x224, Convert.ToByte(modifications["BloodPoolAnimation"]));
                }
                if (modifications.ContainsKey("DamageReceivedEffectThreshold"))
                {
                    Marshal.WriteInt32(ptr + 0x238, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageReceivedEffectThreshold"])), 0));
                }
                if (modifications.ContainsKey("GetDismemberedSmallAdditionalParts"))
                {
                    Marshal.WriteInt32(ptr + 0x248, Convert.ToInt32(modifications["GetDismemberedSmallAdditionalParts"]));
                }
                if (modifications.ContainsKey("DeathEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x250, Convert.ToInt32(modifications["DeathEffectOverrides2"]));
                }
                if (modifications.ContainsKey("DeathEffectTriggerType"))
                {
                    Marshal.WriteInt32(ptr + 0x260, Convert.ToInt32(modifications["DeathEffectTriggerType"]));
                }
                if (modifications.ContainsKey("IsSinkingIntoGroundOnDeath"))
                {
                    Marshal.WriteByte(ptr + 0x270, Convert.ToByte(modifications["IsSinkingIntoGroundOnDeath"]));
                }
                if (modifications.ContainsKey("DeathCameraEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x274, Convert.ToInt32(modifications["DeathCameraEffect"]));
                }
                if (modifications.ContainsKey("SoundOnAim"))
                {
                    Marshal.WriteInt32(ptr + 0x278, Convert.ToInt32(modifications["SoundOnAim"]));
                }
                if (modifications.ContainsKey("SoundWhileAlive"))
                {
                    Marshal.WriteInt32(ptr + 0x280, Convert.ToInt32(modifications["SoundWhileAlive"]));
                }
                if (modifications.ContainsKey("MovementEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x2A0, Convert.ToInt32(modifications["MovementEffectOverrides2"]));
                }
                if (modifications.ContainsKey("TriggerMovementEffectsOnStep"))
                {
                    Marshal.WriteByte(ptr + 0x2A8, Convert.ToByte(modifications["TriggerMovementEffectsOnStep"]));
                }
                if (modifications.ContainsKey("MovementType"))
                {
                    Marshal.WriteInt32(ptr + 0x2B0, Convert.ToInt32(modifications["MovementType"]));
                }
                if (modifications.ContainsKey("VisualPositioning"))
                {
                    Marshal.WriteInt32(ptr + 0x2B8, Convert.ToInt32(modifications["VisualPositioning"]));
                }
                if (modifications.ContainsKey("PullTowardsTileCenter"))
                {
                    Marshal.WriteInt32(ptr + 0x2BC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PullTowardsTileCenter"])), 0));
                }
                if (modifications.ContainsKey("RotationAfterMovement"))
                {
                    Marshal.WriteInt32(ptr + 0x2C0, Convert.ToInt32(modifications["RotationAfterMovement"]));
                }
                if (modifications.ContainsKey("CameraShakeOnMovement"))
                {
                    Marshal.WriteByte(ptr + 0x2C4, Convert.ToByte(modifications["CameraShakeOnMovement"]));
                }
                if (modifications.ContainsKey("CameraShakeOnMovementStepInterval"))
                {
                    Marshal.WriteInt32(ptr + 0x2C8, Convert.ToInt32(modifications["CameraShakeOnMovementStepInterval"]));
                }
                if (modifications.ContainsKey("CameraShakeOnMovementDuration"))
                {
                    Marshal.WriteInt32(ptr + 0x2CC, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CameraShakeOnMovementDuration"])), 0));
                }
                if (modifications.ContainsKey("CameraShakeOnMovementIntensity"))
                {
                    Marshal.WriteInt32(ptr + 0x2D0, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CameraShakeOnMovementIntensity"])), 0));
                }
                if (modifications.ContainsKey("CameraShakeOnMovementRecoverTime"))
                {
                    Marshal.WriteInt32(ptr + 0x2D4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CameraShakeOnMovementRecoverTime"])), 0));
                }
                if (modifications.ContainsKey("InventoryType"))
                {
                    Marshal.WriteInt32(ptr + 0x2D8, Convert.ToInt32(modifications["InventoryType"]));
                }
                if (modifications.ContainsKey("ModularVehicle"))
                {
                    Marshal.WriteInt32(ptr + 0x2F0, Convert.ToInt32(modifications["ModularVehicle"]));
                }
                if (modifications.ContainsKey("Properties"))
                {
                    Marshal.WriteInt32(ptr + 0x2F8, Convert.ToInt32(modifications["Properties"]));
                }
                if (modifications.ContainsKey("AIRole"))
                {
                    Marshal.WriteInt32(ptr + 0x310, Convert.ToInt32(modifications["AIRole"]));
                }
            }

            else if (templateType.Name == "EnvironmentFeatureTemplate")
            {
                if (modifications.ContainsKey("Concealment"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Concealment"]));
                }
                if (modifications.ContainsKey("Cover"))
                {
                    Marshal.WriteInt32(ptr + 0x7C, Convert.ToInt32(modifications["Cover"]));
                }
                if (modifications.ContainsKey("TileEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["TileEffect"]));
                }
                if (modifications.ContainsKey("IsDestroyedByVehicle"))
                {
                    Marshal.WriteByte(ptr + 0x88, Convert.ToByte(modifications["IsDestroyedByVehicle"]));
                }
                if (modifications.ContainsKey("HalfCoverClass"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["HalfCoverClass"]));
                }
                if (modifications.ContainsKey("DestroySound"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["DestroySound"]));
                }
                if (modifications.ContainsKey("SpawnOnDestroy"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["SpawnOnDestroy"]));
                }
            }

            else if (templateType.Name == "FactionTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("AlliedFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["AlliedFactionType"]));
                }
                if (modifications.ContainsKey("EnemyFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["EnemyFactionType"]));
                }
            }

            else if (templateType.Name == "GenericMissionTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("AllowedDifficulties"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["AllowedDifficulties"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("Condition"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Condition"]));
                }
                if (modifications.ContainsKey("BackgroundMusic"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["BackgroundMusic"]));
                }
                if (modifications.ContainsKey("StartAnimationSequence"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["StartAnimationSequence"]));
                }
                if (modifications.ContainsKey("PlayerSupplyMult"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PlayerSupplyMult"])), 0));
                }
                if (modifications.ContainsKey("EnemyArmyPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnemyArmyPointsMult"])), 0));
                }
                if (modifications.ContainsKey("EnemySpawnAreaSettings"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["EnemySpawnAreaSettings"]));
                }
                if (modifications.ContainsKey("EnemyStartInSleepMode"))
                {
                    Marshal.WriteByte(ptr + 0xD9, Convert.ToByte(modifications["EnemyStartInSleepMode"]));
                }
                if (modifications.ContainsKey("RoamWhileSleeping"))
                {
                    Marshal.WriteByte(ptr + 0xDA, Convert.ToByte(modifications["RoamWhileSleeping"]));
                }
                if (modifications.ContainsKey("SetpieceOwner"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, Convert.ToInt32(modifications["SetpieceOwner"]));
                }
                if (modifications.ContainsKey("m_RequiresAIDiscoveredPlayer"))
                {
                    Marshal.WriteByte(ptr + 0x120, Convert.ToByte(modifications["m_RequiresAIDiscoveredPlayer"]));
                }
                if (modifications.ContainsKey("m_InitialEnemyReinforcementStrengthMult"))
                {
                    Marshal.WriteInt32(ptr + 0x130, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_InitialEnemyReinforcementStrengthMult"])), 0));
                }
                if (modifications.ContainsKey("m_EnemyReinforcementStrengthPerRoundMult"))
                {
                    Marshal.WriteInt32(ptr + 0x134, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_EnemyReinforcementStrengthPerRoundMult"])), 0));
                }
                if (modifications.ContainsKey("m_MinUnusedEnemyReinforcementStrengthForSpawn"))
                {
                    Marshal.WriteInt32(ptr + 0x138, Convert.ToInt32(modifications["m_MinUnusedEnemyReinforcementStrengthForSpawn"]));
                }
                if (modifications.ContainsKey("m_ReinforcementStartRound"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, Convert.ToInt32(modifications["m_ReinforcementStartRound"]));
                }
                if (modifications.ContainsKey("m_ReinforcementStopRound"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["m_ReinforcementStopRound"]));
                }
                if (modifications.ContainsKey("m_EnemyReinforcementsSpawnAreaSettings"))
                {
                    Marshal.WriteInt32(ptr + 0x150, Convert.ToInt32(modifications["m_EnemyReinforcementsSpawnAreaSettings"]));
                }
                if (modifications.ContainsKey("m_MaxConcurrentEnemyStrengthMult"))
                {
                    Marshal.WriteInt32(ptr + 0x154, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_MaxConcurrentEnemyStrengthMult"])), 0));
                }
                if (modifications.ContainsKey("m_SoundOnReinforcementSpawn"))
                {
                    Marshal.WriteInt32(ptr + 0x158, Convert.ToInt32(modifications["m_SoundOnReinforcementSpawn"]));
                }
                if (modifications.ContainsKey("m_UnusedEnemyReinforcementStrength"))
                {
                    Marshal.WriteInt32(ptr + 0x160, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_UnusedEnemyReinforcementStrength"])), 0));
                }
                if (modifications.ContainsKey("m_MenacePresenceSpawnAreaSettings"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["m_MenacePresenceSpawnAreaSettings"]));
                }
                if (modifications.ContainsKey("m_MenacePresenceLowPresenceArmyPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x188, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_MenacePresenceLowPresenceArmyPointsMult"])), 0));
                }
                if (modifications.ContainsKey("m_MenacePresenceHighPresenceArmyPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_MenacePresenceHighPresenceArmyPointsMult"])), 0));
                }
            }

            else if (templateType.Name == "GlobalDifficultyTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("PlayerSupplyMult"))
                {
                    Marshal.WriteInt32(ptr + 0x80, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PlayerSupplyMult"])), 0));
                }
                if (modifications.ContainsKey("EnemyArmyPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x84, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnemyArmyPointsMult"])), 0));
                }
                if (modifications.ContainsKey("InitialAuthority"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["InitialAuthority"]));
                }
                if (modifications.ContainsKey("InitialSquaddies"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["InitialSquaddies"]));
                }
            }

            else if (templateType.Name == "HalfCoverTemplate")
            {
                if (modifications.ContainsKey("IsBlockingMovement"))
                {
                    Marshal.WriteByte(ptr + 0x18, Convert.ToByte(modifications["IsBlockingMovement"]));
                }
                if (modifications.ContainsKey("IsBlockingSight"))
                {
                    Marshal.WriteByte(ptr + 0x19, Convert.ToByte(modifications["IsBlockingSight"]));
                }
                if (modifications.ContainsKey("IsVaultedOver"))
                {
                    Marshal.WriteByte(ptr + 0x1A, Convert.ToByte(modifications["IsVaultedOver"]));
                }
                if (modifications.ContainsKey("IsDestroyedOnContactWithVehicles"))
                {
                    Marshal.WriteByte(ptr + 0x1B, Convert.ToByte(modifications["IsDestroyedOnContactWithVehicles"]));
                }
                if (modifications.ContainsKey("IsProvidingCover"))
                {
                    Marshal.WriteByte(ptr + 0x1C, Convert.ToByte(modifications["IsProvidingCover"]));
                }
                if (modifications.ContainsKey("CoverClass"))
                {
                    Marshal.WriteInt32(ptr + 0x20, Convert.ToInt32(modifications["CoverClass"]));
                }
                if (modifications.ContainsKey("OnDeathAnimationSpeed"))
                {
                    Marshal.WriteInt32(ptr + 0x30, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["OnDeathAnimationSpeed"])), 0));
                }
                if (modifications.ContainsKey("OnDeathYOffset"))
                {
                    Marshal.WriteInt32(ptr + 0x34, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["OnDeathYOffset"])), 0));
                }
                if (modifications.ContainsKey("EffectOnDeathOverrides"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["EffectOnDeathOverrides"]));
                }
                if (modifications.ContainsKey("SoundOnDeath"))
                {
                    Marshal.WriteInt32(ptr + 0x40, Convert.ToInt32(modifications["SoundOnDeath"]));
                }
            }

            else if (templateType.Name == "InsideCoverTemplate")
            {
                if (modifications.ContainsKey("AccuracyMult"))
                {
                    Marshal.WriteInt32(ptr + 0x58, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyMult"])), 0));
                }
                if (modifications.ContainsKey("DamageMult"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageMult"])), 0));
                }
                if (modifications.ContainsKey("SuppressionMult"))
                {
                    Marshal.WriteInt32(ptr + 0x60, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SuppressionMult"])), 0));
                }
                if (modifications.ContainsKey("Concealment"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["Concealment"]));
                }
            }

            else if (templateType.Name == "IntPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("DefaultValue"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["DefaultValue"]));
                }
                if (modifications.ContainsKey("MinValue"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["MinValue"]));
                }
                if (modifications.ContainsKey("MaxValue"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["MaxValue"]));
                }
            }

            else if (templateType.Name == "ItemFilterTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("OnlyNewItems"))
                {
                    Marshal.WriteByte(ptr + 0xA0, Convert.ToByte(modifications["OnlyNewItems"]));
                }
                if (modifications.ContainsKey("OnlyAvailableItems"))
                {
                    Marshal.WriteByte(ptr + 0xA1, Convert.ToByte(modifications["OnlyAvailableItems"]));
                }
            }

            else if (templateType.Name == "ItemListTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "KeyBindPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
            }

            else if (templateType.Name == "LightConditionTemplate")
            {
                if (modifications.ContainsKey("SnowAmount"))
                {
                    Marshal.WriteInt32(ptr + 0x38, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["SnowAmount"])), 0));
                }
                if (modifications.ContainsKey("HDRPProfile"))
                {
                    Marshal.WriteInt32(ptr + 0x50, Convert.ToInt32(modifications["HDRPProfile"]));
                }
                if (modifications.ContainsKey("TileHighlightColorOverrides"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["TileHighlightColorOverrides"]));
                }
                if (modifications.ContainsKey("SkillToApply"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["SkillToApply"]));
                }
            }

            else if (templateType.Name == "ListPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("IsActive"))
                {
                    Marshal.WriteByte(ptr + 0x88, Convert.ToByte(modifications["IsActive"]));
                }
                if (modifications.ContainsKey("UIStyle"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["UIStyle"]));
                }
                if (modifications.ContainsKey("DefaultValueIndex"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["DefaultValueIndex"]));
                }
                if (modifications.ContainsKey("TranslateValues"))
                {
                    Marshal.WriteByte(ptr + 0xA0, Convert.ToByte(modifications["TranslateValues"]));
                }
                if (modifications.ContainsKey("m_TranslatedValuesLanguage"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["m_TranslatedValuesLanguage"]));
                }
            }

            else if (templateType.Name == "MissionDifficultyTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("DifficultyType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["DifficultyType"]));
                }
                if (modifications.ContainsKey("Skulls"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Skulls"]));
                }
                if (modifications.ContainsKey("PromotionPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PromotionPointsMult"])), 0));
                }
                if (modifications.ContainsKey("PlayerSupplyMult"))
                {
                    Marshal.WriteInt32(ptr + 0x90, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["PlayerSupplyMult"])), 0));
                }
                if (modifications.ContainsKey("EnemyArmyPointsMult"))
                {
                    Marshal.WriteInt32(ptr + 0x94, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EnemyArmyPointsMult"])), 0));
                }
            }

            else if (templateType.Name == "MissionPOITemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
            }

            else if (templateType.Name == "MissionPreviewConfigTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("BorderWidth"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["BorderWidth"]));
                }
                if (modifications.ContainsKey("EntityEdgeAlpha"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EntityEdgeAlpha"])), 0));
                }
                if (modifications.ContainsKey("MinHeightValue"))
                {
                    Marshal.WriteInt32(ptr + 0x128, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinHeightValue"])), 0));
                }
                if (modifications.ContainsKey("MaxHeightValue"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxHeightValue"])), 0));
                }
                if (modifications.ContainsKey("HeightShades"))
                {
                    Marshal.WriteInt32(ptr + 0x150, Convert.ToInt32(modifications["HeightShades"]));
                }
                if (modifications.ContainsKey("InaccessibleMinHeightValue"))
                {
                    Marshal.WriteInt32(ptr + 0x154, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InaccessibleMinHeightValue"])), 0));
                }
                if (modifications.ContainsKey("InaccessibleMaxHeightValue"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["InaccessibleMaxHeightValue"])), 0));
                }
                if (modifications.ContainsKey("InaccessibleHeightShades"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, Convert.ToInt32(modifications["InaccessibleHeightShades"]));
                }
                if (modifications.ContainsKey("Civilians"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["Civilians"]));
                }
                if (modifications.ContainsKey("AlienWildlife"))
                {
                    Marshal.WriteInt32(ptr + 0x1D0, Convert.ToInt32(modifications["AlienWildlife"]));
                }
                if (modifications.ContainsKey("Allies"))
                {
                    Marshal.WriteInt32(ptr + 0x1D8, Convert.ToInt32(modifications["Allies"]));
                }
                if (modifications.ContainsKey("Enemies"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, Convert.ToInt32(modifications["Enemies"]));
                }
            }

            else if (templateType.Name == "MissionStrategicAssetTemplate")
            {
                if (modifications.ContainsKey("StrategicAsset"))
                {
                    Marshal.WriteInt32(ptr + 0x10, Convert.ToInt32(modifications["StrategicAsset"]));
                }
                if (modifications.ContainsKey("ReqMissionDifficulty"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["ReqMissionDifficulty"]));
                }
                if (modifications.ContainsKey("Weight"))
                {
                    Marshal.WriteInt32(ptr + 0x1C, Convert.ToInt32(modifications["Weight"]));
                }
            }

            else if (templateType.Name == "ModularVehicleTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
            }

            else if (templateType.Name == "ModularVehicleWeaponTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("SlotType"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotType"]));
                }
                if (modifications.ContainsKey("ItemType"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["ItemType"]));
                }
                if (modifications.ContainsKey("ExclusiveCategory"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["ExclusiveCategory"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("AnimType"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["AnimType"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("AnimGrip"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["AnimGrip"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x148, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("AccuracyBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyBonus"])), 0));
                }
                if (modifications.ContainsKey("AccuracyDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x150, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyDropoff"])), 0));
                }
                if (modifications.ContainsKey("Damage"))
                {
                    Marshal.WriteInt32(ptr + 0x154, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["Damage"])), 0));
                }
                if (modifications.ContainsKey("DamageDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamagePctCurrentHitpoints"))
                {
                    Marshal.WriteInt32(ptr + 0x15C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctCurrentHitpoints"])), 0));
                }
                if (modifications.ContainsKey("DamagePctCurrentHitpointsMin"))
                {
                    Marshal.WriteInt32(ptr + 0x160, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctCurrentHitpointsMin"])), 0));
                }
                if (modifications.ContainsKey("DamagePctMaxHitpoints"))
                {
                    Marshal.WriteInt32(ptr + 0x164, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctMaxHitpoints"])), 0));
                }
                if (modifications.ContainsKey("DamagePctMaxHitpointsMin"))
                {
                    Marshal.WriteInt32(ptr + 0x168, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctMaxHitpointsMin"])), 0));
                }
                if (modifications.ContainsKey("ArmorPenetration"))
                {
                    Marshal.WriteInt32(ptr + 0x16C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ArmorPenetration"])), 0));
                }
                if (modifications.ContainsKey("ArmorPenetrationDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x170, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ArmorPenetrationDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurability"))
                {
                    Marshal.WriteInt32(ptr + 0x174, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurability"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityMult"))
                {
                    Marshal.WriteInt32(ptr + 0x178, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityMult"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityDropoffMult"))
                {
                    Marshal.WriteInt32(ptr + 0x180, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityDropoffMult"])), 0));
                }
                if (modifications.ContainsKey("Suppression"))
                {
                    Marshal.WriteInt32(ptr + 0x184, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["Suppression"])), 0));
                }
                if (modifications.ContainsKey("SupportsIntegrationOfLightWeapons"))
                {
                    Marshal.WriteByte(ptr + 0x188, Convert.ToByte(modifications["SupportsIntegrationOfLightWeapons"]));
                }
                if (modifications.ContainsKey("DisableOtherWeaponSlots"))
                {
                    Marshal.WriteByte(ptr + 0x189, Convert.ToByte(modifications["DisableOtherWeaponSlots"]));
                }
            }

            else if (templateType.Name == "OffmapAbilityTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("SkillTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["SkillTemplate"]));
                }
                if (modifications.ContainsKey("DelayInRounds"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["DelayInRounds"]));
                }
                if (modifications.ContainsKey("SoundOnUse"))
                {
                    Marshal.WriteInt32(ptr + 0x84, Convert.ToInt32(modifications["SoundOnUse"]));
                }
            }

            else if (templateType.Name == "OperationDurationTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("OperationRewardRarityIncreasePctPerStar"))
                {
                    Marshal.WriteInt32(ptr + 0x98, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["OperationRewardRarityIncreasePctPerStar"])), 0));
                }
            }

            else if (templateType.Name == "OperationIntrosTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "OperationTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Repeatable"))
                {
                    Marshal.WriteByte(ptr + 0xA0, Convert.ToByte(modifications["Repeatable"]));
                }
                if (modifications.ContainsKey("CanTimeout"))
                {
                    Marshal.WriteByte(ptr + 0xA1, Convert.ToByte(modifications["CanTimeout"]));
                }
                if (modifications.ContainsKey("Condition"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["Condition"]));
                }
                if (modifications.ContainsKey("SkipOperationScreens"))
                {
                    Marshal.WriteByte(ptr + 0xB0, Convert.ToByte(modifications["SkipOperationScreens"]));
                }
                if (modifications.ContainsKey("ShowStartConfirmationDialog"))
                {
                    Marshal.WriteByte(ptr + 0xB1, Convert.ToByte(modifications["ShowStartConfirmationDialog"]));
                }
                if (modifications.ContainsKey("SystemMapIconIdx"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["SystemMapIconIdx"]));
                }
                if (modifications.ContainsKey("CanHaveFriendlyForce"))
                {
                    Marshal.WriteByte(ptr + 0xC4, Convert.ToByte(modifications["CanHaveFriendlyForce"]));
                }
                if (modifications.ContainsKey("OverrideFaction"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["OverrideFaction"]));
                }
                if (modifications.ContainsKey("VictoryEvent"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["VictoryEvent"]));
                }
                if (modifications.ContainsKey("FailureEvent"))
                {
                    Marshal.WriteInt32(ptr + 0x100, Convert.ToInt32(modifications["FailureEvent"]));
                }
                if (modifications.ContainsKey("AbortEvent"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["AbortEvent"]));
                }
            }

            else if (templateType.Name == "PerkTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("Order"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["Order"]));
                }
                if (modifications.ContainsKey("ActionPointCost"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, Convert.ToInt32(modifications["ActionPointCost"]));
                }
                if (modifications.ContainsKey("IsLimitedUses"))
                {
                    Marshal.WriteByte(ptr + 0xB8, Convert.ToByte(modifications["IsLimitedUses"]));
                }
                if (modifications.ContainsKey("Uses"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["Uses"]));
                }
                if (modifications.ContainsKey("UsesDisplayTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["UsesDisplayTemplate"]));
                }
                if (modifications.ContainsKey("IsActive"))
                {
                    Marshal.WriteByte(ptr + 0xC8, Convert.ToByte(modifications["IsActive"]));
                }
                if (modifications.ContainsKey("HideApCosts"))
                {
                    Marshal.WriteByte(ptr + 0xC9, Convert.ToByte(modifications["HideApCosts"]));
                }
                if (modifications.ContainsKey("KeyBind"))
                {
                    Marshal.WriteInt32(ptr + 0xCC, Convert.ToInt32(modifications["KeyBind"]));
                }
                if (modifications.ContainsKey("ExecutingElement"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["ExecutingElement"]));
                }
                if (modifications.ContainsKey("AnimationType"))
                {
                    Marshal.WriteInt32(ptr + 0xD4, Convert.ToInt32(modifications["AnimationType"]));
                }
                if (modifications.ContainsKey("AimingType"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["AimingType"]));
                }
                if (modifications.ContainsKey("IsOverrideAimSlot"))
                {
                    Marshal.WriteByte(ptr + 0xDC, Convert.ToByte(modifications["IsOverrideAimSlot"]));
                }
                if (modifications.ContainsKey("OverrideAimSlot"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["OverrideAimSlot"]));
                }
                if (modifications.ContainsKey("IsTargeted"))
                {
                    Marshal.WriteByte(ptr + 0xE4, Convert.ToByte(modifications["IsTargeted"]));
                }
                if (modifications.ContainsKey("TargetingCursor"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["TargetingCursor"]));
                }
                if (modifications.ContainsKey("TargetsAllowed"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["TargetsAllowed"]));
                }
                if (modifications.ContainsKey("KeepSelectedIfStillUsable"))
                {
                    Marshal.WriteByte(ptr + 0xF0, Convert.ToByte(modifications["KeepSelectedIfStillUsable"]));
                }
                if (modifications.ContainsKey("IsLineOfFireNeeded"))
                {
                    Marshal.WriteByte(ptr + 0xF1, Convert.ToByte(modifications["IsLineOfFireNeeded"]));
                }
                if (modifications.ContainsKey("IsAttack"))
                {
                    Marshal.WriteByte(ptr + 0xF2, Convert.ToByte(modifications["IsAttack"]));
                }
                if (modifications.ContainsKey("IsAlwaysHitting"))
                {
                    Marshal.WriteByte(ptr + 0xF3, Convert.ToByte(modifications["IsAlwaysHitting"]));
                }
                if (modifications.ContainsKey("CanHitAnotherTile"))
                {
                    Marshal.WriteByte(ptr + 0xF4, Convert.ToByte(modifications["CanHitAnotherTile"]));
                }
                if (modifications.ContainsKey("IsUsedInBackground"))
                {
                    Marshal.WriteByte(ptr + 0xF5, Convert.ToByte(modifications["IsUsedInBackground"]));
                }
                if (modifications.ContainsKey("OverrideBackgroundUseWithSkill"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["OverrideBackgroundUseWithSkill"]));
                }
                if (modifications.ContainsKey("IsIgnoringCover"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsIgnoringCover"]));
                }
                if (modifications.ContainsKey("IgnoreCoverInside"))
                {
                    Marshal.WriteInt32(ptr + 0x104, Convert.ToInt32(modifications["IgnoreCoverInside"]));
                }
                if (modifications.ContainsKey("IgnoreCoverInsideSpecific"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["IgnoreCoverInsideSpecific"]));
                }
                if (modifications.ContainsKey("IsSilent"))
                {
                    Marshal.WriteByte(ptr + 0x110, Convert.ToByte(modifications["IsSilent"]));
                }
                if (modifications.ContainsKey("IgnoreMalfunctionChance"))
                {
                    Marshal.WriteByte(ptr + 0x111, Convert.ToByte(modifications["IgnoreMalfunctionChance"]));
                }
                if (modifications.ContainsKey("IsDeploymentRequired"))
                {
                    Marshal.WriteByte(ptr + 0x112, Convert.ToByte(modifications["IsDeploymentRequired"]));
                }
                if (modifications.ContainsKey("IsWeaponSetupRequired"))
                {
                    Marshal.WriteByte(ptr + 0x113, Convert.ToByte(modifications["IsWeaponSetupRequired"]));
                }
                if (modifications.ContainsKey("IsUsableWhileContained"))
                {
                    Marshal.WriteByte(ptr + 0x114, Convert.ToByte(modifications["IsUsableWhileContained"]));
                }
                if (modifications.ContainsKey("IsUsableWhilePinnedDown"))
                {
                    Marshal.WriteByte(ptr + 0x115, Convert.ToByte(modifications["IsUsableWhilePinnedDown"]));
                }
                if (modifications.ContainsKey("IsStacking"))
                {
                    Marshal.WriteByte(ptr + 0x116, Convert.ToByte(modifications["IsStacking"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x117, Convert.ToByte(modifications["IsRemovedAfterCombat"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterOperation"))
                {
                    Marshal.WriteByte(ptr + 0x118, Convert.ToByte(modifications["IsRemovedAfterOperation"]));
                }
                if (modifications.ContainsKey("IsHidden"))
                {
                    Marshal.WriteByte(ptr + 0x119, Convert.ToByte(modifications["IsHidden"]));
                }
                if (modifications.ContainsKey("Shape"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, Convert.ToInt32(modifications["Shape"]));
                }
                if (modifications.ContainsKey("ConeAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ConeAngle"]));
                }
                if (modifications.ContainsKey("IsOverridingRanges"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["IsOverridingRanges"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("MinElementDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x134, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinElementDelay"])), 0));
                }
                if (modifications.ContainsKey("MaxElementDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x138, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxElementDelay"])), 0));
                }
                if (modifications.ContainsKey("ElementDelayBetween"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ElementDelayBetween"])), 0));
                }
                if (modifications.ContainsKey("MinDelayBeforeSkillUse"))
                {
                    Marshal.WriteInt32(ptr + 0x140, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinDelayBeforeSkillUse"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterAnimationTrigger"))
                {
                    Marshal.WriteInt32(ptr + 0x144, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterAnimationTrigger"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterLastRepetition"))
                {
                    Marshal.WriteInt32(ptr + 0x148, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterLastRepetition"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterSkillUse"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterSkillUse"])), 0));
                }
                if (modifications.ContainsKey("IgnoreCameraFocusDelay"))
                {
                    Marshal.WriteByte(ptr + 0x150, Convert.ToByte(modifications["IgnoreCameraFocusDelay"]));
                }
                if (modifications.ContainsKey("Repetitions"))
                {
                    Marshal.WriteInt32(ptr + 0x154, Convert.ToInt32(modifications["Repetitions"]));
                }
                if (modifications.ContainsKey("RepetitionDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RepetitionDelay"])), 0));
                }
                if (modifications.ContainsKey("SkipDelayForLastRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x15C, Convert.ToByte(modifications["SkipDelayForLastRepetition"]));
                }
                if (modifications.ContainsKey("IsPlayingAnimationForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x15D, Convert.ToByte(modifications["IsPlayingAnimationForEachRepetition"]));
                }
                if (modifications.ContainsKey("UseCustomAoEShape"))
                {
                    Marshal.WriteByte(ptr + 0x168, Convert.ToByte(modifications["UseCustomAoEShape"]));
                }
                if (modifications.ContainsKey("CustomAoEShape"))
                {
                    Marshal.WriteInt32(ptr + 0x170, Convert.ToInt32(modifications["CustomAoEShape"]));
                }
                if (modifications.ContainsKey("AoEType"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["AoEType"]));
                }
                if (modifications.ContainsKey("AoEFilter"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["AoEFilter"]));
                }
                if (modifications.ContainsKey("TargetFaction"))
                {
                    Marshal.WriteInt32(ptr + 0x188, Convert.ToInt32(modifications["TargetFaction"]));
                }
                if (modifications.ContainsKey("AoEChanceToHitCenter"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["AoEChanceToHitCenter"]));
                }
                if (modifications.ContainsKey("SelectableTiles"))
                {
                    Marshal.WriteInt32(ptr + 0x190, Convert.ToInt32(modifications["SelectableTiles"]));
                }
                if (modifications.ContainsKey("ScatterMode"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["ScatterMode"]));
                }
                if (modifications.ContainsKey("Scatter"))
                {
                    Marshal.WriteInt32(ptr + 0x198, Convert.ToInt32(modifications["Scatter"]));
                }
                if (modifications.ContainsKey("ScatterChance"))
                {
                    Marshal.WriteInt32(ptr + 0x19C, Convert.ToInt32(modifications["ScatterChance"]));
                }
                if (modifications.ContainsKey("ScatterHitEachTileOnlyOnce"))
                {
                    Marshal.WriteByte(ptr + 0x1A0, Convert.ToByte(modifications["ScatterHitEachTileOnlyOnce"]));
                }
                if (modifications.ContainsKey("ScatterHitOnlyValidTiles"))
                {
                    Marshal.WriteByte(ptr + 0x1A1, Convert.ToByte(modifications["ScatterHitOnlyValidTiles"]));
                }
                if (modifications.ContainsKey("MuzzleType"))
                {
                    Marshal.WriteInt32(ptr + 0x1A4, Convert.ToInt32(modifications["MuzzleType"]));
                }
                if (modifications.ContainsKey("MuzzleSelection"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, Convert.ToInt32(modifications["MuzzleSelection"]));
                }
                if (modifications.ContainsKey("MuzzleEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["MuzzleEffectOverrides2"]));
                }
                if (modifications.ContainsKey("IsSpawningMuzzleForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x1C0, Convert.ToByte(modifications["IsSpawningMuzzleForEachRepetition"]));
                }
                if (modifications.ContainsKey("IsAttachingMuzzleToTransform"))
                {
                    Marshal.WriteByte(ptr + 0x1C1, Convert.ToByte(modifications["IsAttachingMuzzleToTransform"]));
                }
                if (modifications.ContainsKey("CameraEffectOnFire"))
                {
                    Marshal.WriteInt32(ptr + 0x1C4, Convert.ToInt32(modifications["CameraEffectOnFire"]));
                }
                if (modifications.ContainsKey("ProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1D8, Convert.ToInt32(modifications["ProjectileData"]));
                }
                if (modifications.ContainsKey("SecondaryProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, Convert.ToInt32(modifications["SecondaryProjectileData"]));
                }
                if (modifications.ContainsKey("DefaultImpactDecals"))
                {
                    Marshal.WriteInt32(ptr + 0x1F0, Convert.ToInt32(modifications["DefaultImpactDecals"]));
                }
                if (modifications.ContainsKey("DefaultSoundOnRicochet"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["DefaultSoundOnRicochet"]));
                }
                if (modifications.ContainsKey("DefaultSoundOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["DefaultSoundOnImpact"]));
                }
                if (modifications.ContainsKey("ImpactEffectDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x210, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ImpactEffectDelay"])), 0));
                }
                if (modifications.ContainsKey("ImpactDecalDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x214, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ImpactDecalDelay"])), 0));
                }
                if (modifications.ContainsKey("EffectDelayAfterImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x218, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EffectDelayAfterImpact"])), 0));
                }
                if (modifications.ContainsKey("IsImpactShownOnHit"))
                {
                    Marshal.WriteByte(ptr + 0x21C, Convert.ToByte(modifications["IsImpactShownOnHit"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnTile"))
                {
                    Marshal.WriteByte(ptr + 0x21D, Convert.ToByte(modifications["IsImpactCenteredOnTile"]));
                }
                if (modifications.ContainsKey("IsImpactOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x21E, Convert.ToByte(modifications["IsImpactOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsDecalOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x21F, Convert.ToByte(modifications["IsDecalOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnExecutingElement"))
                {
                    Marshal.WriteByte(ptr + 0x220, Convert.ToByte(modifications["IsImpactCenteredOnExecutingElement"]));
                }
                if (modifications.ContainsKey("IsImpactAlignedToInfantry"))
                {
                    Marshal.WriteByte(ptr + 0x221, Convert.ToByte(modifications["IsImpactAlignedToInfantry"]));
                }
                if (modifications.ContainsKey("CameraEffectOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x224, Convert.ToInt32(modifications["CameraEffectOnImpact"]));
                }
                if (modifications.ContainsKey("CameraEffectOnPlayerHit"))
                {
                    Marshal.WriteInt32(ptr + 0x228, Convert.ToInt32(modifications["CameraEffectOnPlayerHit"]));
                }
                if (modifications.ContainsKey("IsTriggeringHeavyDamagedReceivedEffect"))
                {
                    Marshal.WriteByte(ptr + 0x22C, Convert.ToByte(modifications["IsTriggeringHeavyDamagedReceivedEffect"]));
                }
                if (modifications.ContainsKey("RagdollHitArea"))
                {
                    Marshal.WriteInt32(ptr + 0x240, Convert.ToInt32(modifications["RagdollHitArea"]));
                }
                if (modifications.ContainsKey("MalfunctionChance"))
                {
                    Marshal.WriteInt32(ptr + 0x244, Convert.ToInt32(modifications["MalfunctionChance"]));
                }
                if (modifications.ContainsKey("SoundOnMalfunction"))
                {
                    Marshal.WriteInt32(ptr + 0x250, Convert.ToInt32(modifications["SoundOnMalfunction"]));
                }
                if (modifications.ContainsKey("IsAudibleWhenNotVisible"))
                {
                    Marshal.WriteByte(ptr + 0x258, Convert.ToByte(modifications["IsAudibleWhenNotVisible"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x278, Convert.ToByte(modifications["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x2B0, Convert.ToByte(modifications["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("AIConfig"))
                {
                    Marshal.WriteInt32(ptr + 0x2C8, Convert.ToInt32(modifications["AIConfig"]));
                }
            }

            else if (templateType.Name == "PlanetTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("PlanetType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["PlanetType"]));
                }
                if (modifications.ContainsKey("ImageOverlayMargin"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["ImageOverlayMargin"]));
                }
                if (modifications.ContainsKey("MaxMenacePresence"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["MaxMenacePresence"]));
                }
                if (modifications.ContainsKey("MenaceDetectedEvent"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["MenaceDetectedEvent"]));
                }
                if (modifications.ContainsKey("LocalFaction"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["LocalFaction"]));
                }
            }

            else if (templateType.Name == "PropertyDisplayConfigTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("DefaultValue"))
                {
                    Marshal.WriteInt32(ptr + 0x90, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DefaultValue"])), 0));
                }
                if (modifications.ContainsKey("MinValue"))
                {
                    Marshal.WriteInt32(ptr + 0x94, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinValue"])), 0));
                }
                if (modifications.ContainsKey("MaxValue"))
                {
                    Marshal.WriteInt32(ptr + 0x98, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxValue"])), 0));
                }
                if (modifications.ContainsKey("DecimalPlaces"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, Convert.ToInt32(modifications["DecimalPlaces"]));
                }
                if (modifications.ContainsKey("ProgressBarSections"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["ProgressBarSections"]));
                }
                if (modifications.ContainsKey("IsBiggerBetter"))
                {
                    Marshal.WriteByte(ptr + 0xA4, Convert.ToByte(modifications["IsBiggerBetter"]));
                }
            }

            else if (templateType.Name == "RagdollTemplate")
            {
                if (modifications.ContainsKey("BloodPoolPosition"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["BloodPoolPosition"]));
                }
                if (modifications.ContainsKey("UseCustomGravity"))
                {
                    Marshal.WriteByte(ptr + 0x5C, Convert.ToByte(modifications["UseCustomGravity"]));
                }
                if (modifications.ContainsKey("CustomGravity"))
                {
                    Marshal.WriteInt32(ptr + 0x60, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["CustomGravity"])), 0));
                }
                if (modifications.ContainsKey("CustomGravityForceMode"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["CustomGravityForceMode"]));
                }
                if (modifications.ContainsKey("DismemberedPartHitForceMult"))
                {
                    Marshal.WriteInt32(ptr + 0x74, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DismemberedPartHitForceMult"])), 0));
                }
                if (modifications.ContainsKey("AdditionalDismemberedPieceHitForceMult"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AdditionalDismemberedPieceHitForceMult"])), 0));
                }
                if (modifications.ContainsKey("RootHasGeometry"))
                {
                    Marshal.WriteByte(ptr + 0xA8, Convert.ToByte(modifications["RootHasGeometry"]));
                }
                if (modifications.ContainsKey("CenterPartIndex"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["CenterPartIndex"]));
                }
                if (modifications.ContainsKey("GeometryRootIndex"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["GeometryRootIndex"]));
                }
            }

            else if (templateType.Name == "ResolutionPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
            }

            else if (templateType.Name == "RewardTableTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("RarityMultiplier"))
                {
                    Marshal.WriteInt32(ptr + 0x78, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RarityMultiplier"])), 0));
                }
            }

            else if (templateType.Name == "ShipUpgradeSlotTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("UpgradeType"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["UpgradeType"]));
                }
            }

            else if (templateType.Name == "ShipUpgradeTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("UpgradeType"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["UpgradeType"]));
                }
                if (modifications.ContainsKey("OciPointsCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["OciPointsCosts"]));
                }
                if (modifications.ContainsKey("UnlockType"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, Convert.ToInt32(modifications["UnlockType"]));
                }
                if (modifications.ContainsKey("UnlockedByFaction"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["UnlockedByFaction"]));
                }
                if (modifications.ContainsKey("UnlockSelectWeight"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["UnlockSelectWeight"]));
                }
            }

            else if (templateType.Name == "SkillTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("Order"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["Order"]));
                }
                if (modifications.ContainsKey("ActionPointCost"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, Convert.ToInt32(modifications["ActionPointCost"]));
                }
                if (modifications.ContainsKey("IsLimitedUses"))
                {
                    Marshal.WriteByte(ptr + 0xB8, Convert.ToByte(modifications["IsLimitedUses"]));
                }
                if (modifications.ContainsKey("Uses"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["Uses"]));
                }
                if (modifications.ContainsKey("UsesDisplayTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["UsesDisplayTemplate"]));
                }
                if (modifications.ContainsKey("IsActive"))
                {
                    Marshal.WriteByte(ptr + 0xC8, Convert.ToByte(modifications["IsActive"]));
                }
                if (modifications.ContainsKey("HideApCosts"))
                {
                    Marshal.WriteByte(ptr + 0xC9, Convert.ToByte(modifications["HideApCosts"]));
                }
                if (modifications.ContainsKey("KeyBind"))
                {
                    Marshal.WriteInt32(ptr + 0xCC, Convert.ToInt32(modifications["KeyBind"]));
                }
                if (modifications.ContainsKey("ExecutingElement"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["ExecutingElement"]));
                }
                if (modifications.ContainsKey("AnimationType"))
                {
                    Marshal.WriteInt32(ptr + 0xD4, Convert.ToInt32(modifications["AnimationType"]));
                }
                if (modifications.ContainsKey("AimingType"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["AimingType"]));
                }
                if (modifications.ContainsKey("IsOverrideAimSlot"))
                {
                    Marshal.WriteByte(ptr + 0xDC, Convert.ToByte(modifications["IsOverrideAimSlot"]));
                }
                if (modifications.ContainsKey("OverrideAimSlot"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["OverrideAimSlot"]));
                }
                if (modifications.ContainsKey("IsTargeted"))
                {
                    Marshal.WriteByte(ptr + 0xE4, Convert.ToByte(modifications["IsTargeted"]));
                }
                if (modifications.ContainsKey("TargetingCursor"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["TargetingCursor"]));
                }
                if (modifications.ContainsKey("TargetsAllowed"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["TargetsAllowed"]));
                }
                if (modifications.ContainsKey("KeepSelectedIfStillUsable"))
                {
                    Marshal.WriteByte(ptr + 0xF0, Convert.ToByte(modifications["KeepSelectedIfStillUsable"]));
                }
                if (modifications.ContainsKey("IsLineOfFireNeeded"))
                {
                    Marshal.WriteByte(ptr + 0xF1, Convert.ToByte(modifications["IsLineOfFireNeeded"]));
                }
                if (modifications.ContainsKey("IsAttack"))
                {
                    Marshal.WriteByte(ptr + 0xF2, Convert.ToByte(modifications["IsAttack"]));
                }
                if (modifications.ContainsKey("IsAlwaysHitting"))
                {
                    Marshal.WriteByte(ptr + 0xF3, Convert.ToByte(modifications["IsAlwaysHitting"]));
                }
                if (modifications.ContainsKey("CanHitAnotherTile"))
                {
                    Marshal.WriteByte(ptr + 0xF4, Convert.ToByte(modifications["CanHitAnotherTile"]));
                }
                if (modifications.ContainsKey("IsUsedInBackground"))
                {
                    Marshal.WriteByte(ptr + 0xF5, Convert.ToByte(modifications["IsUsedInBackground"]));
                }
                if (modifications.ContainsKey("OverrideBackgroundUseWithSkill"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["OverrideBackgroundUseWithSkill"]));
                }
                if (modifications.ContainsKey("IsIgnoringCover"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsIgnoringCover"]));
                }
                if (modifications.ContainsKey("IgnoreCoverInside"))
                {
                    Marshal.WriteInt32(ptr + 0x104, Convert.ToInt32(modifications["IgnoreCoverInside"]));
                }
                if (modifications.ContainsKey("IgnoreCoverInsideSpecific"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["IgnoreCoverInsideSpecific"]));
                }
                if (modifications.ContainsKey("IsSilent"))
                {
                    Marshal.WriteByte(ptr + 0x110, Convert.ToByte(modifications["IsSilent"]));
                }
                if (modifications.ContainsKey("IgnoreMalfunctionChance"))
                {
                    Marshal.WriteByte(ptr + 0x111, Convert.ToByte(modifications["IgnoreMalfunctionChance"]));
                }
                if (modifications.ContainsKey("IsDeploymentRequired"))
                {
                    Marshal.WriteByte(ptr + 0x112, Convert.ToByte(modifications["IsDeploymentRequired"]));
                }
                if (modifications.ContainsKey("IsWeaponSetupRequired"))
                {
                    Marshal.WriteByte(ptr + 0x113, Convert.ToByte(modifications["IsWeaponSetupRequired"]));
                }
                if (modifications.ContainsKey("IsUsableWhileContained"))
                {
                    Marshal.WriteByte(ptr + 0x114, Convert.ToByte(modifications["IsUsableWhileContained"]));
                }
                if (modifications.ContainsKey("IsUsableWhilePinnedDown"))
                {
                    Marshal.WriteByte(ptr + 0x115, Convert.ToByte(modifications["IsUsableWhilePinnedDown"]));
                }
                if (modifications.ContainsKey("IsStacking"))
                {
                    Marshal.WriteByte(ptr + 0x116, Convert.ToByte(modifications["IsStacking"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x117, Convert.ToByte(modifications["IsRemovedAfterCombat"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterOperation"))
                {
                    Marshal.WriteByte(ptr + 0x118, Convert.ToByte(modifications["IsRemovedAfterOperation"]));
                }
                if (modifications.ContainsKey("IsHidden"))
                {
                    Marshal.WriteByte(ptr + 0x119, Convert.ToByte(modifications["IsHidden"]));
                }
                if (modifications.ContainsKey("Shape"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, Convert.ToInt32(modifications["Shape"]));
                }
                if (modifications.ContainsKey("ConeAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ConeAngle"]));
                }
                if (modifications.ContainsKey("IsOverridingRanges"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["IsOverridingRanges"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("MinElementDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x134, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinElementDelay"])), 0));
                }
                if (modifications.ContainsKey("MaxElementDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x138, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MaxElementDelay"])), 0));
                }
                if (modifications.ContainsKey("ElementDelayBetween"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ElementDelayBetween"])), 0));
                }
                if (modifications.ContainsKey("MinDelayBeforeSkillUse"))
                {
                    Marshal.WriteInt32(ptr + 0x140, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["MinDelayBeforeSkillUse"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterAnimationTrigger"))
                {
                    Marshal.WriteInt32(ptr + 0x144, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterAnimationTrigger"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterLastRepetition"))
                {
                    Marshal.WriteInt32(ptr + 0x148, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterLastRepetition"])), 0));
                }
                if (modifications.ContainsKey("DelayAfterSkillUse"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DelayAfterSkillUse"])), 0));
                }
                if (modifications.ContainsKey("IgnoreCameraFocusDelay"))
                {
                    Marshal.WriteByte(ptr + 0x150, Convert.ToByte(modifications["IgnoreCameraFocusDelay"]));
                }
                if (modifications.ContainsKey("Repetitions"))
                {
                    Marshal.WriteInt32(ptr + 0x154, Convert.ToInt32(modifications["Repetitions"]));
                }
                if (modifications.ContainsKey("RepetitionDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["RepetitionDelay"])), 0));
                }
                if (modifications.ContainsKey("SkipDelayForLastRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x15C, Convert.ToByte(modifications["SkipDelayForLastRepetition"]));
                }
                if (modifications.ContainsKey("IsPlayingAnimationForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x15D, Convert.ToByte(modifications["IsPlayingAnimationForEachRepetition"]));
                }
                if (modifications.ContainsKey("UseCustomAoEShape"))
                {
                    Marshal.WriteByte(ptr + 0x168, Convert.ToByte(modifications["UseCustomAoEShape"]));
                }
                if (modifications.ContainsKey("CustomAoEShape"))
                {
                    Marshal.WriteInt32(ptr + 0x170, Convert.ToInt32(modifications["CustomAoEShape"]));
                }
                if (modifications.ContainsKey("AoEType"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["AoEType"]));
                }
                if (modifications.ContainsKey("AoEFilter"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["AoEFilter"]));
                }
                if (modifications.ContainsKey("TargetFaction"))
                {
                    Marshal.WriteInt32(ptr + 0x188, Convert.ToInt32(modifications["TargetFaction"]));
                }
                if (modifications.ContainsKey("AoEChanceToHitCenter"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["AoEChanceToHitCenter"]));
                }
                if (modifications.ContainsKey("SelectableTiles"))
                {
                    Marshal.WriteInt32(ptr + 0x190, Convert.ToInt32(modifications["SelectableTiles"]));
                }
                if (modifications.ContainsKey("ScatterMode"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["ScatterMode"]));
                }
                if (modifications.ContainsKey("Scatter"))
                {
                    Marshal.WriteInt32(ptr + 0x198, Convert.ToInt32(modifications["Scatter"]));
                }
                if (modifications.ContainsKey("ScatterChance"))
                {
                    Marshal.WriteInt32(ptr + 0x19C, Convert.ToInt32(modifications["ScatterChance"]));
                }
                if (modifications.ContainsKey("ScatterHitEachTileOnlyOnce"))
                {
                    Marshal.WriteByte(ptr + 0x1A0, Convert.ToByte(modifications["ScatterHitEachTileOnlyOnce"]));
                }
                if (modifications.ContainsKey("ScatterHitOnlyValidTiles"))
                {
                    Marshal.WriteByte(ptr + 0x1A1, Convert.ToByte(modifications["ScatterHitOnlyValidTiles"]));
                }
                if (modifications.ContainsKey("MuzzleType"))
                {
                    Marshal.WriteInt32(ptr + 0x1A4, Convert.ToInt32(modifications["MuzzleType"]));
                }
                if (modifications.ContainsKey("MuzzleSelection"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, Convert.ToInt32(modifications["MuzzleSelection"]));
                }
                if (modifications.ContainsKey("MuzzleEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["MuzzleEffectOverrides2"]));
                }
                if (modifications.ContainsKey("IsSpawningMuzzleForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x1C0, Convert.ToByte(modifications["IsSpawningMuzzleForEachRepetition"]));
                }
                if (modifications.ContainsKey("IsAttachingMuzzleToTransform"))
                {
                    Marshal.WriteByte(ptr + 0x1C1, Convert.ToByte(modifications["IsAttachingMuzzleToTransform"]));
                }
                if (modifications.ContainsKey("CameraEffectOnFire"))
                {
                    Marshal.WriteInt32(ptr + 0x1C4, Convert.ToInt32(modifications["CameraEffectOnFire"]));
                }
                if (modifications.ContainsKey("ProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1D8, Convert.ToInt32(modifications["ProjectileData"]));
                }
                if (modifications.ContainsKey("SecondaryProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, Convert.ToInt32(modifications["SecondaryProjectileData"]));
                }
                if (modifications.ContainsKey("DefaultImpactDecals"))
                {
                    Marshal.WriteInt32(ptr + 0x1F0, Convert.ToInt32(modifications["DefaultImpactDecals"]));
                }
                if (modifications.ContainsKey("DefaultSoundOnRicochet"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["DefaultSoundOnRicochet"]));
                }
                if (modifications.ContainsKey("DefaultSoundOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["DefaultSoundOnImpact"]));
                }
                if (modifications.ContainsKey("ImpactEffectDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x210, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ImpactEffectDelay"])), 0));
                }
                if (modifications.ContainsKey("ImpactDecalDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x214, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ImpactDecalDelay"])), 0));
                }
                if (modifications.ContainsKey("EffectDelayAfterImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x218, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["EffectDelayAfterImpact"])), 0));
                }
                if (modifications.ContainsKey("IsImpactShownOnHit"))
                {
                    Marshal.WriteByte(ptr + 0x21C, Convert.ToByte(modifications["IsImpactShownOnHit"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnTile"))
                {
                    Marshal.WriteByte(ptr + 0x21D, Convert.ToByte(modifications["IsImpactCenteredOnTile"]));
                }
                if (modifications.ContainsKey("IsImpactOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x21E, Convert.ToByte(modifications["IsImpactOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsDecalOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x21F, Convert.ToByte(modifications["IsDecalOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnExecutingElement"))
                {
                    Marshal.WriteByte(ptr + 0x220, Convert.ToByte(modifications["IsImpactCenteredOnExecutingElement"]));
                }
                if (modifications.ContainsKey("IsImpactAlignedToInfantry"))
                {
                    Marshal.WriteByte(ptr + 0x221, Convert.ToByte(modifications["IsImpactAlignedToInfantry"]));
                }
                if (modifications.ContainsKey("CameraEffectOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x224, Convert.ToInt32(modifications["CameraEffectOnImpact"]));
                }
                if (modifications.ContainsKey("CameraEffectOnPlayerHit"))
                {
                    Marshal.WriteInt32(ptr + 0x228, Convert.ToInt32(modifications["CameraEffectOnPlayerHit"]));
                }
                if (modifications.ContainsKey("IsTriggeringHeavyDamagedReceivedEffect"))
                {
                    Marshal.WriteByte(ptr + 0x22C, Convert.ToByte(modifications["IsTriggeringHeavyDamagedReceivedEffect"]));
                }
                if (modifications.ContainsKey("RagdollHitArea"))
                {
                    Marshal.WriteInt32(ptr + 0x240, Convert.ToInt32(modifications["RagdollHitArea"]));
                }
                if (modifications.ContainsKey("MalfunctionChance"))
                {
                    Marshal.WriteInt32(ptr + 0x244, Convert.ToInt32(modifications["MalfunctionChance"]));
                }
                if (modifications.ContainsKey("SoundOnMalfunction"))
                {
                    Marshal.WriteInt32(ptr + 0x250, Convert.ToInt32(modifications["SoundOnMalfunction"]));
                }
                if (modifications.ContainsKey("IsAudibleWhenNotVisible"))
                {
                    Marshal.WriteByte(ptr + 0x258, Convert.ToByte(modifications["IsAudibleWhenNotVisible"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x278, Convert.ToByte(modifications["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x2B0, Convert.ToByte(modifications["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("AIConfig"))
                {
                    Marshal.WriteInt32(ptr + 0x2C8, Convert.ToInt32(modifications["AIConfig"]));
                }
            }

            else if (templateType.Name == "SkillUsesDisplayTemplate")
            {
                if (modifications.ContainsKey("ShowInItemTooltips"))
                {
                    Marshal.WriteByte(ptr + 0x58, Convert.ToByte(modifications["ShowInItemTooltips"]));
                }
                if (modifications.ContainsKey("ShowOnSkillBarWeapon"))
                {
                    Marshal.WriteByte(ptr + 0x59, Convert.ToByte(modifications["ShowOnSkillBarWeapon"]));
                }
                if (modifications.ContainsKey("NotchLayout"))
                {
                    Marshal.WriteInt32(ptr + 0x5C, Convert.ToInt32(modifications["NotchLayout"]));
                }
                if (modifications.ContainsKey("NotchHeight"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["NotchHeight"]));
                }
                if (modifications.ContainsKey("NotchWidth"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["NotchWidth"]));
                }
                if (modifications.ContainsKey("NotchGapWidth"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["NotchGapWidth"]));
                }
                if (modifications.ContainsKey("NotchGroupGapWidth"))
                {
                    Marshal.WriteInt32(ptr + 0x6C, Convert.ToInt32(modifications["NotchGroupGapWidth"]));
                }
            }

            else if (templateType.Name == "SpeakerTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("SoundOnTacticalBarkShown"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["SoundOnTacticalBarkShown"]));
                }
                if (modifications.ContainsKey("TacticalBarkSoundDelayInMs"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["TacticalBarkSoundDelayInMs"]));
                }
            }

            else if (templateType.Name == "SquaddieItemTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
            }

            else if (templateType.Name == "StoryFactionTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("AlliedFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["AlliedFactionType"]));
                }
                if (modifications.ContainsKey("EnemyFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["EnemyFactionType"]));
                }
                if (modifications.ContainsKey("FactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["FactionType"]));
                }
                if (modifications.ContainsKey("Representative"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["Representative"]));
                }
                if (modifications.ContainsKey("OperationIntros"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["OperationIntros"]));
                }
                if (modifications.ContainsKey("InitialStatus"))
                {
                    Marshal.WriteInt32(ptr + 0xF0, Convert.ToInt32(modifications["InitialStatus"]));
                }
                if (modifications.ContainsKey("InitialTotalTrust"))
                {
                    Marshal.WriteInt32(ptr + 0xF4, Convert.ToInt32(modifications["InitialTotalTrust"]));
                }
            }

            else if (templateType.Name == "StrategicAssetTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("DisableAfterMission"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["DisableAfterMission"]));
                }
            }

            else if (templateType.Name == "SurfaceDecalsTemplate")
            {
                if (modifications.ContainsKey("ConcreteEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["ConcreteEffect"]));
                }
                if (modifications.ContainsKey("MetalEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x20, Convert.ToInt32(modifications["MetalEffect"]));
                }
                if (modifications.ContainsKey("SandEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x28, Convert.ToInt32(modifications["SandEffect"]));
                }
                if (modifications.ContainsKey("EarthEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x30, Convert.ToInt32(modifications["EarthEffect"]));
                }
                if (modifications.ContainsKey("SnowEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["SnowEffect"]));
                }
                if (modifications.ContainsKey("WaterEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x40, Convert.ToInt32(modifications["WaterEffect"]));
                }
                if (modifications.ContainsKey("RuinsEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x48, Convert.ToInt32(modifications["RuinsEffect"]));
                }
                if (modifications.ContainsKey("SandStoneEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x50, Convert.ToInt32(modifications["SandStoneEffect"]));
                }
                if (modifications.ContainsKey("MudEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["MudEffect"]));
                }
                if (modifications.ContainsKey("GrassEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["GrassEffect"]));
                }
                if (modifications.ContainsKey("GlassEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["GlassEffect"]));
                }
                if (modifications.ContainsKey("ForestEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x70, Convert.ToInt32(modifications["ForestEffect"]));
                }
                if (modifications.ContainsKey("RockEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["RockEffect"]));
                }
            }

            else if (templateType.Name == "SurfaceEffectsTemplate")
            {
                if (modifications.ContainsKey("m_FirstSurfaceTypesWithSameEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["m_FirstSurfaceTypesWithSameEffect"]));
                }
            }

            else if (templateType.Name == "SurfaceSoundsTemplate")
            {
                if (modifications.ContainsKey("ConcreteSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["ConcreteSounds"]));
                }
                if (modifications.ContainsKey("MetalSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x20, Convert.ToInt32(modifications["MetalSounds"]));
                }
                if (modifications.ContainsKey("SandSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x28, Convert.ToInt32(modifications["SandSounds"]));
                }
                if (modifications.ContainsKey("EarthSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x30, Convert.ToInt32(modifications["EarthSounds"]));
                }
                if (modifications.ContainsKey("SnowSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["SnowSounds"]));
                }
                if (modifications.ContainsKey("WaterSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x40, Convert.ToInt32(modifications["WaterSounds"]));
                }
                if (modifications.ContainsKey("RuinsSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x48, Convert.ToInt32(modifications["RuinsSounds"]));
                }
                if (modifications.ContainsKey("SandStoneSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x50, Convert.ToInt32(modifications["SandStoneSounds"]));
                }
                if (modifications.ContainsKey("MudSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x58, Convert.ToInt32(modifications["MudSounds"]));
                }
                if (modifications.ContainsKey("GrassSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["GrassSounds"]));
                }
                if (modifications.ContainsKey("GlassSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["GlassSounds"]));
                }
                if (modifications.ContainsKey("ForestSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x70, Convert.ToInt32(modifications["ForestSounds"]));
                }
                if (modifications.ContainsKey("RockSounds"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["RockSounds"]));
                }
            }

            else if (templateType.Name == "SurfaceTypeTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("SurfaceType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["SurfaceType"]));
                }
            }

            else if (templateType.Name == "TagTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("TagType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["TagType"]));
                }
                if (modifications.ContainsKey("IsVisible"))
                {
                    Marshal.WriteByte(ptr + 0x88, Convert.ToByte(modifications["IsVisible"]));
                }
                if (modifications.ContainsKey("Value"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["Value"]));
                }
            }

            else if (templateType.Name == "UnitLeaderTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("m_Guid"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["m_Guid"]));
                }
                if (modifications.ContainsKey("SpeakerTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["SpeakerTemplate"]));
                }
                if (modifications.ContainsKey("HiringCosts"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["HiringCosts"]));
                }
                if (modifications.ContainsKey("HiringSelectBarkSound"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, Convert.ToInt32(modifications["HiringSelectBarkSound"]));
                }
                if (modifications.ContainsKey("HiredBarkSound"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["HiredBarkSound"]));
                }
                if (modifications.ContainsKey("PromotedBarkSound"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["PromotedBarkSound"]));
                }
                if (modifications.ContainsKey("Gender"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, Convert.ToInt32(modifications["Gender"]));
                }
                if (modifications.ContainsKey("SkinColor"))
                {
                    Marshal.WriteInt32(ptr + 0xB5, Convert.ToInt32(modifications["SkinColor"]));
                }
                if (modifications.ContainsKey("UnitActorType"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["UnitActorType"]));
                }
                if (modifications.ContainsKey("QualityLevel"))
                {
                    Marshal.WriteInt32(ptr + 0xC4, Convert.ToInt32(modifications["QualityLevel"]));
                }
                if (modifications.ContainsKey("GrowthPotential"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["GrowthPotential"]));
                }
                if (modifications.ContainsKey("FixedSupplyCost"))
                {
                    Marshal.WriteInt32(ptr + 0xD4, Convert.ToInt32(modifications["FixedSupplyCost"]));
                }
                if (modifications.ContainsKey("PromotionTax"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["PromotionTax"]));
                }
                if (modifications.ContainsKey("SquaddieTax"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, Convert.ToInt32(modifications["SquaddieTax"]));
                }
                if (modifications.ContainsKey("InfantryUnitTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["InfantryUnitTemplate"]));
                }
                if (modifications.ContainsKey("PilotInventoryTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["PilotInventoryTemplate"]));
                }
                if (modifications.ContainsKey("InitialVehicleItem"))
                {
                    Marshal.WriteInt32(ptr + 0xF0, Convert.ToInt32(modifications["InitialVehicleItem"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0x160, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0x164, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("InitialPerk"))
                {
                    Marshal.WriteInt32(ptr + 0x168, Convert.ToInt32(modifications["InitialPerk"]));
                }
            }

            else if (templateType.Name == "UnitRankTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("RankType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["RankType"]));
                }
                if (modifications.ContainsKey("PromotionCost"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["PromotionCost"]));
                }
            }

            else if (templateType.Name == "VehicleItemTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("SlotType"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotType"]));
                }
                if (modifications.ContainsKey("ItemType"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["ItemType"]));
                }
                if (modifications.ContainsKey("ExclusiveCategory"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["ExclusiveCategory"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("EntityTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["EntityTemplate"]));
                }
                if (modifications.ContainsKey("AccessorySlots"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["AccessorySlots"]));
                }
            }

            else if (templateType.Name == "VideoTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("m_VideoClip"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["m_VideoClip"]));
                }
            }

            else if (templateType.Name == "VoucherTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("VoucherType"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["VoucherType"]));
                }
                if (modifications.ContainsKey("VoucherChange"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["VoucherChange"]));
                }
            }

            else if (templateType.Name == "WeaponTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("TradeValue"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["TradeValue"]));
                }
                if (modifications.ContainsKey("BlackMarketMaxQuantity"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["BlackMarketMaxQuantity"]));
                }
                if (modifications.ContainsKey("SlotType"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotType"]));
                }
                if (modifications.ContainsKey("ItemType"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["ItemType"]));
                }
                if (modifications.ContainsKey("ExclusiveCategory"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["ExclusiveCategory"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x124, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("AnimType"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["AnimType"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x12C, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("AnimGrip"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["AnimGrip"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x148, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("AccuracyBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyBonus"])), 0));
                }
                if (modifications.ContainsKey("AccuracyDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x150, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["AccuracyDropoff"])), 0));
                }
                if (modifications.ContainsKey("Damage"))
                {
                    Marshal.WriteInt32(ptr + 0x154, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["Damage"])), 0));
                }
                if (modifications.ContainsKey("DamageDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x158, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamagePctCurrentHitpoints"))
                {
                    Marshal.WriteInt32(ptr + 0x15C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctCurrentHitpoints"])), 0));
                }
                if (modifications.ContainsKey("DamagePctCurrentHitpointsMin"))
                {
                    Marshal.WriteInt32(ptr + 0x160, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctCurrentHitpointsMin"])), 0));
                }
                if (modifications.ContainsKey("DamagePctMaxHitpoints"))
                {
                    Marshal.WriteInt32(ptr + 0x164, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctMaxHitpoints"])), 0));
                }
                if (modifications.ContainsKey("DamagePctMaxHitpointsMin"))
                {
                    Marshal.WriteInt32(ptr + 0x168, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamagePctMaxHitpointsMin"])), 0));
                }
                if (modifications.ContainsKey("ArmorPenetration"))
                {
                    Marshal.WriteInt32(ptr + 0x16C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ArmorPenetration"])), 0));
                }
                if (modifications.ContainsKey("ArmorPenetrationDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x170, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["ArmorPenetrationDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurability"))
                {
                    Marshal.WriteInt32(ptr + 0x174, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurability"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityMult"))
                {
                    Marshal.WriteInt32(ptr + 0x178, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityMult"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityDropoff"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityDropoff"])), 0));
                }
                if (modifications.ContainsKey("DamageToArmorDurabilityDropoffMult"))
                {
                    Marshal.WriteInt32(ptr + 0x180, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["DamageToArmorDurabilityDropoffMult"])), 0));
                }
                if (modifications.ContainsKey("Suppression"))
                {
                    Marshal.WriteInt32(ptr + 0x184, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["Suppression"])), 0));
                }
            }

            else if (templateType.Name == "WeatherTemplate")
            {
                if (modifications.ContainsKey("m_LocaState"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["m_LocaState"]));
                }
                if (modifications.ContainsKey("m_IsGarbage"))
                {
                    Marshal.WriteByte(ptr + 0x70, Convert.ToByte(modifications["m_IsGarbage"]));
                }
                if (modifications.ContainsKey("m_IsInitialized"))
                {
                    Marshal.WriteByte(ptr + 0x71, Convert.ToByte(modifications["m_IsInitialized"]));
                }
                if (modifications.ContainsKey("AmbientSound"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["AmbientSound"]));
                }
                if (modifications.ContainsKey("DisableDustEffects"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["DisableDustEffects"]));
                }
                if (modifications.ContainsKey("SkillToApply"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["SkillToApply"]));
                }
                if (modifications.ContainsKey("DawnTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["DawnTemplate"]));
                }
                if (modifications.ContainsKey("DayTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["DayTemplate"]));
                }
                if (modifications.ContainsKey("DuskTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["DuskTemplate"]));
                }
                if (modifications.ContainsKey("NightTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["NightTemplate"]));
                }
                if (modifications.ContainsKey("WindControlsTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["WindControlsTemplate"]));
                }
            }

            else if (templateType.Name == "WindControlsTemplate")
            {
                if (modifications.ContainsKey("m_windTurbulence"))
                {
                    Marshal.WriteInt32(ptr + 0x18, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_windTurbulence"])), 0));
                }
                if (modifications.ContainsKey("m_windStrength"))
                {
                    Marshal.WriteInt32(ptr + 0x1C, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_windStrength"])), 0));
                }
                if (modifications.ContainsKey("m_windSpeed"))
                {
                    Marshal.WriteInt32(ptr + 0x20, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_windSpeed"])), 0));
                }
                if (modifications.ContainsKey("m_windTiling"))
                {
                    Marshal.WriteInt32(ptr + 0x24, BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(modifications["m_windTiling"])), 0));
                }
            }
            else
            {
                LoggerInstance.Warning($"Unknown template type for injection: {templateType.Name}");
            }

    LoggerInstance.Msg($"Applied modifications to {obj.name} ({templateType.Name})");
}