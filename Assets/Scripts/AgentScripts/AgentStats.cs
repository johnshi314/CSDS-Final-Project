/***********************************************************************
* File Name     : AbstractAgent.cs
* Author        : John Shi
* Date Created  : March 2, 2026
* Description   : ScriptableObject defining base stats for an agent archetype.
**********************************************************************/

using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {

    [CreateAssetMenu(fileName = "NewAgentStats", menuName = "NetFlower/Agent Stats")]
    public class AgentStats : ScriptableObject {

        [Header("Identity")]
        public string AgentName = "Unnamed Agent";

        [Header("Base Stats")]
        public uint MaxHP = 100;
        public uint MaxRange = 3;
        public Agent.Tunneling CanTunnel = Agent.Tunneling.Nothing;

        [Header("Abilities")]
        public List<Ability> Abilities = new();
    }
}