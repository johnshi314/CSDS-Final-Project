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
        /// Checks if the Caster is even allowed to target the specified target with this ability based on the context.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>True if the context is valid, false otherwise.</returns>
        public static bool IsValidContext(AbilityUseContext context) {
            if (context.Ability == null || context.Caster == null)
                return false;

            // Check if the target tile is valid spot for the caster to use the ability
            // Not based on range, simply target type (e.g., empty vs occupied)
            var targetType = context.Ability.TargetType;
            var targetTile = context.TargetTile;
            var map = targetTile.Map;
            var targetAgent = map.GetAgentAtTile(targetTile);

            if (targetAgent == null) {
                // Targeting an empty tile
                if (!targetType.HasFlag(AbilityTargetType.Empty))
                    return false;
            } else {
                // Targeting an occupied tile
                // TODO: Implement teams checking
                // bool isAlly = (targetAgent.Team == context.Caster.Team);
                bool isAlly = (targetAgent == context.Caster);
                if (isAlly && !targetType.HasFlag(AbilityTargetType.Ally))
                    return false;
                if (!isAlly && !targetType.HasFlag(AbilityTargetType.NonAlly))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Resolve all effects of this ability using the provided context.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        public static void Resolve(AbilityUseContext context) {
            // Warn if context is invalid, but attempt to resolve anyway to avoid breaking the game (e.g., if UI passes in incomplete context)
            if (!IsValidContext(context))
                Debug.LogWarning("Resolving ability with invalid context");

            // If there are no effects, nothing to resolve
            if (context.Ability.Effects == null || context.Ability.Effects.Count == 0)
                return;
            
            // Get all targets in the area of effect based on the ability's shape
            var targetAgents = GetTargetsInShape(context);

            // Apply each effect to each target
            foreach (var effect in context.Ability.Effects) {
                foreach (Agent target in targetAgents) {
                    // Create an instance of the effect for this target
                    var effectInstance = new AbilityEffectInstance(effect, context.Caster);

                    // Apply the effect immediately
                    effectInstance.ApplyTo(target);

                    // If the effect has a duration, add it to the target's active effects
                    if (effect.Duration > 0) {
                        // target.AddEffect(effectInstance);
                        //TODO: Fix how effects are added to agents
                    }
                }
            }
        }

        /// <summary>
        ///  Get all agents affected by the ability based on its shape and the target tile in the context.
        /// </summary>
        /// <param name="context">The ability use context.</param>
        /// <returns>A list of agents affected by the ability.</returns>
        public static List<Agent> GetTargetsInShape(AbilityUseContext context) {
            return GetTargetsInShape(context.Ability.TargetShape, context.TargetTile.Map, context.TargetTile.Position);
        }
        public static List<Agent> GetTargetsInShape(AbilityTargetShape shape, Tile targetTile) {
            return GetTargetsInShape(shape, targetTile.Map, targetTile.Position);
        }

        public static List<Agent> GetTargetsInShape(AbilityTargetShape shape, Map map, Vector2Int targetPos) {
            var agents = new List<Agent>();
            var tiles = GetTilesInShape(shape, map, targetPos);

            foreach (var tile in tiles) {
                var agent = tile.Map.GetAgentAtTile(tile);
                if (agent != null) {
                    agents.Add(agent);
                }
            }
            return agents;
        }

        /// <summary>
        /// Get all tiles affected by the ability based on its shape.
        /// </summary>
        /// <param name="shape">The shape of the ability's area of effect.</param>
        /// <param name="map">The map to get tiles from.</param>
        /// <param name="targetPos">The position of the target tile.</param>
        /// <returns>List of tiles affected by the ability.</returns>
        public static List<Tile> GetTilesInShape(AbilityUseContext context) {
            return GetTilesInShape(context.Ability.TargetShape, context.TargetTile.Map, context.TargetTile.Position);
        }

        public static List<Tile> GetTilesInShape(AbilityTargetShape shape, Tile targetTile) {
            return GetTilesInShape(shape, targetTile.Map, targetTile.Position);
        }
        
        public static List<Tile> GetTilesInShape(AbilityTargetShape shape, Map map, Vector2Int targetPos) {
            var tiles = new List<Tile>();
            Tile centerTile = map.GetTileAtPosition(targetPos);

            switch (shape) {
                case AbilityTargetShape.Single:
                    tiles.Add(centerTile);
                    break;
                case AbilityTargetShape.Circle:
                    //  _ x _
                    //  x x x
                    //  _ x _
                    tiles.Add(centerTile);
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
                    //  x _ x
                    //  _ x _
                    //  x _ x
                    tiles.Add(centerTile);
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
                    tiles.Add(centerTile);
                    break;
                case AbilityTargetShape.Cone:
                    Debug.LogWarning("Cone shape not yet implemented");
                    tiles.Add(centerTile);
                    break;
                case AbilityTargetShape.Square:
                    //  x x x
                    //  x x x
                    //  x x x
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
            // switch (Effect.EffectType) {
            //     case AbilityEffectType.Damage:
            //         target.TakeDamage((int)Effect.Amount);
            //         break;
            //     case AbilityEffectType.Heal:
            //         target.Heal((int)Effect.Amount);
            //         break;
            //     case AbilityEffectType.BuffRange:
            //         // TODO: Implement buff system
            //         Debug.LogWarning("BuffRange effect not yet implemented");
            //         break;
            //     case AbilityEffectType.DebuffRange:
            //         // TODO: Implement debuff system
            //         Debug.LogWarning("DebuffRange effect not yet implemented");
            //         break;
            // }
        }
    }
}
