#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetFlower.Net {
    public sealed class DesktopWebSocket : IWebSocket {

        readonly Uri _uri;
        ClientWebSocket _socket;
        CancellationTokenSource _cts;
        // ClientWebSocket isn't thread-safe for sending, so we use a lock to serialize SendTextAsync calls.
        readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        readonly ConcurrentQueue<byte[]> msgQ = new ConcurrentQueue<byte[]>();
        readonly ConcurrentQueue<Action> eventQ = new ConcurrentQueue<Action>();

        volatile NetFlower.Net.WebSocketState wsState = NetFlower.Net.WebSocketState.Closed;

        // Events
        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<string> OnError;
        public event Action<int> OnClose;

        public NetFlower.Net.WebSocketState State => wsState;

        public DesktopWebSocket(string url) {
            var ub = new UriBuilder(url);
            if (ub.Scheme == "https") ub.Scheme = "wss";
            else if (ub.Scheme == "http") ub.Scheme = "ws";
            _uri = ub.Uri;
        }

        public async Task ConnectAsync() {
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            wsState = NetFlower.Net.WebSocketState.Connecting;

            int closeCode = 1006;
            try {
                await _socket.ConnectAsync(_uri, _cts.Token).ConfigureAwait(false);
                wsState = NetFlower.Net.WebSocketState.Open;
                eventQ.Enqueue(() => OnOpen?.Invoke());

                closeCode = await ReceiveLoop().ConfigureAwait(false);
            } catch (OperationCanceledException) {
                closeCode = 1000;
            } catch (Exception e) {
                eventQ.Enqueue(() => OnError?.Invoke(e.Message));
            } finally {
                wsState = NetFlower.Net.WebSocketState.Closed;
                var code = closeCode;
                eventQ.Enqueue(() => OnClose?.Invoke(code));
                try { _socket?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Continuously receive messages until the socket is closed. Enqueue received messages and close event.
        /// </summary>
        async Task<int> ReceiveLoop() {
            var buf = new ArraySegment<byte>(new byte[65536]);
            int closeCode = 1006;
            while (_socket.State == System.Net.WebSockets.WebSocketState.Open) {
                WebSocketReceiveResult result;

                // Get data from the socket
                try {
                    result = await _socket.ReceiveAsync(buf, _cts.Token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    closeCode = 1000;
                    break;
                } catch {
                    break;
                }

                // Check if the socket was closed
                if (result.MessageType == WebSocketMessageType.Close) {
                    closeCode = result.CloseStatus.HasValue ? (int)result.CloseStatus.Value : 1000;
                    try {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                            .ConfigureAwait(false);
                    } catch { }
                    break;
                }

                // Handle received data (handle multi-frame messages)
                byte[] data;
                if (result.EndOfMessage) {
                    // Single frame message
                    data = new byte[result.Count];
                    Buffer.BlockCopy(buf.Array, buf.Offset, data, 0, result.Count);
                } else {
                    // Keep reading until end of message
                    using (var ms = new MemoryStream()) {
                        ms.Write(buf.Array, buf.Offset, result.Count);
                        do {
                            result = await _socket.ReceiveAsync(buf, _cts.Token).ConfigureAwait(false);
                            ms.Write(buf.Array, buf.Offset, result.Count);
                        } while (!result.EndOfMessage);
                        data = ms.ToArray();
                    }
                }

                // Push the data onto the queue
                msgQ.Enqueue(data);
            }
            return closeCode;
        }

        public async Task SendTextAsync(string message) {
            if (wsState != NetFlower.Net.WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);
            await _lock.WaitAsync().ConfigureAwait(false);
            try {
                await _socket.SendAsync(segment, WebSocketMessageType.Text, true, _cts.Token)
                    .ConfigureAwait(false);
            } finally {
                _lock.Release();
            }
        }

        public async Task CloseAsync() {
            if (_socket == null || _socket.State != System.Net.WebSockets.WebSocketState.Open) return;
            wsState = NetFlower.Net.WebSocketState.Closing;
            try {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
            } catch { }
        }

        public void CancelConnection() {
            _cts?.Cancel();
        }

        public void DispatchMessageQueue() {
            while (msgQ.TryDequeue(out var msg))
                OnMessage?.Invoke(msg);
            while (eventQ.TryDequeue(out var ev))
                ev();
        }
    }
}

#endif
