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
        [SerializeField, HideInInspector] private uint version = 2; // For future use in data migration if needed
        [Header("Effect Type")]
        [SerializeField] private AbilityEffectType effectType;
        [SerializeField] private StatusEffect statusEffect;
        [SerializeField] private TerrainEffect terrainEffect;
        
        [Header("Effect Targeting")]
        [SerializeField] private AbilityTargetType targetType = AbilityTargetType.Everything;  // What targets can be affected by this effect
        
        [Header("Amount")]
        [SerializeField] private ValueSource amountSource;
        [SerializeField] private double amount;    // Amount of effect (e.g., damage amount, heal amount)
        
        [Header("Timing")]
        [SerializeField] private List<ValueCondition> delayConditions = new(); // Conditions that trigger the effect (Fixed = turns)
        [SerializeField] private List<ValueCondition> durationConditions = new(); // Conditions that end the effect (Fixed = turns)

        public AbilityEffectType EffectType => effectType;
        public StatusEffect StatusEffect => statusEffect;
        public TerrainEffect TerrainEffect => terrainEffect;
        public AbilityTargetType TargetType => targetType;
        public ValueSource AmountSource => amountSource;
        public double Amount => amount;
        public List<ValueCondition> DelayConditions => delayConditions;
        public List<ValueCondition> DurationConditions => durationConditions;
        
        [NonSerialized, HideInInspector] private StatusEffect prevSE;
        [NonSerialized, HideInInspector] private TerrainEffect prevTE;
        [NonSerialized, HideInInspector] private AbilityTargetType prevATT;  // Last target type when in non-terrain mode
        [NonSerialized, HideInInspector] private AbilityEffectType prevEffectType;
        [NonSerialized, HideInInspector] private double prevAmount = 1;      // Last amount when amount is free (not state status)
        [NonSerialized, HideInInspector] private ValueSource prevAmountSource = ValueSource.Fixed;
        [NonSerialized, HideInInspector] private bool prevWasStateEffect;    // True when last run was Status + state effect (amount locked to 1)
        public uint Version => version;
        public string DurationDescription {
            get {
                if (durationConditions.Count == 0) return "Instant";
                return string.Join(", ", durationConditions.ConvertAll(c => {
                    bool isMultiplier = c.ValueType == ConditionValueType.Scaled;
                    string suffix = isMultiplier ? "x" : "";
                    return $"{c.Source} {c.Type} {c.Value}{suffix}";
                }));
            }
        }

        /// <summary>
        /// Called when the ScriptableObject is loaded or values change in the editor.
        /// Enforces constraints.
        /// </summary>
        private void OnValidate() {
            // When EffectType is not Status: statusEffect must be None
            if (effectType != AbilityEffectType.Status) {
                // Only save to prevSE when leaving Status mode (statusEffect is not None)
                if (statusEffect != StatusEffect.None) {
                    prevSE = statusEffect;
                }
                statusEffect = StatusEffect.None;
            } else {
                // When EffectType is Status: statusEffect cannot be None
                if (statusEffect == StatusEffect.None) {
                    statusEffect = prevSE != StatusEffect.None ? prevSE : StatusEffect.WillUp;
                }
            }

            // When EffectType is not Terrain: terrainEffect must be None
            if (effectType != AbilityEffectType.Terrain) {
                if (terrainEffect != TerrainEffect.None) {
                    prevTE = terrainEffect;
                }
                terrainEffect = TerrainEffect.None;
                // When leaving Terrain, restore last non-terrain target type
                if (prevEffectType == AbilityEffectType.Terrain) {
                    targetType = prevATT != 0 ? prevATT : AbilityTargetType.Agents;
                }
                // Strip Empty from targetType for non-terrain effects
                targetType &= ~AbilityTargetType.Empty;
                if (targetType == 0) {
                    targetType = AbilityTargetType.Agents;
                }
                // Remember non-terrain target type for next time we leave Terrain
                prevATT = targetType;
                prevEffectType = effectType;
            } else {
                // When EffectType is Terrain: target type is always Everything
                targetType = AbilityTargetType.Everything;
                // When EffectType is Terrain: terrainEffect cannot be None
                if (terrainEffect == TerrainEffect.None) {
                    terrainEffect = prevTE != TerrainEffect.None ? prevTE : TerrainEffect.DifficultUp;
                }
                prevEffectType = effectType;
            }

            if (targetType == 0) {
                targetType = AbilityTargetType.Agents;
            }

            // Amount: state status (Status + statusEffect < 101) or state terrain (Terrain + terrainEffect < 101) locks to 1/Fixed; otherwise remember and restore
            bool isAmountLocked = (effectType == AbilityEffectType.Status && statusEffect < (StatusEffect)101)
                || (effectType == AbilityEffectType.Terrain && terrainEffect < (TerrainEffect)101);
            if (isAmountLocked) {
                amountSource = ValueSource.Fixed;
                amount = 1;
            } else {
                if (prevWasStateEffect) {
                    amount = prevAmount;
                    amountSource = prevAmountSource;
                }
                prevAmount = amount;
                prevAmountSource = amountSource;
            }
            prevWasStateEffect = isAmountLocked;

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
                    // Round to nearest integer for turn count
                    condition.Value = Math.Round(condition.Value);
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
                    // Round to nearest integer for turn count
                    condition.Value = Math.Round(condition.Value);
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
        // States
        None = 0,
        Targeted = 1,
        Targeting = 2,
        SpecializedMaintenance = 3,

        // Up effects (buffs)
        WillUp = 101,
        MomentumUp = 102,
        PowerUp = 103,
        PoisonUp = 104,
        RegenUp = 105,
        ShieldUp = 106,
        MovementUp = 107,
        ExplosionUp = 108,

        // Down effects (debuffs)
        WillDown = 201,
        MomentumDown = 202,
        PowerDown = 203,
        PoisonDown = 204,
        RegenDown = 205,
        ShieldDown = 206,
        MovementDown = 207,
        ExplosionDown = 208,
    }
    public enum TerrainEffect {
        // States
        None = 0,
        Unwalkable = 1, // Cannot be entered (e.g., wall, chasm)

        // Up effects (buffs)
        DifficultUp = 101, // Impairs movement (e.g., swamp, rubble)
        DamagingUp = 102,  // Damages agents on it (e.g., fire, acid)
        HealingUp = 103,   // Heals agents on it (e.g., healing pool)
        
        // Down effects (debuffs)
        DifficultDown = 201, // Impairs movement (e.g., swamp, rubble)
        DamagingDown = 202,  // Damages agents on it (e.g., fire, acid)
        HealingDown = 203,   // Heals agents on it (e.g., healing pool)
    }
}
