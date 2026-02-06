/***********************************************************************
* File Name     : Agent.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-01-31
* Description   : Data structure representing an agent in the game.
**********************************************************************/
using UnityEngine;
using System;
using System.Collections.Generic;

// GameData: Data related to the client-side game
namespace GameData {

    /// <summary>
    /// Data structure representing an agent in the game who can perform actions and take and deal damage.
    /// </summary>
    public class Agent : MonoBehaviour {

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        static void Start() { }

        // Update is called once per frame
        static void Update() { }

        [Header("Identity")]
        GameData.Player Player;  // The player controlling this agent
        [SerializeField] string AgentName;       // Agent's display name

        [Header("Base Stats")]
        [SerializeField] uint MaxHP = 20;       // Maximum health points
        [SerializeField] uint MaxRange = 3;     // Maximum movement range (per turn)
        [SerializeField] List<GameData.Ability> Abilities;  // List of Abilities this agent can use
        [SerializeField] Tunneling CanTunnel;               // How other agents can pass through this agent

        [Header("Current Stats")]
        [SerializeField] uint HP;                   // Current health points

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
        public static Agent NewAgent(// Agent properties
                                    GameData.Player player = null,
                                    string agent_name = "MissingNo.",
                                    uint hp = 20,
                                    uint range = 3,
                                    IEnumerable<GameData.Ability> abilities = null,
                                    Tunneling tunneling = default,
                                    // How to place it in the scene
                                    string gameObjectName = null,
                                    GameObject parent = null,
                                    Vector3 position = default) {

            // If no GameObject name is provided, create one based on timestamp
            if (gameObjectName == null) {
                long timestamp_now = System.DateTime.Now.Ticks;
                gameObjectName = "Agent_" + timestamp_now;
            }

            // Create the GameObject
            GameObject agentObject = new GameObject(gameObjectName);

            // If a parent is specified, set this new agent as a child of that parent
            if (parent != null) {
                agentObject.transform.parent = parent.transform;
            }

            // Set the 2D position of the agent
            agentObject.transform.position = position;

            // Add the Agent component and initialize its properties
            Agent agent = agentObject.AddComponent<Agent>();
            agent.Player = player;
            agent.AgentName = agent_name;
            agent.MaxHP = hp;
            agent.MaxRange = range;
            agent.Abilities = abilities != null ? new List<GameData.Ability>(abilities) : new List<GameData.Ability>();
            agent.CanTunnel = tunneling;
            // Example check to see if ally can tunnel through
            bool canAllyTunnel = (agent.CanTunnel & Tunneling.Ally) != 0;
            agent.HP = hp;
            return agent;
        }

        // ===================================================================== //
        // ======================= Public Agent Methods ======================== //

        /// <summary>
        /// Apply damage to the agent, reducing its HP.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        public void TakeDamage(int damage) {
            this.HP -= (uint)damage;
            if (this.HP < 0) {
                this.HP = 0;
            }
        }

        /// <summary>
        /// Check if the agent is knocked out (HP <= 0).
        /// </summary>
        /// <returns>True if the agent is knocked out, otherwise false.</returns>
        public bool KOed() {
            return this.HP <= 0;
        }

        /// <summary>
        /// Reset the agent's HP to its maximum value.
        /// </summary>
        public void ResetHP() {
            this.HP = this.MaxHP;
        }
    }
}
