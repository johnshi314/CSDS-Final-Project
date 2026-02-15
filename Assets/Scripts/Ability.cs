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
                effect.Apply(context);
            }
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
        public AbilityEffectType EffectType; // Type of effect (e.g., damage, heal, buff)
        public uint Amount; // Amount of effect (e.g., damage amount, heal amount)
        public uint Duration; // Duration of effect (e.g., number of turns)
        public Agent Source {get; private set;} // The agent that is the source of this effect (e.g., the caster)

        /// <summary>
        /// Create a copy of this effect.
        /// </summary>
        /// <returns>A new AbilityEffect with the same values.</returns>
        public AbilityEffect Clone() {
            return new AbilityEffect {
                Source = this.Source,
                EffectType = this.EffectType,
                Amount = this.Amount,
                Duration = this.Duration
            };
        }

        /// <summary>
        /// Apply this effect to all valid targets based on the ability context.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        public void Apply(AbilityUseContext context) {
            // Set the source of the effect to the caster if not already set
            if (Source == null) {
                Source = context.Caster;
            }

            // Get all targets affected by this ability
            var targets = GetTargetsInShape(context);
            
            foreach (var target in targets) {
                // TODO: Implement proper ally/non-ally checking when team system is in place
                bool targetIsAlly = target == context.Caster;
                
                // Skip invalid targets based on ability's TargetType
                if (targetIsAlly && !context.Ability.TargetType.HasFlag(AbilityTargetType.Ally))
                    continue;
                if (!targetIsAlly && !context.Ability.TargetType.HasFlag(AbilityTargetType.NonAlly))
                    continue;
 
                // Apply immediately
                ApplyToAgent(target);
                
                // If persistent effect, add to agent's active effects
                if (Duration > 0) {
                    target.AddEffect(this);
                }
            }
        }

        /// <summary>
        /// Apply the effect to a single agent immediately.
        /// </summary>
        /// <param name="agent">The agent to apply the effect to.</param>
        public void ApplyToAgent(Agent agent) {
            switch (EffectType) {
                case AbilityEffectType.Damage:
                    agent.TakeDamage((int)Amount);
                    break;
                case AbilityEffectType.Heal:
                    agent.Heal((int)Amount);
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

        /// <summary>
        /// Get all agents affected by this ability based on its shape and targeting.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>List of agents affected by this ability.</returns>
        private List<Agent> GetTargetsInShape(AbilityUseContext context) {
            var targets = new List<Agent>();
            var ability = context.Ability;
            var targetTile = context.TargetTile;
            var map = targetTile.Map;

            // Get all tiles affected by the ability's shape
            var affectedTiles = GetAffectedTiles(context);

            // For each affected tile, check if there's an agent and if it's a valid target
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
        private List<Tile> GetAffectedTiles(AbilityUseContext context) {
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
                    // Get center tile
                    tiles.Add(context.TargetTile);
                    // Get orthogonally adjacent tiles
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
                    // Get the center tile
                    tiles.Add(context.TargetTile);
                    // Get top left corner
                    Tile tl = map.GetTileAtPosition(new Vector2Int(targetPos.x - 1, targetPos.y - 1));
                    if (tl != null) tiles.Add(tl);
                    // Get top right corner
                    Tile tr = map.GetTileAtPosition(new Vector2Int(targetPos.x + 1, targetPos.y - 1));
                    if (tr != null) tiles.Add(tr);
                    // Get bottom left corner
                    Tile bl = map.GetTileAtPosition(new Vector2Int(targetPos.x - 1, targetPos.y + 1));
                    if (bl != null) tiles.Add(bl);
                    // Get bottom right corner
                    Tile br = map.GetTileAtPosition(new Vector2Int(targetPos.x + 1, targetPos.y + 1));
                    if (br != null) tiles.Add(br);
                    break;
                case AbilityTargetShape.Line:
                    // TODO: Implement line shape
                    Debug.LogWarning("Line shape not yet implemented");
                    tiles.Add(context.TargetTile); // Placeholder: just target the center tile for now
                    break;
                case AbilityTargetShape.Cone:
                    // TODO: Implement cone shape
                    Debug.LogWarning("Cone shape not yet implemented");
                    tiles.Add(context.TargetTile); // Placeholder: just target the center tile for now
                    break;
                case AbilityTargetShape.Square:
                    // xxx
                    // xxx
                    // xxx
                    // Get all tiles touching the target tile in a 3x3 square
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
                    // Shape determined by resolver, for now just return empty list
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

            // Check if agent is knocked out
            if (agent.KOed())
                return false;

            // Determine if target is ally or non-ally
            // TODO: Implement proper team checking when team system is in place
            bool isAlly = (agent == caster); // Simplified: only caster is considered ally for now

            // Check target type flags
            if (isAlly && !targetType.HasFlag(AbilityTargetType.Ally))
                return false;
            if (!isAlly && !targetType.HasFlag(AbilityTargetType.NonAlly))
                return false;

            return true;
        }
    }

    public enum AbilityEffectType {
        Damage = 0,
        Heal = 1,
        BuffRange = 2,
        DebuffRange = 3,
    }
}
