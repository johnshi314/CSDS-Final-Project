/***********************************************************************
* File Name     : ValueCondition.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-22
* Description   : Shared types for value-based conditions (duration, delay, etc.).
*                 Used by AbilityEffect and Summon.
**********************************************************************/
using System;
using System.Collections.Generic;

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

        public bool IsMet(int turn) {
            if (Source != ValueSource.Fixed) return false;
            switch (Type) {
                case ConditionType.EQ:
                    return Value == turn;
                case ConditionType.NE:
                    return Value != turn;
                case ConditionType.GT:
                    return Value > turn;
                case ConditionType.LT:
                    return Value < turn;
                case ConditionType.GE:
                    return Value >= turn;
                case ConditionType.LE:
                    return Value <= turn;
                default:
                    return false;
            }
        }

        /// <summary>Gets the numeric value for the given source from caster/target (0 for missing stats).</summary>
        public static double GetValueFromAgents(ValueSource source, Agent casterAgent, Agent targetAgent) {
            Agent a = (source >= ValueSource.CasterHP && source <= ValueSource.CasterMaxHP) ? casterAgent : targetAgent;
            if (a == null) return 0;
            switch (source) {
                case ValueSource.TargetHP:
                case ValueSource.CasterHP:
                    return a.HP;
                case ValueSource.TargetMaxHP:
                case ValueSource.CasterMaxHP:
                    return a.MaxHP;
                case ValueSource.TargetMovement:
                case ValueSource.CasterMovement:
                    return a.MovementRange;
                case ValueSource.TargetWill:
                case ValueSource.CasterWill:
                case ValueSource.TargetMomentum:
                case ValueSource.CasterMomentum:
                case ValueSource.TargetPower:
                case ValueSource.CasterPower:
                case ValueSource.TargetShield:
                case ValueSource.CasterShield:
                    // TODO: add these to Agent when implemented
                    return 0;
                default:
                    return 0;
            }
        }

        public bool IsMet(Agent casterAgent, Agent targetAgent) {
            double valueSourceValue = GetValueFromAgents(Source, casterAgent, targetAgent);

            switch (Type) {
                case ConditionType.EQ:
                    return valueSourceValue == Value;
                case ConditionType.NE:
                    return valueSourceValue != Value;
                case ConditionType.GT:
                    return valueSourceValue > Value;
                case ConditionType.LT:
                    return valueSourceValue < Value;
                case ConditionType.GE:
                    return valueSourceValue >= Value;
                case ConditionType.LE:
                    return valueSourceValue <= Value;
                default:
                    // Invalid type, return false
                    return false;
            }
        }
    }

    /// <summary>
    /// Runtime evaluation of a list of ValueConditions. You don't store context on the instance;
    /// you pass it when calling IsMet so the same instance can be reused (e.g. for duration checks each turn).
    /// </summary>
    public class ValueConditions {
        public List<ValueCondition> Conditions;

        public ValueConditions() { Conditions = new List<ValueCondition>(); }
        public ValueConditions(List<ValueCondition> conditions) {
            Conditions = conditions ?? new List<ValueCondition>();
        }

        /// <summary>
        /// Evaluate all conditions with the given context. ConnectorToNext on each condition
        /// connects that condition's result to the next (first condition stands alone, then
        /// result = result AND/OR nextResult using the previous condition's connector).
        /// </summary>
        /// <param name="currentTurn">Current turn number (for Fixed source conditions).</param>
        /// <param name="sourceAgent">Caster/source agent (for Caster* sources).</param>
        /// <param name="targetAgent">Target agent (for Target* sources).</param>
        /// <returns>True if the combined condition list is met.</returns>
        public bool IsMet(int currentTurn, Agent sourceAgent, Agent targetAgent) {
            if (Conditions == null || Conditions.Count == 0)
                return true; // empty = vacuously true (e.g. "no duration" means never expires)

            bool result = EvalOne(Conditions[0], currentTurn, sourceAgent, targetAgent);
            for (int i = 1; i < Conditions.Count; i++) {
                bool nextResult = EvalOne(Conditions[i], currentTurn, sourceAgent, targetAgent);
                ConditionConnector connector = Conditions[i - 1].ConnectorToNext;
                if (connector == ConditionConnector.AND)
                    result = result && nextResult;
                else
                    result = result || nextResult;
            }
            return result;
        }

        static bool EvalOne(ValueCondition c, int currentTurn, Agent sourceAgent, Agent targetAgent) {
            if (c == null) return false;
            if (c.Source == ValueSource.Fixed)
                return c.IsMet(currentTurn);
            return c.IsMet(sourceAgent, targetAgent);
        }
    }
}
