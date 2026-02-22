/***********************************************************************
* File Name     : Agent.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-01-31
* Description   : Data structure representing an agent in the game.
**********************************************************************/
using UnityEngine;
using System;
using System.Collections.Generic;

namespace NetFlower {

    /// <summary>
    /// Data structure representing an agent in the game who can perform actions and take and deal damage.
    /// </summary>
    public class Agent : MonoBehaviour {


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public void Start() {
            // Initialize current HP to max HP at the start
            this.hp = this.maxHP;

            // Initialize cooldowns for all abilities to 0 (available)
            foreach (var ability in Abilities) {
                if (ability.StartsOnCooldown)
                    currentCooldowns[ability] = (int) ability.Cooldown;
                else
                currentCooldowns[ability] = 0;
            }
        }

        // Update is called once per frame
        public void Update() { }

        [Header("Identity")]
        Player Player;  // The player controlling this agent
        [SerializeField] string AgentName;       // Agent's display name

        [Header("Base Stats")]
        [SerializeField] uint maxHP = 20;       // Maximum health points
        [SerializeField] uint maxRange = 3;     // Maximum movement range (per turn)
        [SerializeField] Tunneling CanTunnel;   // How other agents can pass through this agent
        [SerializeField] List<Ability> Abilities;  // List of Abilities this agent can use

        [Header("Current Stats")]
        [SerializeField] uint hp;                   // Current health points
        private Dictionary<Ability, int> currentCooldowns = new(); // Maps ability to current cooldown
        private List<AbilityEffectInstance> activeEffects = new();  // List of active effect instances with duration
        public string Name { get { return AgentName; } }
        public uint MovementRange { get { return maxRange; } }
        public uint HP { get { return hp; } }
        public uint MaxHP { get { return maxHP; } }
        public uint MaxRange { get { return maxRange; } }

        /// <summary>
        /// Add an agent-bound duration effect (Damage, Heal, Status) so it follows this agent and is ticked each turn.
        /// </summary>
        public void AddEffect(AbilityEffectInstance instance) {
            if (instance != null && !instance.IsTileBound) activeEffects.Add(instance);
        }

        /// <summary>
        /// Called by turn order each turn: remove agent-bound effects expired at the given turn number.
        /// </summary>
        /// <param name="currentTurn">Current turn number (used for expiry: effect expires when currentTurn >= TurnApplied + duration).</param>
        public void TickEffects(int currentTurn) {
            for (int i = activeEffects.Count - 1; i >= 0; i--) {
                if (activeEffects[i].IsExpired(currentTurn)) activeEffects.RemoveAt(i);
            }
        }

        // TODO: Determine if it is sufficient that a MapManager tracks agents and the map, that the
        // agent themselves can keep track of where they are located (which map and which tile)
        // uint map { public get; private set;}     // Current map ID
        // uint tile { public get; private set;}    // Current tile index

        /// <summary>
        /// Flags for how other agents can walk through this agent when pathing on the map
        /// </summary>

        [Flags]
        public enum Tunneling {
            Nothing     = 0,                // Cannot be tunneled through
            Ally        = 1 << 0,           // Can be tunneled through by allies
            NonAlly     = 1 << 1,           // Can be tunneled through by non-allies
            Everything  = Ally | NonAlly,   // Can be tunneled through by everyone
        }

        // ===================================================================== //
        // ======================= Static Factory Method ======================= //

        /// <summary>
        /// Factory method to create a new Agent GameObject with specified properties.
        /// </summary>
        /// <param name="player">The Player controlling this agent.</param>
        /// <param name="agent_name">Display name of the agent.</param>
        /// <param name="hp">Maximum health points of the agent.</param>
        /// <param name="range">Maximum movement range of the agent.</param>
        /// <param name="abilities">Array of ability IDs for the agent.</param>
        /// <param name="tunneling">Tunneling capabilities of the agent.</param>
        /// <param name="gameObjectName">Name of the GameObject to be created. If null, a default name will be generated.</param>
        /// <param name="parent">Parent GameObject under which the new agent will be placed. If null, it will be placed at the root.</param>
        /// <param name="position">2D position where the agent will be placed.</param>
        /// <returns>The newly created Agent component.</returns>
        public static GameObject NewAgent(// Agent properties
                                        Player player = null,
                                        string agent_name = "MissingNo.",
                                        uint hp = 20,
                                        uint range = 3,
                                        IEnumerable<Ability> abilities = null,
                                        Tunneling tunneling = default,
                                        // How to place it in the scene
                                        string gameObjectName = null,
                                        GameObject parent = null,
                                        Vector3 position = default) {

            // If no GameObject name is provided, create one based on timestamp
            if (gameObjectName == null) {
                long timestamp_now = DateTime.Now.Ticks;
                gameObjectName = "Agent_" + timestamp_now;
            }

            // Create the GameObject
            GameObject agentObject = new GameObject(gameObjectName);

            // If a parent is specified, set this new agent as a child of that parent
            if (parent != null) {
                agentObject.transform.SetParent(parent.transform);
            }

            // Set the 2D position of the agent
            agentObject.transform.position = position;

            // Add the Agent component and initialize its properties
            Agent agent = agentObject.AddComponent<Agent>();
            agent.Initialize(player, agent_name, hp, range, abilities, tunneling);
            return agentObject;
        }

        public void Initialize(// Agent properties
                            Player player = null,
                            string agent_name = "MissingNo.",
                            uint hp = 20,
                            uint range = 3,
                            IEnumerable<Ability> abilities = null,
                            Tunneling tunneling = default) {
            this.Player = player;
            this.AgentName = agent_name;
            this.maxHP = hp;
            this.maxRange = range;
            this.Abilities = abilities != null ? new List<Ability>(abilities) : new List<Ability>();
            this.CanTunnel = tunneling;
            this.hp = hp;
        }

        // ===================================================================== //
        // ======================= Public Agent Methods ======================== //

        /// <summary>
        /// Getter methods for all variables
        /// </summary>
        public NetFlower.Player GetPlayer() { return this.Player; }
        public String GetAgentName() {  return this.AgentName; }
        public uint GetmaxHP() {  return this.maxHP; }
        public uint GetMaxRange() { return this.maxRange; }
        public List<Ability> GetAbilities() { return this.Abilities; }
        public Tunneling GetTunneling() { return this.CanTunnel; }
        public uint GetHP() {  return this.hp; }
  

        /// <summary>
        /// Apply damage to the agent, reducing its HP.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        public void TakeDamage(int damage) {
            this.hp -= (uint)damage;
            if (this.hp < 0) {
                this.hp = 0;
            }
        }

        /// <summary>
        /// Heal the agent, increasing its HP up to maximum.
        /// </summary>
        /// <param name="amount">Amount of HP to restore.</param>
        public void Heal(int amount) {
            this.hp += (uint)amount;
            if (this.hp > this.maxHP) {
                this.hp = this.maxHP;
            }
        }

        /// <summary>
        /// Check if the agent is knocked out (HP <= 0).
        /// </summary>
        /// <returns>True if the agent is knocked out, otherwise false.</returns>
        public bool KOed() {
            return this.hp <= 0;
        }

        /// <summary>
        /// Reset the agent's HP to its maximum value.
        /// </summary>
        public void ResetHP() {
            this.hp = this.maxHP;
        }

        /// <summary>
        /// Check if the agent can use the given ability.
        /// </summary>
        /// <param name="ability">The ability to check.</param>
        /// <returns>True if the ability is available (not on cooldown), otherwise false.</returns>
        public bool CanUseAbility(Ability ability) {
            if (ability == null || !Abilities.Contains(ability))
                return false;
            int cooldown = currentCooldowns[ability];
            return cooldown <= 0;
        }

        /// <summary>
        /// Use an ability on a target and resolve its effects.
        /// </summary>
        /// <param name="ability">The ability to use.</param>
        /// <param name="targetTile">The tile being targeted.</param>
        /// <returns>True if the ability was successfully used, false otherwise.</returns>
        public bool UseAbility(Ability ability, Tile targetTile) {
            if (!CanUseAbility(ability))
                return false;
            
            // Create context for ability resolution
            var context = new AbilityUseContext {
                Ability = ability,
                Caster = this,
                TargetTile = targetTile
            };
            
            // Resolve the ability's effects (dispatches to AbilitySummon.Resolve for summon abilities)
            ability.Resolve(context);
            
            // Set cooldown after successful use
            currentCooldowns[ability] = (int) ability.Cooldown;
            
            return true;
        }

        public bool UseAbility(Ability ability, Map map, Vector2Int targetPos) {
            return UseAbility(ability, map.GetTileAtPosition(targetPos));
        }

        /// <summary>
        /// Called at the start of the agent's turn to reapply active effects and decrement their durations.
        /// </summary>
        public void OnTurnStart() {
            
        }

        /// <summary>
        /// Called at the end of the agent's turn to decrement cooldowns.
        /// </summary>
        public void OnTurnEnd() {
            DecrementCooldowns();
        }

        /// <summary>
        /// Get the name of the parent GameObject as a string.
        /// Returns null if there is no parent.
        /// </summary>
        public string ParentName {
            get {
                return this.transform.parent != null ? this.transform.parent.name : null;
            }
        }

        /// <summary>
        /// Stores data about each player and their agent in a match
        /// Returns PlayerMatchStats object
        /// </summary>
        public PlayerMatchStats RegisterPlayer(
            int matchId
        ) {
            var stats = new PlayerMatchStats(
                matchId: matchId,
                playerId: this.Player.Id,
                characterId: this.AgentName,
                teamId: this.ParentName);

            return stats;
        }

        // ===================================================================== //
        // ======================= Private Agent Methods ======================= //

        /// <summary>
        /// Decrement all active cooldowns by 1.
        /// </summary>
        private void DecrementCooldowns() {
            var keys = new List<Ability>(currentCooldowns.Keys);
            foreach (var key in keys) {
                if (currentCooldowns[key] > 0)
                    currentCooldowns[key]--;
            }
        }

    }
}
