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
                if (modifications.ContainsKey("TTL_MAX"))
                {
                    Marshal.WriteInt32(ptr + 0x1C, Convert.ToInt32(modifications["TTL_MAX"]));
                }
                if (modifications.ContainsKey("OppositeSideDistanceFromOpponentCap"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["OppositeSideDistanceFromOpponentCap"]));
                }
                if (modifications.ContainsKey("CullTilesDistances"))
                {
                    Marshal.WriteInt32(ptr + 0xCC, Convert.ToInt32(modifications["CullTilesDistances"]));
                }
                if (modifications.ContainsKey("NearTileLimit"))
                {
                    Marshal.WriteInt32(ptr + 0x134, Convert.ToInt32(modifications["NearTileLimit"]));
                }
                if (modifications.ContainsKey("PathfindingHiddenFromOpponentsBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x14C, Convert.ToInt32(modifications["PathfindingHiddenFromOpponentsBonus"]));
                }
                if (modifications.ContainsKey("TestValueInt"))
                {
                    Marshal.WriteInt32(ptr + 0x1C0, Convert.ToInt32(modifications["TestValueInt"]));
                }
            }

            else if (templateType.Name == "AccessoryTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("IconEquipment"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IconEquipment"]));
                }
                if (modifications.ContainsKey("IconEquipmentDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["IconEquipmentDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBar"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["IconSkillBar"]));
                }
                if (modifications.ContainsKey("IconSkillBarDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["IconSkillBarDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternative"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["IconSkillBarAlternative"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternativeDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["IconSkillBarAlternativeDisabled"]));
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
                if (modifications.ContainsKey("IsDestroyedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsDestroyedAfterCombat"]));
                }
                if (modifications.ContainsKey("Model"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Model"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("ModelSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ModelSecondary"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x12C, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
            }

            else if (templateType.Name == "AnimationSequenceTemplate")
            {
                if (modifications.ContainsKey("Prefab"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Prefab"]));
                }
                if (modifications.ContainsKey("HasRandomRotation"))
                {
                    Marshal.WriteByte(ptr + 0x80, Convert.ToByte(modifications["HasRandomRotation"]));
                }
            }

            else if (templateType.Name == "AnimatorParameterNameTemplate")
            {
                if (modifications.ContainsKey("ParameterType"))
                {
                    Marshal.WriteInt32(ptr + 0x60, Convert.ToInt32(modifications["ParameterType"]));
                }
            }

            else if (templateType.Name == "ArmorTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("IconEquipment"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IconEquipment"]));
                }
                if (modifications.ContainsKey("IconEquipmentDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["IconEquipmentDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBar"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["IconSkillBar"]));
                }
                if (modifications.ContainsKey("IconSkillBarDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["IconSkillBarDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternative"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["IconSkillBarAlternative"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternativeDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["IconSkillBarAlternativeDisabled"]));
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
                if (modifications.ContainsKey("IsDestroyedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsDestroyedAfterCombat"]));
                }
                if (modifications.ContainsKey("Model"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Model"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("ModelSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ModelSecondary"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x12C, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("HasSpecialFemaleModels"))
                {
                    Marshal.WriteByte(ptr + 0x130, Convert.ToByte(modifications["HasSpecialFemaleModels"]));
                }
                if (modifications.ContainsKey("SquadLeaderMode"))
                {
                    Marshal.WriteInt32(ptr + 0x148, Convert.ToInt32(modifications["SquadLeaderMode"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelMaleWhite"))
                {
                    Marshal.WriteInt32(ptr + 0x150, Convert.ToInt32(modifications["SquadLeaderModelMaleWhite"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelMaleBrown"))
                {
                    Marshal.WriteInt32(ptr + 0x158, Convert.ToInt32(modifications["SquadLeaderModelMaleBrown"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelMaleBlack"))
                {
                    Marshal.WriteInt32(ptr + 0x160, Convert.ToInt32(modifications["SquadLeaderModelMaleBlack"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelFemaleWhite"))
                {
                    Marshal.WriteInt32(ptr + 0x168, Convert.ToInt32(modifications["SquadLeaderModelFemaleWhite"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelFemaleBrown"))
                {
                    Marshal.WriteInt32(ptr + 0x170, Convert.ToInt32(modifications["SquadLeaderModelFemaleBrown"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelFemaleBlack"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["SquadLeaderModelFemaleBlack"]));
                }
                if (modifications.ContainsKey("SquadLeaderModelFixed"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["SquadLeaderModelFixed"]));
                }
                if (modifications.ContainsKey("OverrideScale"))
                {
                    Marshal.WriteByte(ptr + 0x188, Convert.ToByte(modifications["OverrideScale"]));
                }
                if (modifications.ContainsKey("Scale"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["Scale"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("Armor"))
                {
                    Marshal.WriteInt32(ptr + 0x198, Convert.ToInt32(modifications["Armor"]));
                }
                if (modifications.ContainsKey("DurabilityPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0x19C, Convert.ToInt32(modifications["DurabilityPerElement"]));
                }
                if (modifications.ContainsKey("HitpointsPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0x1A4, Convert.ToInt32(modifications["HitpointsPerElement"]));
                }
                if (modifications.ContainsKey("Accuracy"))
                {
                    Marshal.WriteInt32(ptr + 0x1AC, Convert.ToInt32(modifications["Accuracy"]));
                }
                if (modifications.ContainsKey("Vision"))
                {
                    Marshal.WriteInt32(ptr + 0x1C0, Convert.ToInt32(modifications["Vision"]));
                }
                if (modifications.ContainsKey("Detection"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["Detection"]));
                }
                if (modifications.ContainsKey("Concealment"))
                {
                    Marshal.WriteInt32(ptr + 0x1D0, Convert.ToInt32(modifications["Concealment"]));
                }
                if (modifications.ContainsKey("GetDismemberedChanceBonus"))
                {
                    Marshal.WriteInt32(ptr + 0x1DC, Convert.ToInt32(modifications["GetDismemberedChanceBonus"]));
                }
                if (modifications.ContainsKey("ActionPoints"))
                {
                    Marshal.WriteInt32(ptr + 0x1E4, Convert.ToInt32(modifications["ActionPoints"]));
                }
                if (modifications.ContainsKey("AdditionalMovementCost"))
                {
                    Marshal.WriteInt32(ptr + 0x1EC, Convert.ToInt32(modifications["AdditionalMovementCost"]));
                }
                if (modifications.ContainsKey("SoundOnMovementStep"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["SoundOnMovementStep"]));
                }
                if (modifications.ContainsKey("SoundOnMovementStepOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["SoundOnMovementStepOverrides2"]));
                }
                if (modifications.ContainsKey("SoundOnMovementSymbolic"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["SoundOnMovementSymbolic"]));
                }
                if (modifications.ContainsKey("SoundOnArmorHit"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["SoundOnArmorHit"]));
                }
                if (modifications.ContainsKey("SoundOnHitpointsHit"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["SoundOnHitpointsHit"]));
                }
                if (modifications.ContainsKey("SoundOnHitpointsHitFemale"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["SoundOnHitpointsHitFemale"]));
                }
                if (modifications.ContainsKey("SoundOnDeath"))
                {
                    Marshal.WriteInt32(ptr + 0x228, Convert.ToInt32(modifications["SoundOnDeath"]));
                }
                if (modifications.ContainsKey("SoundOnDeathFemale"))
                {
                    Marshal.WriteInt32(ptr + 0x230, Convert.ToInt32(modifications["SoundOnDeathFemale"]));
                }
            }

            else if (templateType.Name == "BiomeTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("BiomeType"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["BiomeType"]));
                }
                if (modifications.ContainsKey("ShowInCheatMenu"))
                {
                    Marshal.WriteByte(ptr + 0x84, Convert.ToByte(modifications["ShowInCheatMenu"]));
                }
                if (modifications.ContainsKey("Material"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Material"]));
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
                if (modifications.ContainsKey("WindZone"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["WindZone"]));
                }
                if (modifications.ContainsKey("HasGrass"))
                {
                    Marshal.WriteByte(ptr + 0xC0, Convert.ToByte(modifications["HasGrass"]));
                }
                if (modifications.ContainsKey("LightConditions"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["LightConditions"]));
                }
            }

            else if (templateType.Name == "BoolPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
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
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
            }

            else if (templateType.Name == "ConversationStageTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("BackgroundImage"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["BackgroundImage"]));
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
            }

            else if (templateType.Name == "DecalTemplate")
            {
                if (modifications.ContainsKey("Index"))
                {
                    Marshal.WriteInt32(ptr + 0x10, Convert.ToInt32(modifications["Index"]));
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
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
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
                if (modifications.ContainsKey("Measure"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Measure"]));
                }
            }

            else if (templateType.Name == "ElementAnimatorTemplate")
            {
                if (modifications.ContainsKey("MovementDelayPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["MovementDelayPerElement"]));
                }
                if (modifications.ContainsKey("InitialAimDelay"))
                {
                    Marshal.WriteInt32(ptr + 0x74, Convert.ToInt32(modifications["InitialAimDelay"]));
                }
                if (modifications.ContainsKey("AnimatorInPlaceTurningSpeedCurve"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["AnimatorInPlaceTurningSpeedCurve"]));
                }
                if (modifications.ContainsKey("DeathBehaviour"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["DeathBehaviour"]));
                }
                if (modifications.ContainsKey("AdditionalRagdollKillImpulse"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["AdditionalRagdollKillImpulse"]));
                }
                if (modifications.ContainsKey("AdditionalRagdollKillImpulseArea"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["AdditionalRagdollKillImpulseArea"]));
                }
                if (modifications.ContainsKey("DeathAnimationVariants"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, Convert.ToInt32(modifications["DeathAnimationVariants"]));
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
                    Marshal.WriteByte(ptr + 0xB3, Convert.ToByte(modifications["HumanIK"]));
                }
                if (modifications.ContainsKey("IKHintLeftElbowOffset"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IKHintLeftElbowOffset"]));
                }
                if (modifications.ContainsKey("NegativeSpeedTurns"))
                {
                    Marshal.WriteByte(ptr + 0xC4, Convert.ToByte(modifications["NegativeSpeedTurns"]));
                }
                if (modifications.ContainsKey("SteeringDirection"))
                {
                    Marshal.WriteByte(ptr + 0xC5, Convert.ToByte(modifications["SteeringDirection"]));
                }
                if (modifications.ContainsKey("Aiming"))
                {
                    Marshal.WriteByte(ptr + 0xCC, Convert.ToByte(modifications["Aiming"]));
                }
                if (modifications.ContainsKey("UseRootMotionAiming"))
                {
                    Marshal.WriteByte(ptr + 0xD8, Convert.ToByte(modifications["UseRootMotionAiming"]));
                }
                if (modifications.ContainsKey("AngleMapping"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, Convert.ToInt32(modifications["AngleMapping"]));
                }
            }

            else if (templateType.Name == "EmotionalStateTemplate")
            {
                if (modifications.ContainsKey("StateType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["StateType"]));
                }
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("TooltipTitle"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["TooltipTitle"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Effect"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Effect"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconBig"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["IconBig"]));
                }
                if (modifications.ContainsKey("IconTint"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["IconTint"]));
                }
                if (modifications.ContainsKey("DurationInMissions"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["DurationInMissions"]));
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
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconBig"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["IconBig"]));
                }
                if (modifications.ContainsKey("DisableAfterMission"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["DisableAfterMission"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Description"]));
                }
            }

            else if (templateType.Name == "EntityTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
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
                if (modifications.ContainsKey("DeployCostsPerElement"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["DeployCostsPerElement"]));
                }
                if (modifications.ContainsKey("DeployCosts"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["DeployCosts"]));
                }
                if (modifications.ContainsKey("ArmyPointCost"))
                {
                    Marshal.WriteInt32(ptr + 0xB4, Convert.ToInt32(modifications["ArmyPointCost"]));
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
                if (modifications.ContainsKey("SpeakerTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["SpeakerTemplate"]));
                }
                if (modifications.ContainsKey("AnimationSoundTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["AnimationSoundTemplate"]));
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
                if (modifications.ContainsKey("OverrideMissionPreviewColor"))
                {
                    Marshal.WriteByte(ptr + 0x120, Convert.ToByte(modifications["OverrideMissionPreviewColor"]));
                }
                if (modifications.ContainsKey("MissionPreviewColorOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x124, Convert.ToInt32(modifications["MissionPreviewColorOverride"]));
                }
                if (modifications.ContainsKey("Scale"))
                {
                    Marshal.WriteInt32(ptr + 0x168, Convert.ToInt32(modifications["Scale"]));
                }
                if (modifications.ContainsKey("OverrideScaleForSquadLeader"))
                {
                    Marshal.WriteByte(ptr + 0x170, Convert.ToByte(modifications["OverrideScaleForSquadLeader"]));
                }
                if (modifications.ContainsKey("ActorLightOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["ActorLightOverride"]));
                }
                if (modifications.ContainsKey("IsBlockingLineOfSight"))
                {
                    Marshal.WriteByte(ptr + 0x198, Convert.ToByte(modifications["IsBlockingLineOfSight"]));
                }
                if (modifications.ContainsKey("Badge"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["Badge"]));
                }
                if (modifications.ContainsKey("BadgeWhite"))
                {
                    Marshal.WriteInt32(ptr + 0x1C0, Convert.ToInt32(modifications["BadgeWhite"]));
                }
                if (modifications.ContainsKey("PreviewMapIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["PreviewMapIcon"]));
                }
                if (modifications.ContainsKey("FactionSpecificAnimation"))
                {
                    Marshal.WriteInt32(ptr + 0x1D8, Convert.ToInt32(modifications["FactionSpecificAnimation"]));
                }
                if (modifications.ContainsKey("AimWithVisualSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x1DC, Convert.ToInt32(modifications["AimWithVisualSlot"]));
                }
                if (modifications.ContainsKey("AnimatorTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x1E0, Convert.ToInt32(modifications["AnimatorTemplate"]));
                }
                if (modifications.ContainsKey("MinSkinQuality"))
                {
                    Marshal.WriteInt32(ptr + 0x1E8, Convert.ToInt32(modifications["MinSkinQuality"]));
                }
                if (modifications.ContainsKey("BloodDecals"))
                {
                    Marshal.WriteInt32(ptr + 0x1F0, Convert.ToInt32(modifications["BloodDecals"]));
                }
                if (modifications.ContainsKey("BloodDecalsOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["BloodDecalsOverride"]));
                }
                if (modifications.ContainsKey("BloodPool"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["BloodPool"]));
                }
                if (modifications.ContainsKey("BloodPoolOverride"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["BloodPoolOverride"]));
                }
                if (modifications.ContainsKey("BloodPoolTriggerType"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["BloodPoolTriggerType"]));
                }
                if (modifications.ContainsKey("BloodPoolAnimation"))
                {
                    Marshal.WriteByte(ptr + 0x214, Convert.ToByte(modifications["BloodPoolAnimation"]));
                }
                if (modifications.ContainsKey("DamageReceivedEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["DamageReceivedEffect"]));
                }
                if (modifications.ContainsKey("HeavyDamageReceivedEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["HeavyDamageReceivedEffect"]));
                }
                if (modifications.ContainsKey("GetDismemberedBloodSprayEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x230, Convert.ToInt32(modifications["GetDismemberedBloodSprayEffect"]));
                }
                if (modifications.ContainsKey("GetDismemberedSmallAdditionalParts"))
                {
                    Marshal.WriteInt32(ptr + 0x238, Convert.ToInt32(modifications["GetDismemberedSmallAdditionalParts"]));
                }
                if (modifications.ContainsKey("DeathEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x240, Convert.ToInt32(modifications["DeathEffectOverrides2"]));
                }
                if (modifications.ContainsKey("DeathEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x248, Convert.ToInt32(modifications["DeathEffect"]));
                }
                if (modifications.ContainsKey("DeathEffectTriggerType"))
                {
                    Marshal.WriteInt32(ptr + 0x250, Convert.ToInt32(modifications["DeathEffectTriggerType"]));
                }
                if (modifications.ContainsKey("DeathAttachEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x258, Convert.ToInt32(modifications["DeathAttachEffect"]));
                }
                if (modifications.ContainsKey("IsSinkingIntoGroundOnDeath"))
                {
                    Marshal.WriteByte(ptr + 0x260, Convert.ToByte(modifications["IsSinkingIntoGroundOnDeath"]));
                }
                if (modifications.ContainsKey("DeathCameraEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x264, Convert.ToInt32(modifications["DeathCameraEffect"]));
                }
                if (modifications.ContainsKey("SoundOnAim"))
                {
                    Marshal.WriteInt32(ptr + 0x268, Convert.ToInt32(modifications["SoundOnAim"]));
                }
                if (modifications.ContainsKey("SoundWhileAlive"))
                {
                    Marshal.WriteInt32(ptr + 0x270, Convert.ToInt32(modifications["SoundWhileAlive"]));
                }
                if (modifications.ContainsKey("ExhaustDriveEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x278, Convert.ToInt32(modifications["ExhaustDriveEffect"]));
                }
                if (modifications.ContainsKey("ExhaustRevEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x280, Convert.ToInt32(modifications["ExhaustRevEffect"]));
                }
                if (modifications.ContainsKey("ExhaustIdleEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x288, Convert.ToInt32(modifications["ExhaustIdleEffect"]));
                }
                if (modifications.ContainsKey("MovementEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x290, Convert.ToInt32(modifications["MovementEffectOverrides2"]));
                }
                if (modifications.ContainsKey("TriggerMovementEffectsOnStep"))
                {
                    Marshal.WriteByte(ptr + 0x298, Convert.ToByte(modifications["TriggerMovementEffectsOnStep"]));
                }
                if (modifications.ContainsKey("MovementType"))
                {
                    Marshal.WriteInt32(ptr + 0x2A0, Convert.ToInt32(modifications["MovementType"]));
                }
                if (modifications.ContainsKey("VisualPositioning"))
                {
                    Marshal.WriteInt32(ptr + 0x2A8, Convert.ToInt32(modifications["VisualPositioning"]));
                }
                if (modifications.ContainsKey("RotationAfterMovement"))
                {
                    Marshal.WriteInt32(ptr + 0x2B0, Convert.ToInt32(modifications["RotationAfterMovement"]));
                }
                if (modifications.ContainsKey("CameraShakeOnMovement"))
                {
                    Marshal.WriteByte(ptr + 0x2B4, Convert.ToByte(modifications["CameraShakeOnMovement"]));
                }
                if (modifications.ContainsKey("CameraShakeOnMovementStepInterval"))
                {
                    Marshal.WriteInt32(ptr + 0x2B8, Convert.ToInt32(modifications["CameraShakeOnMovementStepInterval"]));
                }
                if (modifications.ContainsKey("InventoryType"))
                {
                    Marshal.WriteInt32(ptr + 0x2C8, Convert.ToInt32(modifications["InventoryType"]));
                }
                if (modifications.ContainsKey("ModularVehicle"))
                {
                    Marshal.WriteInt32(ptr + 0x2D8, Convert.ToInt32(modifications["ModularVehicle"]));
                }
                if (modifications.ContainsKey("Properties"))
                {
                    Marshal.WriteInt32(ptr + 0x2E0, Convert.ToInt32(modifications["Properties"]));
                }
                if (modifications.ContainsKey("AIRole"))
                {
                    Marshal.WriteInt32(ptr + 0x2F8, Convert.ToInt32(modifications["AIRole"]));
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
                if (modifications.ContainsKey("DestroyEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["DestroyEffect"]));
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
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("TurnOrderIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["TurnOrderIcon"]));
                }
                if (modifications.ContainsKey("TurnOrderInactiveIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["TurnOrderInactiveIcon"]));
                }
                if (modifications.ContainsKey("AlliedFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["AlliedFactionType"]));
                }
                if (modifications.ContainsKey("EnemyFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["EnemyFactionType"]));
                }
                if (modifications.ContainsKey("ArmyList"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["ArmyList"]));
                }
            }

            else if (templateType.Name == "GenericMissionTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("ObjectiveProgressText"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["ObjectiveProgressText"]));
                }
                if (modifications.ContainsKey("AllowedDifficulties"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["AllowedDifficulties"]));
                }
                if (modifications.ContainsKey("ProgressRequired"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["ProgressRequired"]));
                }
                if (modifications.ContainsKey("Condition"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Condition"]));
                }
                if (modifications.ContainsKey("EffectivenessConfig"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["EffectivenessConfig"]));
                }
                if (modifications.ContainsKey("PoiIcon"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["PoiIcon"]));
                }
                if (modifications.ContainsKey("BackgroundMusic"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["BackgroundMusic"]));
                }
                if (modifications.ContainsKey("StartAnimationSequence"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["StartAnimationSequence"]));
                }
                if (modifications.ContainsKey("IdealDuration"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["IdealDuration"]));
                }
                if (modifications.ContainsKey("ShowProgressBarLabel"))
                {
                    Marshal.WriteByte(ptr + 0xC0, Convert.ToByte(modifications["ShowProgressBarLabel"]));
                }
                if (modifications.ContainsKey("EnemyArmyFlags"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, Convert.ToInt32(modifications["EnemyArmyFlags"]));
                }
                if (modifications.ContainsKey("EnemyArmyExcludedFlags"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["EnemyArmyExcludedFlags"]));
                }
                if (modifications.ContainsKey("EnemySpawnAreaSettings"))
                {
                    Marshal.WriteInt32(ptr + 0xE4, Convert.ToInt32(modifications["EnemySpawnAreaSettings"]));
                }
                if (modifications.ContainsKey("EnemyStartInSleepMode"))
                {
                    Marshal.WriteByte(ptr + 0xE5, Convert.ToByte(modifications["EnemyStartInSleepMode"]));
                }
                if (modifications.ContainsKey("RoamWhileSleeping"))
                {
                    Marshal.WriteByte(ptr + 0xE6, Convert.ToByte(modifications["RoamWhileSleeping"]));
                }
                if (modifications.ContainsKey("SetpieceOwner"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SetpieceOwner"]));
                }
                if (modifications.ContainsKey("m_EnemyReinforcementsSpawnAreaSettings"))
                {
                    Marshal.WriteInt32(ptr + 0x150, Convert.ToInt32(modifications["m_EnemyReinforcementsSpawnAreaSettings"]));
                }
            }

            else if (templateType.Name == "GlobalDifficultyTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("InitialSquaddies"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["InitialSquaddies"]));
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
                if (modifications.ContainsKey("EffectOnDeath"))
                {
                    Marshal.WriteInt32(ptr + 0x28, Convert.ToInt32(modifications["EffectOnDeath"]));
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
                if (modifications.ContainsKey("Concealment"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["Concealment"]));
                }
            }

            else if (templateType.Name == "IntPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
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
                if (modifications.ContainsKey("Measure"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Measure"]));
                }
            }

            else if (templateType.Name == "ItemFilterTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
            }

            else if (templateType.Name == "KeyBindPlayerSettingTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("Default"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["Default"]));
                }
            }

            else if (templateType.Name == "LightConditionTemplate")
            {
                if (modifications.ContainsKey("DustColor"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["DustColor"]));
                }
                if (modifications.ContainsKey("SnowColor"))
                {
                    Marshal.WriteInt32(ptr + 0x28, Convert.ToInt32(modifications["SnowColor"]));
                }
                if (modifications.ContainsKey("DirectionalLightPrefab"))
                {
                    Marshal.WriteInt32(ptr + 0x40, Convert.ToInt32(modifications["DirectionalLightPrefab"]));
                }
                if (modifications.ContainsKey("DirectionalActorLightPrefab"))
                {
                    Marshal.WriteInt32(ptr + 0x48, Convert.ToInt32(modifications["DirectionalActorLightPrefab"]));
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
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("IsActive"))
                {
                    Marshal.WriteByte(ptr + 0x88, Convert.ToByte(modifications["IsActive"]));
                }
                if (modifications.ContainsKey("DefaultValueIndex"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["DefaultValueIndex"]));
                }
                if (modifications.ContainsKey("Values"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Values"]));
                }
            }

            else if (templateType.Name == "MissionDifficultyTemplate")
            {
                if (modifications.ContainsKey("DifficultyType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["DifficultyType"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Skulls"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Skulls"]));
                }
            }

            else if (templateType.Name == "MissionPOITemplate")
            {
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
                }
            }

            else if (templateType.Name == "MissionPreviewConfigTemplate")
            {
                if (modifications.ContainsKey("BorderWidth"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["BorderWidth"]));
                }
                if (modifications.ContainsKey("BorderColor"))
                {
                    Marshal.WriteInt32(ptr + 0x7C, Convert.ToInt32(modifications["BorderColor"]));
                }
                if (modifications.ContainsKey("GridColor"))
                {
                    Marshal.WriteInt32(ptr + 0x8C, Convert.ToInt32(modifications["GridColor"]));
                }
                if (modifications.ContainsKey("TileHighlightColor"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, Convert.ToInt32(modifications["TileHighlightColor"]));
                }
                if (modifications.ContainsKey("TileDragStartColor"))
                {
                    Marshal.WriteInt32(ptr + 0xAC, Convert.ToInt32(modifications["TileDragStartColor"]));
                }
                if (modifications.ContainsKey("RoadsColor"))
                {
                    Marshal.WriteInt32(ptr + 0xBC, Convert.ToInt32(modifications["RoadsColor"]));
                }
                if (modifications.ContainsKey("DeploymentZoneColor"))
                {
                    Marshal.WriteInt32(ptr + 0xCC, Convert.ToInt32(modifications["DeploymentZoneColor"]));
                }
                if (modifications.ContainsKey("ObjectiveAreaColor"))
                {
                    Marshal.WriteInt32(ptr + 0xDC, Convert.ToInt32(modifications["ObjectiveAreaColor"]));
                }
                if (modifications.ContainsKey("StructureColor"))
                {
                    Marshal.WriteInt32(ptr + 0xEC, Convert.ToInt32(modifications["StructureColor"]));
                }
                if (modifications.ContainsKey("VegetationColor"))
                {
                    Marshal.WriteInt32(ptr + 0xFC, Convert.ToInt32(modifications["VegetationColor"]));
                }
                if (modifications.ContainsKey("ActorAreaColor"))
                {
                    Marshal.WriteInt32(ptr + 0x10C, Convert.ToInt32(modifications["ActorAreaColor"]));
                }
                if (modifications.ContainsKey("MinHeightColor"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["MinHeightColor"]));
                }
                if (modifications.ContainsKey("MaxHeightColor"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["MaxHeightColor"]));
                }
                if (modifications.ContainsKey("HeightShades"))
                {
                    Marshal.WriteInt32(ptr + 0x150, Convert.ToInt32(modifications["HeightShades"]));
                }
                if (modifications.ContainsKey("InaccessibleMinHeightColor"))
                {
                    Marshal.WriteInt32(ptr + 0x15C, Convert.ToInt32(modifications["InaccessibleMinHeightColor"]));
                }
                if (modifications.ContainsKey("InaccessibleMaxHeightColor"))
                {
                    Marshal.WriteInt32(ptr + 0x16C, Convert.ToInt32(modifications["InaccessibleMaxHeightColor"]));
                }
                if (modifications.ContainsKey("InaccessibleHeightShades"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, Convert.ToInt32(modifications["InaccessibleHeightShades"]));
                }
                if (modifications.ContainsKey("UnknownFactionName"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["UnknownFactionName"]));
                }
                if (modifications.ContainsKey("UnknownFactionShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x188, Convert.ToInt32(modifications["UnknownFactionShortName"]));
                }
                if (modifications.ContainsKey("UnknownUnitTypeName"))
                {
                    Marshal.WriteInt32(ptr + 0x190, Convert.ToInt32(modifications["UnknownUnitTypeName"]));
                }
                if (modifications.ContainsKey("UnknownNormalColor"))
                {
                    Marshal.WriteInt32(ptr + 0x198, Convert.ToInt32(modifications["UnknownNormalColor"]));
                }
                if (modifications.ContainsKey("UnknownHoverColor"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, Convert.ToInt32(modifications["UnknownHoverColor"]));
                }
                if (modifications.ContainsKey("IconUnitUnknown"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["IconUnitUnknown"]));
                }
                if (modifications.ContainsKey("InfoLevelRevealDelayInSec"))
                {
                    Marshal.WriteInt32(ptr + 0x1C0, Convert.ToInt32(modifications["InfoLevelRevealDelayInSec"]));
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
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
            }

            else if (templateType.Name == "ModularVehicleWeaponTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("IconEquipment"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IconEquipment"]));
                }
                if (modifications.ContainsKey("IconEquipmentDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["IconEquipmentDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBar"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["IconSkillBar"]));
                }
                if (modifications.ContainsKey("IconSkillBarDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["IconSkillBarDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternative"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["IconSkillBarAlternative"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternativeDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["IconSkillBarAlternativeDisabled"]));
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
                if (modifications.ContainsKey("IsDestroyedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsDestroyedAfterCombat"]));
                }
                if (modifications.ContainsKey("Model"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Model"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("ModelSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ModelSecondary"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x12C, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("AnimType"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["AnimType"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x134, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("AnimGrip"))
                {
                    Marshal.WriteInt32(ptr + 0x138, Convert.ToInt32(modifications["AnimGrip"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["MaxRange"]));
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

            else if (templateType.Name == "MoraleEffectTemplate")
            {
                if (modifications.ContainsKey("MoraleEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x18, Convert.ToInt32(modifications["MoraleEffect"]));
                }
                if (modifications.ContainsKey("Chance"))
                {
                    Marshal.WriteInt32(ptr + 0x20, Convert.ToInt32(modifications["Chance"]));
                }
                if (modifications.ContainsKey("AmountOfMoraleEffectsRequired"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["AmountOfMoraleEffectsRequired"]));
                }
                if (modifications.ContainsKey("Prerequisites"))
                {
                    Marshal.WriteInt32(ptr + 0x40, Convert.ToInt32(modifications["Prerequisites"]));
                }
            }

            else if (templateType.Name == "OffmapAbilityTemplate")
            {
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
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("RequiredCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["RequiredCampaignProgress"]));
                }
                if (modifications.ContainsKey("TotalMissionOptions"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["TotalMissionOptions"]));
                }
                if (modifications.ContainsKey("EnemyAssets"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["EnemyAssets"]));
                }
                if (modifications.ContainsKey("MaxRating"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["MaxRating"]));
                }
                if (modifications.ContainsKey("ClientTrustChange"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["ClientTrustChange"]));
                }
                if (modifications.ContainsKey("EnemyTrustChange"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["EnemyTrustChange"]));
                }
            }

            else if (templateType.Name == "OperationTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Goal"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Goal"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("VictoryDescription"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["VictoryDescription"]));
                }
                if (modifications.ContainsKey("FailureDescription"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["FailureDescription"]));
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
                if (modifications.ContainsKey("StartConfirmationDialogText"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["StartConfirmationDialogText"]));
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
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("ShortDescription"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["ShortDescription"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["IconDisabled"]));
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
                if (modifications.ContainsKey("IsIgnoringCoverInside"))
                {
                    Marshal.WriteByte(ptr + 0x101, Convert.ToByte(modifications["IsIgnoringCoverInside"]));
                }
                if (modifications.ContainsKey("IsSilent"))
                {
                    Marshal.WriteByte(ptr + 0x102, Convert.ToByte(modifications["IsSilent"]));
                }
                if (modifications.ContainsKey("IgnoreMalfunctionChance"))
                {
                    Marshal.WriteByte(ptr + 0x103, Convert.ToByte(modifications["IgnoreMalfunctionChance"]));
                }
                if (modifications.ContainsKey("IsDeploymentRequired"))
                {
                    Marshal.WriteByte(ptr + 0x104, Convert.ToByte(modifications["IsDeploymentRequired"]));
                }
                if (modifications.ContainsKey("IsWeaponSetupRequired"))
                {
                    Marshal.WriteByte(ptr + 0x105, Convert.ToByte(modifications["IsWeaponSetupRequired"]));
                }
                if (modifications.ContainsKey("IsUsableWhileContained"))
                {
                    Marshal.WriteByte(ptr + 0x106, Convert.ToByte(modifications["IsUsableWhileContained"]));
                }
                if (modifications.ContainsKey("IsUsableWhilePinnedDown"))
                {
                    Marshal.WriteByte(ptr + 0x107, Convert.ToByte(modifications["IsUsableWhilePinnedDown"]));
                }
                if (modifications.ContainsKey("IsStacking"))
                {
                    Marshal.WriteByte(ptr + 0x108, Convert.ToByte(modifications["IsStacking"]));
                }
                if (modifications.ContainsKey("IsSerialized"))
                {
                    Marshal.WriteByte(ptr + 0x109, Convert.ToByte(modifications["IsSerialized"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x10A, Convert.ToByte(modifications["IsRemovedAfterCombat"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterOperation"))
                {
                    Marshal.WriteByte(ptr + 0x10B, Convert.ToByte(modifications["IsRemovedAfterOperation"]));
                }
                if (modifications.ContainsKey("IsHidden"))
                {
                    Marshal.WriteByte(ptr + 0x10C, Convert.ToByte(modifications["IsHidden"]));
                }
                if (modifications.ContainsKey("Shape"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Shape"]));
                }
                if (modifications.ContainsKey("ConeAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x114, Convert.ToInt32(modifications["ConeAngle"]));
                }
                if (modifications.ContainsKey("IsOverridingRanges"))
                {
                    Marshal.WriteByte(ptr + 0x118, Convert.ToByte(modifications["IsOverridingRanges"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x124, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("Repetitions"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["Repetitions"]));
                }
                if (modifications.ContainsKey("SkipDelayForLastRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x14C, Convert.ToByte(modifications["SkipDelayForLastRepetition"]));
                }
                if (modifications.ContainsKey("IsPlayingAnimationForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x14D, Convert.ToByte(modifications["IsPlayingAnimationForEachRepetition"]));
                }
                if (modifications.ContainsKey("UseCustomAoEShape"))
                {
                    Marshal.WriteByte(ptr + 0x158, Convert.ToByte(modifications["UseCustomAoEShape"]));
                }
                if (modifications.ContainsKey("CustomAoEShape"))
                {
                    Marshal.WriteInt32(ptr + 0x160, Convert.ToInt32(modifications["CustomAoEShape"]));
                }
                if (modifications.ContainsKey("AoEType"))
                {
                    Marshal.WriteInt32(ptr + 0x168, Convert.ToInt32(modifications["AoEType"]));
                }
                if (modifications.ContainsKey("AoEFilter"))
                {
                    Marshal.WriteInt32(ptr + 0x170, Convert.ToInt32(modifications["AoEFilter"]));
                }
                if (modifications.ContainsKey("TargetFaction"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["TargetFaction"]));
                }
                if (modifications.ContainsKey("AoEChanceToHitCenter"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, Convert.ToInt32(modifications["AoEChanceToHitCenter"]));
                }
                if (modifications.ContainsKey("SelectableTiles"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["SelectableTiles"]));
                }
                if (modifications.ContainsKey("ScatterMode"))
                {
                    Marshal.WriteInt32(ptr + 0x184, Convert.ToInt32(modifications["ScatterMode"]));
                }
                if (modifications.ContainsKey("Scatter"))
                {
                    Marshal.WriteInt32(ptr + 0x188, Convert.ToInt32(modifications["Scatter"]));
                }
                if (modifications.ContainsKey("ScatterChance"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["ScatterChance"]));
                }
                if (modifications.ContainsKey("ScatterHitEachTileOnlyOnce"))
                {
                    Marshal.WriteByte(ptr + 0x190, Convert.ToByte(modifications["ScatterHitEachTileOnlyOnce"]));
                }
                if (modifications.ContainsKey("ScatterHitOnlyValidTiles"))
                {
                    Marshal.WriteByte(ptr + 0x191, Convert.ToByte(modifications["ScatterHitOnlyValidTiles"]));
                }
                if (modifications.ContainsKey("MuzzleType"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["MuzzleType"]));
                }
                if (modifications.ContainsKey("MuzzleEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x1A0, Convert.ToInt32(modifications["MuzzleEffect"]));
                }
                if (modifications.ContainsKey("MuzzleEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, Convert.ToInt32(modifications["MuzzleEffectOverrides2"]));
                }
                if (modifications.ContainsKey("IsSpawningMuzzleForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x1B0, Convert.ToByte(modifications["IsSpawningMuzzleForEachRepetition"]));
                }
                if (modifications.ContainsKey("IsAttachingMuzzleToTransform"))
                {
                    Marshal.WriteByte(ptr + 0x1B1, Convert.ToByte(modifications["IsAttachingMuzzleToTransform"]));
                }
                if (modifications.ContainsKey("CameraEffectOnFire"))
                {
                    Marshal.WriteInt32(ptr + 0x1B4, Convert.ToInt32(modifications["CameraEffectOnFire"]));
                }
                if (modifications.ContainsKey("ProjectileSpawnPositionOffset"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["ProjectileSpawnPositionOffset"]));
                }
                if (modifications.ContainsKey("ProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["ProjectileData"]));
                }
                if (modifications.ContainsKey("SecondaryProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1D0, Convert.ToInt32(modifications["SecondaryProjectileData"]));
                }
                if (modifications.ContainsKey("IsImpactShownOnHit"))
                {
                    Marshal.WriteByte(ptr + 0x1EC, Convert.ToByte(modifications["IsImpactShownOnHit"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnTile"))
                {
                    Marshal.WriteByte(ptr + 0x1ED, Convert.ToByte(modifications["IsImpactCenteredOnTile"]));
                }
                if (modifications.ContainsKey("IsImpactOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x1EE, Convert.ToByte(modifications["IsImpactOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsDecalOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x1EF, Convert.ToByte(modifications["IsDecalOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnExecutingElement"))
                {
                    Marshal.WriteByte(ptr + 0x1F0, Convert.ToByte(modifications["IsImpactCenteredOnExecutingElement"]));
                }
                if (modifications.ContainsKey("IsImpactAlignedToInfantry"))
                {
                    Marshal.WriteByte(ptr + 0x1F1, Convert.ToByte(modifications["IsImpactAlignedToInfantry"]));
                }
                if (modifications.ContainsKey("CameraEffectOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x1F4, Convert.ToInt32(modifications["CameraEffectOnImpact"]));
                }
                if (modifications.ContainsKey("CameraEffectOnPlayerHit"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["CameraEffectOnPlayerHit"]));
                }
                if (modifications.ContainsKey("IsTriggeringHeavyDamagedReceivedEffect"))
                {
                    Marshal.WriteByte(ptr + 0x1FC, Convert.ToByte(modifications["IsTriggeringHeavyDamagedReceivedEffect"]));
                }
                if (modifications.ContainsKey("RagdollImpactMult"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["RagdollImpactMult"]));
                }
                if (modifications.ContainsKey("VerticalRagdollImpactMult"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["VerticalRagdollImpactMult"]));
                }
                if (modifications.ContainsKey("RagdollHitArea"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["RagdollHitArea"]));
                }
                if (modifications.ContainsKey("MalfunctionChance"))
                {
                    Marshal.WriteInt32(ptr + 0x214, Convert.ToInt32(modifications["MalfunctionChance"]));
                }
                if (modifications.ContainsKey("MalfunctionEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["MalfunctionEffect"]));
                }
                if (modifications.ContainsKey("SoundOnMalfunction"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["SoundOnMalfunction"]));
                }
                if (modifications.ContainsKey("IsAudibleWhenNotVisible"))
                {
                    Marshal.WriteByte(ptr + 0x228, Convert.ToByte(modifications["IsAudibleWhenNotVisible"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x248, Convert.ToByte(modifications["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x280, Convert.ToByte(modifications["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("AIConfig"))
                {
                    Marshal.WriteInt32(ptr + 0x298, Convert.ToInt32(modifications["AIConfig"]));
                }
                if (modifications.ContainsKey("PerkIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x2A8, Convert.ToInt32(modifications["PerkIcon"]));
                }
            }

            else if (templateType.Name == "PlanetTemplate")
            {
                if (modifications.ContainsKey("PlanetType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["PlanetType"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("TypeName"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["TypeName"]));
                }
                if (modifications.ContainsKey("Tags"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Tags"]));
                }
                if (modifications.ContainsKey("Temperature"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Temperature"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("MoodImage"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["MoodImage"]));
                }
                if (modifications.ContainsKey("ImageOverlayMargin"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["ImageOverlayMargin"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("MaxMenacePresence"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["MaxMenacePresence"]));
                }
                if (modifications.ContainsKey("MenaceDetectedEvent"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["MenaceDetectedEvent"]));
                }
                if (modifications.ContainsKey("OperationSelectScenePrefab"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["OperationSelectScenePrefab"]));
                }
                if (modifications.ContainsKey("MissionSelectPrefab"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["MissionSelectPrefab"]));
                }
                if (modifications.ContainsKey("LocalFaction"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["LocalFaction"]));
                }
            }

            else if (templateType.Name == "PropertyDisplayConfigTemplate")
            {
                if (modifications.ContainsKey("Type"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Type"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("ProgressBarFillColor"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["ProgressBarFillColor"]));
                }
                if (modifications.ContainsKey("ProgressBarPreviewFillColor"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["ProgressBarPreviewFillColor"]));
                }
                if (modifications.ContainsKey("ProgressBarSectionLabels"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["ProgressBarSectionLabels"]));
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
                if (modifications.ContainsKey("CustomGravityForceMode"))
                {
                    Marshal.WriteInt32(ptr + 0x64, Convert.ToInt32(modifications["CustomGravityForceMode"]));
                }
                if (modifications.ContainsKey("DismemberedPartMaxDirectionOffsetInDeg"))
                {
                    Marshal.WriteInt32(ptr + 0x68, Convert.ToInt32(modifications["DismemberedPartMaxDirectionOffsetInDeg"]));
                }
                if (modifications.ContainsKey("DismemberedPartHitForceMultOffset"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["DismemberedPartHitForceMultOffset"]));
                }
                if (modifications.ContainsKey("AdditionalDismemberedPieces"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["AdditionalDismemberedPieces"]));
                }
                if (modifications.ContainsKey("AdditionalDismemberedPieceScale"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["AdditionalDismemberedPieceScale"]));
                }
                if (modifications.ContainsKey("AdditionalDismemberedPieceMaxDirectionOffsetInDeg"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["AdditionalDismemberedPieceMaxDirectionOffsetInDeg"]));
                }
                if (modifications.ContainsKey("AdditionalDismemberedPieceHitForceMultOffset"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["AdditionalDismemberedPieceHitForceMultOffset"]));
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
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
            }

            else if (templateType.Name == "ShipUpgradeSlotTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("UpgradeType"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["UpgradeType"]));
                }
            }

            else if (templateType.Name == "ShipUpgradeTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("UpgradeType"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["UpgradeType"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconInactive"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["IconInactive"]));
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
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("ShortDescription"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["ShortDescription"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["IconDisabled"]));
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
                if (modifications.ContainsKey("IsIgnoringCoverInside"))
                {
                    Marshal.WriteByte(ptr + 0x101, Convert.ToByte(modifications["IsIgnoringCoverInside"]));
                }
                if (modifications.ContainsKey("IsSilent"))
                {
                    Marshal.WriteByte(ptr + 0x102, Convert.ToByte(modifications["IsSilent"]));
                }
                if (modifications.ContainsKey("IgnoreMalfunctionChance"))
                {
                    Marshal.WriteByte(ptr + 0x103, Convert.ToByte(modifications["IgnoreMalfunctionChance"]));
                }
                if (modifications.ContainsKey("IsDeploymentRequired"))
                {
                    Marshal.WriteByte(ptr + 0x104, Convert.ToByte(modifications["IsDeploymentRequired"]));
                }
                if (modifications.ContainsKey("IsWeaponSetupRequired"))
                {
                    Marshal.WriteByte(ptr + 0x105, Convert.ToByte(modifications["IsWeaponSetupRequired"]));
                }
                if (modifications.ContainsKey("IsUsableWhileContained"))
                {
                    Marshal.WriteByte(ptr + 0x106, Convert.ToByte(modifications["IsUsableWhileContained"]));
                }
                if (modifications.ContainsKey("IsUsableWhilePinnedDown"))
                {
                    Marshal.WriteByte(ptr + 0x107, Convert.ToByte(modifications["IsUsableWhilePinnedDown"]));
                }
                if (modifications.ContainsKey("IsStacking"))
                {
                    Marshal.WriteByte(ptr + 0x108, Convert.ToByte(modifications["IsStacking"]));
                }
                if (modifications.ContainsKey("IsSerialized"))
                {
                    Marshal.WriteByte(ptr + 0x109, Convert.ToByte(modifications["IsSerialized"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x10A, Convert.ToByte(modifications["IsRemovedAfterCombat"]));
                }
                if (modifications.ContainsKey("IsRemovedAfterOperation"))
                {
                    Marshal.WriteByte(ptr + 0x10B, Convert.ToByte(modifications["IsRemovedAfterOperation"]));
                }
                if (modifications.ContainsKey("IsHidden"))
                {
                    Marshal.WriteByte(ptr + 0x10C, Convert.ToByte(modifications["IsHidden"]));
                }
                if (modifications.ContainsKey("Shape"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Shape"]));
                }
                if (modifications.ContainsKey("ConeAngle"))
                {
                    Marshal.WriteInt32(ptr + 0x114, Convert.ToInt32(modifications["ConeAngle"]));
                }
                if (modifications.ContainsKey("IsOverridingRanges"))
                {
                    Marshal.WriteByte(ptr + 0x118, Convert.ToByte(modifications["IsOverridingRanges"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x11C, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x124, Convert.ToInt32(modifications["MaxRange"]));
                }
                if (modifications.ContainsKey("Repetitions"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["Repetitions"]));
                }
                if (modifications.ContainsKey("SkipDelayForLastRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x14C, Convert.ToByte(modifications["SkipDelayForLastRepetition"]));
                }
                if (modifications.ContainsKey("IsPlayingAnimationForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x14D, Convert.ToByte(modifications["IsPlayingAnimationForEachRepetition"]));
                }
                if (modifications.ContainsKey("UseCustomAoEShape"))
                {
                    Marshal.WriteByte(ptr + 0x158, Convert.ToByte(modifications["UseCustomAoEShape"]));
                }
                if (modifications.ContainsKey("CustomAoEShape"))
                {
                    Marshal.WriteInt32(ptr + 0x160, Convert.ToInt32(modifications["CustomAoEShape"]));
                }
                if (modifications.ContainsKey("AoEType"))
                {
                    Marshal.WriteInt32(ptr + 0x168, Convert.ToInt32(modifications["AoEType"]));
                }
                if (modifications.ContainsKey("AoEFilter"))
                {
                    Marshal.WriteInt32(ptr + 0x170, Convert.ToInt32(modifications["AoEFilter"]));
                }
                if (modifications.ContainsKey("TargetFaction"))
                {
                    Marshal.WriteInt32(ptr + 0x178, Convert.ToInt32(modifications["TargetFaction"]));
                }
                if (modifications.ContainsKey("AoEChanceToHitCenter"))
                {
                    Marshal.WriteInt32(ptr + 0x17C, Convert.ToInt32(modifications["AoEChanceToHitCenter"]));
                }
                if (modifications.ContainsKey("SelectableTiles"))
                {
                    Marshal.WriteInt32(ptr + 0x180, Convert.ToInt32(modifications["SelectableTiles"]));
                }
                if (modifications.ContainsKey("ScatterMode"))
                {
                    Marshal.WriteInt32(ptr + 0x184, Convert.ToInt32(modifications["ScatterMode"]));
                }
                if (modifications.ContainsKey("Scatter"))
                {
                    Marshal.WriteInt32(ptr + 0x188, Convert.ToInt32(modifications["Scatter"]));
                }
                if (modifications.ContainsKey("ScatterChance"))
                {
                    Marshal.WriteInt32(ptr + 0x18C, Convert.ToInt32(modifications["ScatterChance"]));
                }
                if (modifications.ContainsKey("ScatterHitEachTileOnlyOnce"))
                {
                    Marshal.WriteByte(ptr + 0x190, Convert.ToByte(modifications["ScatterHitEachTileOnlyOnce"]));
                }
                if (modifications.ContainsKey("ScatterHitOnlyValidTiles"))
                {
                    Marshal.WriteByte(ptr + 0x191, Convert.ToByte(modifications["ScatterHitOnlyValidTiles"]));
                }
                if (modifications.ContainsKey("MuzzleType"))
                {
                    Marshal.WriteInt32(ptr + 0x194, Convert.ToInt32(modifications["MuzzleType"]));
                }
                if (modifications.ContainsKey("MuzzleEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x1A0, Convert.ToInt32(modifications["MuzzleEffect"]));
                }
                if (modifications.ContainsKey("MuzzleEffectOverrides2"))
                {
                    Marshal.WriteInt32(ptr + 0x1A8, Convert.ToInt32(modifications["MuzzleEffectOverrides2"]));
                }
                if (modifications.ContainsKey("IsSpawningMuzzleForEachRepetition"))
                {
                    Marshal.WriteByte(ptr + 0x1B0, Convert.ToByte(modifications["IsSpawningMuzzleForEachRepetition"]));
                }
                if (modifications.ContainsKey("IsAttachingMuzzleToTransform"))
                {
                    Marshal.WriteByte(ptr + 0x1B1, Convert.ToByte(modifications["IsAttachingMuzzleToTransform"]));
                }
                if (modifications.ContainsKey("CameraEffectOnFire"))
                {
                    Marshal.WriteInt32(ptr + 0x1B4, Convert.ToInt32(modifications["CameraEffectOnFire"]));
                }
                if (modifications.ContainsKey("ProjectileSpawnPositionOffset"))
                {
                    Marshal.WriteInt32(ptr + 0x1B8, Convert.ToInt32(modifications["ProjectileSpawnPositionOffset"]));
                }
                if (modifications.ContainsKey("ProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1C8, Convert.ToInt32(modifications["ProjectileData"]));
                }
                if (modifications.ContainsKey("SecondaryProjectileData"))
                {
                    Marshal.WriteInt32(ptr + 0x1D0, Convert.ToInt32(modifications["SecondaryProjectileData"]));
                }
                if (modifications.ContainsKey("IsImpactShownOnHit"))
                {
                    Marshal.WriteByte(ptr + 0x1EC, Convert.ToByte(modifications["IsImpactShownOnHit"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnTile"))
                {
                    Marshal.WriteByte(ptr + 0x1ED, Convert.ToByte(modifications["IsImpactCenteredOnTile"]));
                }
                if (modifications.ContainsKey("IsImpactOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x1EE, Convert.ToByte(modifications["IsImpactOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsDecalOnlyOnAOECenterTile"))
                {
                    Marshal.WriteByte(ptr + 0x1EF, Convert.ToByte(modifications["IsDecalOnlyOnAOECenterTile"]));
                }
                if (modifications.ContainsKey("IsImpactCenteredOnExecutingElement"))
                {
                    Marshal.WriteByte(ptr + 0x1F0, Convert.ToByte(modifications["IsImpactCenteredOnExecutingElement"]));
                }
                if (modifications.ContainsKey("IsImpactAlignedToInfantry"))
                {
                    Marshal.WriteByte(ptr + 0x1F1, Convert.ToByte(modifications["IsImpactAlignedToInfantry"]));
                }
                if (modifications.ContainsKey("CameraEffectOnImpact"))
                {
                    Marshal.WriteInt32(ptr + 0x1F4, Convert.ToInt32(modifications["CameraEffectOnImpact"]));
                }
                if (modifications.ContainsKey("CameraEffectOnPlayerHit"))
                {
                    Marshal.WriteInt32(ptr + 0x1F8, Convert.ToInt32(modifications["CameraEffectOnPlayerHit"]));
                }
                if (modifications.ContainsKey("IsTriggeringHeavyDamagedReceivedEffect"))
                {
                    Marshal.WriteByte(ptr + 0x1FC, Convert.ToByte(modifications["IsTriggeringHeavyDamagedReceivedEffect"]));
                }
                if (modifications.ContainsKey("RagdollImpactMult"))
                {
                    Marshal.WriteInt32(ptr + 0x200, Convert.ToInt32(modifications["RagdollImpactMult"]));
                }
                if (modifications.ContainsKey("VerticalRagdollImpactMult"))
                {
                    Marshal.WriteInt32(ptr + 0x208, Convert.ToInt32(modifications["VerticalRagdollImpactMult"]));
                }
                if (modifications.ContainsKey("RagdollHitArea"))
                {
                    Marshal.WriteInt32(ptr + 0x210, Convert.ToInt32(modifications["RagdollHitArea"]));
                }
                if (modifications.ContainsKey("MalfunctionChance"))
                {
                    Marshal.WriteInt32(ptr + 0x214, Convert.ToInt32(modifications["MalfunctionChance"]));
                }
                if (modifications.ContainsKey("MalfunctionEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x218, Convert.ToInt32(modifications["MalfunctionEffect"]));
                }
                if (modifications.ContainsKey("SoundOnMalfunction"))
                {
                    Marshal.WriteInt32(ptr + 0x220, Convert.ToInt32(modifications["SoundOnMalfunction"]));
                }
                if (modifications.ContainsKey("IsAudibleWhenNotVisible"))
                {
                    Marshal.WriteByte(ptr + 0x228, Convert.ToByte(modifications["IsAudibleWhenNotVisible"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x248, Convert.ToByte(modifications["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"))
                {
                    Marshal.WriteByte(ptr + 0x280, Convert.ToByte(modifications["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"]));
                }
                if (modifications.ContainsKey("AIConfig"))
                {
                    Marshal.WriteInt32(ptr + 0x298, Convert.ToInt32(modifications["AIConfig"]));
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
                if (modifications.ContainsKey("NotchFullIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x70, Convert.ToInt32(modifications["NotchFullIcon"]));
                }
                if (modifications.ContainsKey("NotchFullTint"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["NotchFullTint"]));
                }
                if (modifications.ContainsKey("NotchFullDisabledIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["NotchFullDisabledIcon"]));
                }
                if (modifications.ContainsKey("NotchFullDisabledTint"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["NotchFullDisabledTint"]));
                }
                if (modifications.ContainsKey("NotchEmptyIcon"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["NotchEmptyIcon"]));
                }
                if (modifications.ContainsKey("NotchEmptyTint"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["NotchEmptyTint"]));
                }
                if (modifications.ContainsKey("NotchEmptyDisabledIcon"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["NotchEmptyDisabledIcon"]));
                }
                if (modifications.ContainsKey("NotchEmptyDisabledTint"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["NotchEmptyDisabledTint"]));
                }
            }

            else if (templateType.Name == "SpeakerTemplate")
            {
                if (modifications.ContainsKey("Nickname"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Nickname"]));
                }
                if (modifications.ContainsKey("Forename"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Forename"]));
                }
                if (modifications.ContainsKey("Surname"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Surname"]));
                }
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("BarkImage"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["BarkImage"]));
                }
                if (modifications.ContainsKey("OperationSelectImage"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["OperationSelectImage"]));
                }
                if (modifications.ContainsKey("SoundOnTacticalBarkShown"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["SoundOnTacticalBarkShown"]));
                }
                if (modifications.ContainsKey("TacticalBarkSoundDelayInMs"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["TacticalBarkSoundDelayInMs"]));
                }
                if (modifications.ContainsKey("StandLookLeftImage"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["StandLookLeftImage"]));
                }
                if (modifications.ContainsKey("StandLookRightImage"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["StandLookRightImage"]));
                }
                if (modifications.ContainsKey("StandLookRightInactiveImage"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["StandLookRightInactiveImage"]));
                }
            }

            else if (templateType.Name == "SquaddieItemTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("SquaddieNames"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["SquaddieNames"]));
                }
                if (modifications.ContainsKey("SquaddieNicknames"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["SquaddieNicknames"]));
                }
            }

            else if (templateType.Name == "StoryFactionTemplate")
            {
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("TurnOrderIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["TurnOrderIcon"]));
                }
                if (modifications.ContainsKey("TurnOrderInactiveIcon"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["TurnOrderInactiveIcon"]));
                }
                if (modifications.ContainsKey("AlliedFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["AlliedFactionType"]));
                }
                if (modifications.ContainsKey("EnemyFactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["EnemyFactionType"]));
                }
                if (modifications.ContainsKey("ArmyList"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["ArmyList"]));
                }
                if (modifications.ContainsKey("FactionType"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["FactionType"]));
                }
                if (modifications.ContainsKey("Representative"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["Representative"]));
                }
                if (modifications.ContainsKey("FactionWindow"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["FactionWindow"]));
                }
                if (modifications.ContainsKey("SystemMapHUDIcon"))
                {
                    Marshal.WriteInt32(ptr + 0xF0, Convert.ToInt32(modifications["SystemMapHUDIcon"]));
                }
                if (modifications.ContainsKey("OperationIntros"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["OperationIntros"]));
                }
                if (modifications.ContainsKey("InitialStatus"))
                {
                    Marshal.WriteInt32(ptr + 0x100, Convert.ToInt32(modifications["InitialStatus"]));
                }
                if (modifications.ContainsKey("InitialTotalTrust"))
                {
                    Marshal.WriteInt32(ptr + 0x104, Convert.ToInt32(modifications["InitialTotalTrust"]));
                }
            }

            else if (templateType.Name == "StrategicAssetTemplate")
            {
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("IconBig"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["IconBig"]));
                }
                if (modifications.ContainsKey("DisableAfterMission"))
                {
                    Marshal.WriteByte(ptr + 0x90, Convert.ToByte(modifications["DisableAfterMission"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x98, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0xA0, Convert.ToInt32(modifications["Description"]));
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

            else if (templateType.Name == "SurfaceTypeTemplate")
            {
                if (modifications.ContainsKey("SurfaceType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["SurfaceType"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
            }

            else if (templateType.Name == "TagTemplate")
            {
                if (modifications.ContainsKey("TagType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["TagType"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
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
                if (modifications.ContainsKey("SpeakerTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["SpeakerTemplate"]));
                }
                if (modifications.ContainsKey("UnitTitle"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["UnitTitle"]));
                }
                if (modifications.ContainsKey("UnitDescription"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["UnitDescription"]));
                }
                if (modifications.ContainsKey("HiringCosts"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["HiringCosts"]));
                }
                if (modifications.ContainsKey("HiringSelectBarkSound"))
                {
                    Marshal.WriteInt32(ptr + 0x94, Convert.ToInt32(modifications["HiringSelectBarkSound"]));
                }
                if (modifications.ContainsKey("HiredBarkSound"))
                {
                    Marshal.WriteInt32(ptr + 0x9C, Convert.ToInt32(modifications["HiredBarkSound"]));
                }
                if (modifications.ContainsKey("Gender"))
                {
                    Marshal.WriteInt32(ptr + 0xA4, Convert.ToInt32(modifications["Gender"]));
                }
                if (modifications.ContainsKey("SkinColor"))
                {
                    Marshal.WriteInt32(ptr + 0xA5, Convert.ToInt32(modifications["SkinColor"]));
                }
                if (modifications.ContainsKey("CustomHead"))
                {
                    Marshal.WriteInt32(ptr + 0xA8, Convert.ToInt32(modifications["CustomHead"]));
                }
                if (modifications.ContainsKey("UnitActorType"))
                {
                    Marshal.WriteInt32(ptr + 0xB0, Convert.ToInt32(modifications["UnitActorType"]));
                }
                if (modifications.ContainsKey("InfantryUnitTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["InfantryUnitTemplate"]));
                }
                if (modifications.ContainsKey("PilotInventoryTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["PilotInventoryTemplate"]));
                }
                if (modifications.ContainsKey("InitialVehicleItem"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["InitialVehicleItem"]));
                }
                if (modifications.ContainsKey("Slot"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["Slot"]));
                }
                if (modifications.ContainsKey("SlotInactive"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["SlotInactive"]));
                }
                if (modifications.ContainsKey("SlotInjured"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["SlotInjured"]));
                }
                if (modifications.ContainsKey("SlotFactionBackground"))
                {
                    Marshal.WriteInt32(ptr + 0xE8, Convert.ToInt32(modifications["SlotFactionBackground"]));
                }
                if (modifications.ContainsKey("SlotBadge"))
                {
                    Marshal.WriteInt32(ptr + 0xF0, Convert.ToInt32(modifications["SlotBadge"]));
                }
                if (modifications.ContainsKey("BigBackground"))
                {
                    Marshal.WriteInt32(ptr + 0xF8, Convert.ToInt32(modifications["BigBackground"]));
                }
                if (modifications.ContainsKey("FactionBackground"))
                {
                    Marshal.WriteInt32(ptr + 0x100, Convert.ToInt32(modifications["FactionBackground"]));
                }
                if (modifications.ContainsKey("BadgeMini"))
                {
                    Marshal.WriteInt32(ptr + 0x108, Convert.ToInt32(modifications["BadgeMini"]));
                }
                if (modifications.ContainsKey("BadgeDragged"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["BadgeDragged"]));
                }
                if (modifications.ContainsKey("BadgeUnitWindow"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["BadgeUnitWindow"]));
                }
                if (modifications.ContainsKey("Badge"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["Badge"]));
                }
                if (modifications.ContainsKey("BadgeWhite"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["BadgeWhite"]));
                }
                if (modifications.ContainsKey("BigBadge"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["BigBadge"]));
                }
                if (modifications.ContainsKey("Rarity"))
                {
                    Marshal.WriteInt32(ptr + 0x138, Convert.ToInt32(modifications["Rarity"]));
                }
                if (modifications.ContainsKey("MinCampaignProgress"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, Convert.ToInt32(modifications["MinCampaignProgress"]));
                }
                if (modifications.ContainsKey("InitialPerk"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["InitialPerk"]));
                }
            }

            else if (templateType.Name == "UnitRankTemplate")
            {
                if (modifications.ContainsKey("RankType"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["RankType"]));
                }
                if (modifications.ContainsKey("Name"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["Name"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Icon"]));
                }
                if (modifications.ContainsKey("PromotionCost"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["PromotionCost"]));
                }
            }

            else if (templateType.Name == "VehicleItemTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("IconEquipment"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IconEquipment"]));
                }
                if (modifications.ContainsKey("IconEquipmentDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["IconEquipmentDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBar"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["IconSkillBar"]));
                }
                if (modifications.ContainsKey("IconSkillBarDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["IconSkillBarDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternative"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["IconSkillBarAlternative"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternativeDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["IconSkillBarAlternativeDisabled"]));
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
                if (modifications.ContainsKey("IsDestroyedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsDestroyedAfterCombat"]));
                }
                if (modifications.ContainsKey("Model"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Model"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("ModelSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ModelSecondary"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x12C, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("EntityTemplate"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["EntityTemplate"]));
                }
                if (modifications.ContainsKey("AccessorySlots"))
                {
                    Marshal.WriteInt32(ptr + 0x138, Convert.ToInt32(modifications["AccessorySlots"]));
                }
            }

            else if (templateType.Name == "VoucherTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("ShortName"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["ShortName"]));
                }
                if (modifications.ContainsKey("Description"))
                {
                    Marshal.WriteInt32(ptr + 0x88, Convert.ToInt32(modifications["Description"]));
                }
                if (modifications.ContainsKey("Icon"))
                {
                    Marshal.WriteInt32(ptr + 0x90, Convert.ToInt32(modifications["Icon"]));
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
                if (modifications.ContainsKey("IconEquipment"))
                {
                    Marshal.WriteInt32(ptr + 0xB8, Convert.ToInt32(modifications["IconEquipment"]));
                }
                if (modifications.ContainsKey("IconEquipmentDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xC0, Convert.ToInt32(modifications["IconEquipmentDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBar"))
                {
                    Marshal.WriteInt32(ptr + 0xC8, Convert.ToInt32(modifications["IconSkillBar"]));
                }
                if (modifications.ContainsKey("IconSkillBarDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xD0, Convert.ToInt32(modifications["IconSkillBarDisabled"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternative"))
                {
                    Marshal.WriteInt32(ptr + 0xD8, Convert.ToInt32(modifications["IconSkillBarAlternative"]));
                }
                if (modifications.ContainsKey("IconSkillBarAlternativeDisabled"))
                {
                    Marshal.WriteInt32(ptr + 0xE0, Convert.ToInt32(modifications["IconSkillBarAlternativeDisabled"]));
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
                if (modifications.ContainsKey("IsDestroyedAfterCombat"))
                {
                    Marshal.WriteByte(ptr + 0x100, Convert.ToByte(modifications["IsDestroyedAfterCombat"]));
                }
                if (modifications.ContainsKey("Model"))
                {
                    Marshal.WriteInt32(ptr + 0x110, Convert.ToInt32(modifications["Model"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlot"))
                {
                    Marshal.WriteInt32(ptr + 0x118, Convert.ToInt32(modifications["VisualAlterationSlot"]));
                }
                if (modifications.ContainsKey("ModelSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x120, Convert.ToInt32(modifications["ModelSecondary"]));
                }
                if (modifications.ContainsKey("VisualAlterationSlotSecondary"))
                {
                    Marshal.WriteInt32(ptr + 0x128, Convert.ToInt32(modifications["VisualAlterationSlotSecondary"]));
                }
                if (modifications.ContainsKey("AttachLightAtNight"))
                {
                    Marshal.WriteByte(ptr + 0x12C, Convert.ToByte(modifications["AttachLightAtNight"]));
                }
                if (modifications.ContainsKey("AnimType"))
                {
                    Marshal.WriteInt32(ptr + 0x130, Convert.ToInt32(modifications["AnimType"]));
                }
                if (modifications.ContainsKey("AnimSize"))
                {
                    Marshal.WriteInt32(ptr + 0x134, Convert.ToInt32(modifications["AnimSize"]));
                }
                if (modifications.ContainsKey("AnimGrip"))
                {
                    Marshal.WriteInt32(ptr + 0x138, Convert.ToInt32(modifications["AnimGrip"]));
                }
                if (modifications.ContainsKey("MinRange"))
                {
                    Marshal.WriteInt32(ptr + 0x13C, Convert.ToInt32(modifications["MinRange"]));
                }
                if (modifications.ContainsKey("IdealRange"))
                {
                    Marshal.WriteInt32(ptr + 0x140, Convert.ToInt32(modifications["IdealRange"]));
                }
                if (modifications.ContainsKey("MaxRange"))
                {
                    Marshal.WriteInt32(ptr + 0x144, Convert.ToInt32(modifications["MaxRange"]));
                }
            }

            else if (templateType.Name == "WeatherTemplate")
            {
                if (modifications.ContainsKey("Title"))
                {
                    Marshal.WriteInt32(ptr + 0x78, Convert.ToInt32(modifications["Title"]));
                }
                if (modifications.ContainsKey("CameraEffect"))
                {
                    Marshal.WriteInt32(ptr + 0x80, Convert.ToInt32(modifications["CameraEffect"]));
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
                if (modifications.ContainsKey("m_direction"))
                {
                    Marshal.WriteInt32(ptr + 0x28, Convert.ToInt32(modifications["m_direction"]));
                }
                if (modifications.ContainsKey("m_windDirection"))
                {
                    Marshal.WriteInt32(ptr + 0x38, Convert.ToInt32(modifications["m_windDirection"]));
                }
            }
            else
            {
                LoggerInstance.Warning($"Unknown template type for injection: {templateType.Name}");
            }

    LoggerInstance.Msg($"Applied modifications to {obj.name} ({templateType.Name})");
}