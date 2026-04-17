/**********************************************************************
 * OnlineBattleManager - server-authoritative turns over the battle WebSocket.
 * Subclass of BattleManager: same UX; timers and turn order come from Server/battle_ws.py.
 * Lobby match id + auth token come from Match / PlayerPrefs (set after Matchmaking).
 **********************************************************************/
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine;
using NetFlower.UI;

namespace NetFlower {

    public class OnlineBattleManager : BattleManager {

        [Tooltip("REST base, no trailing slash - used to derive wss://host/ws/battle/{matchId}.")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";
        [Tooltip("Optional override for WebSocket base (no trailing slash). Empty = derive from HTTP.")]
        [SerializeField] string lobbyWebSocketBaseUrl = "";
        [SerializeField] float serverTurnSeconds = 30f;

        int _myPlayerId = -1;
        int _hostPlayerId = int.MaxValue;
        WebSocket _ws;
        CancellationTokenSource _cts;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        bool _receivedYou;
        bool _handshakeSent;
        DateTime _turnEndsUtc;
        /// <summary>Server <c>claims_complete</c> spawn host (lowest connected player id); -1 = none pending.</summary>
        int _spawnHandshakeAuthorityPid = -1;
        int _spawnHandshakeAttemptsRemaining;

        Match _match;
        /// <summary>True when we should use the battle WebSocket (lobby match + token). Otherwise behave like offline <see cref="BattleManager"/>.</summary>
        bool _localFallback;

        /// <summary>True only when connected battle flow should run (defer first turn, server timer, relay).</summary>
        public bool UsesNetworkBattle => !_localFallback;

        /// <summary>When false, <see cref="BattleManager"/> runs the local turn countdown.</summary>
        public bool UsesServerTurnTimer => !_localFallback;

        void Awake() {
            _match = Match.PersistentInstance;
            var prefs = PersistentPlayerPreferences.instance;
            _localFallback = prefs == null || !prefs.isPlayingOnline
                || _match == null || _match.dbMatchId <= 0
                || string.IsNullOrEmpty(PlayerPrefs.GetString("auth_token", ""));
        }

        public new void Start() {
            base.Start();
            _myPlayerId = PlayerPrefs.GetInt("player_id", -1);
            if (_localFallback) {
                Debug.Log("[OnlineBattle] Local fallback - no networked match (or offline). Using normal BattleManager turn/timer.");
                return;
            }
            _cts = new CancellationTokenSource();
            _ = RunBattleWebSocketAsync(PlayerPrefs.GetString("auth_token", ""));
        }

        void OnDestroy() {
            _cts?.Cancel();
            var ws = _ws;
            _ws = null;
            _cts = null;
            if (ws == null) return;
            try {
                ws.CancelConnection();
            } catch { /* ignore */ }
            try {
                _ = ws.Close();
            } catch { /* ignore */ }
        }

        protected override void Update() {
            if (!_localFallback) {
                while (_incoming.TryDequeue(out var line))
                    HandleServerLine(line);

                TryProcessPendingSpawnHandshake();

                if (_turnEndsUtc != default && State != BattleState.NotStarted) {
                    turnTimer = Mathf.Max(0f, (float)(_turnEndsUtc - DateTime.UtcNow).TotalSeconds);
                    timerActive = true;
                }

                TrySendBattleHandshakeIfReady();
            }

            base.Update();
        }

        /// <summary>
        /// Sends <c>start|</c> + <c>claim|</c> once the roster exists. Called from Update and right after
        /// <see cref="BattleManager.StartBattle"/> so execution order vs GameplayDemo cannot leave the
        /// handshake unsent with a populated <c>turnOrder</c>.
        /// </summary>
        public void TrySendBattleHandshakeIfReady() {
            if (_localFallback) return;
            if (!_receivedYou || _handshakeSent || turnOrder.Count == 0 || State != BattleState.NotStarted
                || _ws == null || _ws.State != WebSocketState.Open)
                return;
            _handshakeSent = true;
            BindAgentsToLobbyRoster();
            ComputeHostPlayerId();
            _ = SendHandshakeAsync();
        }

        /// <summary>Same as <see cref="BattleManager.StartBattle"/> but immediately retries the WS handshake.</summary>
        public new void StartBattle(bool deferFirstBeginTurn = false) {
            base.StartBattle(deferFirstBeginTurn);
            TrySendBattleHandshakeIfReady();
        }

        /// <summary>Host passes turns for NPC slots (server uses lowest connected player id).</summary>
        void ComputeHostPlayerId() {
            _hostPlayerId = _myPlayerId;
            var red = _match?.lobbyRedPlayerIds;
            var blue = _match?.lobbyBluePlayerIds;
            void consider(int[] ids) {
                if (ids == null) return;
                foreach (var id in ids) {
                    if (id > 0 && id < _hostPlayerId)
                        _hostPlayerId = id;
                }
            }
            consider(red);
            consider(blue);
            if (_hostPlayerId == int.MaxValue)
                _hostPlayerId = _myPlayerId;
        }

        void BindAgentsToLobbyRoster() {
            if (_match == null || gridMap == null) return;
            var red = _match.lobbyRedPlayerIds;
            var blue = _match.lobbyBluePlayerIds;
            // Turn order interleaves teams (see BattleManager.StartBattle) but sizes may differ (e.g. 1v5).
            // Never infer team from turn-order index; map each agent by its index on RedAgents / BlueAgents.
            for (int i = 0; i < turnOrder.Count; i++) {
                var agent = turnOrder[i];
                if (agent == null) continue;
                int pid = _myPlayerId;
                bool bound = false;
                var reds = gridMap.RedAgents;
                for (int r = 0; r < reds.Count; r++) {
                    if (reds[r] != agent) continue;
                    pid = red != null && r < red.Length ? red[r] : agent.Player != null ? agent.Player.Id : _myPlayerId;
                    bound = true;
                    break;
                }
                if (!bound) {
                    var blues = gridMap.BlueAgents;
                    for (int b = 0; b < blues.Count; b++) {
                        if (blues[b] != agent) continue;
                        pid = blue != null && b < blue.Length ? blue[b] : agent.Player != null ? agent.Player.Id : _myPlayerId;
                        bound = true;
                        break;
                    }
                }
                if (!bound)
                    Debug.LogWarning($"[OnlineBattle] BindAgentsToLobbyRoster: agent {agent.Name} not found on red or blue team list.");
                if (agent.Player == null)
                    agent.Player = new Player(pid, "Player " + pid, "0.0.0.0");
                else
                    agent.Player.Id = pid;
            }
        }

        public bool LocalMayControlCurrentAgent() {
            if (CurrentAgent == null) return false;
            if (_localFallback) {
                var npc = CurrentAgent.GetComponent<NPCBehavior>();
                return npc == null || !npc.IsNPC;
            }
            var npcNet = CurrentAgent.GetComponent<NPCBehavior>();
            if (npcNet != null && npcNet.IsNPC)
                return _myPlayerId == _hostPlayerId;
            return CurrentAgent.Player != null && CurrentAgent.Player.Id == _myPlayerId;
        }

        protected override void AdvanceTurn() {
            if (_localFallback) {
                base.AdvanceTurn();
                return;
            }
            _ = SendPassAsync();
        }

        protected override void OnAfterLocalMoveCommitted(Vector2Int destinationMapIndex) {
            if (_localFallback) return;
            if (TryGetNetworkUnitId(CurrentAgent, out var unitId))
                _ = SendTextAsync($"relay|moveu|{unitId}|{destinationMapIndex.x}|{destinationMapIndex.y}");
            else
                _ = SendTextAsync($"relay|move|{currentAgentIndex}|{destinationMapIndex.x}|{destinationMapIndex.y}");
        }

        protected override void OnAfterLocalAbilityUsed(Ability ability, Tile targetTile) {
            if (_localFallback) return;
            var agent = CurrentAgent;
            if (agent == null || ability == null || targetTile == null) return;
            int idx = AbilityIndex(agent, ability);
            if (idx < 0) return;
            if (TryGetNetworkUnitId(agent, out var unitId))
                _ = SendTextAsync($"relay|abilityu|{unitId}|{idx}|{targetTile.Position.x}|{targetTile.Position.y}");
            else
                _ = SendTextAsync($"relay|ability|{currentAgentIndex}|{idx}|{targetTile.Position.x}|{targetTile.Position.y}");
        }

        static int AbilityIndex(Agent agent, Ability ability) {
            var list = agent.GetAbilities();
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == ability) return i;
            }
            return -1;
        }

        async Task RunBattleWebSocketAsync(string authToken) {
            var api = GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);
            var url = GameApiEndpoints.BattleWebSocketUri(api, lobbyWebSocketBaseUrl, _match.dbMatchId, authToken);
            var socket = new WebSocket(url);
            _ws = socket;

            socket.OnMessage += bytes => {
                if (bytes == null || bytes.Length == 0) return;
                _incoming.Enqueue(Encoding.UTF8.GetString(bytes));
            };

            socket.OnOpen += () => {
                try {
                    var u = new Uri(url);
                    Debug.Log($"[OnlineBattle] Connected {u.GetLeftPart(UriPartial.Path)}");
                } catch {
                    Debug.Log("[OnlineBattle] Connected to battle WebSocket");
                }
            };

            socket.OnError += msg => Debug.LogWarning($"[OnlineBattle] WebSocket error: {msg}");

            socket.OnClose += _ => {
                if (ReferenceEquals(_ws, socket))
                    _ws = null;
            };

            try {
                await socket.Connect();
            } catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] WebSocket error: {e.Message}");
                if (ReferenceEquals(_ws, socket))
                    _ws = null;
            }
        }

        void HandleServerLine(string raw) {
            if (string.IsNullOrEmpty(raw)) return;
            var parts = raw.Split('|');
            var head = parts[0];
            switch (head) {
                case "you":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var pid))
                        _myPlayerId = pid;
                    _receivedYou = true;
                    Debug.Log($"[OnlineBattle] Authenticated as player {_myPlayerId}");
                    TryProcessPendingSpawnHandshake();
                    break;
                case "newTurn":
                    if (parts.Length >= 4
                        && int.TryParse(parts[1], out var slot)
                        && int.TryParse(parts[2], out var sync)
                        && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var unixEnd)) {
                        _turnEndsUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)(unixEnd * 1000d)).UtcDateTime;
                        ApplyServerDirectedTurn(slot, sync, Mathf.Max(1f, (float)(_turnEndsUtc - DateTime.UtcNow).TotalSeconds));
                    }
                    break;
                case "tick":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var secLeft))
                        turnTimer = Mathf.Max(0f, secLeft);
                    break;
                case "relay":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var fromPid)) {
                        var payload = string.Join("|", parts, 2, parts.Length - 2);
                        HandleRelay(fromPid, payload);
                    }
                    break;
                case "claims_complete":
                    // Lowest connected battle WebSocket player id must send spawns|n|x|y|...
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var spawnHostPid) && spawnHostPid > 0) {
                        Debug.Log($"[OnlineBattle] claims_complete; spawn host playerId={spawnHostPid}");
                        _spawnHandshakeAuthorityPid = spawnHostPid;
                        _spawnHandshakeAttemptsRemaining = 300;
                    }
                    break;
                case "spawnLayout":
                    if (TryParseSpawnLayoutParts(parts, out var layout))
                        TryApplyServerSpawnLayoutByTurnSlot(layout);
                    break;
                case "battle_ready":
                    Debug.Log("[OnlineBattle] Server locked spawn layout - waiting for first turn.");
                    break;
                case "err":
                    if (parts.Length >= 2)
                        Debug.LogWarning("[OnlineBattle] " + string.Join("|", parts, 1, parts.Length - 1));
                    break;
                default:
                    break;
            }
        }

        void HandleRelay(int fromPid, string payload) {
            if (fromPid == _myPlayerId) return;
            var p = payload.Split('|');
            if (p.Length < 4) return;
            if (p[0] == "moveu" && int.TryParse(p[2], out var ux) && int.TryParse(p[3], out var uy))
                ApplyRemoteMoveForUnit(p[1], ux, uy);
            else if (p[0] == "abilityu" && p.Length >= 5
                     && int.TryParse(p[2], out var uaidx)
                     && int.TryParse(p[3], out var utx)
                     && int.TryParse(p[4], out var uty))
                ApplyRemoteAbilityForUnit(p[1], uaidx, utx, uty);
            else if (p[0] == "move" && int.TryParse(p[1], out var slot) && int.TryParse(p[2], out var x) && int.TryParse(p[3], out var y))
                ApplyRemoteMoveForSlot(slot, x, y);
            else if (p[0] == "ability" && p.Length >= 5
                     && int.TryParse(p[1], out var aslot)
                     && int.TryParse(p[2], out var aidx)
                     && int.TryParse(p[3], out var tx)
                     && int.TryParse(p[4], out var ty))
                ApplyRemoteAbilityForSlot(aslot, aidx, tx, ty);
        }

        async Task SendHandshakeAsync() {
            int n = turnOrder.Count;
            int t = Mathf.Clamp(Mathf.RoundToInt(serverTurnSeconds), 3, 600);
            await SendTextAsync($"start|{n}|{t}");
            for (int i = 0; i < n; i++) {
                var agent = turnOrder[i];
                var npc = agent != null ? agent.GetComponent<NPCBehavior>() : null;
                bool isNpc = npc != null && npc.IsNPC;
                if (isNpc) {
                    if (_myPlayerId == _hostPlayerId)
                        await SendTextAsync($"claim|{i}|npc");
                } else if (agent?.Player != null && agent.Player.Id == _myPlayerId) {
                    await SendTextAsync($"claim|{i}");
                }
            }
        }

        bool IsLocalSpawnHostAuthority() {
            if (_spawnHandshakeAuthorityPid <= 0) return false;
            if (_myPlayerId == _spawnHandshakeAuthorityPid) return true;
            return PlayerPrefs.GetInt("player_id", -1) == _spawnHandshakeAuthorityPid;
        }

        /// <summary>
        /// Spawn host sends <c>spawns|...</c> after <c>claims_complete</c>, with retries and a deterministic fallback
        /// so the battle does not stall if tile lookups are briefly inconsistent.
        /// </summary>
        void TryProcessPendingSpawnHandshake() {
            if (_spawnHandshakeAuthorityPid <= 0 || !IsLocalSpawnHostAuthority()) return;
            if (_ws == null || _ws.State != WebSocketState.Open) return;

            string line = null;
            if (TryBuildTurnSlotSpawnHandshakeLine(out line) || TryBuildDeterministicTurnSlotSpawnHandshakeLine(out line)) {
                _ = SendTextAsync(line);
                _spawnHandshakeAuthorityPid = -1;
                _spawnHandshakeAttemptsRemaining = 0;
                return;
            }

            if (--_spawnHandshakeAttemptsRemaining <= 0) {
                Debug.LogError("[OnlineBattle] Could not build or send spawns after claims_complete; check roster placement and spawn lists.");
                _spawnHandshakeAuthorityPid = -1;
            }
        }

        static bool TryParseSpawnLayoutParts(string[] parts, out List<Vector2Int> positions) {
            positions = null;
            if (parts == null || parts.Length < 3) return false;
            if (!int.TryParse(parts[1], out int n) || n < 1) return false;
            if (parts.Length != 2 + 2 * n) return false;
            var list = new List<Vector2Int>(n);
            for (int i = 0; i < n; i++) {
                if (!int.TryParse(parts[2 + 2 * i], out int x) || !int.TryParse(parts[3 + 2 * i], out int y))
                    return false;
                list.Add(new Vector2Int(x, y));
            }
            positions = list;
            return true;
        }

        async Task SendPassAsync() {
            await SendTextAsync("pass");
        }

        async Task SendTextAsync(string text) {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync();
            try {
                await _ws.SendText(text);
            } catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] Send failed: {e.Message}");
            } finally {
                _sendLock.Release();
            }
        }
    }
}
