/**********************************************************************
 * OnlineBattleManager — server-authoritative turns over the battle WebSocket.
 * Subclass of BattleManager: same UX; timers and turn order come from Server/battle_ws.py.
 * Lobby match id + auth token come from Match / PlayerPrefs (set after Matchmaking).
 **********************************************************************/
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NetFlower.UI;

namespace NetFlower {

    public class OnlineBattleManager : BattleManager {

        [Tooltip("REST base, no trailing slash — used to derive wss://…/ws/battle/{matchId}.")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";
        [Tooltip("Optional override for WebSocket base (no trailing slash). Empty = derive from HTTP.")]
        [SerializeField] string lobbyWebSocketBaseUrl = "";
        [SerializeField] float serverTurnSeconds = 30f;

        int _myPlayerId = -1;
        int _hostPlayerId = int.MaxValue;
        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        readonly ConcurrentQueue<string> _incoming = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        bool _receivedYou;
        bool _handshakeSent;
        DateTime _turnEndsUtc;

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
                Debug.Log("[OnlineBattle] Local fallback — no networked match (or offline). Using normal BattleManager turn/timer.");
                return;
            }
            _cts = new CancellationTokenSource();
            _ = RunBattleWebSocketAsync(PlayerPrefs.GetString("auth_token", ""));
        }

        void OnDestroy() {
            _cts?.Cancel();
            try {
                _ws?.Abort();
                _ws?.Dispose();
            } catch { /* ignore */ }
            _ws = null;
            _cts = null;
        }

        protected override void Update() {
            if (!_localFallback) {
                while (_incoming.TryDequeue(out var line))
                    HandleServerLine(line);

                if (_turnEndsUtc != default && State != BattleState.NotStarted) {
                    turnTimer = Mathf.Max(0f, (float)(_turnEndsUtc - DateTime.UtcNow).TotalSeconds);
                    timerActive = true;
                }

                if (_receivedYou && !_handshakeSent && turnOrder.Count > 0 && State == BattleState.NotStarted
                    && _ws != null && _ws.State == WebSocketState.Open) {
                    _handshakeSent = true;
                    BindAgentsToLobbyRoster();
                    ComputeHostPlayerId();
                    _ = SendHandshakeAsync();
                }
            }

            base.Update();
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
            if (_match == null) return;
            var red = _match.lobbyRedPlayerIds;
            var blue = _match.lobbyBluePlayerIds;
            for (int i = 0; i < turnOrder.Count; i++) {
                var agent = turnOrder[i];
                if (agent == null) continue;
                int pid;
                if ((i % 2) == 0) {
                    int idx = i / 2;
                    pid = red != null && idx < red.Length ? red[idx] : agent.Player != null ? agent.Player.Id : _myPlayerId;
                } else {
                    int idx = i / 2;
                    pid = blue != null && idx < blue.Length ? blue[idx] : agent.Player != null ? agent.Player.Id : _myPlayerId;
                }
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
            var token = _cts.Token;
            var api = GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);
            var uri = new Uri(GameApiEndpoints.BattleWebSocketUri(api, lobbyWebSocketBaseUrl, _match.dbMatchId, authToken));
            _ws = new ClientWebSocket();
            try {
                await _ws.ConnectAsync(uri, token);
                Debug.Log($"[OnlineBattle] Connected {uri.GetLeftPart(UriPartial.Path)}");
                await ReceiveLoopAsync(token);
            } catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] WebSocket error: {e.Message}");
            } finally {
                try { _ws?.Dispose(); } catch { }
                _ws = null;
            }
        }

        async Task ReceiveLoopAsync(CancellationToken token) {
            var chunk = new byte[16384];
            while (_ws != null && _ws.State == WebSocketState.Open && !token.IsCancellationRequested) {
                using (var message = new MemoryStream()) {
                    WebSocketReceiveResult result;
                    do {
                        var segment = new ArraySegment<byte>(chunk);
                        result = await _ws.ReceiveAsync(segment, token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                            message.Write(chunk, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (message.Length > 0)
                        _incoming.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
                }
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
                case "battle_ready":
                    Debug.Log("[OnlineBattle] Server locked roster — waiting for first turn.");
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
                if (npc != null && npc.IsNPC)
                    await SendTextAsync($"claim|{i}|npc");
                else
                    await SendTextAsync($"claim|{i}");
            }
        }

        async Task SendPassAsync() {
            await SendTextAsync("pass");
        }

        async Task SendTextAsync(string text) {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            var data = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync();
            try {
                await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token);
            } catch (Exception e) {
                Debug.LogWarning($"[OnlineBattle] Send failed: {e.Message}");
            } finally {
                _sendLock.Release();
            }
        }
    }
}
