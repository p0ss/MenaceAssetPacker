# Struct Field Offsets

## EntityProperties

### Accuracy & Hit Chance
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x68 | float | BaseAccuracy | Base accuracy value |
| +0x6c | float | AccuracyMult | Accuracy multiplier (1.0 = 100%) |
| +0x70 | float | AccuracyDropoffBase | Per-tile accuracy penalty base |
| +0x74 | float | AccuracyDropoffMult | Accuracy dropoff multiplier |
| +0x78 | int | MinHitChance | Minimum hit chance floor |

### Health & Defense
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x14 | int | Health | Current/max health |
| +0x18 | float | HealthMult | Health multiplier |
| +0x1c | int | ArmorBase | Base armor value |
| +0x20 | int | ArmorFront | Frontal armor |
| +0x24 | int | ArmorSide | Side armor |
| +0x28 | float | ArmorMult | Armor multiplier |
| +0x2c | float | ArmorDurability | Current armor durability |

### Movement & Actions
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x34 | int | ActionPoints | Action point bonus |
| +0x38 | float | MovementMult | Movement multiplier |
| +0x3c | int | MovementPoints | Movement point bonus |

### Evasion & Dodge
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x84 | float | EvasionMult | Evasion multiplier |
| +0x8c | float | DodgeMult | Dodge multiplier |

### Resistance & Morale
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xa0 | float | ResistBase | Resistance base value |
| +0xa4 | float | ResistMult | Resistance multiplier |
| +0xc4 | int | MoraleBase | Base morale value |
| +0xc8 | float | MoraleMult | Morale multiplier |
| +0xcc | int | SuppressionResist | Base suppression resistance |
| +0xd0 | float | SuppressionResistMult | Suppression resistance mult |
| +0xd4 | int | Unknown1 | Modified by armor |
| +0xd8 | float | Unknown1Mult | Modified by armor |
| +0xdc | float | MoveSpeedMult | Movement speed multiplier |
| +0xe0 | float | Unknown2 | Modified by WeaponTemplate+0x184 |

### Armor Penetration
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x100 | float | ArmorPenBase | Armor penetration base |
| +0x104 | float | ArmorPenMult | Armor penetration multiplier |
| +0x108 | float | ArmorPenDropoff | Armor pen dropoff per tile |

### Damage
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x118 | float | BaseDamage | Base damage value |
| +0x11c | float | DamageMult | Damage multiplier |
| +0x120 | float | DamageDropoffBase | Damage dropoff base |
| +0x124 | float | DamageDropoffMult | Damage dropoff multiplier |
| +0x12c | float | ArmorDurDmgBase | Anti-armor damage base |
| +0x130 | float | ArmorDurDmgMult | Anti-armor damage mult |
| +0x134 | float | ArmorDurDmgDropoff | Anti-armor dropoff base |
| +0x138 | float | ArmorDurDmgDropoffMult | Anti-armor dropoff mult |

### Combat Modifiers
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x144 | float | Unknown3 | Modified by WeaponTemplate+0x15c |
| +0x148 | float | Unknown4 | Modified by WeaponTemplate+0x160 |
| +0x14c | float | Unknown5 | Modified by WeaponTemplate+0x164 |
| +0x150 | float | Unknown6 | Modified by WeaponTemplate+0x168 |

### Sight & Detection
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x154 | int | SightRange | Sight range bonus |
| +0x158 | float | SightRangeMult | Sight range multiplier |

## WeaponTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x13C | int | MinRange | From schema |
| +0x140 | int | IdealRange | From schema |
| +0x144 | int | MaxRange | From schema |
| +0x14c | float | AccuracyBonus | Base accuracy bonus |
| +0x150 | float | AccuracyDropoff | Accuracy drop per tile |
| +0x154 | float | DamageBonus | Base damage bonus |
| +0x158 | float | DamageDropoff | Damage drop per tile |
| +0x15c | float | Unknown1 | Applied to EntityProps+0x144 |
| +0x160 | float | Unknown2 | Applied to EntityProps+0x148 |
| +0x164 | float | Unknown3 | Applied to EntityProps+0x14c |
| +0x168 | float | Unknown4 | Applied to EntityProps+0x150 |
| +0x16c | float | ArmorPenBonus | Armor penetration bonus |
| +0x170 | float | ArmorPenDropoff | AP drop per tile |
| +0x174 | float | ArmorDurDmgBonus | Anti-armor damage |
| +0x178 | float | ArmorDurDmgMult | Anti-armor multiplier |
| +0x17c | float | ArmorDurDmgDropoff | Anti-armor dropoff |
| +0x180 | float | ArmorDurDmgDropoffMult | Anti-armor dropoff mult |
| +0x184 | float | Unknown5 | Applied to EntityProps+0xe0 |

## ArmorTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x190 | int | ArmorBonus | Added to all armor zones |
| +0x194 | int | ArmorDurabilityBonus | Added to durability |
| +0x198 | float | DodgePenalty | Reduces dodge (inverted) |
| +0x19c | int | HealthBonus | Added to health |
| +0x1a0 | float | HealthMult | Health multiplier |
| +0x1a4 | int | AccuracyBonus | Added to accuracy |
| +0x1a8 | float | AccuracyMult | Accuracy multiplier |
| +0x1ac | float | EvasionMult | Evasion multiplier |
| +0x1b0 | float | ResistBonus | Resistance bonus |
| +0x1b4 | float | ResistMult | Resistance multiplier |
| +0x1b8 | int | MoraleBonus | Morale bonus |
| +0x1bc | float | MoraleMult | Morale multiplier |
| +0x1c0 | int | SuppressionResistBonus | Suppression resist bonus |
| +0x1c4 | float | SuppressionResistMult | Suppression resist mult |
| +0x1d0 | float | MoveSpeedMult | Movement speed mult |
| +0x1d4 | int | SightRangeBonus | Sight range bonus |
| +0x1d8 | float | SightRangeMult | Sight range mult |
| +0x1dc | int | ActionPointBonus | Action point bonus |
| +0x1e0 | float | MovementMult | Movement multiplier |
| +0x1e4 | int | MovementBonus | Movement point bonus |

## Skill
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x10 | ptr | SkillTemplate | Reference to skill template |
| +0x18 | ptr | SkillContainer | Parent container |
| +0xB8 | int | IdealRange | Used in distance penalty calculation |

## SkillTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xF3 | byte | AlwaysHits | If nonzero, returns 100% hit |
| +0x100 | byte | IgnoresCoverAtRange | If set, cover doesn't apply |

## HitchanceEffect
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x58 | float | AccuracyBonus | Added to BaseAccuracy |
| +0x5c | float | AccuracyMult | Multiplied with AccuracyMult |
| +0x60 | float | DropoffBonus | Added to AccuracyDropoffBase |
| +0x64 | float | DropoffMult | Multiplied with AccuracyDropoffMult |

## Tile
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x20 | int | InherentCover | Tile's base cover value |
| +0x28 | int[] | CoverValues | Cover per direction (8 directions) |

## Entity
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x10 | int | ID | Unique entity ID |
| +0x18 | List | m_Segments | Entity segments |
| +0x20 | List | m_Elements | Entity elements |
| +0x28 | int | m_OriginalElementCount | Original element count |
| +0x48 | bool | m_IsAlive | Alive flag |
| +0x4C | int | m_FactionID | Faction ID |
| +0x54 | int | m_Hitpoints | Current HP |
| +0x58 | int | m_HitpointsMax | Maximum HP |
| +0x5C | int | m_ArmorDurability | Current armor durability |
| +0x60 | int | m_ArmorDurabilityMax | Maximum armor durability |
| +0x68 | Entity | m_ContainedEntity | Entity inside this one |
| +0x70 | Entity | m_ContainerEntity | Entity containing this one |
| +0x78 | Entity | m_LastAttacker | Last attacker |
| +0x80 | SkillTemplate | m_LastAttackedBySkill | Skill used in last attack |

## Actor (extends Entity)
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xA0 | Tile | m_LastTile | Last tile occupied |
| +0xC8 | Agent | m_Agent | AI agent |
| +0xD0 | ActorStance | m_Stance | Current stance |
| +0xD4 | MoraleState | m_LastMoraleState | Previous morale state |
| +0x148 | int | m_ActionPoints | Current action points |
| +0x14C | int | m_ActionPointsAtTurnStart | AP at turn start |
| +0x15C | float | m_Suppression | Current suppression |
| +0x160 | float | m_Morale | Current morale |
| +0x164 | bool | m_IsTurnDone | Turn completed flag |
| +0x16F | bool | m_IsHeavyWeaponDeployed | Heavy weapon deployed |
| +0x190 | Skill | m_ActiveSkill | Currently active skill |

## DamageInfo
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xc | int | Damage | Final damage value |
| +0x14 | int | ArmorPenetration | AP value |
| +0x18 | int | ArmorDurabilityDamage | Anti-armor damage |
| +0x1c | int | TotalShots | Number of shots/hits |
| +0x2e | byte | CanDismember | Whether can cause dismemberment |

## FactionTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x78 | LocalizedLine | Name | Faction name |
| +0x80 | LocalizedMultiLine | Description | Faction description |
| +0x88 | Sprite | Icon | Faction icon |
| +0xA0 | FactionType | AlliedFactionType | Allied faction type |
| +0xA4 | FactionType | EnemyFactionType | Enemy faction type |
| +0xA8 | OperationTemplate[] | Operations | Available operations |
| +0xB0 | EnemyAssetTemplate[] | EnemyAssets | Enemy assets |
| +0xB8 | RewardTableTemplate[] | OperationRewardTables | Reward tables |

## ArmyTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x78 | FactionType | FactionType | Faction type |
| +0x80 | FactionTemplate | FactionTemplate | Reference to faction |
| +0x88 | Vector2Int | ReqCampaignProgress | Required progress range |
| +0x90 | List | PossibleUnits | List of ArmyTemplateEntry |

## ArmyTemplateEntry
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x10 | EntityTemplate | Template | Entity template |
| +0x18 | int | Weight | Selection weight |
| +0x1C | float | WeightMultAfterPick | Weight decay after selection |

## To Be Discovered
- Full skill effect offset mappings
- AI behavior tree offsets
- Complete ElementProperties mapping
