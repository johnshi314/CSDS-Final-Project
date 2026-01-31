 /***********************************************************************
 * File Name     : Agent.cs
 * Author        : Mikey Maldonado
 * Date Created  : 2026-01-31
 * Description   : Data structure representing an agent in the game.
 **********************************************************************/
using UnityEngine;
using BackendData;

namespace GameData {

    public struct Tunneling {
        public bool Ally { get; set; }
        public bool NonAlly { get; set; }

        public Tunneling(bool Ally = true,
                        bool NonAlly = false) {
            this.Ally = Ally;
            this.NonAlly = NonAlly;
        }
    }

    public class Agent: MonoBehaviour {

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        static void Start() {}

        // Update is called once per frame
        static void Update() {}

        // Flags for how other agents can pass through this agent


        // Base Data
        public BackendData.Player Player { get; private set; }  // The player controlling this agent
        public string AgentName { get; private set; }       // Agent's display name
        public uint MaxHP { get; private set; } = 20;       // Maximum health points
        public uint MaxRange { get; private set; } = 3;     // Maximum movement range (per turn)
        public uint[] Abilities { get; private set; }       // List of ability IDs
        public Tunneling CanTunnel { get; set; }            // How other agents can pass through this agent

        // Current Status
        public uint HP { get; private set;}     // Current health points
        public uint Range { get; private set;}  // Current movement range

        // TODO: Determine if it is sufficient that a MapManager tracks agents and the map, that the
        // agent themselves can keep track of where they are located (which map and which tile)
        // uint map { public get; private set;}     // Current map ID
        // uint tile { public get; private set;}    // Current tile index



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
                                    BackendData.Player player = null,
                                    string agent_name = "MissingNo.",
                                    uint hp = 20,
                                    uint range = 3,
                                    uint[] abilities = null,
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
            agent.HP = hp;
            agent.Range = range;
            agent.Abilities = abilities;
            agent.CanTunnel = tunneling;
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
            if (this.HP < 0){
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
        /// Check if the agent can move a specified distance.
        /// </summary>
        /// <param name="distance">Distance to check.</param>
        /// <returns>True if the agent can move the specified distance, otherwise false.</returns>
        public bool CanMove(uint distance) {
            return this.Range >= distance;
        }

        /// <summary>
        /// Move the agent a specified distance, reducing its available range.
        /// </summary>
        /// <param name="distance">Distance to move.</param>
        /// <returns>True if the move was successful, otherwise false.</returns>
        public bool Move(uint distance) {
            if (CanMove(distance)) {
                this.Range -= distance;
                return true;
            } else {
                Debug.Log("Move distance exceeds agent's range.");
                return false;
            }
        }

        // ===================================================================== //
        // ======================= Public Reset Methods ======================== //

        /// <summary>
        /// Reset the agent's range to its maximum value.
        /// </summary>
        public void ResetRange() {
            this.Range = this.MaxRange;
        }

        /// <summary>
        /// Reset the agent's HP to its maximum value.
        /// </summary>
        public void ResetHP() {
            this.HP = this.MaxHP;
        }

        /// <summary>
        /// Reset both the agent's HP and range to their maximum values.
        /// </summary>
        public void Reset() {
            this.ResetHP();
            this.ResetRange();
        }
    }
}
