/***********************************************************************
* File Name     : TestAgent.cs
* Description   : Concrete Agent subclass for testing. Mirrors the original
*                 hardcoded defaults (maxHP: 20, maxRange: 3).
*                 Requires a TestAgentStats.asset assigned in the Inspector.
**********************************************************************/
using UnityEngine;

namespace NetFlower {

    public class TestAgent : Agent {

        protected override void Start() {
            base.Start(); // loads stats, initializes hp and cooldowns
            Debug.Log($"[TestAgent] '{Name}' spawned with {HP}/{MaxHP} HP.");
        }

        /// <summary>
        /// Minimal turn logic for testing — just logs that the turn started.
        /// Replace with real input/AI logic in proper subclasses.
        /// </summary>
        public override void OnTurnStart() {
            Debug.Log($"[TestAgent] '{Name}' turn started. HP: {HP}/{MaxHP}");
        }

        /// <summary>
        /// Minimal death response for testing — just logs the event.
        /// Replace with animations, loot drops, etc. in proper subclasses.
        /// </summary>
        protected override void OnDeath() {
            Debug.Log($"[TestAgent] '{Name}' has been KO'd.");
            gameObject.SetActive(false);
        }
    }
}