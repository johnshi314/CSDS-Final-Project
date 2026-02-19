/***********************************************************************
* File Name     : Ability.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   : Data structures representing abilities in the game,
*                 including:
*   - Ability:              Core ability data,
*   - AbilityTargetType:    Categories of targets abilities can affect.
*   - AbilityTargetMode:    How abilities select targets,
*   - AbilityTargetShape:   Shape of the area affected by abilities,
*   - AbilityUseContext:    Context information for ability usage.
*   - AbilityModifiers:     Modifiers that can be applied to ability effects.
**********************************************************************/
using UnityEngine;
using System;
using System.Collections.Generic;

namespace NetFlower {
    /// <summary>
    /// Data structure representing an ability that agents can use in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Scriptable Objects/Ability")]
    public class Ability: ScriptableObject {
        [Header("Identity")]
        public string Id;                       // ID for referencing cross system and database
        public string DisplayName;              // Name of ability

        [Header("Targeting")]
        public AbilityTargetType TargetType;    // Type of targets allowed
        public AbilityTargetMode TargetMode;    // If this is point-select or global
        public AbilityTargetShape TargetShape;  // Shape of area affected
        public uint RangeMax;                   // Max range from caster
        public uint RangeMin;                   // Min range from caster (0 will allow self-targeting)

        [Header("Costs")]
        public uint Cost;                       // Resource cost to use ability (e.g., mana, stamina)
        public uint Cooldown;                   // Number of turns before ability can be used again after use

        [Header("Effects")]
        public List<AbilityEffect> Effects;     // List of effects this ability applies to targets

        /// <summary>
        /// Resolve all effects of this ability using the provided context.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        public void Resolve(AbilityUseContext context) {
            if (Effects == null || Effects.Count == 0)
                return;
            
            foreach (var effect in Effects) {
                List<Agent> validTargets = GetValidTargets(context);

                foreach (Agent target in validTargets) {
                    // Create an instance of the effect for this target
                    var effectInstance = new AbilityEffectInstance(effect, context.Caster);

                    // Apply the effect immediately
                    effectInstance.ApplyTo(target);

                    // If the effect has a duration, add it to the target's active effects
                    if (effect.Duration > 0) {
                        target.AddEffect(effectInstance);
                    }
                }
            }
        }

        /// <summary>
        /// Get all agents affected by this ability based on its shape and targeting.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>List of agents affected by this ability.</returns>
        private List<Agent> GetValidTargets(AbilityUseContext context) {
            var targets = new List<Agent>();
            var targetTile = context.TargetTile;
            var map = targetTile.Map;

            var affectedTiles = GetTargetsInShape(context);

            foreach (var tile in affectedTiles) {
                var agent = map.GetAgentAtTile(tile);
                if (agent != null && IsValidTarget(agent, context)) {
                    targets.Add(agent);
                }
            }

            return targets;
        }

        /// <summary>
        /// Get all tiles affected by the ability based on its shape.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>List of tiles affected by the ability.</returns>
        private List<Tile> GetTargetsInShape(AbilityUseContext context) {
            var tiles = new List<Tile>();
            var shape = context.Ability.TargetShape;
            var targetPos = context.TargetTile.Position;
            var map = context.TargetTile.Map;

            switch (shape) {
                case AbilityTargetShape.Single:
                    tiles.Add(context.TargetTile);
                    break;
                case AbilityTargetShape.Circle:
                    // _x_
                    // xxx
                    // _x_
                    tiles.Add(context.TargetTile);
                    Tile up = map.GetTileAtPosition(new Vector2Int(targetPos.x, targetPos.y - 1));
                    if (up != null) tiles.Add(up);
                    Tile down = map.GetTileAtPosition(new Vector2Int(targetPos.x, targetPos.y + 1));
                    if (down != null) tiles.Add(down);
                    Tile left = map.GetTileAtPosition(new Vector2Int(targetPos.x - 1, targetPos.y));
                    if (left != null) tiles.Add(left);
                    Tile right = map.GetTileAtPosition(new Vector2Int(targetPos.x + 1, targetPos.y));
                    if (right != null) tiles.Add(right);
                    break;
                case AbilityTargetShape.Cross:
                    // x_x
                    // _x_
                    // x_x
                    tiles.Add(context.TargetTile);
                    Tile tl = map.GetTileAtPosition(new Vector2Int(targetPos.x - 1, targetPos.y - 1));
                    if (tl != null) tiles.Add(tl);
                    Tile tr = map.GetTileAtPosition(new Vector2Int(targetPos.x + 1, targetPos.y - 1));
                    if (tr != null) tiles.Add(tr);
                    Tile bl = map.GetTileAtPosition(new Vector2Int(targetPos.x - 1, targetPos.y + 1));
                    if (bl != null) tiles.Add(bl);
                    Tile br = map.GetTileAtPosition(new Vector2Int(targetPos.x + 1, targetPos.y + 1));
                    if (br != null) tiles.Add(br);
                    break;
                case AbilityTargetShape.Line:
                    Debug.LogWarning("Line shape not yet implemented");
                    tiles.Add(context.TargetTile);
                    break;
                case AbilityTargetShape.Cone:
                    Debug.LogWarning("Cone shape not yet implemented");
                    tiles.Add(context.TargetTile);
                    break;
                case AbilityTargetShape.Square:
                    // xxx
                    // xxx
                    // xxx
                    for (int dx = -1; dx <= 1; dx++) {
                        for (int dy = -1; dy <= 1; dy++) {
                            int x = targetPos.x + dx;
                            int y = targetPos.y + dy;
                            Tile tile = map.GetTileAtPosition(new Vector2Int(x, y));
                            if (tile != null) {
                                tiles.Add(tile);
                            }
                        }
                    }
                    break;
                case AbilityTargetShape.None:
                    break;
            }

            return tiles;
        }

        /// <summary>
        /// Check if an agent is a valid target for this ability.
        /// </summary>
        /// <param name="agent">The agent to check.</param>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>True if the agent is a valid target, false otherwise.</returns>
        private bool IsValidTarget(Agent agent, AbilityUseContext context) {
            var targetType = context.Ability.TargetType;
            var caster = context.Caster;

            if (agent.KOed())
                return false;

            bool isAlly = (agent == caster);

            if (isAlly && !targetType.HasFlag(AbilityTargetType.Ally))
                return false;
            if (!isAlly && !targetType.HasFlag(AbilityTargetType.NonAlly))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Specifies categories of targets that an ability can affect.
    /// </summary>
    [Flags]
    public enum AbilityTargetType {
        Ally        = 1 << 0,                  // Able to target allies
        NonAlly     = 1 << 1,                  // Able to target non-allies
        Empty       = 1 << 2,                  // Able to target empty tiles
        Agent       = Ally | NonAlly,          // Able to target all units
        Everything  = Empty | Ally | NonAlly,  // Able to target all units
    }

    /// <summary>
    /// Specifies how an ability selects its targets.
    /// </summary>
    public enum AbilityTargetMode {
        Point = 0,  // Select a tile/unit in range
        Global = 1, // Applies to all valid targets on the board
    }

    /// <summary>
    /// Specifies the shape of the area affected by an ability.
    /// </summary>
    public enum AbilityTargetShape {
        Single = 0,     // Single target
        Line = 1,       // Line shape
        Cone = 2,       // Cone shape
        Circle = 3,     // Circular area
        Cross = 4,      // Cross shape
        Square =  5,    // Square shape
        None = 6        // Shape determined by resolver (e.g., FFT Mathematician)
    }

    /// <summary>
    /// Context information for an ability being used for when resolving its effects.
    /// </summary>
    public class AbilityUseContext {
        public Ability Ability;
        public Agent Caster;
        public Tile TargetTile;
    }

    /// <summary>
    /// Modifiers that can be applied to an ability's effects.
    /// </summary>
    public struct AbilityModifier
    {
        public int RangeMaxDelta;
        public int RangeMinDelta;
        public int DamageDelta;
    }

    [Serializable]
    public class AbilityEffect {
        [SerializeField] private AbilityEffectType effectType;
        [SerializeField] private uint amount;    // Amount of effect (e.g., damage amount, heal amount)
        [SerializeField] private uint duration;           // Duration of effect (e.g., number of turns)

        public AbilityEffectType EffectType => effectType;
        public uint Amount => amount;
        public uint Duration => duration;

        public AbilityEffect(AbilityEffectType effectType, uint amount, uint duration = 0) {
            this.effectType = effectType;
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
                amount: this.Amount,
                duration: this.Duration);
        }
    }

    public enum AbilityEffectType {
        Damage = 0,
        Heal = 1,
        BuffRange = 2,
        DebuffRange = 3,
    }

    public class AbilityEffectInstance {
        public AbilityEffect Effect;
        public Agent Source; // The agent that caused this effect (e.g., the caster of the ability)
        public int Duration; // Number of turns remaining for this effect (0 for instant effects)

        public AbilityEffectInstance(AbilityEffect effect, Agent source) {
            Effect = effect.Clone(); // Clone to ensure each instance is independent
            Source = source;
            Duration = (int)effect.Duration;
        }

        public void ApplyTo(Agent target) {
            switch (Effect.EffectType) {
                case AbilityEffectType.Damage:
                    target.TakeDamage((int)Effect.Amount);
                    break;
                case AbilityEffectType.Heal:
                    target.Heal((int)Effect.Amount);
                    break;
                case AbilityEffectType.BuffRange:
                    // TODO: Implement buff system
                    Debug.LogWarning("BuffRange effect not yet implemented");
                    break;
                case AbilityEffectType.DebuffRange:
                    // TODO: Implement debuff system
                    Debug.LogWarning("DebuffRange effect not yet implemented");
                    break;
            }
        }
    }
}
