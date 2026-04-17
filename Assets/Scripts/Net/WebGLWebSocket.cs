#if UNITY_WEBGL && !UNITY_EDITOR

using System;
using System.Threading.Tasks;
using NativeWebSocket;

namespace NetFlower.Net {

    /// <summary>
    /// WebGL wrapper — delegates everything to NativeWebSocket which already
    /// works correctly in WebGL builds. Just adapt the interface.
    /// </summary>
    public sealed class WebGLWebSocket : IWebSocket {

        readonly WebSocket _ws;

        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<string> OnError;
        public event Action<int> OnClose;

        public WebSocketState State => _ws.State switch {
            NativeWebSocket.WebSocketState.Connecting => NetFlower.Net.WebSocketState.Connecting,
            NativeWebSocket.WebSocketState.Open       => NetFlower.Net.WebSocketState.Open,
            NativeWebSocket.WebSocketState.Closing    => NetFlower.Net.WebSocketState.Closing,
            _                                         => NetFlower.Net.WebSocketState.Closed,
        };

        public WebGLWebSocket(string url) {
            _ws = new WebSocket(url);
            _ws.OnOpen    += () => OnOpen?.Invoke();
            _ws.OnMessage += bytes => OnMessage?.Invoke(bytes);
            _ws.OnError   += msg => OnError?.Invoke(msg);
            _ws.OnClose   += code => OnClose?.Invoke((int)code);
        }

        public Task ConnectAsync()              => _ws.Connect();
        public Task SendTextAsync(string msg)   => _ws.SendText(msg);
        public Task CloseAsync()                => _ws.Close();
        public void CancelConnection()          => _ws.CancelConnection();
        public void DispatchMessageQueue()      { }
    }
}

#endif
