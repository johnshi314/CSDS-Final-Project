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
using System.Linq;

namespace NetFlower {
    /// <summary>
    /// Data structure representing an ability that agents can use in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Scriptable Objects/Ability")]
    public class Ability: ScriptableObject {
        [SerializeField, HideInInspector] private uint version = 1; // For future use in data migration if needed
        [Header("Identity")]
        public string Id;                       // ID for referencing cross system and database
        public string DisplayName;              // Name of ability

        [Header("Targeting")]
        public AbilityTargetType TargetType;    // Type of targets allowed
        public AbilityTargetMode TargetMode;    // If this is point-select or global
        public AbilityTargetShape TargetShape;  // Shape of area affected
        public uint RangeMax = 1;               // Max range from caster
        public uint RangeMin = 0;               // Min range from caster (0 will allow self-targeting)
        public uint ShapeRangeMax = 1;          // Size of area shape (e.g., radius for circle, length for line)
        public uint ShapeRangeMin = 0;          // Minimum size of area shape (e.g., minimum radius for circle, minimum length for line)
        public uint SelectCount = 1;            // Number of targets that can be selected (only applicable for Point mode)

        [Header("Costs")]
        public uint Cost = 1;                   // Resource cost to use ability (e.g., mana, stamina)
        public uint Cooldown = 0;               // Number of turns before ability can be used again after use
        [Tooltip("If true, ability is on cooldown from turn 0 (becomes available at turn Cooldown).")]
        public bool StartsOnCooldown = true;

        // [Header("Effects")]
        public List<AbilityEffect> TargetEffects;     // List of effects this ability applies to targets
        public List<AbilityEffect> CasterEffects;     // List of effects this ability applies to the caster

        public uint Version => version;

        /// <summary>
        /// Called when the ScriptableObject is loaded or values change in the editor.
        /// Enforces targeting constraints.
        /// </summary>
        protected virtual void OnValidate() {
            // TargetType cannot be empty (0) — default to Everything
            if (TargetType == 0) {
                TargetType = AbilityTargetType.Everything;
            }

            // When Global mode: shape and ranges are not applicable
            if (TargetMode == AbilityTargetMode.Global) {
                TargetShape = AbilityTargetShape.None;
                ShapeRangeMax = 0;
                ShapeRangeMin = 0;
                RangeMax = 0;
                RangeMin = 0;
                SelectCount = 1; // Global always affects exactly 1 "selection" (the whole board)
            } else {
                // When Point mode: shape cannot be None
                if (TargetShape == AbilityTargetShape.None) {
                    TargetShape = AbilityTargetShape.Single;
                }
                
                // When Single shape: shape range is not applicable (only one tile)
                if (TargetShape == AbilityTargetShape.Single) {
                    ShapeRangeMax = 0;
                    ShapeRangeMin = 0;
                }
                
                // SelectCount must be at least 1 for Point mode
                if (SelectCount < 1) {
                    SelectCount = 1;
                }
            }
        }

        /// <summary>
        /// Checks if the Caster is even allowed to target the specified target with this ability based on the context.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>True if the context is valid, false otherwise.</returns>
        public static bool IsValidContext(AbilityUseContext context) {
            if (context == null) return false;
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
        /// Override in subclasses (e.g. AbilitySummon) for custom resolution.
        /// </summary>
        /// <param name="context">Context information for ability resolution.</param>
        /// <returns>Resolution information including all effect instances created during resolution.</returns>
        public virtual AbilityUseResolution Resolve(AbilityUseContext context) {
            // Keep track of all effect instances created during this resolution to return
            List<AbilityEffectInstance> effectInstances = new List<AbilityEffectInstance>();

            // Warn if context is invalid, but attempt to resolve anyway to avoid breaking the game (e.g., if UI passes in incomplete context)
            if (!IsValidContext(context))
                Debug.LogWarning("Resolving ability with invalid context");


            // Apply caster effects to the caster
            if (context.Ability.CasterEffects != null)
            {
                foreach (var effect in context.Ability.CasterEffects)
                {
                    var instance = new AbilityEffectInstance(
                        effect: effect,
                        source: context.Caster,
                        targetAgent: context.Caster,
                        turnApplied: context.TurnNumber);
                    instance.Apply();
                    effectInstances.Add(instance);
                    if (instance.HasDuration)
                        context.Caster.AddEffect(instance);
                }
            }

            // Apply target effects to all valid targets (including caster if valid)
            if (context.Ability.TargetEffects != null)
            {
                foreach (var effect in context.Ability.TargetEffects)
                {
                    if (effect.IsTileBound)
                    {
                        var tiles = GetTilesInShape(context);
                        var map = context.TargetTile.Map;
                        foreach (Tile tile in tiles)
                        {
                            var instance = new AbilityEffectInstance(
                                effect: effect,
                                source: context.Caster,
                                targetTile: tile,
                                turnApplied: context.TurnNumber);
                            effectInstances.Add(instance);
                            instance.Apply();
                            if (instance.HasDuration && map != null)
                                map.AddEffect(tile, instance);
                        }
                    }
                    else
                    {
                        var agents = GetTargetsInShape(context);
                        foreach (Agent agent in agents)
                        {
                            // Only apply if agent is a valid target type for this ability
                            bool isCaster = agent == context.Caster;
                            bool isAlly = isCaster; // TODO: Replace with real team logic
                            var targetType = context.Ability.TargetType;
                            if (isAlly && !targetType.HasFlag(AbilityTargetType.Ally))
                                continue;
                            // TODO: Add checks for NonAlly, Summon, etc. when team info is available
                            var instance = new AbilityEffectInstance(
                                effect: effect,
                                source: context.Caster,
                                targetAgent: agent,
                                turnApplied: context.TurnNumber);
                            effectInstances.Add(instance);
                            instance.Apply();
                            if (instance.HasDuration)
                                agent.AddEffect(instance);
                        }
                    }
                }
            }
            return new AbilityUseResolution(context, effectInstances);
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
        Ally          = 1 << 0,  // Able to target allies
        NonAlly       = 1 << 1,  // Able to target non-allies
        Empty         = 1 << 2,  // Able to target empty tiles
        AllySummon    = 1 << 3,     // Convenience: allies or summons (hidden from editor)
        NonAllySummon = 1 << 4,  // Convenience: non-allies or summons (hidden from editor)
        Agents        = Ally | NonAlly,             // Able to target agents only
        Summons       = AllySummon | NonAllySummon, // Able to target summons only
        Everything    = Agents | Summons |  Empty,  // Convenience: all targets (hidden from editor)
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
        /// <summary>Current turn number when the ability is used (for effect expiry and cooldowns). Use 0 if unknown.</summary>
        public int TurnNumber;
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
    /// <summary>
    /// Runtime instance of an ability effect. Turn order decides when to call Apply and when to remove expired instances.
    /// Agent-bound (Damage, Heal, Status): stored on the agent, follows that agent. Tile-bound (Terrain): stored on the map by tile, lingers on the tile.
    /// </summary>
    public class AbilityEffectInstance {
        public AbilityEffect Effect;
        public Agent Source;       // The agent that caused this effect (e.g., the caster)
        public Tile TargetTile;    // Tile at application time; for tile-bound (Terrain) this is where the effect lingers
        public Agent TargetAgent;  // For agent-bound (Damage, Heal, Status): the agent this effect follows; null for Terrain
        /// <summary>Turn number when this effect was applied (used for expiry with current turn).</summary>
        public int TurnApplied;

        /// <summary>Instantiated from Effect.DelayConditions when this instance was created.</summary>
        public ValueConditions DelayConditions { get; private set; }
        /// <summary>Instantiated from Effect.DurationConditions when this instance was created.</summary>
        public ValueConditions DurationConditions { get; private set; }

        [Obsolete("Use DurationConditions instead")]
        public int Duration = 1; // TODO: Depricate this and use DurationConditions instead

        public AbilityEffectInstance(AbilityEffect effect,
                                    Agent source,
                                    Tile targetTile = null,
                                    Agent targetAgent = null,
                                    int turnApplied = 0) {
            if (effect == null || source == null)
                throw new Exception("AbilityEffectInstance: Effect and source cannot be null");
            if (effect.IsTileBound && targetTile == null)
                throw new Exception("AbilityEffectInstance: Tile-bound effect must have a target tile");
            if (!effect.IsTileBound && targetAgent == null)
                throw new Exception("AbilityEffectInstance: Agent-bound effect must have a target agent");
            Effect = effect;
            Source = source;
            TargetTile = targetTile;
            TargetAgent = targetAgent;
            TurnApplied = turnApplied;
            DelayConditions = new ValueConditions(effect.DelayConditions);
            DurationConditions = new ValueConditions(effect.DurationConditions);
        }

        /// <summary>
        /// True if this effect has duration conditions (should be tracked and ticked by turn order).
        /// </summary>
        public bool HasDuration => Effect != null && Effect.DurationConditions != null && Effect.DurationConditions.Count > 0;

        /// <summary>
        /// True if this effect is tile-bound (Terrain). Otherwise agent-bound (Damage, Heal, Status). Delegates to Effect.
        /// </summary>
        public bool IsTileBound => Effect != null && Effect.IsTileBound;

        /// <summary>
        /// True if this instance should be removed at the given turn. Uses the instantiated duration conditions;
        /// for Fixed conditions, elapsed turns (currentTurn - TurnApplied) are passed so "Fixed GE 3" means "lasts 3 turns".
        /// </summary>
        public bool IsExpired(int currentTurn) {
            if (DurationConditions == null || DurationConditions.Conditions == null || DurationConditions.Conditions.Count == 0)
                return true; // no duration = instant, consider expired
            int elapsedTurns = currentTurn - TurnApplied;
            return DurationConditions.IsMet(elapsedTurns, Source, TargetAgent);
        }

        /// <summary>
        /// True if the delay conditions are met at the given turn (e.g. effect should trigger this turn).
        /// Uses the instantiated delay conditions; pass currentTurn for Fixed-based delay.
        /// </summary>
        public bool IsDelayMet(int currentTurn) {
            if (DelayConditions == null || DelayConditions.Conditions == null || DelayConditions.Conditions.Count == 0)
                return true; // no delay = trigger immediately
            return DelayConditions.IsMet(currentTurn, Source, TargetAgent);
        }

        /// <summary>
        /// Apply this effect once. Agent-bound: apply to TargetAgent (or occupant at TargetTile). Tile-bound: apply to TargetTile.
        /// </summary>
        public void Apply() {
            if (Effect == null) return;
            int amount = (int)Effect.Amount;
            switch (Effect.EffectType) {
                case AbilityEffectType.Damage:
                case AbilityEffectType.Heal:
                case AbilityEffectType.Status:
                    Agent agent = TargetAgent ?? (TargetTile?.Map != null ? TargetTile.Map.GetAgentAtTile(TargetTile) : null);
                    if (agent != null) {
                        if (Effect.EffectType == AbilityEffectType.Damage) agent.TakeDamage(amount);
                        else if (Effect.EffectType == AbilityEffectType.Heal) agent.Heal(amount);
                        else if (Effect.EffectType == AbilityEffectType.Status) {
                            switch (Effect.StatusEffect) {
                                case StatusEffect.MovementDown:
                                    agent.Move(amount); // For simplicity, we use the Amount field to indicate how much to decrease movement range; in a real implementation we might want a more flexible system for different status effects
                                    break;
                                case StatusEffect.MovementUp:
                                    agent.Move(-amount);
                                    break;
                                // Add more status effects as needed
                                default:
                                    Debug.LogWarning($"Unknown status effect type: {Effect.StatusEffect}");
                                    break;
                            }
                        
                        }
                    }
                    break;
                case AbilityEffectType.Terrain:
                    // TODO: apply terrain to tile state when we have tile modifiers
                    if (TargetTile != null) { /* tile modifier */ }
                    break;
                default:
                    break;
            }
        }
    }
    public class AbilityUseResolution {
        public readonly int TotalDamageDealt;
        // TODO: Add more stats to keep track of during resolution (e.g., total healing done, status effects applied, tiles affected) for analytics and database recording
        public AbilityUseContext Context;
        public IReadOnlyList<Agent> TargetAgents;
        public IReadOnlyList<AbilityEffectInstance> EffectInstances;
        public Agent Caster => Context?.Caster;
        public Tile TargetTile => Context?.TargetTile;
        
        public AbilityUseResolution(AbilityUseContext context, IEnumerable<AbilityEffectInstance> effectInstances) {
            Context = context;
            EffectInstances = effectInstances.ToList().AsReadOnly();
            
            // Determine affected agents based on effect instances (for stats tracking and other post-resolution processing)
            var targets = new HashSet<Agent>();
            foreach (var instance in effectInstances) {
                if (instance.TargetAgent != null) {
                    targets.Add(instance.TargetAgent);
                } else if (instance.TargetTile != null) {
                    var agent = instance.TargetTile.Occupant;
                    if (agent != null) {
                        targets.Add(agent);
                    }
                }
            }
            TargetAgents = targets.ToList().AsReadOnly();

            // Calculate total damage dealt for stats tracking (can be expanded later to include other effect types and amounts)
            int totalDamageDealt = 0;
            foreach (var instance in EffectInstances) {
                if (instance.Effect.EffectType == AbilityEffectType.Damage) {
                    totalDamageDealt += (int)instance.Effect.Amount;
                }
            }
            TotalDamageDealt = totalDamageDealt;
        }
    }
}
