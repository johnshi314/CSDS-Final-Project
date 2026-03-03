/***********************************************************************
* File Name     : AbstractAgent.cs
* Author        : John Shi
* Date Created  : March 2, 2026
* Description   : Abstract class used to define overarching behavior across
*                 all agents in the game. Based heavily on Mikey Maldonado's Agent.cs script. 
**********************************************************************/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using System.Text;

namespace NetFlower {

    public abstract class Agent : MonoBehaviour {

        // ============================================================ //
        // ==================== Abstract Contract ===================== //
        // Every subclass MUST implement these two methods.

        /// <summary>
        /// PlayerAgent waits for input; NPCAgent runs AI. Subclass decides.
        /// </summary>
        public abstract void OnTurnStart();

        /// <summary>
        /// Called automatically when HP hits 0. Play death anim, drop loot, etc.
        /// </summary>
        protected abstract void OnDeath();

        // ============================================================ //
        // ====================== Serialized Fields =================== //

        [Header("Stats Asset")]
        [SerializeField] protected AgentStats stats; // Drag archetype .asset here — NO hardcoded defaults

        [Header("Identity")]
        [SerializeField] protected Player Player;

        [SerializeField, HideInInspector] protected uint hp; // runtime only

        // ============================================================ //
        // ====================== Private State ======================= //

        private string agentNameOverride;
        private Dictionary<Ability, int> currentCooldowns = new();
        private List<AbilityEffectInstance> activeEffects  = new();
        private PlayerMatchStats playerMatchStats;

        public enum RequestType { AbilitySubmit }

        // ============================================================ //
        // ====================== Unity Lifecycle ===================== //

        protected virtual void Start() {
            if (stats == null) {
                Debug.LogError($"[Agent] '{name}' has no AgentStats asset assigned!", this);
                return;
            }
            hp = stats.MaxHP;
            foreach (var ability in stats.Abilities)
                currentCooldowns[ability] = ability.StartsOnCooldown ? (int)ability.Cooldown : 0;
        }

        protected virtual void Update() { }

        // ============================================================ //
        // ====================== Public Accessors ==================== //

        public string Name          => agentNameOverride ?? stats?.AgentName ?? "Unknown";
        public uint   HP            => hp;
        public uint   MaxHP         => stats?.MaxHP    ?? 0;
        public uint   MaxRange      => stats?.MaxRange ?? 0;
        public uint   MovementRange => MaxRange;
        public string ParentName    => transform.parent != null ? transform.parent.name : null;

        public Player          GetPlayer()    => Player;
        public string          GetAgentName() => Name;
        public uint            GetMaxHP()     => MaxHP;
        public uint            GetMaxRange()  => MaxRange;
        public List<Ability>   GetAbilities() => stats?.Abilities ?? new();
        public Agent.Tunneling GetTunneling() => stats?.CanTunnel ?? default;
        public uint            GetHP()        => hp;

        // ============================================================ //
        // ====================== Initialisation ====================== //

        /// <summary>
        /// Used when spawning agents via code. For prefabs, just assign the
        /// stats asset in the Inspector instead.
        /// </summary>
        public void Initialize(AgentStats agentStats, Player player = null, string nameOverride = null) {
            stats             = agentStats;
            Player            = player;
            agentNameOverride = nameOverride;
            hp                = stats.MaxHP;
            currentCooldowns.Clear();
            foreach (var ability in stats.Abilities)
                currentCooldowns[ability] = ability.StartsOnCooldown ? (int)ability.Cooldown : 0;
        }

        // ============================================================ //
        // ====================== Factory Method ====================== //

        /// <summary>
        /// Spawns any Agent subtype T. The stats asset is required — nothing is hardcoded.
        /// Usage: Agent.NewAgent&lt;GuardNPC&gt;(guardStats, player, parent: teamContainer);
        /// </summary>
        public static GameObject NewAgent<T>(
            AgentStats agentStats,
            Player     player         = null,
            string     nameOverride   = null,
            string     gameObjectName = null,
            GameObject parent         = null,
            Vector3    position       = default
        ) where T : Agent {
            if (agentStats == null) {
                Debug.LogError("[Agent.NewAgent] agentStats must not be null.");
                return null;
            }
            gameObjectName ??= "Agent_" + DateTime.Now.Ticks;
            var go = new GameObject(gameObjectName);
            if (parent != null) go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.AddComponent<T>().Initialize(agentStats, player, nameOverride);
            return go;
        }

        // ============================================================ //
        // ====================== Combat ============================== //

        public void TakeDamage(int damage) {
            hp = (uint)Mathf.Max(0, (int)hp - damage);
            if (playerMatchStats != null) playerMatchStats.damageTaken += damage;
            if (hp == 0) OnDeath(); // delegate to subclass
        }

        public void Heal(int amount)  => hp = (uint)Mathf.Min((int)MaxHP, (int)hp + amount);
        public bool KOed()            => hp <= 0;
        public void ResetHP()         => hp = MaxHP;

        public void AddEffect(AbilityEffectInstance instance) {
            if (instance != null && !instance.IsTileBound) activeEffects.Add(instance);
        }

        public void TickEffects(int currentTurn) {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
                if (activeEffects[i].IsExpired(currentTurn)) activeEffects.RemoveAt(i);
        }

        // ============================================================ //
        // ====================== Abilities =========================== //

        public bool CanUseAbility(Ability ability) {
            if (ability == null || !stats.Abilities.Contains(ability)) return false;
            return currentCooldowns.TryGetValue(ability, out int cd) && cd <= 0;
        }

        public bool UseAbility(Ability ability, Tile targetTile) {
            if (!CanUseAbility(ability)) return false;
            ability.Resolve(new AbilityUseContext { Ability = ability, Caster = this, TargetTile = targetTile });
            currentCooldowns[ability] = (int)ability.Cooldown;
            var usageStats = new AbilityUsageStats(characterId: Name, playerId: Player.Id);
            usageStats.damageDone = ability.TargetEffects.Count;
            StartCoroutine(SubmitAbilityUsageRoutine(usageStats.ToJson()));
            return true;
        }

        public bool UseAbility(Ability ability, Map map, Vector2Int targetPos) =>
            UseAbility(ability, map.GetTileAtPosition(targetPos));

        // ============================================================ //
        // ====================== Turn Methods ======================== //

        // OnTurnStart() is abstract above

        /// <summary>
        /// Subclasses can override but should call base.OnTurnEnd().
        /// </summary>
        public virtual void OnTurnEnd() => DecrementCooldowns();

        // ============================================================ //
        // ====================== Tracking ============================ //

        public PlayerMatchStats RegisterPlayer(int matchId) =>
            new PlayerMatchStats(matchId: matchId, playerId: Player.Id,
                                 characterId: Name, teamId: ParentName);

        // ============================================================ //
        // ====================== Private Helpers ===================== //

        private void DecrementCooldowns() {
            foreach (var key in new List<Ability>(currentCooldowns.Keys))
                if (currentCooldowns[key] > 0) currentCooldowns[key]--;
        }

        IEnumerator SubmitAbilityUsageRoutine(string json) =>
            SendRequest("http://localhost:8000/submit-abilityusagestats", json, RequestType.AbilitySubmit);

        IEnumerator SendRequest(string url, string jsonBody, RequestType requestType) {
            using var request = new UnityWebRequest(url, "POST") {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout         = 10
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept",       "application/json");
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log($"Request succeeded: {request.downloadHandler.text}");
            else
                Debug.LogError($"Request failed ({request.responseCode}): {request.error}");
        }

        // ============================================================ //
        // ====================== Tunneling Enum ====================== //

        [Flags]
        public enum Tunneling {
            Nothing    = 0,
            Ally       = 1 << 0,
            NonAlly    = 1 << 1,
            Everything = Ally | NonAlly,
        }
    }
}