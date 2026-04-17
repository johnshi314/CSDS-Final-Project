/**********************************************************************
 * OnlineBattleManager - server-authoritative turns over the battle WebSocket.
 * Subclass of BattleManager: same UX; timers and turn order come from Server/battle_ws.py.
 * Lobby match id + auth token come from Match / PlayerPrefs (set after Matchmaking).
 **********************************************************************/
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using NetFlower.Net;
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
        IWebSocket _ws;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();

        bool _receivedYou;
        bool _handshakeSent;
        DateTime _turnEndsUtc;
        int _spawnHostPid = -1;
        int _spawnAttemptsRemaining;

        Match _match;
        bool _localFallback;

        public bool UsesNetworkBattle => !_localFallback;
        public bool UsesServerTurnTimer => !_localFallback;

        public new void Start() {
            base.Start();

            _match = Match.PersistentInstance;
            var prefs = PersistentPlayerPreferences.instance;
            _localFallback = prefs == null || !prefs.isPlayingOnline
                || _match == null || _match.dbMatchId <= 0
                || string.IsNullOrEmpty(PlayerPrefs.GetString("auth_token", ""));

            _myPlayerId = PlayerPrefs.GetInt("player_id", -1);
            if (_localFallback) {
                Debug.Log($"[OnlineBattle] Local fallback (prefs={prefs != null}, online={prefs?.isPlayingOnline}, match={_match?.dbMatchId}, token={!string.IsNullOrEmpty(PlayerPrefs.GetString("auth_token", ""))})");
                return;
            }
            Debug.Log($"[OnlineBattle] Online mode: match={_match.dbMatchId} player={_myPlayerId}");
            StartBattleWebSocket(PlayerPrefs.GetString("auth_token", ""));
        }

        void StartBattleWebSocket(string authToken) {
            var api = GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);
            var url = GameApiEndpoints.BattleWebSocketUri(api, lobbyWebSocketBaseUrl, _match.dbMatchId, authToken);
            Debug.Log($"[OnlineBattle] Connecting WS: {url}");

            var socket = WebSocketFactory.Create(url);
            _ws = socket;

            socket.OnMessage += bytes => {
                if (bytes != null && bytes.Length > 0)
                    _incoming.Enqueue(Encoding.UTF8.GetString(bytes));
            };
            socket.OnOpen += () => Debug.Log("[OnlineBattle] WS connected");
            socket.OnError += msg => Debug.LogWarning($"[OnlineBattle] WS error: {msg}");
            socket.OnClose += code => {
                Debug.Log($"[OnlineBattle] WS closed (code={code})");
                if (ReferenceEquals(_ws, socket))
                    _ws = null;
            };

            _ = RunWebSocketAsync(socket);
        }

        async Task RunWebSocketAsync(IWebSocket socket) {
            try {
                await socket.ConnectAsync();
            } catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] WS connect exception: {e.Message}");
                if (ReferenceEquals(_ws, socket))
                    _ws = null;
            }
        }

        void OnDestroy() {
            var ws = _ws;
            _ws = null;
            if (ws == null) return;
            try { ws.CancelConnection(); } catch { }
            try { _ = ws.CloseAsync(); } catch { }
        }

        protected override void Update() {
            if (!_localFallback) {
                _ws?.DispatchMessageQueue();

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

        public void TrySendBattleHandshakeIfReady() {
            if (_localFallback) return;
            if (!_receivedYou || _handshakeSent || turnOrder.Count == 0 || State != BattleState.NotStarted
                || _ws == null || _ws.State != NetFlower.Net.WebSocketState.Open)
                return;
            Debug.Log($"[OnlineBattle] Sending handshake: {turnOrder.Count} agents, myPid={_myPlayerId}");
            _handshakeSent = true;
            BindAgentsToLobbyRoster();
            ComputeHostPlayerId();
            _ = SendHandshakeAsync();
        }

        public new void StartBattle(bool deferFirstBeginTurn = false) {
            base.StartBattle(deferFirstBeginTurn);
            TrySendBattleHandshakeIfReady();
        }

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
                _ = SendWsTextAsync($"relay|moveu|{unitId}|{destinationMapIndex.x}|{destinationMapIndex.y}");
            else
                _ = SendWsTextAsync($"relay|move|{currentAgentIndex}|{destinationMapIndex.x}|{destinationMapIndex.y}");
        }

        protected override void OnAfterLocalAbilityUsed(Ability ability, Tile targetTile) {
            if (_localFallback) return;
            var agent = CurrentAgent;
            if (agent == null || ability == null || targetTile == null) return;
            int idx = AbilityIndex(agent, ability);
            if (idx < 0) return;
            if (TryGetNetworkUnitId(agent, out var unitId))
                _ = SendWsTextAsync($"relay|abilityu|{unitId}|{idx}|{targetTile.Position.x}|{targetTile.Position.y}");
            else
                _ = SendWsTextAsync($"relay|ability|{currentAgentIndex}|{idx}|{targetTile.Position.x}|{targetTile.Position.y}");
        }

        static int AbilityIndex(Agent agent, Ability ability) {
            var list = agent.GetAbilities();
            for (int i = 0; i < list.Count; i++) {
                if (list[i] == ability) return i;
            }
            return -1;
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
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var spawnHostPid) && spawnHostPid > 0) {
                        Debug.Log($"[OnlineBattle] claims_complete; spawn host playerId={spawnHostPid}");
                        _spawnHostPid = spawnHostPid;
                        _spawnAttemptsRemaining = 300;
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
            else if (p[0] == "move" && int.TryParse(p[1], out var mslot) && int.TryParse(p[2], out var x) && int.TryParse(p[3], out var y))
                ApplyRemoteMoveForSlot(mslot, x, y);
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
            await SendWsTextAsync($"start|{n}|{t}");
            for (int i = 0; i < n; i++) {
                var agent = turnOrder[i];
                var npc = agent != null ? agent.GetComponent<NPCBehavior>() : null;
                bool isNpc = npc != null && npc.IsNPC;
                if (isNpc) {
                    if (_myPlayerId == _hostPlayerId)
                        await SendWsTextAsync($"claim|{i}|npc");
                } else if (agent?.Player != null && agent.Player.Id == _myPlayerId) {
                    await SendWsTextAsync($"claim|{i}");
                }
            }
        }

        async Task SendPassAsync() => await SendWsTextAsync("pass");

        async Task SendWsTextAsync(string text) {
            if (_ws == null || _ws.State != NetFlower.Net.WebSocketState.Open) return;
            try {
                await _ws.SendTextAsync(text);
            } catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] Send failed: {e.Message}");
            }
        }

        bool IsLocalSpawnHostAuthority() {
            if (_spawnHostPid <= 0) return false;
            if (_myPlayerId == _spawnHostPid) return true;
            return PlayerPrefs.GetInt("player_id", -1) == _spawnHostPid;
        }

        void TryProcessPendingSpawnHandshake() {
            if (_spawnHostPid <= 0 || !IsLocalSpawnHostAuthority()) return;
            if (_ws == null || _ws.State != NetFlower.Net.WebSocketState.Open) return;

            string line = null;
            if (TryBuildTurnSlotSpawnLine(out line) || TryDetermineTurnSlotSpawnLine(out line)) {
                _ = SendWsTextAsync(line);
                _spawnHostPid = -1;
                _spawnAttemptsRemaining = 0;
                return;
            }

            if (--_spawnAttemptsRemaining <= 0) {
                Debug.LogError("[OnlineBattle] Could not build or send spawns after claims_complete; check roster placement and spawn lists.");
                _spawnHostPid = -1;
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
    }
}
