/***********************************************************************
* File Name     : WebSocketClient.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-01-31
* Description   : Turn-based WebSocket client for Unity.
**********************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


namespace Backend {

    /// <summary>
    /// Turn-based WebSocket client. Connects to the server, receives you/turn/said/epoch
    /// messages, waits for its turn to send, and listens for what other players said
    /// and for the server epoch counter.
    /// </summary>
    public class WebSocketClient : MonoBehaviour {
        // Configurable fields
        [SerializeField]
        string serverUrl = "ws://localhost:8765";

        [SerializeField]
        bool connectOnStart = true;

        [SerializeField]
        bool sendOnMyTurn = true;

        [SerializeField]
        string messageOnTurn = "Player {0} here!";

        // WebSocket and related fields
        private ClientWebSocket ws;
        private CancellationTokenSource cts;
        readonly ConcurrentQueue<string> incoming = new();

        // State fields
        private string _myPlayerId = "";
        private bool _isMyTurn;
        private int _lastEpoch = -1;
        private bool _sentThisTurn;

        /// <summary>Our assigned player id (set after you|id from server).</summary>
        public string MyPlayerId => _myPlayerId;

        /// <summary>True when the server said it's our turn (we can send).</summary>
        public bool IsMyTurn => _isMyTurn;

        /// <summary>Last server epoch we received.</summary>
        public int LastEpoch => _lastEpoch;

        void Start() {
            if (connectOnStart)
                Connect();
        }

        void Update() {
            while (incoming.TryDequeue(out var raw))
                HandleServerMessage(raw);
        }

        void OnDestroy() {
            Disconnect();
        }

        /// <summary>Handle a raw message from the server.</summary>
        /// <param name="raw">Raw message string.</param>
        /// </summary>
        void HandleServerMessage(string raw) {
            if (string.IsNullOrEmpty(raw))
                return;
            var parts = raw.Split(new[] { '|' }, 3, StringSplitOptions.None);
            if (parts.Length < 2)
                return;

            switch (parts[0]) {
                case "you":
                    _myPlayerId = parts[1];
                    Debug.Log($"[WebSocket] You are player {_myPlayerId}");
                    break;
                case "turn":
                    _isMyTurn = parts[1] == _myPlayerId;
                    _sentThisTurn = false;
                    if (_isMyTurn)
                        Debug.Log("[WebSocket] Your turn! You can send a message.");
                    else
                        Debug.Log($"[WebSocket] Player {parts[1]}'s turn.");
                    if (_isMyTurn && sendOnMyTurn && !_sentThisTurn) {
                        var msg = string.Format(messageOnTurn, _myPlayerId);
                        Send(msg);
                        _sentThisTurn = true;
                    }

                    break;
                case "said":
                    if (parts.Length >= 3)
                        Debug.Log($"[WebSocket] Player {parts[1]} said: {parts[2]}");
                    else
                        Debug.Log($"[WebSocket] Player {parts[1]} said nothing this turn.");
                    break;
                case "epoch":
                    if (int.TryParse(parts[1], out var n)) {
                        _lastEpoch = n;
                        Debug.Log($"[WebSocket] Server epoch: {n}");
                    }

                    break;
                default:
                    Debug.Log($"[WebSocket] Unknown: {raw}");
                    break;
            }
        }

        /// <summary>Connect to the server. Safe to call from main thread.</summary>
        public void Connect() {
            if (ws?.State == WebSocketState.Open)
                return;
            Disconnect();
            cts = new CancellationTokenSource();
            ws = new ClientWebSocket();
            _ = RunConnectionAsync();
        }

        /// <summary>Disconnect.</summary>
        public void Disconnect() {
            cts?.Cancel();
            try {
                ws?.Abort();
                ws?.Dispose();
            } catch {
                /* ignore */
            }

            ws = null;
            cts = null;
        }

        /// <summary>Send a message. Only sent to server when it's our turn; server ignores otherwise.</summary>
        public void Send(string text) {
            if (ws?.State != WebSocketState.Open) {
                Debug.LogWarning("[WebSocket] Not connected.");
                return;
            }

            if (!_isMyTurn) {
                Debug.LogWarning("[WebSocket] Not your turn; server will ignore this message.");
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            _ = ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }

        /// <summary>Run the connection: connect and start receiving messages.</summary>
        /// <returns>Task.</returns>
        async Task RunConnectionAsync() {
            var token = cts.Token;
            try {
                await ws.ConnectAsync(new Uri(serverUrl), token);
                Debug.Log($"[WebSocket] Connected to {serverUrl}");
                await ReceiveLoopAsync(token);
            } catch (OperationCanceledException) {
                Debug.Log("[WebSocket] Connection cancelled.");
            } catch (Exception e) {
                incoming.Enqueue($"[Error] {e.Message}");
            } finally {
                try {
                    ws?.Dispose();
                } catch { }
                ws = null;
            }
        }

        /// <summary> Receive messages from the server in a loop.
        /// This method is used to receive messages from the server.
        /// It is called in the RunConnectionAsync method.
        /// It is a loop that receives messages from the server and enqueues them into the incoming queue.
        /// It is also used to check if the websocket is not null, open, and the token is not cancelled.
        /// If the websocket is not null, open, and the token is not cancelled, it will receive a message from the server.
        /// If the message is a close message, it will break the loop.
        /// If the message is a text message, it will enqueue the message into the incoming queue.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        async Task ReceiveLoopAsync(CancellationToken token) {
            var buffer = new byte[4096];
            // check if the websocket is not null, open, and the token is not cancelled
            while (ws != null && ws.State == WebSocketState.Open && !token.IsCancellationRequested) {
                var segment = new ArraySegment<byte>(buffer);
                Debug.Log("[WebSocket] Receiving message");
                var result = await ws.ReceiveAsync(segment, token);
                if (result.MessageType == WebSocketMessageType.Close) {
                    Debug.Log("[WebSocket] Closing connection");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text && result.Count > 0) {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log("[WebSocket] Received message: " + text);
                    incoming.Enqueue(text);
                }
            }
        }
    }
}