/***********************************************************************
* File Name     : ValueCondition.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-22
* Description   : Shared types for value-based conditions (duration, delay, etc.).
*                 Used by AbilityEffect and Summon.
**********************************************************************/
using System;

namespace NetFlower {

    public enum ValueSource {
        // Fixed
        Fixed = 0,
        TargetCount = 100,

        // Values Derived from Target of AbilityEffect
        TargetHP = 101,
        TargetMovement = 102,
        TargetWill = 103,
        TargetMomentum = 104,
        TargetPower = 105,
        TargetShield = 106,
        TargetMaxHP = 107,

        // Values Derived from Caster of AbilityEffect
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
        Scaled = 1,
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
        public double Value = 0;
        public ConditionValueType ValueType = ConditionValueType.Fixed; // If Scaled, Value is treated as a multiplier (e.g., "TargetHP x 2")
    }
}
