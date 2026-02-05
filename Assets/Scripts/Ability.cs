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
using GameManager;

namespace GameData {
    /// <summary>
    /// Data structure representing an ability that agents can use in the game.
    /// </summary>
    public class Ability {
        [Header("Identity")]
        public string Id;                       // ID for referencing cross system and database
        public string DisplayName;              // Name of ability

        [Header("Targeting")]
        public AbilityTargetMode TargetingMode; // If this is point-select or global
        public uint RangeMax;                   // Max range from caster
        public uint RangeMin;                   // Min range from caster (0 will allow self-targeting)
        public AbilityTargetType TargetType;    // Type of targets allowed

        [Header("Costs")]
        public uint Cost;                       // Resource cost to use ability (e.g., mana, stamina)

        [Header("Effects")]
        public int Damage;                      // Base damage value
        public List<AbilityModifier> Modifiers; // List of modifiers that can be applied to this ability
    }

    /// <summary>
    /// Specifies categories of targets that an ability can affect.
    /// </summary>
    [Flags]
    public enum AbilityTargetType {
        Empty        = 0,       // Able to target empty tiles
        Ally        = 1 << 0,   // Able to target allies
        NonAlly     = 1 << 1    // Able to target non-allies
    }

    /// <summary>
    /// Specifies how an ability selects its targets.
    /// </summary>
    public enum AbilityTargetMode {
        Point,      // Select a tile/unit in range
        Global,     // Applies to all valid targets on the board
    }

    /// <summary>
    /// Specifies the shape of the area affected by an ability.
    /// </summary>
    public enum AbilityTargetShape {
        Single,     // Single target
        Line,       // Line shape
        Cone,       // Cone shape
        Circle,     // Circular area
        Cross,      // Cross shape
        None        // Shape determined by resolver (e.g., FFT Mathematician)
    }

    /// <summary>
    /// Context information for an ability being used for when resolving its effects.
    /// </summary>
    public class AbilityUseContext {
        public Agent Caster;
        public MapManager Board;
        // public TurnSystem TurnSystem;
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
}
