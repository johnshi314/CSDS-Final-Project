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
        [SerializeField] private AbilityEffectType effectType;
        [SerializeField] private StatusEffect statusEffect;
        [SerializeField] private uint amount;    // Amount of effect (e.g., damage amount, heal amount)
        [SerializeField] private uint duration;           // Duration of effect (e.g., number of turns)

        public AbilityEffectType EffectType => effectType;
        public StatusEffect StatusEffect => statusEffect;
        public uint Amount => amount;
        public uint Duration => duration;

        public AbilityEffect(AbilityEffectType effectType, StatusEffect statusEffect, uint amount, uint duration = 0) {
            this.effectType = effectType;
            this.statusEffect = statusEffect;
            this.amount = amount;
            this.duration = duration;
        }

        /// <summary>
        /// Create a copy of this effect.
        /// </summary>
        /// <returns>A new AbilityEffect with the same values.</returns>
        public AbilityEffect Clone() {
            return new AbilityEffect(
                effectType: this.EffectType,
                statusEffect: this.StatusEffect,
                amount: this.Amount,
                duration: this.Duration);
        }
    }


    public enum AbilityEffectType {
        Damage = 0,
        Heal = 1,
        Status = 2,
    }

    public enum StatusEffect {
        Will = 0,
        Momentum = 1,
        PowerUp = 2,
        PowerDown = 3,
        Shield = 4,
        MovementUp = 5,
        MovementDown = 6,
    }
}
