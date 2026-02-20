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
        [Header("Amount")]
        [SerializeField] private ValueSource amountSource;
        [SerializeField] private int amount;    // Amount of effect (e.g., damage amount, heal amount)
        
        [Header("Duration")]
        [SerializeField] private ValueSource durationSource;
        [SerializeField] private int duration;  // Duration of effect (e.g., number of turns)

        public AbilityEffectType EffectType => effectType;
        public StatusEffect StatusEffect => statusEffect;
        public ValueSource AmountSource => amountSource;
        public int Amount => amount;
        public ValueSource DurationSource => durationSource;
        public int Duration => duration;
    }


    public enum AbilityEffectType {
        Damage = 0,
        Heal = 1,
        Status = 2,
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
        Poison = 8,
        Regen = 9,
    }

    public enum ValueSource {
        Fixed = 0,
        TargetHP = 101,
        TargetMovement = 102,
        TargetWill = 103,
        TargetMomentum = 104,
        TargetPower = 105,
        TargetShield = 106,
        CasterHP = 201,
        CasterMovement = 202,
        CasterWill = 203,
        CasterMomentum = 204,
        CasterPower = 205,
        CasterShield = 206,
    }
}
