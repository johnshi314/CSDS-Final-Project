using System;
using System.Threading.Tasks;

namespace NetFlower.Net {

    public enum WebSocketState { Connecting, Open, Closing, Closed }

    public interface IWebSocket {
        event Action OnOpen;
        event Action<byte[]> OnMessage;
        event Action<string> OnError;
        event Action<int> OnClose;

        WebSocketState State { get; }

        Task ConnectAsync();
        Task SendTextAsync(string message);
        Task CloseAsync();
        void CancelConnection();

        /// <summary>
        /// Pump queued events on the main thread. Call from Update().
        /// No-op on WebGL (browser dispatches on main thread already).
        /// </summary>
        void DispatchMessageQueue();
    }

    public static class WebSocketFactory {
        public static IWebSocket Create(string url) {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWebSocket(url);
#else
            return new DesktopWebSocket(url);
#endif
        }
    }
}
