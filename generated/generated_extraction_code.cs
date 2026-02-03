// Auto-generated template extraction code
// Generated from IL2CPP dump.cs

private object ExtractTemplateDataDirect(UnityEngine.Object obj, Type templateType)
{
    var data = new Dictionary<string, object>();

    // Get IL2CPP pointer from the object
    IntPtr ptr = IntPtr.Zero;
    if (obj is Il2CppObjectBase il2cppObj)
    {
        ptr = il2cppObj.Pointer;
    }
    else
    {
        data["name"] = $"ERROR: Object is not Il2CppObjectBase, type is {obj.GetType().Name}";
        return data;
    }

    data["name"] = obj.name;

    // Template-specific extraction
            if (templateType.Name == "AIWeightsTemplate")
            {
                data["BehaviorScorePOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x18)), 0);
                data["TTL_MAX"] = Marshal.ReadInt32(ptr + 0x1C);
                data["UtilityPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x20)), 0);
                data["UtilityScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x24)), 0);
                data["UtilityPostPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x28)), 0);
                data["UtilityPostScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2C)), 0);
                data["SafetyPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x30)), 0);
                data["SafetyScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x34)), 0);
                data["SafetyPostPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x38)), 0);
                data["SafetyPostScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x3C)), 0);
                data["DistanceScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x40)), 0);
                data["DistancePickScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x44)), 0);
                data["ThreatLevelPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x48)), 0);
                data["OpportunityLevelPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x4C)), 0);
                data["PickingScoreMultPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x50)), 0);
                data["DistanceToCurrentTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x54)), 0);
                data["DistanceToZones"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x58)), 0);
                data["DistanceToAdvanceZones"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x5C)), 0);
                data["SafetyOutsideDefendZones"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x60)), 0);
                data["SafetyOutsideDefendZonesVehicles"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x64)), 0);
                data["OccupyZoneValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x68)), 0);
                data["CaptureZoneValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x6C)), 0);
                data["CoverAgainstOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x70)), 0);
                data["ThreatFromOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x74)), 0);
                data["ThreatFromUnknownOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x78)), 0);
                data["ThreatFromTileEffects"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x7C)), 0);
                data["ThreatFromOpponentsDamage"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x80)), 0);
                data["ThreatFromOpponentsArmorDamage"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x84)), 0);
                data["ThreatFromOpponentsSuppression"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x88)), 0);
                data["ThreatFromOpponentsStun"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x8C)), 0);
                data["ThreatFromPinnedDownOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x90)), 0);
                data["ThreatFromSuppressedOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x94)), 0);
                data["ThreatFrom2xStunnedOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x98)), 0);
                data["ThreatFromFleeingOpponents"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x9C)), 0);
                data["ThreatFromOpponentsAlreadyActed"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xA0)), 0);
                data["ThreatFromOpponentsStaggered"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xA4)), 0);
                data["ThreatFromOpponentsButAlliesInControl"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xA8)), 0);
                data["ThreatFromOpponentsAtHypotheticalPositionsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xAC)), 0);
                data["AllyMetascoreAgainstThreshold"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xB0)), 0);
                data["AvoidAlliesPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xB4)), 0);
                data["AvoidOpponentsPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xB8)), 0);
                data["FleeFromOpponentsPOW"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xBC)), 0);
                data["ScalePositionWithTags"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xC0)), 0);
                data["IncludeAttacksAgainstAllOpponentsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xC4)), 0);
                data["OppositeSideDistanceFromOpponentCap"] = Marshal.ReadInt32(ptr + 0xC8);
                data["CullTilesDistances"] = Marshal.ReadInt32(ptr + 0xCC);
                data["DistanceToZoneDeployScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD0)), 0);
                data["DistanceToAlliesScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD4)), 0);
                data["CoverInEachDirectionBonus"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD8)), 0);
                data["InsideBuildingDuringDeployment"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xDC)), 0);
                data["DeploymentConcealmentMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xE0)), 0);
                data["InvisibleTargetValueMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xE4)), 0);
                data["TargetValueDamageScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xE8)), 0);
                data["TargetValueArmorScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xEC)), 0);
                data["TargetValueSuppressionScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xF0)), 0);
                data["TargetValueStunScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xF4)), 0);
                data["TargetValueThreatScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xF8)), 0);
                data["TargetValueMaxThreatSuppressScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xFC)), 0);
                data["ScoreThresholdWithLimitedUses"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x100)), 0);
                data["FriendlyFirePenalty"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x104)), 0);
                data["DamageBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x108)), 0);
                data["DamageScoreMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x10C)), 0);
                data["InflictDamageFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x110)), 0);
                data["SuppressionBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x114)), 0);
                data["SuppressionScoreMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x118)), 0);
                data["InflictSuppressionFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x11C)), 0);
                data["StunBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x120)), 0);
                data["StunScoreMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x124)), 0);
                data["StunFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x128)), 0);
                data["MoveBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x12C)), 0);
                data["MoveScoreMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x130)), 0);
                data["NearTileLimit"] = Marshal.ReadInt32(ptr + 0x134);
                data["TileScoreDifferenceMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x138)), 0);
                data["TileScoreDifferencePow"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x13C)), 0);
                data["UtilityThreshold"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x140)), 0);
                data["PathfindingSafetyCostMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x144)), 0);
                data["PathfindingUnknownTileSafety"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x148)), 0);
                data["PathfindingHiddenFromOpponentsBonus"] = Marshal.ReadInt32(ptr + 0x14C);
                data["EntirePathScoreContribution"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x150)), 0);
                data["MoveIfNewTileIsBetterBy"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x154)), 0);
                data["GetUpIfNewTileIsBetterBy"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x158)), 0);
                data["DistanceTooFarForOneTurnMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x15C)), 0);
                data["ConsiderAlternativeIfBetterBy"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x160)), 0);
                data["ConsiderAlternativeToUltimateIfBetterBy"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x164)), 0);
                data["EnoughAPToPerformSkillAfterwards"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x168)), 0);
                data["EnoughAPToPerformOnlySkillAfterwards"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x16C)), 0);
                data["EnoughAPToDeployAfterwards"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x170)), 0);
                data["BuffBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x174)), 0);
                data["BuffTargetValueMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x178)), 0);
                data["BuffFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x17C)), 0);
                data["RemoveSuppressionMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x180)), 0);
                data["RemoveStunnedMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x184)), 0);
                data["RestoreMoraleMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x188)), 0);
                data["IncreaseMovementMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x18C)), 0);
                data["IncreaseOffensiveStatsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x190)), 0);
                data["IncreaseDefensiveStatsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x194)), 0);
                data["SupplyAmmoBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x198)), 0);
                data["SupplyAmmoTargetValueMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x19C)), 0);
                data["SupplyAmmoNoAmmoMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1A0)), 0);
                data["SupplyAmmoSpecialWeaponMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1A4)), 0);
                data["SupplyAmmoGoalThreshold"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1A8)), 0);
                data["SupplyAmmoFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1AC)), 0);
                data["TargetDesignatorBaseScore"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B0)), 0);
                data["TargetDesignatorScoreMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B4)), 0);
                data["TargetDesignatorFromTile"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B8)), 0);
                data["GainBonusTurnBaseMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1BC)), 0);
                data["TestValueInt"] = Marshal.ReadInt32(ptr + 0x1C0);
                data["TestValueFloat"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1C4)), 0);
            }

            else if (templateType.Name == "AccessoryTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // Sprite reference
                data["IconEquipment"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Sprite reference
                data["IconEquipmentDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // Sprite reference
                data["IconSkillBar"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["IconSkillBarDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["IconSkillBarAlternative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["IconSkillBarAlternativeDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // ItemSlot
                data["SlotType"] = Marshal.ReadInt32(ptr + 0xE8);
                // ItemType
                data["ItemType"] = Marshal.ReadInt32(ptr + 0xEC);
                // TODO: Array/List - OnlyEquipableBy: List<TagTemplate>
                // ExclusiveItemCategory
                data["ExclusiveCategory"] = Marshal.ReadInt32(ptr + 0xF8);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xFC);
                data["IsDestroyedAfterCombat"] = Marshal.ReadByte(ptr + 0x100) != 0;
                // TODO: Array/List - SkillsGranted: List<SkillTemplate>
                // GameObject reference
                data["Model"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // VisualAlterationSlot
                data["VisualAlterationSlot"] = Marshal.ReadInt32(ptr + 0x118);
                // GameObject reference
                data["ModelSecondary"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // VisualAlterationSlot
                data["VisualAlterationSlotSecondary"] = Marshal.ReadInt32(ptr + 0x128);
                data["AttachLightAtNight"] = Marshal.ReadByte(ptr + 0x12C) != 0;
            }

            else if (templateType.Name == "AnimationSequenceTemplate")
            {
                // GameObject reference
                data["Prefab"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                data["HasRandomRotation"] = Marshal.ReadByte(ptr + 0x80) != 0;
                data["MinRandomAngle"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x84)), 0);
                data["MaxRandomAngle"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x88)), 0);
                // TODO: Array/List - Phases: BaseAnimationPhase[]
            }

            else if (templateType.Name == "AnimationSoundTemplate")
            {
                // TODO: Array/List - SoundTriggers: AnimationSoundTemplate.SoundTrigger[]
            }

            else if (templateType.Name == "AnimatorParameterNameTemplate")
            {
                data["ParameterName"] = // TODO: String reading;
                // AnimatorParameterType
                data["ParameterType"] = Marshal.ReadInt32(ptr + 0x60);
            }

            else if (templateType.Name == "ArmorTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // Sprite reference
                data["IconEquipment"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Sprite reference
                data["IconEquipmentDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // Sprite reference
                data["IconSkillBar"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["IconSkillBarDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["IconSkillBarAlternative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["IconSkillBarAlternativeDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // ItemSlot
                data["SlotType"] = Marshal.ReadInt32(ptr + 0xE8);
                // ItemType
                data["ItemType"] = Marshal.ReadInt32(ptr + 0xEC);
                // TODO: Array/List - OnlyEquipableBy: List<TagTemplate>
                // ExclusiveItemCategory
                data["ExclusiveCategory"] = Marshal.ReadInt32(ptr + 0xF8);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xFC);
                data["IsDestroyedAfterCombat"] = Marshal.ReadByte(ptr + 0x100) != 0;
                // TODO: Array/List - SkillsGranted: List<SkillTemplate>
                // GameObject reference
                data["Model"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // VisualAlterationSlot
                data["VisualAlterationSlot"] = Marshal.ReadInt32(ptr + 0x118);
                // GameObject reference
                data["ModelSecondary"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // VisualAlterationSlot
                data["VisualAlterationSlotSecondary"] = Marshal.ReadInt32(ptr + 0x128);
                data["AttachLightAtNight"] = Marshal.ReadByte(ptr + 0x12C) != 0;
                data["HasSpecialFemaleModels"] = Marshal.ReadByte(ptr + 0x130) != 0;
                // TODO: Array/List - MaleModels: GameObject[]
                // TODO: Array/List - FemaleModels: GameObject[]
                // SquadLeaderModelMode
                data["SquadLeaderMode"] = Marshal.ReadInt32(ptr + 0x148);
                // GameObject reference
                data["SquadLeaderModelMaleWhite"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x150));
                // GameObject reference
                data["SquadLeaderModelMaleBrown"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x158));
                // GameObject reference
                data["SquadLeaderModelMaleBlack"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x160));
                // GameObject reference
                data["SquadLeaderModelFemaleWhite"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x168));
                // GameObject reference
                data["SquadLeaderModelFemaleBrown"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x170));
                // GameObject reference
                data["SquadLeaderModelFemaleBlack"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x178));
                // GameObject reference
                data["SquadLeaderModelFixed"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x180));
                data["OverrideScale"] = Marshal.ReadByte(ptr + 0x188) != 0;
                // Vector2
                data["Scale"] = Marshal.ReadInt32(ptr + 0x18C);
                // AnimArmorSize
                data["AnimSize"] = Marshal.ReadInt32(ptr + 0x194);
                data["Armor"] = Marshal.ReadInt32(ptr + 0x198);
                data["DurabilityPerElement"] = Marshal.ReadInt32(ptr + 0x19C);
                data["DamageResistance"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1A0)), 0);
                data["HitpointsPerElement"] = Marshal.ReadInt32(ptr + 0x1A4);
                data["HitpointsPerElementMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1A8)), 0);
                data["Accuracy"] = Marshal.ReadInt32(ptr + 0x1AC);
                data["AccuracyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B0)), 0);
                data["DefenseMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B4)), 0);
                data["Discipline"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B8)), 0);
                data["DisciplineMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1BC)), 0);
                data["Vision"] = Marshal.ReadInt32(ptr + 0x1C0);
                data["VisionMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1C4)), 0);
                data["Detection"] = Marshal.ReadInt32(ptr + 0x1C8);
                data["DetectionMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1CC)), 0);
                data["Concealment"] = Marshal.ReadInt32(ptr + 0x1D0);
                data["ConcealmentMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1D4)), 0);
                data["SuppressionImpactMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1D8)), 0);
                data["GetDismemberedChanceBonus"] = Marshal.ReadInt32(ptr + 0x1DC);
                data["GetDismemberedChanceMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E0)), 0);
                data["ActionPoints"] = Marshal.ReadInt32(ptr + 0x1E4);
                data["ActionPointsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E8)), 0);
                data["AdditionalMovementCost"] = Marshal.ReadInt32(ptr + 0x1EC);
                // TODO: Array/List - ItemSlots: uint[]
                // ID
                data["SoundOnMovementStep"] = Marshal.ReadInt32(ptr + 0x1F8);
                // SurfaceSoundsTemplate reference
                data["SoundOnMovementStepOverrides2"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x200));
                // ID
                data["SoundOnMovementSymbolic"] = Marshal.ReadInt32(ptr + 0x208);
                // ID
                data["SoundOnArmorHit"] = Marshal.ReadInt32(ptr + 0x210);
                // ID
                data["SoundOnHitpointsHit"] = Marshal.ReadInt32(ptr + 0x218);
                // ID
                data["SoundOnHitpointsHitFemale"] = Marshal.ReadInt32(ptr + 0x220);
                // ID
                data["SoundOnDeath"] = Marshal.ReadInt32(ptr + 0x228);
                // ID
                data["SoundOnDeathFemale"] = Marshal.ReadInt32(ptr + 0x230);
            }

            else if (templateType.Name == "ArmyListTemplate")
            {
                // TODO: Array/List - Compositions: List<Army>
            }

            else if (templateType.Name == "BiomeTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // BiomeType
                data["BiomeType"] = Marshal.ReadInt32(ptr + 0x80);
                data["ShowInCheatMenu"] = Marshal.ReadByte(ptr + 0x84) != 0;
                // TODO: Array/List - Graphs: Graph[]
                // TODO: Array/List - MapgenTemplates: List<MissionMapgenTemplateList>
                // Material reference
                data["Material"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // MicroSplatPropData
                data["PropData"] = Marshal.ReadInt32(ptr + 0xA0);
                // TextureArrayConfig
                data["TextureArray"] = Marshal.ReadInt32(ptr + 0xA8);
                // PhysicsMaterial
                data["PhysicMaterial"] = Marshal.ReadInt32(ptr + 0xB0);
                // GameObject reference
                data["WindZone"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                data["HasGrass"] = Marshal.ReadByte(ptr + 0xC0) != 0;
                // LightConditions
                data["LightConditions"] = Marshal.ReadInt32(ptr + 0xC8);
                // TODO: Array/List - WeatherChances: WeatherEntry[]
            }

            else if (templateType.Name == "BoolPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // BoolPlayerSetting
                data["Type"] = Marshal.ReadInt32(ptr + 0x88);
                data["DefaultValue"] = Marshal.ReadByte(ptr + 0x8C) != 0;
            }

            else if (templateType.Name == "ChunkTemplate")
            {
                data["Width"] = Marshal.ReadInt32(ptr + 0x58);
                data["Height"] = Marshal.ReadInt32(ptr + 0x5C);
                // CoverConfig
                data["CoverConfig"] = Marshal.ReadInt32(ptr + 0x60);
                // ChunkType
                data["Type"] = Marshal.ReadInt32(ptr + 0x68);
                // TODO: Array/List - FixedChildren: FixedChunkEntry[]
                // TODO: Array/List - RandomChildren: RandomChunkEntry[]
                // TODO: Array/List - FixedPrefabs: FixedPrefabEntry[]
                // ChunkSpawnMode
                data["SpawnMode"] = Marshal.ReadInt32(ptr + 0x88);
                data["MaxSpawns"] = Marshal.ReadInt32(ptr + 0x8C);
                data["RandomlyRotateChildren"] = Marshal.ReadByte(ptr + 0x90) != 0;
            }

            else if (templateType.Name == "CommodityTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
            }

            else if (templateType.Name == "ConversationEffectsTemplate")
            {
                // TODO: Array/List - Effects: BaseGameEffect[]
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
            }

            else if (templateType.Name == "ConversationStageTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // Texture2D reference
                data["BackgroundImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
            }

            else if (templateType.Name == "ConversationTemplate")
            {
                // ConversationType
                data["ConversationType"] = Marshal.ReadInt32(ptr + 0x18);
                data["Active"] = Marshal.ReadByte(ptr + 0x1C) != 0;
                // LocaState
                data["LocaState"] = Marshal.ReadInt32(ptr + 0x20);
                data["Comment"] = // TODO: String reading;
                // TODO: Array/List - EventSettings: List<EventData>
                // ConversationTriggerTagType
                data["TriggerTag"] = Marshal.ReadInt32(ptr + 0x38);
                data["Path"] = // TODO: String reading;
                // ConversationStageTemplate reference
                data["Stage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x48));
                // ConversationCondition
                data["Condition"] = Marshal.ReadInt32(ptr + 0x50);
                data["Repeatable"] = Marshal.ReadByte(ptr + 0x58) != 0;
                data["Repetitions"] = Marshal.ReadInt32(ptr + 0x5C);
                data["Priority"] = Marshal.ReadInt32(ptr + 0x60);
                data["PlayChance"] = Marshal.ReadInt32(ptr + 0x64);
                // TODO: Array/List - Roles: List<Role>
                // TODO: Array/List - Triggers: List<ConversationTriggerType>
                // ConversationNodeContainer
                data["Nodes"] = Marshal.ReadInt32(ptr + 0x78);
                data["Version"] = Marshal.ReadInt32(ptr + 0x80);
            }

            else if (templateType.Name == "DecalTemplate")
            {
                data["Index"] = Marshal.ReadInt32(ptr + 0x10);
                data["Name"] = // TODO: String reading;
                data["MinSize"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x20)), 0);
                data["MaxSize"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x24)), 0);
                data["MinRotation"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x28)), 0);
                data["MaxRotation"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2C)), 0);
            }

            else if (templateType.Name == "DefectTemplate")
            {
                // SkillTemplate reference
                data["DamageEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x58));
                // DefectSeverity
                data["Severity"] = Marshal.ReadInt32(ptr + 0x60);
                data["Chance"] = Marshal.ReadInt32(ptr + 0x64);
                // TODO: Array/List - DisqualifierConditions: ITacticalCondition[]
                // TODO: Complex type - SkillsRemoved: HashSet<SkillTemplate>
            }

            else if (templateType.Name == "DisplayIndexPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // IntPlayerSetting
                data["Type"] = Marshal.ReadInt32(ptr + 0x88);
                data["DefaultValue"] = Marshal.ReadInt32(ptr + 0x8C);
                data["MinValue"] = Marshal.ReadInt32(ptr + 0x90);
                // LocalizedLine reference
                data["Measure"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
            }

            else if (templateType.Name == "ElementAnimatorTemplate")
            {
                data["SpeedBlendTime"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x58)), 0);
                data["StanceDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x5C)), 0);
                data["DisableDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x60)), 0);
                data["MovementStanceDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x64)), 0);
                // Vector2
                data["MovementDelayPerElement"] = Marshal.ReadInt32(ptr + 0x68);
                data["UnderAttackResetDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x70)), 0);
                // Vector2
                data["InitialAimDelay"] = Marshal.ReadInt32(ptr + 0x74);
                // AnimationCurve
                data["AnimatorInPlaceTurningSpeedCurve"] = Marshal.ReadInt32(ptr + 0x80);
                // AnimatorDeathBehaviour
                data["DeathBehaviour"] = Marshal.ReadInt32(ptr + 0x88);
                // Vector3
                data["AdditionalRagdollKillImpulse"] = Marshal.ReadInt32(ptr + 0x8C);
                // RagdollHitArea
                data["AdditionalRagdollKillImpulseArea"] = Marshal.ReadInt32(ptr + 0x98);
                // Vector2Int
                data["DeathAnimationVariants"] = Marshal.ReadInt32(ptr + 0x9C);
                data["HitAnimations"] = Marshal.ReadByte(ptr + 0xA4) != 0;
                // AnimationCurve
                data["DmgToHitAnimationStrength"] = Marshal.ReadInt32(ptr + 0xA8);
                data["RecoilOnHit"] = Marshal.ReadByte(ptr + 0xB0) != 0;
                data["DisableAttachmentAnimatorsOnDeath"] = Marshal.ReadByte(ptr + 0xB1) != 0;
                data["ExhaustEffects"] = Marshal.ReadByte(ptr + 0xB2) != 0;
                data["HumanIK"] = Marshal.ReadByte(ptr + 0xB3) != 0;
                data["LeftHandIKBlendTime"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xB4)), 0);
                // Vector3
                data["IKHintLeftElbowOffset"] = Marshal.ReadInt32(ptr + 0xB8);
                data["NegativeSpeedTurns"] = Marshal.ReadByte(ptr + 0xC4) != 0;
                data["SteeringDirection"] = Marshal.ReadByte(ptr + 0xC5) != 0;
                data["MaxClampSteeringAngle"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xC8)), 0);
                data["Aiming"] = Marshal.ReadByte(ptr + 0xCC) != 0;
                data["AimSpeed"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD0)), 0);
                data["TurnDelay180Degree"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD4)), 0);
                data["UseRootMotionAiming"] = Marshal.ReadByte(ptr + 0xD8) != 0;
                // AnimatorAngleMapping
                data["AngleMapping"] = Marshal.ReadInt32(ptr + 0xDC);
            }

            else if (templateType.Name == "EmotionalStateTemplate")
            {
                // EmotionalStateType
                data["StateType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedLine reference
                data["TooltipTitle"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // SkillTemplate reference
                data["Effect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
                // Sprite reference
                data["IconBig"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                // Color
                data["IconTint"] = Marshal.ReadInt32(ptr + 0xB0);
                // Vector2Int
                data["DurationInMissions"] = Marshal.ReadInt32(ptr + 0xC0);
                // EmotionalStateCategory
                data["Category"] = Marshal.ReadInt32(ptr + 0xC8);
                data["IsPositive"] = Marshal.ReadByte(ptr + 0xCC) != 0;
                data["IsSuperState"] = Marshal.ReadByte(ptr + 0xCD) != 0;
                // EmotionalStateTemplate reference
                data["SuperState"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
            }

            else if (templateType.Name == "EnemyAssetTemplate")
            {
                // TODO: Array/List - Effects: BaseGameEffect[]
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["IconBig"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                data["DisableAfterMission"] = Marshal.ReadByte(ptr + 0x90) != 0;
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
            }

            else if (templateType.Name == "EntityTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // EntityType
                data["Type"] = Marshal.ReadInt32(ptr + 0x88);
                // ActorType
                data["ActorType"] = Marshal.ReadInt32(ptr + 0x8C);
                // StructureType
                data["StructureType"] = Marshal.ReadInt32(ptr + 0x90);
                // SurfaceType
                data["SurfaceType"] = Marshal.ReadInt32(ptr + 0x94);
                // TODO: Array/List - Tags: List<TagTemplate>
                data["ElementsMin"] = Marshal.ReadInt32(ptr + 0xA0);
                data["ElementsMax"] = Marshal.ReadInt32(ptr + 0xA4);
                data["ChanceForFemaleElements"] = Marshal.ReadInt32(ptr + 0xA8);
                // OperationResources
                data["DeployCostsPerElement"] = Marshal.ReadInt32(ptr + 0xAC);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xB0);
                data["ArmyPointCost"] = Marshal.ReadInt32(ptr + 0xB4);
                // CoverType
                data["ProvidesCover"] = Marshal.ReadInt32(ptr + 0xB8);
                data["ProvidesCoverWhenDestroyed"] = Marshal.ReadByte(ptr + 0xBC) != 0;
                // UseCoverType
                data["UsesCover"] = Marshal.ReadInt32(ptr + 0xC0);
                data["IsContainableInEntities"] = Marshal.ReadByte(ptr + 0xC4) != 0;
                // EntityContainerType
                data["ContainerType"] = Marshal.ReadInt32(ptr + 0xC8);
                // EntityTemplate reference
                data["ContainedEntityOnSpawn"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                data["DespawnIfEmpty"] = Marshal.ReadByte(ptr + 0xD8) != 0;
                // InsideCoverTemplate reference
                data["ProvidesCoverInside"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // SkillTemplate reference
                data["EffectOnContained"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE8));
                data["IsIgnoredInActorCount"] = Marshal.ReadByte(ptr + 0xF0) != 0;
                data["IsDestructible"] = Marshal.ReadByte(ptr + 0xF1) != 0;
                data["IsSurfaceChangedOnDeath"] = Marshal.ReadByte(ptr + 0xF2) != 0;
                // SurfaceType
                data["ChangeSurfaceOnDeath"] = Marshal.ReadInt32(ptr + 0xF4);
                data["IsTraversableByInfantry"] = Marshal.ReadByte(ptr + 0xF8) != 0;
                data["IsAffectedByFatalities"] = Marshal.ReadByte(ptr + 0xF9) != 0;
                data["DestroyPropsOnDeath"] = Marshal.ReadByte(ptr + 0xFA) != 0;
                data["DestroyPropsRadius"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xFC)), 0);
                // TODO: Array/List - DefectGroups: List<DefectGroup>
                // SpeakerTemplate reference
                data["SpeakerTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x108));
                // AnimationSoundTemplate reference
                data["AnimationSoundTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                data["IsAligningWithTerrain"] = Marshal.ReadByte(ptr + 0x118) != 0;
                data["FixedRotationForDestroyedPrefab"] = Marshal.ReadByte(ptr + 0x119) != 0;
                data["IsCompatibleWithCables"] = Marshal.ReadByte(ptr + 0x11A) != 0;
                data["HasExtendedRangeForCables"] = Marshal.ReadByte(ptr + 0x11B) != 0;
                data["CameraAutoHeightOffset"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x11C)), 0);
                data["OverrideMissionPreviewColor"] = Marshal.ReadByte(ptr + 0x120) != 0;
                // Color
                data["MissionPreviewColorOverride"] = Marshal.ReadInt32(ptr + 0x124);
                // TODO: Array/List - Prefabs: List<GameObject>
                // TODO: Array/List - Decoration: List<PrefabListTemplate>
                // TODO: Array/List - SmallDecoration: List<PrefabListTemplate>
                // TODO: Array/List - DestroyedPrefabs: List<GameObject>
                // TODO: Array/List - DestroyedDecoration: List<PrefabListTemplate>
                // TODO: Array/List - DestroyedWalls: List<GameObject>
                // Vector2
                data["Scale"] = Marshal.ReadInt32(ptr + 0x168);
                data["OverrideScaleForSquadLeader"] = Marshal.ReadByte(ptr + 0x170) != 0;
                data["ScaleOffsetSquadLeader"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x174)), 0);
                // GameObject reference
                data["ActorLightOverride"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x178));
                data["ActorLightParentName"] = // TODO: String reading;
                data["IsBlockingLineOfSight"] = Marshal.ReadByte(ptr + 0x198) != 0;
                data["HudYOffsetScale"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1B0)), 0);
                // Sprite reference
                data["Badge"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1B8));
                // Sprite reference
                data["BadgeWhite"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1C0));
                // Sprite reference
                data["PreviewMapIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1C8));
                // TODO: Array/List - AttachedPrefabs: PrefabAttachment[]
                // FactionAnimationType
                data["FactionSpecificAnimation"] = Marshal.ReadInt32(ptr + 0x1D8);
                // VisualAlterationSlot
                data["AimWithVisualSlot"] = Marshal.ReadInt32(ptr + 0x1DC);
                // ElementAnimatorTemplate reference
                data["AnimatorTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1E0));
                // SkinQuality
                data["MinSkinQuality"] = Marshal.ReadInt32(ptr + 0x1E8);
                // DecalCollection
                data["BloodDecals"] = Marshal.ReadInt32(ptr + 0x1F0);
                // SurfaceDecalsTemplate reference
                data["BloodDecalsOverride"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1F8));
                // DecalCollection
                data["BloodPool"] = Marshal.ReadInt32(ptr + 0x200);
                // SurfaceDecalsTemplate reference
                data["BloodPoolOverride"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x208));
                // EffectTriggerType
                data["BloodPoolTriggerType"] = Marshal.ReadInt32(ptr + 0x210);
                data["BloodPoolAnimation"] = Marshal.ReadByte(ptr + 0x214) != 0;
                // GameObject reference
                data["DamageReceivedEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x218));
                // GameObject reference
                data["HeavyDamageReceivedEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x220));
                data["DamageReceivedEffectThreshold"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x228)), 0);
                // GameObject reference
                data["GetDismemberedBloodSprayEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x230));
                // PrefabListTemplate reference
                data["GetDismemberedSmallAdditionalParts"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x238));
                // SurfaceEffectsTemplate reference
                data["DeathEffectOverrides2"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x240));
                // GameObject reference
                data["DeathEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x248));
                // EffectTriggerType
                data["DeathEffectTriggerType"] = Marshal.ReadInt32(ptr + 0x250);
                // GameObject reference
                data["DeathAttachEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x258));
                data["IsSinkingIntoGroundOnDeath"] = Marshal.ReadByte(ptr + 0x260) != 0;
                // CameraEffectType
                data["DeathCameraEffect"] = Marshal.ReadInt32(ptr + 0x264);
                // ID
                data["SoundOnAim"] = Marshal.ReadInt32(ptr + 0x268);
                // ID
                data["SoundWhileAlive"] = Marshal.ReadInt32(ptr + 0x270);
                // GameObject reference
                data["ExhaustDriveEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x278));
                // GameObject reference
                data["ExhaustRevEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x280));
                // GameObject reference
                data["ExhaustIdleEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x288));
                // SurfaceEffectsTemplate reference
                data["MovementEffectOverrides2"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x290));
                data["TriggerMovementEffectsOnStep"] = Marshal.ReadByte(ptr + 0x298) != 0;
                // TODO: Complex type - TriggerMovementStepIntervall: uint
                // MovementType
                data["MovementType"] = Marshal.ReadInt32(ptr + 0x2A0);
                // TilePositioning
                data["VisualPositioning"] = Marshal.ReadInt32(ptr + 0x2A8);
                data["PullTowardsTileCenter"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2AC)), 0);
                // RotationAfterMovement
                data["RotationAfterMovement"] = Marshal.ReadInt32(ptr + 0x2B0);
                data["CameraShakeOnMovement"] = Marshal.ReadByte(ptr + 0x2B4) != 0;
                data["CameraShakeOnMovementStepInterval"] = Marshal.ReadInt32(ptr + 0x2B8);
                data["CameraShakeOnMovementDuration"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2BC)), 0);
                data["CameraShakeOnMovementIntensity"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2C0)), 0);
                data["CameraShakeOnMovementRecoverTime"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x2C4)), 0);
                // InventoryType
                data["InventoryType"] = Marshal.ReadInt32(ptr + 0x2C8);
                // TODO: Array/List - Items: List<ItemTemplate>
                // ModularVehicleTemplate reference
                data["ModularVehicle"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x2D8));
                // EntityProperties
                data["Properties"] = Marshal.ReadInt32(ptr + 0x2E0);
                // TODO: Array/List - SkillGroups: List<SkillGroup>
                // TODO: Array/List - Skills: List<SkillTemplate>
                // RoleData
                data["AIRole"] = Marshal.ReadInt32(ptr + 0x2F8);
            }

            else if (templateType.Name == "EnvironmentFeatureTemplate")
            {
                // TODO: Complex type - Mode: EnvironmentFeatureTemplate.SpawnMode
                // TODO: Array/List - Prefabs: GameObject[]
                // TODO: Array/List - Details: GameObject[]
                data["Concealment"] = Marshal.ReadInt32(ptr + 0x78);
                // CoverType
                data["Cover"] = Marshal.ReadInt32(ptr + 0x7C);
                // TileEffectTemplate reference
                data["TileEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                data["IsDestroyedByVehicle"] = Marshal.ReadByte(ptr + 0x88) != 0;
                // HalfCoverClass
                data["HalfCoverClass"] = Marshal.ReadInt32(ptr + 0x8C);
                // GameObject reference
                data["DestroyEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // ID
                data["DestroySound"] = Marshal.ReadInt32(ptr + 0x98);
                // TODO: Complex type - ReplaceSurfaceOnDestroy: Nullable<SurfaceType>
                // EnvironmentFeatureTemplate reference
                data["SpawnOnDestroy"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
            }

            else if (templateType.Name == "FactionTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["TurnOrderIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // Sprite reference
                data["TurnOrderInactiveIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // FactionType
                data["AlliedFactionType"] = Marshal.ReadInt32(ptr + 0xA0);
                // FactionType
                data["EnemyFactionType"] = Marshal.ReadInt32(ptr + 0xA4);
                // TODO: Array/List - Operations: OperationTemplate[]
                // TODO: Array/List - EnemyAssets: EnemyAssetTemplate[]
                // ArmyListTemplate reference
                data["ArmyList"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // TODO: Array/List - MissionRewardTables: RewardTableTemplate[]
                // TODO: Array/List - MissionTrashRewardTables: RewardTableTemplate[]
            }

            else if (templateType.Name == "GenericMissionTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["ObjectiveProgressText"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // MissionDifficultyFlag
                data["AllowedDifficulties"] = Marshal.ReadInt32(ptr + 0x90);
                data["ProgressRequired"] = Marshal.ReadInt32(ptr + 0x94);
                // ConversationCondition
                data["Condition"] = Marshal.ReadInt32(ptr + 0x98);
                // MissionEffectivenessConfig
                data["EffectivenessConfig"] = Marshal.ReadInt32(ptr + 0xA0);
                // Sprite reference
                data["PoiIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                // ID
                data["BackgroundMusic"] = Marshal.ReadInt32(ptr + 0xB0);
                // AnimationSequenceType
                data["StartAnimationSequence"] = Marshal.ReadInt32(ptr + 0xB8);
                data["IdealDuration"] = Marshal.ReadInt32(ptr + 0xBC);
                data["ShowProgressBarLabel"] = Marshal.ReadByte(ptr + 0xC0) != 0;
                data["PlayerSupplyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xC4)), 0);
                // TODO: Array/List - PotentialStrategicAssets: MissionStrategicAssetTemplate[]
                data["EnemyArmyPointsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0xD8)), 0);
                // ArmyFlag
                data["EnemyArmyFlags"] = Marshal.ReadInt32(ptr + 0xDC);
                // ArmyFlag
                data["EnemyArmyExcludedFlags"] = Marshal.ReadInt32(ptr + 0xE0);
                // ActorSpawnAreaSettings
                data["EnemySpawnAreaSettings"] = Marshal.ReadInt32(ptr + 0xE4);
                data["EnemyStartInSleepMode"] = Marshal.ReadByte(ptr + 0xE5) != 0;
                data["RoamWhileSleeping"] = Marshal.ReadByte(ptr + 0xE6) != 0;
                // FactionType
                data["SetpieceOwner"] = Marshal.ReadInt32(ptr + 0xE8);
                // TODO: Array/List - PrimarySetpieces: List<MissionSetpieceList>
                // TODO: Array/List - SecondarySetpieces: List<MissionSetpieceList>
                // TODO: Array/List - Actors: List<MissionActorConfig>
                // ActorSpawnAreaSettings
                data["m_EnemyReinforcementsSpawnAreaSettings"] = Marshal.ReadInt32(ptr + 0x150);
            }

            else if (templateType.Name == "GlobalDifficultyTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                data["PlayerSupplyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x80)), 0);
                data["EnemyArmyPointsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x84)), 0);
                data["InitialSquaddies"] = Marshal.ReadInt32(ptr + 0x88);
            }

            else if (templateType.Name == "HalfCoverTemplate")
            {
                data["IsBlockingMovement"] = Marshal.ReadByte(ptr + 0x18) != 0;
                data["IsBlockingSight"] = Marshal.ReadByte(ptr + 0x19) != 0;
                data["IsVaultedOver"] = Marshal.ReadByte(ptr + 0x1A) != 0;
                data["IsDestroyedOnContactWithVehicles"] = Marshal.ReadByte(ptr + 0x1B) != 0;
                data["IsProvidingCover"] = Marshal.ReadByte(ptr + 0x1C) != 0;
                // HalfCoverClass
                data["CoverClass"] = Marshal.ReadInt32(ptr + 0x20);
                // GameObject reference
                data["EffectOnDeath"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x28));
                data["OnDeathAnimationSpeed"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x30)), 0);
                data["OnDeathYOffset"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x34)), 0);
                // SurfaceEffectsTemplate reference
                data["EffectOnDeathOverrides"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x38));
                // ID
                data["SoundOnDeath"] = Marshal.ReadInt32(ptr + 0x40);
            }

            else if (templateType.Name == "InsideCoverTemplate")
            {
                data["AccuracyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x58)), 0);
                data["DamageMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x5C)), 0);
                data["SuppressionMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x60)), 0);
                data["Concealment"] = Marshal.ReadInt32(ptr + 0x64);
            }

            else if (templateType.Name == "IntPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // IntPlayerSetting
                data["Type"] = Marshal.ReadInt32(ptr + 0x88);
                data["DefaultValue"] = Marshal.ReadInt32(ptr + 0x8C);
                data["MinValue"] = Marshal.ReadInt32(ptr + 0x90);
                // LocalizedLine reference
                data["Measure"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
            }

            else if (templateType.Name == "ItemFilterTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // TODO: Complex type - ItemSlots: HashSet<ItemSlot>
                // TODO: Complex type - Tags: HashSet<TagType>
                data["OnlyNewItems"] = Marshal.ReadByte(ptr + 0xA0) != 0;
                data["OnlyAvailableItems"] = Marshal.ReadByte(ptr + 0xA1) != 0;
            }

            else if (templateType.Name == "ItemListTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // TODO: Array/List - Items: List<BaseItemTemplate>
            }

            else if (templateType.Name == "KeyBindPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // KeyBindPlayerSetting
                data["Type"] = Marshal.ReadInt32(ptr + 0x88);
                // KeyBinding
                data["Default"] = Marshal.ReadInt32(ptr + 0x8C);
            }

            else if (templateType.Name == "LightConditionTemplate")
            {
                // Color
                data["DustColor"] = Marshal.ReadInt32(ptr + 0x18);
                // Color
                data["SnowColor"] = Marshal.ReadInt32(ptr + 0x28);
                data["SnowAmount"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x38)), 0);
                // GameObject reference
                data["DirectionalLightPrefab"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x40));
                // GameObject reference
                data["DirectionalActorLightPrefab"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x48));
                // VolumeProfile
                data["HDRPProfile"] = Marshal.ReadInt32(ptr + 0x50);
                // TileHighlightColorOverrides
                data["TileHighlightColorOverrides"] = Marshal.ReadInt32(ptr + 0x58);
                // SkillTemplate reference
                data["SkillToApply"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x60));
            }

            else if (templateType.Name == "ListPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                data["IsActive"] = Marshal.ReadByte(ptr + 0x88) != 0;
                data["DefaultValueIndex"] = Marshal.ReadInt32(ptr + 0x8C);
                // LocalizedMultiLine reference
                data["Values"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
            }

            else if (templateType.Name == "MissionDifficultyTemplate")
            {
                // MissionDifficultyType
                data["DifficultyType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                data["MissionPointsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x88)), 0);
                data["RewardRarityMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x8C)), 0);
                data["Skulls"] = Marshal.ReadInt32(ptr + 0x90);
                data["PlayerSupplyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x94)), 0);
                data["EnemyArmyPointsMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x98)), 0);
            }

            else if (templateType.Name == "MissionPOITemplate")
            {
                // MissionPOIType
                data["Type"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
            }

            else if (templateType.Name == "MissionPreviewConfigTemplate")
            {
                data["BorderWidth"] = Marshal.ReadInt32(ptr + 0x78);
                // Color
                data["BorderColor"] = Marshal.ReadInt32(ptr + 0x7C);
                // Color
                data["GridColor"] = Marshal.ReadInt32(ptr + 0x8C);
                // Color
                data["TileHighlightColor"] = Marshal.ReadInt32(ptr + 0x9C);
                // Color
                data["TileDragStartColor"] = Marshal.ReadInt32(ptr + 0xAC);
                // Color
                data["RoadsColor"] = Marshal.ReadInt32(ptr + 0xBC);
                // Color
                data["DeploymentZoneColor"] = Marshal.ReadInt32(ptr + 0xCC);
                // Color
                data["ObjectiveAreaColor"] = Marshal.ReadInt32(ptr + 0xDC);
                // Color
                data["StructureColor"] = Marshal.ReadInt32(ptr + 0xEC);
                // Color
                data["VegetationColor"] = Marshal.ReadInt32(ptr + 0xFC);
                // Color
                data["ActorAreaColor"] = Marshal.ReadInt32(ptr + 0x10C);
                data["EntityEdgeAlpha"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x11C)), 0);
                // TODO: Array/List - EntityCoverTint: Color[]
                data["MinHeightValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x128)), 0);
                data["MaxHeightValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x12C)), 0);
                // Color
                data["MinHeightColor"] = Marshal.ReadInt32(ptr + 0x130);
                // Color
                data["MaxHeightColor"] = Marshal.ReadInt32(ptr + 0x140);
                data["HeightShades"] = Marshal.ReadInt32(ptr + 0x150);
                data["InaccessibleMinHeightValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x154)), 0);
                data["InaccessibleMaxHeightValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x158)), 0);
                // Color
                data["InaccessibleMinHeightColor"] = Marshal.ReadInt32(ptr + 0x15C);
                // Color
                data["InaccessibleMaxHeightColor"] = Marshal.ReadInt32(ptr + 0x16C);
                data["InaccessibleHeightShades"] = Marshal.ReadInt32(ptr + 0x17C);
                // LocalizedLine reference
                data["UnknownFactionName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x180));
                // LocalizedLine reference
                data["UnknownFactionShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x188));
                // LocalizedLine reference
                data["UnknownUnitTypeName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x190));
                // Color
                data["UnknownNormalColor"] = Marshal.ReadInt32(ptr + 0x198);
                // Color
                data["UnknownHoverColor"] = Marshal.ReadInt32(ptr + 0x1A8);
                // Sprite reference
                data["IconUnitUnknown"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1B8));
                // Vector2
                data["InfoLevelRevealDelayInSec"] = Marshal.ReadInt32(ptr + 0x1C0);
                // MissionPreviewFactionConfig
                data["Civilians"] = Marshal.ReadInt32(ptr + 0x1C8);
                // MissionPreviewFactionConfig
                data["AlienWildlife"] = Marshal.ReadInt32(ptr + 0x1D0);
                // MissionPreviewFactionConfig
                data["Allies"] = Marshal.ReadInt32(ptr + 0x1D8);
                // MissionPreviewFactionConfig
                data["Enemies"] = Marshal.ReadInt32(ptr + 0x1E0);
            }

            else if (templateType.Name == "MissionStrategicAssetTemplate")
            {
                // StrategicAssetTemplate reference
                data["StrategicAsset"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x10));
                // MissionDifficultyFlag
                data["ReqMissionDifficulty"] = Marshal.ReadInt32(ptr + 0x18);
                data["Weight"] = Marshal.ReadInt32(ptr + 0x1C);
            }

            else if (templateType.Name == "ModularVehicleTemplate")
            {
                // ModularVehicleType
                data["Type"] = Marshal.ReadInt32(ptr + 0x78);
                // TODO: Array/List - Slots: ModularVehicleSlot[]
            }

            else if (templateType.Name == "ModularVehicleWeaponTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // Sprite reference
                data["IconEquipment"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Sprite reference
                data["IconEquipmentDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // Sprite reference
                data["IconSkillBar"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["IconSkillBarDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["IconSkillBarAlternative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["IconSkillBarAlternativeDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // ItemSlot
                data["SlotType"] = Marshal.ReadInt32(ptr + 0xE8);
                // ItemType
                data["ItemType"] = Marshal.ReadInt32(ptr + 0xEC);
                // TODO: Array/List - OnlyEquipableBy: List<TagTemplate>
                // ExclusiveItemCategory
                data["ExclusiveCategory"] = Marshal.ReadInt32(ptr + 0xF8);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xFC);
                data["IsDestroyedAfterCombat"] = Marshal.ReadByte(ptr + 0x100) != 0;
                // TODO: Array/List - SkillsGranted: List<SkillTemplate>
                // GameObject reference
                data["Model"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // VisualAlterationSlot
                data["VisualAlterationSlot"] = Marshal.ReadInt32(ptr + 0x118);
                // GameObject reference
                data["ModelSecondary"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // VisualAlterationSlot
                data["VisualAlterationSlotSecondary"] = Marshal.ReadInt32(ptr + 0x128);
                data["AttachLightAtNight"] = Marshal.ReadByte(ptr + 0x12C) != 0;
                // WeaponAnimType
                data["AnimType"] = Marshal.ReadInt32(ptr + 0x130);
                // AnimWeaponSize
                data["AnimSize"] = Marshal.ReadInt32(ptr + 0x134);
                // AnimWeaponGrip
                data["AnimGrip"] = Marshal.ReadInt32(ptr + 0x138);
                data["MinRange"] = Marshal.ReadInt32(ptr + 0x13C);
                data["IdealRange"] = Marshal.ReadInt32(ptr + 0x140);
                data["MaxRange"] = Marshal.ReadInt32(ptr + 0x144);
                data["AccuracyBonus"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x148)), 0);
                data["AccuracyDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x14C)), 0);
                data["Damage"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x150)), 0);
                data["DamageDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x154)), 0);
                data["DamagePctCurrentHitpoints"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x158)), 0);
                data["DamagePctCurrentHitpointsMin"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x15C)), 0);
                data["DamagePctMaxHitpoints"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x160)), 0);
                data["DamagePctMaxHitpointsMin"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x164)), 0);
                data["ArmorPenetration"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x168)), 0);
                data["ArmorPenetrationDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x16C)), 0);
                data["DamageToArmorDurability"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x170)), 0);
                data["DamageToArmorDurabilityMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x174)), 0);
                data["DamageToArmorDurabilityDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x178)), 0);
                data["DamageToArmorDurabilityDropoffMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x17C)), 0);
                data["Suppression"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x180)), 0);
                data["SupportsIntegrationOfLightWeapons"] = Marshal.ReadByte(ptr + 0x188) != 0;
                data["DisableOtherWeaponSlots"] = Marshal.ReadByte(ptr + 0x189) != 0;
                // TODO: Array/List - Setups: ModularVehicleWeaponSetup[]
            }

            else if (templateType.Name == "MoraleEffectTemplate")
            {
                // SkillTemplate reference
                data["MoraleEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x18));
                data["Chance"] = Marshal.ReadInt32(ptr + 0x20);
                // TODO: Array/List - SkipIfTheseExist: List<MoraleEffectTemplate>
                data["MinHitpointsRequired"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x30)), 0);
                data["MaxHitpointsRequired"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x34)), 0);
                data["AmountOfMoraleEffectsRequired"] = Marshal.ReadInt32(ptr + 0x38);
                // MoraleEffectPrerequisite
                data["Prerequisites"] = Marshal.ReadInt32(ptr + 0x40);
            }

            else if (templateType.Name == "OffmapAbilityTemplate")
            {
                // SkillTemplate reference
                data["SkillTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                data["DelayInRounds"] = Marshal.ReadInt32(ptr + 0x80);
                // ID
                data["SoundOnUse"] = Marshal.ReadInt32(ptr + 0x84);
            }

            else if (templateType.Name == "OperationDurationTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // TODO: Array/List - Layers: MissionLayerConfig[]
                // Vector2Int
                data["RequiredCampaignProgress"] = Marshal.ReadInt32(ptr + 0x88);
                // Vector2Int
                data["TotalMissionOptions"] = Marshal.ReadInt32(ptr + 0x90);
                // Vector2Int
                data["EnemyAssets"] = Marshal.ReadInt32(ptr + 0x98);
                data["MaxRating"] = Marshal.ReadInt32(ptr + 0xA0);
                // OperationTrustChange
                data["ClientTrustChange"] = Marshal.ReadInt32(ptr + 0xA4);
                // OperationTrustChange
                data["EnemyTrustChange"] = Marshal.ReadInt32(ptr + 0xC8);
                // TODO: Array/List - StrategyVarChanges: StrategyVarChangeOnOperationEvent[]
            }

            else if (templateType.Name == "OperationIntrosTemplate")
            {
                // TODO: Array/List - CustomConditions: List<ConditionalOperationIntro>
                // TODO: Array/List - ConditionSettings: ConditionSetting[]
                // TODO: Array/List - Generic: List<OperationIntro>
                // TODO: Array/List - FailedLastOp: List<OperationIntro>
                // TODO: Array/List - FailedManyOpsInRow: List<OperationIntro>
                // TODO: Array/List - SucceededLastOp: List<OperationIntro>
                // TODO: Array/List - SucceededManyOpsInRow: List<OperationIntro>
                // TODO: Array/List - FoughtThereLastOp: List<OperationIntro>
                // TODO: Array/List - MenaceOnPlanetLow: List<OperationIntro>
                // TODO: Array/List - MenaceOnPlanetMid: List<OperationIntro>
                // TODO: Array/List - MenaceOnPlanetHigh: List<OperationIntro>
                // TODO: Array/List - TrustLevel3: List<OperationIntro>
                // TODO: Array/List - TrustLevel6: List<OperationIntro>
                // TODO: Array/List - TrustLevelMax: List<OperationIntro>
            }

            else if (templateType.Name == "OperationTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Goal"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // LocalizedMultiLine reference
                data["VictoryDescription"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // LocalizedMultiLine reference
                data["FailureDescription"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                data["Repeatable"] = Marshal.ReadByte(ptr + 0xA0) != 0;
                data["CanTimeout"] = Marshal.ReadByte(ptr + 0xA1) != 0;
                // ConversationCondition
                data["Condition"] = Marshal.ReadInt32(ptr + 0xA8);
                data["SkipOperationScreens"] = Marshal.ReadByte(ptr + 0xB0) != 0;
                data["ShowStartConfirmationDialog"] = Marshal.ReadByte(ptr + 0xB1) != 0;
                // LocalizedMultiLine reference
                data["StartConfirmationDialogText"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                data["SystemMapIconIdx"] = Marshal.ReadInt32(ptr + 0xC0);
                data["CanHaveFriendlyForce"] = Marshal.ReadByte(ptr + 0xC4) != 0;
                // FactionTemplate reference
                data["OverrideFaction"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // TODO: Array/List - Durations: OperationDurationTemplate[]
                // TODO: Array/List - MissionsFirstLayer: MissionConfig[]
                // TODO: Array/List - MissionsMiddleLayer: MissionConfig[]
                // TODO: Array/List - MissionsFinalLayer: MissionConfig[]
                // TODO: Array/List - StoryMissions: StoryMission[]
                // ConversationTemplate reference
                data["VictoryEvent"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                // ConversationTemplate reference
                data["FailureEvent"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x100));
                // ConversationTemplate reference
                data["AbortEvent"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x108));
                // TODO: Array/List - IntroConversations: ConversationTemplate[]
                // TODO: Array/List - VictoryConversations: ConversationTemplate[]
                // TODO: Array/List - FailureConversations: ConversationTemplate[]
                // TODO: Array/List - AbortConversations: ConversationTemplate[]
            }

            else if (templateType.Name == "PerkTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["ShortDescription"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // Sprite reference
                data["IconDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // SkillType
                data["Type"] = Marshal.ReadInt32(ptr + 0xA0);
                // TODO: Array/List - Tags: List<TagTemplate>
                // SkillOrder
                data["Order"] = Marshal.ReadInt32(ptr + 0xB0);
                data["ActionPointCost"] = Marshal.ReadInt32(ptr + 0xB4);
                data["IsLimitedUses"] = Marshal.ReadByte(ptr + 0xB8) != 0;
                data["Uses"] = Marshal.ReadInt32(ptr + 0xBC);
                // SkillUsesDisplayTemplate reference
                data["UsesDisplayTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                data["IsActive"] = Marshal.ReadByte(ptr + 0xC8) != 0;
                data["HideApCosts"] = Marshal.ReadByte(ptr + 0xC9) != 0;
                // KeyBindPlayerSetting
                data["KeyBind"] = Marshal.ReadInt32(ptr + 0xCC);
                // ExecutingElementType
                data["ExecutingElement"] = Marshal.ReadInt32(ptr + 0xD0);
                // AnimationType
                data["AnimationType"] = Marshal.ReadInt32(ptr + 0xD4);
                // AimingType
                data["AimingType"] = Marshal.ReadInt32(ptr + 0xD8);
                data["IsOverrideAimSlot"] = Marshal.ReadByte(ptr + 0xDC) != 0;
                // VisualAlterationSlot
                data["OverrideAimSlot"] = Marshal.ReadInt32(ptr + 0xE0);
                data["IsTargeted"] = Marshal.ReadByte(ptr + 0xE4) != 0;
                // CursorType
                data["TargetingCursor"] = Marshal.ReadInt32(ptr + 0xE8);
                // SkillTarget
                data["TargetsAllowed"] = Marshal.ReadInt32(ptr + 0xEC);
                data["KeepSelectedIfStillUsable"] = Marshal.ReadByte(ptr + 0xF0) != 0;
                data["IsLineOfFireNeeded"] = Marshal.ReadByte(ptr + 0xF1) != 0;
                data["IsAttack"] = Marshal.ReadByte(ptr + 0xF2) != 0;
                data["IsAlwaysHitting"] = Marshal.ReadByte(ptr + 0xF3) != 0;
                data["CanHitAnotherTile"] = Marshal.ReadByte(ptr + 0xF4) != 0;
                data["IsUsedInBackground"] = Marshal.ReadByte(ptr + 0xF5) != 0;
                // SkillTemplate reference
                data["OverrideBackgroundUseWithSkill"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                data["IsIgnoringCover"] = Marshal.ReadByte(ptr + 0x100) != 0;
                data["IsIgnoringCoverInside"] = Marshal.ReadByte(ptr + 0x101) != 0;
                data["IsSilent"] = Marshal.ReadByte(ptr + 0x102) != 0;
                data["IgnoreMalfunctionChance"] = Marshal.ReadByte(ptr + 0x103) != 0;
                data["IsDeploymentRequired"] = Marshal.ReadByte(ptr + 0x104) != 0;
                data["IsWeaponSetupRequired"] = Marshal.ReadByte(ptr + 0x105) != 0;
                data["IsUsableWhileContained"] = Marshal.ReadByte(ptr + 0x106) != 0;
                data["IsUsableWhilePinnedDown"] = Marshal.ReadByte(ptr + 0x107) != 0;
                data["IsStacking"] = Marshal.ReadByte(ptr + 0x108) != 0;
                data["IsSerialized"] = Marshal.ReadByte(ptr + 0x109) != 0;
                data["IsRemovedAfterCombat"] = Marshal.ReadByte(ptr + 0x10A) != 0;
                data["IsRemovedAfterOperation"] = Marshal.ReadByte(ptr + 0x10B) != 0;
                data["IsHidden"] = Marshal.ReadByte(ptr + 0x10C) != 0;
                // RangeShape
                data["Shape"] = Marshal.ReadInt32(ptr + 0x110);
                data["ConeAngle"] = Marshal.ReadInt32(ptr + 0x114);
                data["IsOverridingRanges"] = Marshal.ReadByte(ptr + 0x118) != 0;
                data["MinRange"] = Marshal.ReadInt32(ptr + 0x11C);
                data["IdealRange"] = Marshal.ReadInt32(ptr + 0x120);
                data["MaxRange"] = Marshal.ReadInt32(ptr + 0x124);
                data["MinElementDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x128)), 0);
                data["MaxElementDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x12C)), 0);
                data["ElementDelayBetween"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x130)), 0);
                data["MinDelayBeforeSkillUse"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x134)), 0);
                data["DelayAfterAnimationTrigger"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x138)), 0);
                data["DelayAfterLastRepetition"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x13C)), 0);
                data["DelayAfterSkillUse"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x140)), 0);
                data["Repetitions"] = Marshal.ReadInt32(ptr + 0x144);
                data["RepetitionDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x148)), 0);
                data["SkipDelayForLastRepetition"] = Marshal.ReadByte(ptr + 0x14C) != 0;
                data["IsPlayingAnimationForEachRepetition"] = Marshal.ReadByte(ptr + 0x14D) != 0;
                data["UseCustomAoEShape"] = Marshal.ReadByte(ptr + 0x158) != 0;
                // ICustomAoEShape
                data["CustomAoEShape"] = Marshal.ReadInt32(ptr + 0x160);
                // SkillAoEType
                data["AoEType"] = Marshal.ReadInt32(ptr + 0x168);
                // ITacticalCondition
                data["AoEFilter"] = Marshal.ReadInt32(ptr + 0x170);
                // FactionType
                data["TargetFaction"] = Marshal.ReadInt32(ptr + 0x178);
                data["AoEChanceToHitCenter"] = Marshal.ReadInt32(ptr + 0x17C);
                data["SelectableTiles"] = Marshal.ReadInt32(ptr + 0x180);
                // ScatterMode
                data["ScatterMode"] = Marshal.ReadInt32(ptr + 0x184);
                data["Scatter"] = Marshal.ReadInt32(ptr + 0x188);
                data["ScatterChance"] = Marshal.ReadInt32(ptr + 0x18C);
                data["ScatterHitEachTileOnlyOnce"] = Marshal.ReadByte(ptr + 0x190) != 0;
                data["ScatterHitOnlyValidTiles"] = Marshal.ReadByte(ptr + 0x191) != 0;
                // MuzzleType
                data["MuzzleType"] = Marshal.ReadInt32(ptr + 0x194);
                // TODO: Array/List - MuzzleSelection: MuzzleType[]
                // GameObject reference
                data["MuzzleEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1A0));
                // SurfaceEffectsTemplate reference
                data["MuzzleEffectOverrides2"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1A8));
                data["IsSpawningMuzzleForEachRepetition"] = Marshal.ReadByte(ptr + 0x1B0) != 0;
                data["IsAttachingMuzzleToTransform"] = Marshal.ReadByte(ptr + 0x1B1) != 0;
                // CameraEffectType
                data["CameraEffectOnFire"] = Marshal.ReadInt32(ptr + 0x1B4);
                // Vector3
                data["ProjectileSpawnPositionOffset"] = Marshal.ReadInt32(ptr + 0x1B8);
                // BaseProjectileData
                data["ProjectileData"] = Marshal.ReadInt32(ptr + 0x1C8);
                // BaseProjectileData
                data["SecondaryProjectileData"] = Marshal.ReadInt32(ptr + 0x1D0);
                // TODO: Array/List - ImpactOnSurface: List<SkillOnSurfaceDefinition>
                data["ImpactEffectDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E0)), 0);
                data["ImpactDecalDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E4)), 0);
                data["EffectDelayAfterImpact"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E8)), 0);
                data["IsImpactShownOnHit"] = Marshal.ReadByte(ptr + 0x1EC) != 0;
                data["IsImpactCenteredOnTile"] = Marshal.ReadByte(ptr + 0x1ED) != 0;
                data["IsImpactOnlyOnAOECenterTile"] = Marshal.ReadByte(ptr + 0x1EE) != 0;
                data["IsDecalOnlyOnAOECenterTile"] = Marshal.ReadByte(ptr + 0x1EF) != 0;
                data["IsImpactCenteredOnExecutingElement"] = Marshal.ReadByte(ptr + 0x1F0) != 0;
                data["IsImpactAlignedToInfantry"] = Marshal.ReadByte(ptr + 0x1F1) != 0;
                // CameraEffectType
                data["CameraEffectOnImpact"] = Marshal.ReadInt32(ptr + 0x1F4);
                // CameraEffectType
                data["CameraEffectOnPlayerHit"] = Marshal.ReadInt32(ptr + 0x1F8);
                data["IsTriggeringHeavyDamagedReceivedEffect"] = Marshal.ReadByte(ptr + 0x1FC) != 0;
                // Vector2
                data["RagdollImpactMult"] = Marshal.ReadInt32(ptr + 0x200);
                // Vector2
                data["VerticalRagdollImpactMult"] = Marshal.ReadInt32(ptr + 0x208);
                // RagdollHitArea
                data["RagdollHitArea"] = Marshal.ReadInt32(ptr + 0x210);
                data["MalfunctionChance"] = Marshal.ReadInt32(ptr + 0x214);
                // GameObject reference
                data["MalfunctionEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x218));
                // ID
                data["SoundOnMalfunction"] = Marshal.ReadInt32(ptr + 0x220);
                data["IsAudibleWhenNotVisible"] = Marshal.ReadByte(ptr + 0x228) != 0;
                // TODO: Array/List - SoundsOnBeforeUse: List<ID>
                // TODO: Array/List - SoundsOnUse: List<ID>
                // TODO: Array/List - SoundsOnAttackPerElement: List<ID>
                data["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"] = Marshal.ReadByte(ptr + 0x248) != 0;
                // TODO: Array/List - SoundsOnAttack: List<ID>
                // TODO: Array/List - SoundsOnHit: List<ID>
                // TODO: Array/List - SoundsOnElementDestroyed: List<ID>
                // TODO: Array/List - SoundsOnBeforeUseFar: List<ID>
                // TODO: Array/List - SoundsOnUseFar: List<ID>
                // TODO: Array/List - SoundsOnAttackPerElementFar: List<ID>
                data["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"] = Marshal.ReadByte(ptr + 0x280) != 0;
                // TODO: Array/List - SoundsOnAttackFar: List<ID>
                // TODO: Array/List - EventHandlers: List<SkillEventHandlerTemplate>
                // SkillBehavior
                data["AIConfig"] = Marshal.ReadInt32(ptr + 0x298);
                // Sprite reference
                data["PerkIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x2A8));
            }

            else if (templateType.Name == "PerkTreeTemplate")
            {
                // TODO: Array/List - Perks: Perk[]
            }

            else if (templateType.Name == "PlanetTemplate")
            {
                // PlanetType
                data["PlanetType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedLine reference
                data["TypeName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // LocalizedLine reference
                data["Tags"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // LocalizedLine reference
                data["Temperature"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
                // Texture2D reference
                data["MoodImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                data["ImageOverlayMargin"] = Marshal.ReadInt32(ptr + 0xB0);
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                data["MaxMenacePresence"] = Marshal.ReadInt32(ptr + 0xC0);
                // ConversationTemplate reference
                data["MenaceDetectedEvent"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // GameObject reference
                data["OperationSelectScenePrefab"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // GameObject reference
                data["MissionSelectPrefab"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // TODO: Array/List - Neighbors: PlanetTemplate[]
                // TODO: Array/List - Biomes: BiomeTemplate[]
                // TODO: Array/List - Effects: BaseGameEffect[]
                // StoryFactionTemplate reference
                data["LocalFaction"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                // TODO: Array/List - RegularEnemyFactions: FactionTemplate[]
            }

            else if (templateType.Name == "PrefabListTemplate")
            {
                // TODO: Array/List - Prefabs: GameObject[]
            }

            else if (templateType.Name == "PropertyDisplayConfigTemplate")
            {
                // PropertyDisplayConfig
                data["Type"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                data["DefaultValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x90)), 0);
                data["MinValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x94)), 0);
                data["MaxValue"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x98)), 0);
                data["DecimalPlaces"] = Marshal.ReadInt32(ptr + 0x9C);
                data["ProgressBarSections"] = Marshal.ReadInt32(ptr + 0xA0);
                data["IsBiggerBetter"] = Marshal.ReadByte(ptr + 0xA4) != 0;
                // Color
                data["ProgressBarFillColor"] = Marshal.ReadInt32(ptr + 0xA8);
                // Color
                data["ProgressBarPreviewFillColor"] = Marshal.ReadInt32(ptr + 0xB8);
                // LocalizedMultiLine reference
                data["ProgressBarSectionLabels"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
            }

            else if (templateType.Name == "RagdollTemplate")
            {
                // RagdollBloodPoolPosition
                data["BloodPoolPosition"] = Marshal.ReadInt32(ptr + 0x58);
                data["UseCustomGravity"] = Marshal.ReadByte(ptr + 0x5C) != 0;
                data["CustomGravity"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x60)), 0);
                // ForceMode
                data["CustomGravityForceMode"] = Marshal.ReadInt32(ptr + 0x64);
                // Vector3
                data["DismemberedPartMaxDirectionOffsetInDeg"] = Marshal.ReadInt32(ptr + 0x68);
                data["DismemberedPartHitForceMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x74)), 0);
                // Vector2
                data["DismemberedPartHitForceMultOffset"] = Marshal.ReadInt32(ptr + 0x78);
                // Vector2Int
                data["AdditionalDismemberedPieces"] = Marshal.ReadInt32(ptr + 0x80);
                // Vector2
                data["AdditionalDismemberedPieceScale"] = Marshal.ReadInt32(ptr + 0x88);
                // Vector3
                data["AdditionalDismemberedPieceMaxDirectionOffsetInDeg"] = Marshal.ReadInt32(ptr + 0x90);
                data["AdditionalDismemberedPieceHitForceMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x9C)), 0);
                // Vector2
                data["AdditionalDismemberedPieceHitForceMultOffset"] = Marshal.ReadInt32(ptr + 0xA0);
                data["RootHasGeometry"] = Marshal.ReadByte(ptr + 0xA8) != 0;
                data["CenterPartIndex"] = Marshal.ReadInt32(ptr + 0xAC);
                data["GeometryRootIndex"] = Marshal.ReadInt32(ptr + 0xB0);
                // TODO: Array/List - Parts: RagdollPartConfig[]
            }

            else if (templateType.Name == "ResolutionPlayerSettingTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
            }

            else if (templateType.Name == "RewardTableTemplate")
            {
                data["RarityMultiplier"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x78)), 0);
                // TODO: Array/List - Items: List<BaseItemTemplate>
            }

            else if (templateType.Name == "ShipUpgradeSlotTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // ShipUpgradeType
                data["UpgradeType"] = Marshal.ReadInt32(ptr + 0x80);
            }

            else if (templateType.Name == "ShipUpgradeTemplate")
            {
                // TODO: Array/List - Effects: BaseGameEffect[]
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // ShipUpgradeType
                data["UpgradeType"] = Marshal.ReadInt32(ptr + 0x98);
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
                // Sprite reference
                data["IconInactive"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                data["OciPointsCosts"] = Marshal.ReadInt32(ptr + 0xB0);
                // ShipUpgradeUnlockType
                data["UnlockType"] = Marshal.ReadInt32(ptr + 0xB4);
                // StoryFactionType
                data["UnlockedByFaction"] = Marshal.ReadInt32(ptr + 0xB8);
                data["UnlockSelectWeight"] = Marshal.ReadInt32(ptr + 0xBC);
            }

            else if (templateType.Name == "SkillTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["ShortDescription"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // Sprite reference
                data["IconDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // SkillType
                data["Type"] = Marshal.ReadInt32(ptr + 0xA0);
                // TODO: Array/List - Tags: List<TagTemplate>
                // SkillOrder
                data["Order"] = Marshal.ReadInt32(ptr + 0xB0);
                data["ActionPointCost"] = Marshal.ReadInt32(ptr + 0xB4);
                data["IsLimitedUses"] = Marshal.ReadByte(ptr + 0xB8) != 0;
                data["Uses"] = Marshal.ReadInt32(ptr + 0xBC);
                // SkillUsesDisplayTemplate reference
                data["UsesDisplayTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                data["IsActive"] = Marshal.ReadByte(ptr + 0xC8) != 0;
                data["HideApCosts"] = Marshal.ReadByte(ptr + 0xC9) != 0;
                // KeyBindPlayerSetting
                data["KeyBind"] = Marshal.ReadInt32(ptr + 0xCC);
                // ExecutingElementType
                data["ExecutingElement"] = Marshal.ReadInt32(ptr + 0xD0);
                // AnimationType
                data["AnimationType"] = Marshal.ReadInt32(ptr + 0xD4);
                // AimingType
                data["AimingType"] = Marshal.ReadInt32(ptr + 0xD8);
                data["IsOverrideAimSlot"] = Marshal.ReadByte(ptr + 0xDC) != 0;
                // VisualAlterationSlot
                data["OverrideAimSlot"] = Marshal.ReadInt32(ptr + 0xE0);
                data["IsTargeted"] = Marshal.ReadByte(ptr + 0xE4) != 0;
                // CursorType
                data["TargetingCursor"] = Marshal.ReadInt32(ptr + 0xE8);
                // SkillTarget
                data["TargetsAllowed"] = Marshal.ReadInt32(ptr + 0xEC);
                data["KeepSelectedIfStillUsable"] = Marshal.ReadByte(ptr + 0xF0) != 0;
                data["IsLineOfFireNeeded"] = Marshal.ReadByte(ptr + 0xF1) != 0;
                data["IsAttack"] = Marshal.ReadByte(ptr + 0xF2) != 0;
                data["IsAlwaysHitting"] = Marshal.ReadByte(ptr + 0xF3) != 0;
                data["CanHitAnotherTile"] = Marshal.ReadByte(ptr + 0xF4) != 0;
                data["IsUsedInBackground"] = Marshal.ReadByte(ptr + 0xF5) != 0;
                // SkillTemplate reference
                data["OverrideBackgroundUseWithSkill"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                data["IsIgnoringCover"] = Marshal.ReadByte(ptr + 0x100) != 0;
                data["IsIgnoringCoverInside"] = Marshal.ReadByte(ptr + 0x101) != 0;
                data["IsSilent"] = Marshal.ReadByte(ptr + 0x102) != 0;
                data["IgnoreMalfunctionChance"] = Marshal.ReadByte(ptr + 0x103) != 0;
                data["IsDeploymentRequired"] = Marshal.ReadByte(ptr + 0x104) != 0;
                data["IsWeaponSetupRequired"] = Marshal.ReadByte(ptr + 0x105) != 0;
                data["IsUsableWhileContained"] = Marshal.ReadByte(ptr + 0x106) != 0;
                data["IsUsableWhilePinnedDown"] = Marshal.ReadByte(ptr + 0x107) != 0;
                data["IsStacking"] = Marshal.ReadByte(ptr + 0x108) != 0;
                data["IsSerialized"] = Marshal.ReadByte(ptr + 0x109) != 0;
                data["IsRemovedAfterCombat"] = Marshal.ReadByte(ptr + 0x10A) != 0;
                data["IsRemovedAfterOperation"] = Marshal.ReadByte(ptr + 0x10B) != 0;
                data["IsHidden"] = Marshal.ReadByte(ptr + 0x10C) != 0;
                // RangeShape
                data["Shape"] = Marshal.ReadInt32(ptr + 0x110);
                data["ConeAngle"] = Marshal.ReadInt32(ptr + 0x114);
                data["IsOverridingRanges"] = Marshal.ReadByte(ptr + 0x118) != 0;
                data["MinRange"] = Marshal.ReadInt32(ptr + 0x11C);
                data["IdealRange"] = Marshal.ReadInt32(ptr + 0x120);
                data["MaxRange"] = Marshal.ReadInt32(ptr + 0x124);
                data["MinElementDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x128)), 0);
                data["MaxElementDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x12C)), 0);
                data["ElementDelayBetween"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x130)), 0);
                data["MinDelayBeforeSkillUse"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x134)), 0);
                data["DelayAfterAnimationTrigger"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x138)), 0);
                data["DelayAfterLastRepetition"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x13C)), 0);
                data["DelayAfterSkillUse"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x140)), 0);
                data["Repetitions"] = Marshal.ReadInt32(ptr + 0x144);
                data["RepetitionDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x148)), 0);
                data["SkipDelayForLastRepetition"] = Marshal.ReadByte(ptr + 0x14C) != 0;
                data["IsPlayingAnimationForEachRepetition"] = Marshal.ReadByte(ptr + 0x14D) != 0;
                data["UseCustomAoEShape"] = Marshal.ReadByte(ptr + 0x158) != 0;
                // ICustomAoEShape
                data["CustomAoEShape"] = Marshal.ReadInt32(ptr + 0x160);
                // SkillAoEType
                data["AoEType"] = Marshal.ReadInt32(ptr + 0x168);
                // ITacticalCondition
                data["AoEFilter"] = Marshal.ReadInt32(ptr + 0x170);
                // FactionType
                data["TargetFaction"] = Marshal.ReadInt32(ptr + 0x178);
                data["AoEChanceToHitCenter"] = Marshal.ReadInt32(ptr + 0x17C);
                data["SelectableTiles"] = Marshal.ReadInt32(ptr + 0x180);
                // ScatterMode
                data["ScatterMode"] = Marshal.ReadInt32(ptr + 0x184);
                data["Scatter"] = Marshal.ReadInt32(ptr + 0x188);
                data["ScatterChance"] = Marshal.ReadInt32(ptr + 0x18C);
                data["ScatterHitEachTileOnlyOnce"] = Marshal.ReadByte(ptr + 0x190) != 0;
                data["ScatterHitOnlyValidTiles"] = Marshal.ReadByte(ptr + 0x191) != 0;
                // MuzzleType
                data["MuzzleType"] = Marshal.ReadInt32(ptr + 0x194);
                // TODO: Array/List - MuzzleSelection: MuzzleType[]
                // GameObject reference
                data["MuzzleEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1A0));
                // SurfaceEffectsTemplate reference
                data["MuzzleEffectOverrides2"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x1A8));
                data["IsSpawningMuzzleForEachRepetition"] = Marshal.ReadByte(ptr + 0x1B0) != 0;
                data["IsAttachingMuzzleToTransform"] = Marshal.ReadByte(ptr + 0x1B1) != 0;
                // CameraEffectType
                data["CameraEffectOnFire"] = Marshal.ReadInt32(ptr + 0x1B4);
                // Vector3
                data["ProjectileSpawnPositionOffset"] = Marshal.ReadInt32(ptr + 0x1B8);
                // BaseProjectileData
                data["ProjectileData"] = Marshal.ReadInt32(ptr + 0x1C8);
                // BaseProjectileData
                data["SecondaryProjectileData"] = Marshal.ReadInt32(ptr + 0x1D0);
                // TODO: Array/List - ImpactOnSurface: List<SkillOnSurfaceDefinition>
                data["ImpactEffectDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E0)), 0);
                data["ImpactDecalDelay"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E4)), 0);
                data["EffectDelayAfterImpact"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1E8)), 0);
                data["IsImpactShownOnHit"] = Marshal.ReadByte(ptr + 0x1EC) != 0;
                data["IsImpactCenteredOnTile"] = Marshal.ReadByte(ptr + 0x1ED) != 0;
                data["IsImpactOnlyOnAOECenterTile"] = Marshal.ReadByte(ptr + 0x1EE) != 0;
                data["IsDecalOnlyOnAOECenterTile"] = Marshal.ReadByte(ptr + 0x1EF) != 0;
                data["IsImpactCenteredOnExecutingElement"] = Marshal.ReadByte(ptr + 0x1F0) != 0;
                data["IsImpactAlignedToInfantry"] = Marshal.ReadByte(ptr + 0x1F1) != 0;
                // CameraEffectType
                data["CameraEffectOnImpact"] = Marshal.ReadInt32(ptr + 0x1F4);
                // CameraEffectType
                data["CameraEffectOnPlayerHit"] = Marshal.ReadInt32(ptr + 0x1F8);
                data["IsTriggeringHeavyDamagedReceivedEffect"] = Marshal.ReadByte(ptr + 0x1FC) != 0;
                // Vector2
                data["RagdollImpactMult"] = Marshal.ReadInt32(ptr + 0x200);
                // Vector2
                data["VerticalRagdollImpactMult"] = Marshal.ReadInt32(ptr + 0x208);
                // RagdollHitArea
                data["RagdollHitArea"] = Marshal.ReadInt32(ptr + 0x210);
                data["MalfunctionChance"] = Marshal.ReadInt32(ptr + 0x214);
                // GameObject reference
                data["MalfunctionEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x218));
                // ID
                data["SoundOnMalfunction"] = Marshal.ReadInt32(ptr + 0x220);
                data["IsAudibleWhenNotVisible"] = Marshal.ReadByte(ptr + 0x228) != 0;
                // TODO: Array/List - SoundsOnBeforeUse: List<ID>
                // TODO: Array/List - SoundsOnUse: List<ID>
                // TODO: Array/List - SoundsOnAttackPerElement: List<ID>
                data["IsSoundOnAttackPerElementPlayingAfterAnimationDelay"] = Marshal.ReadByte(ptr + 0x248) != 0;
                // TODO: Array/List - SoundsOnAttack: List<ID>
                // TODO: Array/List - SoundsOnHit: List<ID>
                // TODO: Array/List - SoundsOnElementDestroyed: List<ID>
                // TODO: Array/List - SoundsOnBeforeUseFar: List<ID>
                // TODO: Array/List - SoundsOnUseFar: List<ID>
                // TODO: Array/List - SoundsOnAttackPerElementFar: List<ID>
                data["IsSoundOnAttackPerElementFarPlayingAfterAnimationDelay"] = Marshal.ReadByte(ptr + 0x280) != 0;
                // TODO: Array/List - SoundsOnAttackFar: List<ID>
                // TODO: Array/List - EventHandlers: List<SkillEventHandlerTemplate>
                // SkillBehavior
                data["AIConfig"] = Marshal.ReadInt32(ptr + 0x298);
            }

            else if (templateType.Name == "SkillUsesDisplayTemplate")
            {
                data["ShowInItemTooltips"] = Marshal.ReadByte(ptr + 0x58) != 0;
                data["ShowOnSkillBarWeapon"] = Marshal.ReadByte(ptr + 0x59) != 0;
                // FlexDirection
                data["NotchLayout"] = Marshal.ReadInt32(ptr + 0x5C);
                data["NotchHeight"] = Marshal.ReadInt32(ptr + 0x60);
                data["NotchWidth"] = Marshal.ReadInt32(ptr + 0x64);
                data["NotchGapWidth"] = Marshal.ReadInt32(ptr + 0x68);
                data["NotchGroupGapWidth"] = Marshal.ReadInt32(ptr + 0x6C);
                // Sprite reference
                data["NotchFullIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x70));
                // Color
                data["NotchFullTint"] = Marshal.ReadInt32(ptr + 0x78);
                // Sprite reference
                data["NotchFullDisabledIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Color
                data["NotchFullDisabledTint"] = Marshal.ReadInt32(ptr + 0x90);
                // Sprite reference
                data["NotchEmptyIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
                // Color
                data["NotchEmptyTint"] = Marshal.ReadInt32(ptr + 0xA8);
                // Sprite reference
                data["NotchEmptyDisabledIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Color
                data["NotchEmptyDisabledTint"] = Marshal.ReadInt32(ptr + 0xC0);
            }

            else if (templateType.Name == "SpeakerTemplate")
            {
                // LocalizedLine reference
                data["Nickname"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["Forename"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedLine reference
                data["Surname"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                data["Tags"] = // TODO: String reading;
                // Sprite reference
                data["BarkImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                // Texture2D reference
                data["OperationSelectImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB0));
                // ID
                data["SoundOnTacticalBarkShown"] = Marshal.ReadInt32(ptr + 0xB8);
                data["TacticalBarkSoundDelayInMs"] = Marshal.ReadInt32(ptr + 0xC0);
                // Texture2D reference
                data["StandLookLeftImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Texture2D reference
                data["StandLookRightImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Texture2D reference
                data["StandLookRightInactiveImage"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
            }

            else if (templateType.Name == "SquaddieItemTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // TODO: Array/List - Squaddies: SquaddieConfig[]
                // LocalizedMultiLine reference
                data["SquaddieNames"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // LocalizedMultiLine reference
                data["SquaddieNicknames"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
            }

            else if (templateType.Name == "StoryFactionTemplate")
            {
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["TurnOrderIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                // Sprite reference
                data["TurnOrderInactiveIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // FactionType
                data["AlliedFactionType"] = Marshal.ReadInt32(ptr + 0xA0);
                // FactionType
                data["EnemyFactionType"] = Marshal.ReadInt32(ptr + 0xA4);
                // TODO: Array/List - Operations: OperationTemplate[]
                // TODO: Array/List - EnemyAssets: EnemyAssetTemplate[]
                // ArmyListTemplate reference
                data["ArmyList"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // TODO: Array/List - MissionRewardTables: RewardTableTemplate[]
                // TODO: Array/List - MissionTrashRewardTables: RewardTableTemplate[]
                // StoryFactionType
                data["FactionType"] = Marshal.ReadInt32(ptr + 0xD8);
                // SpeakerTemplate reference
                data["Representative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // Texture2D reference
                data["FactionWindow"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE8));
                // Sprite reference
                data["SystemMapHUDIcon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF0));
                // OperationIntrosTemplate reference
                data["OperationIntros"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                // StoryFactionStatus
                data["InitialStatus"] = Marshal.ReadInt32(ptr + 0x100);
                data["InitialTotalTrust"] = Marshal.ReadInt32(ptr + 0x104);
                // TODO: Array/List - RequiredTotalTrustForLevel: int[]
                // TODO: Array/List - HostileFactions: FactionTemplate[]
            }

            else if (templateType.Name == "StrategicAssetTemplate")
            {
                // TODO: Array/List - Effects: BaseGameEffect[]
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["IconBig"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                data["DisableAfterMission"] = Marshal.ReadByte(ptr + 0x90) != 0;
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
            }

            else if (templateType.Name == "SurfaceDecalsTemplate")
            {
                // DecalCollection
                data["ConcreteEffect"] = Marshal.ReadInt32(ptr + 0x18);
                // DecalCollection
                data["MetalEffect"] = Marshal.ReadInt32(ptr + 0x20);
                // DecalCollection
                data["SandEffect"] = Marshal.ReadInt32(ptr + 0x28);
                // DecalCollection
                data["EarthEffect"] = Marshal.ReadInt32(ptr + 0x30);
                // DecalCollection
                data["SnowEffect"] = Marshal.ReadInt32(ptr + 0x38);
                // DecalCollection
                data["WaterEffect"] = Marshal.ReadInt32(ptr + 0x40);
                // DecalCollection
                data["RuinsEffect"] = Marshal.ReadInt32(ptr + 0x48);
                // DecalCollection
                data["SandStoneEffect"] = Marshal.ReadInt32(ptr + 0x50);
                // DecalCollection
                data["MudEffect"] = Marshal.ReadInt32(ptr + 0x58);
                // DecalCollection
                data["GrassEffect"] = Marshal.ReadInt32(ptr + 0x60);
                // DecalCollection
                data["GlassEffect"] = Marshal.ReadInt32(ptr + 0x68);
                // DecalCollection
                data["ForestEffect"] = Marshal.ReadInt32(ptr + 0x70);
                // DecalCollection
                data["RockEffect"] = Marshal.ReadInt32(ptr + 0x78);
            }

            else if (templateType.Name == "SurfaceEffectsTemplate")
            {
                // GameObject reference
                data["ConcreteEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x18));
                // GameObject reference
                data["MetalEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x20));
                // GameObject reference
                data["SandEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x28));
                // GameObject reference
                data["EarthEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x30));
                // GameObject reference
                data["SnowEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x38));
                // GameObject reference
                data["WaterEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x40));
                // GameObject reference
                data["RuinsEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x48));
                // GameObject reference
                data["SandStoneEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x50));
                // GameObject reference
                data["MudEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x58));
                // GameObject reference
                data["GrassEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x60));
                // GameObject reference
                data["GlassEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x68));
                // GameObject reference
                data["ForestEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x70));
                // GameObject reference
                data["RockEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
            }

            else if (templateType.Name == "SurfaceSoundsTemplate")
            {
                // TODO: Array/List - ConcreteSounds: ID[]
                // TODO: Array/List - MetalSounds: ID[]
                // TODO: Array/List - SandSounds: ID[]
                // TODO: Array/List - EarthSounds: ID[]
                // TODO: Array/List - SnowSounds: ID[]
                // TODO: Array/List - WaterSounds: ID[]
                // TODO: Array/List - RuinsSounds: ID[]
                // TODO: Array/List - SandStoneSounds: ID[]
                // TODO: Array/List - MudSounds: ID[]
                // TODO: Array/List - GrassSounds: ID[]
                // TODO: Array/List - GlassSounds: ID[]
                // TODO: Array/List - ForestSounds: ID[]
                // TODO: Array/List - RockSounds: ID[]
            }

            else if (templateType.Name == "SurfaceTypeTemplate")
            {
                // SurfaceType
                data["SurfaceType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
            }

            else if (templateType.Name == "TagTemplate")
            {
                // TagType
                data["TagType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                data["IsVisible"] = Marshal.ReadByte(ptr + 0x88) != 0;
                // TagValue
                data["Value"] = Marshal.ReadInt32(ptr + 0x8C);
                // TODO: Array/List - GoodAgainst: List<TagTemplate>
                // TODO: Array/List - BadAgainst: List<TagTemplate>
            }

            else if (templateType.Name == "UnitLeaderTemplate")
            {
                // SpeakerTemplate reference
                data["SpeakerTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["UnitTitle"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["UnitDescription"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                data["HiringCosts"] = Marshal.ReadInt32(ptr + 0x90);
                // ID
                data["HiringSelectBarkSound"] = Marshal.ReadInt32(ptr + 0x94);
                // ID
                data["HiredBarkSound"] = Marshal.ReadInt32(ptr + 0x9C);
                // Gender
                data["Gender"] = Marshal.ReadInt32(ptr + 0xA4);
                // SkinColor
                data["SkinColor"] = Marshal.ReadInt32(ptr + 0xA5);
                // GameObject reference
                data["CustomHead"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                // ActorType
                data["UnitActorType"] = Marshal.ReadInt32(ptr + 0xB0);
                // EntityTemplate reference
                data["InfantryUnitTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // InventoryType
                data["PilotInventoryTemplate"] = Marshal.ReadInt32(ptr + 0xC0);
                // VehicleItemTemplate reference
                data["InitialVehicleItem"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["Slot"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["SlotInactive"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["SlotInjured"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // Texture2D reference
                data["SlotFactionBackground"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE8));
                // Sprite reference
                data["SlotBadge"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF0));
                // Texture2D reference
                data["BigBackground"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xF8));
                // Texture2D reference
                data["FactionBackground"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x100));
                // Sprite reference
                data["BadgeMini"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x108));
                // Sprite reference
                data["BadgeDragged"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // Sprite reference
                data["BadgeUnitWindow"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x118));
                // Sprite reference
                data["Badge"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // Sprite reference
                data["BadgeWhite"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x128));
                // Sprite reference
                data["BigBadge"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x130));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0x138);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0x13C);
                // PerkTemplate reference
                data["InitialPerk"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x140));
                // TODO: Array/List - PerkTrees: PerkTreeTemplate[]
                // TODO: Array/List - EmotionalTriggerReactions: List<EmotionalTriggerReaction>
                // TODO: Array/List - EmotionalStateResponses: List<EmotionalStateResponse>
                // TODO: Array/List - FavorablePlanets: PlanetTemplate[]
            }

            else if (templateType.Name == "UnitRankTemplate")
            {
                // UnitRankType
                data["RankType"] = Marshal.ReadInt32(ptr + 0x78);
                // LocalizedLine reference
                data["Name"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                data["PromotionCost"] = Marshal.ReadInt32(ptr + 0x90);
            }

            else if (templateType.Name == "VehicleItemTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // Sprite reference
                data["IconEquipment"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Sprite reference
                data["IconEquipmentDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // Sprite reference
                data["IconSkillBar"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["IconSkillBarDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["IconSkillBarAlternative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["IconSkillBarAlternativeDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // ItemSlot
                data["SlotType"] = Marshal.ReadInt32(ptr + 0xE8);
                // ItemType
                data["ItemType"] = Marshal.ReadInt32(ptr + 0xEC);
                // TODO: Array/List - OnlyEquipableBy: List<TagTemplate>
                // ExclusiveItemCategory
                data["ExclusiveCategory"] = Marshal.ReadInt32(ptr + 0xF8);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xFC);
                data["IsDestroyedAfterCombat"] = Marshal.ReadByte(ptr + 0x100) != 0;
                // TODO: Array/List - SkillsGranted: List<SkillTemplate>
                // GameObject reference
                data["Model"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // VisualAlterationSlot
                data["VisualAlterationSlot"] = Marshal.ReadInt32(ptr + 0x118);
                // GameObject reference
                data["ModelSecondary"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // VisualAlterationSlot
                data["VisualAlterationSlotSecondary"] = Marshal.ReadInt32(ptr + 0x128);
                data["AttachLightAtNight"] = Marshal.ReadByte(ptr + 0x12C) != 0;
                // EntityTemplate reference
                data["EntityTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x130));
                data["AccessorySlots"] = Marshal.ReadInt32(ptr + 0x138);
            }

            else if (templateType.Name == "VoucherTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // StrategyVars
                data["VoucherType"] = Marshal.ReadInt32(ptr + 0xB8);
                data["VoucherChange"] = Marshal.ReadInt32(ptr + 0xBC);
            }

            else if (templateType.Name == "WeaponTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // LocalizedLine reference
                data["ShortName"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // LocalizedMultiLine reference
                data["Description"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x88));
                // Sprite reference
                data["Icon"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x90));
                data["Rarity"] = Marshal.ReadInt32(ptr + 0xA0);
                data["MinCampaignProgress"] = Marshal.ReadInt32(ptr + 0xA4);
                data["TradeValue"] = Marshal.ReadInt32(ptr + 0xA8);
                data["BlackMarketMaxQuantity"] = Marshal.ReadInt32(ptr + 0xAC);
                // Sprite reference
                data["IconEquipment"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // Sprite reference
                data["IconEquipmentDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
                // Sprite reference
                data["IconSkillBar"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC8));
                // Sprite reference
                data["IconSkillBarDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD0));
                // Sprite reference
                data["IconSkillBarAlternative"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xD8));
                // Sprite reference
                data["IconSkillBarAlternativeDisabled"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xE0));
                // ItemSlot
                data["SlotType"] = Marshal.ReadInt32(ptr + 0xE8);
                // ItemType
                data["ItemType"] = Marshal.ReadInt32(ptr + 0xEC);
                // TODO: Array/List - OnlyEquipableBy: List<TagTemplate>
                // ExclusiveItemCategory
                data["ExclusiveCategory"] = Marshal.ReadInt32(ptr + 0xF8);
                // OperationResources
                data["DeployCosts"] = Marshal.ReadInt32(ptr + 0xFC);
                data["IsDestroyedAfterCombat"] = Marshal.ReadByte(ptr + 0x100) != 0;
                // TODO: Array/List - SkillsGranted: List<SkillTemplate>
                // GameObject reference
                data["Model"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x110));
                // VisualAlterationSlot
                data["VisualAlterationSlot"] = Marshal.ReadInt32(ptr + 0x118);
                // GameObject reference
                data["ModelSecondary"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x120));
                // VisualAlterationSlot
                data["VisualAlterationSlotSecondary"] = Marshal.ReadInt32(ptr + 0x128);
                data["AttachLightAtNight"] = Marshal.ReadByte(ptr + 0x12C) != 0;
                // WeaponAnimType
                data["AnimType"] = Marshal.ReadInt32(ptr + 0x130);
                // AnimWeaponSize
                data["AnimSize"] = Marshal.ReadInt32(ptr + 0x134);
                // AnimWeaponGrip
                data["AnimGrip"] = Marshal.ReadInt32(ptr + 0x138);
                data["MinRange"] = Marshal.ReadInt32(ptr + 0x13C);
                data["IdealRange"] = Marshal.ReadInt32(ptr + 0x140);
                data["MaxRange"] = Marshal.ReadInt32(ptr + 0x144);
                data["AccuracyBonus"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x148)), 0);
                data["AccuracyDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x14C)), 0);
                data["Damage"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x150)), 0);
                data["DamageDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x154)), 0);
                data["DamagePctCurrentHitpoints"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x158)), 0);
                data["DamagePctCurrentHitpointsMin"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x15C)), 0);
                data["DamagePctMaxHitpoints"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x160)), 0);
                data["DamagePctMaxHitpointsMin"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x164)), 0);
                data["ArmorPenetration"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x168)), 0);
                data["ArmorPenetrationDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x16C)), 0);
                data["DamageToArmorDurability"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x170)), 0);
                data["DamageToArmorDurabilityMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x174)), 0);
                data["DamageToArmorDurabilityDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x178)), 0);
                data["DamageToArmorDurabilityDropoffMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x17C)), 0);
                data["Suppression"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x180)), 0);
            }

            else if (templateType.Name == "WeatherTemplate")
            {
                // LocalizedLine reference
                data["Title"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x78));
                // GameObject reference
                data["CameraEffect"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x80));
                // ID
                data["AmbientSound"] = Marshal.ReadInt32(ptr + 0x88);
                data["DisableDustEffects"] = Marshal.ReadByte(ptr + 0x90) != 0;
                // SkillTemplate reference
                data["SkillToApply"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0x98));
                // LightConditionTemplate reference
                data["DawnTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA0));
                // LightConditionTemplate reference
                data["DayTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xA8));
                // LightConditionTemplate reference
                data["DuskTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB0));
                // LightConditionTemplate reference
                data["NightTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xB8));
                // WindControlsTemplate reference
                data["WindControlsTemplate"] = ReadUnityObjectReference(Marshal.ReadIntPtr(ptr + 0xC0));
            }

            else if (templateType.Name == "WindControlsTemplate")
            {
                data["m_windTurbulence"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x18)), 0);
                data["m_windStrength"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x1C)), 0);
                data["m_windSpeed"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x20)), 0);
                data["m_windTiling"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(ptr + 0x24)), 0);
                // Color
                data["m_direction"] = Marshal.ReadInt32(ptr + 0x28);
                // Vector3
                data["m_windDirection"] = Marshal.ReadInt32(ptr + 0x38);
            }
            else
            {
                // Unknown template type - return basic info only
                data["_template_type"] = templateType.Name;
            }

    return data;
}