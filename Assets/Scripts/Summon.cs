/***********************************************************************
* File Name     : Summon.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   : ScriptableObject template for summonable agents.
*                 Contains agent-like data used to spawn an Agent at runtime.
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {
    /// <summary>
    /// Template data for a summonable agent. Used by AbilitySummon to spawn agents.
    /// This is the ScriptableObject "blueprint" — the actual Agent MonoBehaviour
    /// is instantiated at runtime using this data.
    /// </summary>
    [CreateAssetMenu(fileName = "Summon", menuName = "Scriptable Objects/Summon")]
    public class Summon : ScriptableObject {
        [Header("Identity")]
        public string Id;                           // Unique identifier
        public string DisplayName;                  // Display name for the summoned agent
        public Sprite Icon;                         // Visual icon for the summon

        [Header("Base Stats")]
        public uint MaxHP = 10;                     // Maximum health points
        public uint MaxRange = 2;                   // Maximum movement range
        public Agent.Tunneling CanTunnel;           // Tunneling capabilities

        [Header("Abilities")]
        public List<Ability> Abilities;             // Abilities this summon can use

        [Header("Lifespan")]
        public int Duration = -1;                   // Turns the summon lasts (-1 for permanent)

        [Header("Visuals")]
        public GameObject Prefab;                   // Prefab to instantiate for the summon

        /// <summary>
        /// Create an Agent instance from this template.
        /// </summary>
        /// <param name="owner">The player who owns/controls this summon.</param>
        /// <param name="parent">Parent GameObject for the new agent.</param>
        /// <param name="position">World position to spawn at.</param>
        /// <returns>The newly created Agent GameObject.</returns>
        public GameObject SpawnAgent(Player owner, GameObject parent = null, Vector3 position = default) {
            // If we have a prefab, instantiate it; otherwise create a new GameObject
            GameObject agentObj;
            if (Prefab != null) {
                agentObj = Object.Instantiate(Prefab, position, Quaternion.identity);
                if (parent != null) {
                    agentObj.transform.SetParent(parent.transform);
                }
                // Ensure it has an Agent component
                var agent = agentObj.GetComponent<Agent>();
                if (agent == null) {
                    agent = agentObj.AddComponent<Agent>();
                }
                agent.Initialize(owner, DisplayName, MaxHP, MaxRange, Abilities, CanTunnel);
            } else {
                agentObj = Agent.NewAgent(
                    player: owner,
                    agent_name: DisplayName,
                    hp: MaxHP,
                    range: MaxRange,
                    abilities: Abilities,
                    tunneling: CanTunnel,
                    gameObjectName: $"Summon_{DisplayName}",
                    parent: parent,
                    position: position
                );
            }

            // TODO: Track duration for summon expiration
            return agentObj;
        }
    }
}
