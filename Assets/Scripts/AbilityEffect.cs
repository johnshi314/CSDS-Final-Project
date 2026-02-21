/***********************************************************************
* File Name     : AbilityEffect.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   : ...
**********************************************************************/
using UnityEngine;
using System;
using System.Collections.Generic;

namespace NetFlower {
    /// <summary>
    ///
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityEffect", menuName = "Scriptable Objects/AbilityEffect")]
    public class AbilityEffect: ScriptableObject {
        [Header("Effect Type")]
        [SerializeField] private AbilityEffectType effectType;
        [SerializeField] private StatusEffect statusEffect;
        [SerializeField] private TerrainEffect terrainEffect;
        
        [Header("Effect Targeting")]
        [SerializeField] private AbilityTargetType targetType = AbilityTargetType.Everything;  // What targets can be affected by this effect
        
        [Header("Amount")]
        [SerializeField] private ValueSource amountSource;
        [SerializeField] private int amount;    // Amount of effect (e.g., damage amount, heal amount)
        
        [Header("Timing")]
        [SerializeField] private List<ValueCondition> delayConditions = new(); // Conditions that trigger the effect (Fixed = turns)
        [SerializeField] private List<ValueCondition> durationConditions = new(); // Conditions that end the effect (Fixed = turns)

        public AbilityEffectType EffectType => effectType;
        public StatusEffect StatusEffect => statusEffect;
        public TerrainEffect TerrainEffect => terrainEffect;
        public AbilityTargetType TargetType => targetType;
        public ValueSource AmountSource => amountSource;
        public int Amount => amount;
        public List<ValueCondition> DelayConditions => delayConditions;
        public List<ValueCondition> DurationConditions => durationConditions;
        public string Duration {
            get {
                if (durationConditions.Count == 0) return "Instant";
                return string.Join(", ", durationConditions.ConvertAll(c => {
                    bool isTurns = c.Source == ValueSource.Fixed;
                    string suffix = isTurns ? " turns" : (c.ValueType == ConditionValueType.Percentage ? "%" : "");
                    return $"{(isTurns ? "" : c.Source + " ")}{c.Type} {c.Value}{suffix}";
                }));
            }
        }

        /// <summary>
        /// Called when the ScriptableObject is loaded or values change in the editor.
        /// Enforces constraints.
        /// </summary>
        private void OnValidate() {
            // TargetType cannot be empty (0) — default to Everything
            if (targetType == 0) {
                targetType = AbilityTargetType.Everything;
            }
            
            // When EffectType is not Status: statusEffect must be None
            if (effectType != AbilityEffectType.Status) {
                statusEffect = StatusEffect.None;
            } else {
                // When EffectType is Status: statusEffect cannot be None
                if (statusEffect == StatusEffect.None) {
                    statusEffect = StatusEffect.Will; // Default to first non-None
                }
            }
            
            // When EffectType is not Terrain: terrainEffect must be None
            if (effectType != AbilityEffectType.Terrain) {
                terrainEffect = TerrainEffect.None;
            } else {
                // When EffectType is Terrain: terrainEffect cannot be None
                if (terrainEffect == TerrainEffect.None) {
                    terrainEffect = TerrainEffect.Difficult; // Default to first non-None
                }
            }
            // Check conditions, if the source is fixed, the value must be non-negative, and condition value type must be Fixed as well.
            foreach (var condition in delayConditions) {
                // TargetCount is not valid for conditions - reset to Fixed
                if (condition.Source == ValueSource.TargetCount) {
                    condition.Source = ValueSource.Fixed;
                }
                if (condition.Source == ValueSource.Fixed) {
                    if (condition.Value < 0) {
                        condition.Value = 0;
                    }
                    condition.ValueType = ConditionValueType.Fixed;
                }
            }
            foreach (var condition in durationConditions) {
                // TargetCount is not valid for conditions - reset to Fixed
                if (condition.Source == ValueSource.TargetCount) {
                    condition.Source = ValueSource.Fixed;
                }
                if (condition.Source == ValueSource.Fixed) {
                    if (condition.Value < 0) {
                        condition.Value = 0;
                    }
                    condition.ValueType = ConditionValueType.Fixed;
                }
            }
        }
    }


    public enum AbilityEffectType {
        Damage = 0,
        Heal = 1,
        Status = 2,
        Terrain = 3,
    }

    public enum StatusEffect {
        None = 0,
        Will = 1,
        Momentum = 2,
        PowerUp = 3,
        PowerDown = 4,
        Shield = 5,
        MovementUp = 6,
        MovementDown = 7,
        Targeted = 8,
        Targeting = 9,
        CanOnlyBeHealedBySourceOfThisEffect = 10,
        Poison = 11,
        Regen = 12,
        PopOnDeath = 13,
    }
    public enum TerrainEffect {
        None = 0,
        Difficult = 1, // Impairs movement (e.g., swamp, rubble)
        Unwalkable = 2, // Cannot be entered (e.g., wall, chasm)
        Damaging = 3,  // Damages agents on it (e.g., fire, acid)
        Healing = 4,   // Heals agents on it (e.g., healing pool)
    }

    public enum ValueSource {
        Fixed = 0,
        TargetCount = 100,
        TargetHP = 101,
        TargetMovement = 102,
        TargetWill = 103,
        TargetMomentum = 104,
        TargetPower = 105,
        TargetShield = 106,
        TargetMaxHP = 107,
        CasterHP = 201,
        CasterMovement = 202,
        CasterWill = 203,
        CasterMomentum = 204,
        CasterPower = 205,
        CasterShield = 206,
        CasterMaxHP = 207,
    }

    public enum ConditionType {
        EQ = 0,
        NE = 1,
        GT = 2,
        LT = 3,
        GE = 4,
        LE = 5,
    }

    public enum ConditionValueType {
        Fixed = 0,
        Percentage = 1,
    }

    public enum ConditionConnector {
        AND = 0,
        OR = 1,
    }

    [Serializable]
    public class ValueCondition {
        public ConditionConnector ConnectorToNext = ConditionConnector.AND; // How this connects to the next condition in the list (AND/OR)
        public ConditionType Type = ConditionType.EQ; // Type of comparison
        public ValueSource Source = ValueSource.Fixed; // What value to check (e.g., TargetHP, CasterWill, etc.)
        public int Value = 0;
        public ConditionValueType ValueType = ConditionValueType.Fixed; // If Percentage, Value is treated as a percentage (e.g., "TargetHP < 50%")
    }
}
