using System;
using UnityEngine;

namespace NetFlower {
    /// <summary>
    /// Shared REST / WebSocket base URL normalization for Login, Matchmaking, and Match.
    /// </summary>
    public static class GameApiEndpoints {
        /// <summary>
        /// REST base for HTTP calls. Bare https://litecoders.com becomes …/api.
        /// </summary>
        public static string EffectiveApiBase(string httpApiBaseUrl) {
            var raw = (httpApiBaseUrl ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(raw))
                return "https://litecoders.com/api";
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var u))
                return raw;
            var host = u.Host;
            if (host.Equals("litecoders.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("www.litecoders.com", StringComparison.OrdinalIgnoreCase)) {
                var path = u.AbsolutePath.TrimEnd('/');
                if (string.IsNullOrEmpty(path) || path == "/")
                    return $"{u.Scheme}://{u.Authority}/api";
            }
            return raw;
        }

        /// <summary>ws(s)://host[:port] from REST base (strips any /api path).</summary>
        public static string WebSocketSchemeHostFromHttpApi(string httpApiBase) {
            if (string.IsNullOrWhiteSpace(httpApiBase))
                httpApiBase = "http://localhost:8000";
            if (!Uri.TryCreate(httpApiBase.Trim(), UriKind.Absolute, out var u))
                return "ws://localhost:8000";
            var sch = u.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            return $"{sch}://{u.Authority}";
        }

        /// <summary>Lobby WebSocket base, no trailing slash (…/ws).</summary>
        public static string LobbyWebSocketBaseTrimmed(string effectiveHttpApiBase, string lobbyWebSocketBaseUrl) {
            var explicitBase = (lobbyWebSocketBaseUrl ?? "").Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(explicitBase))
                return explicitBase;
            return WebSocketSchemeHostFromHttpApi(effectiveHttpApiBase) + "/ws";
        }

        /// <summary>Authenticated battle WebSocket URI for a match (query token).</summary>
        public static string BattleWebSocketUri(string effectiveHttpApiBase, string lobbyWebSocketBaseOverride, int matchId, string authToken) {
            var baseTrim = LobbyWebSocketBaseTrimmed(effectiveHttpApiBase, lobbyWebSocketBaseOverride);
            var tok = Uri.EscapeDataString(authToken ?? "");
            return $"{baseTrim}/battle/{matchId}?authToken={tok}";
        }
    }
}
