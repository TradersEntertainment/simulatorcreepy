// NetManager — the game's one door to the lobby server.
//
// Holds the connection, mirrors the lobby state (phase, seats, players) on the main thread,
// and speaks the little line-JSON dialect of web/lobby-core.js. Nothing else in the game
// touches a socket — and since slice 6 the socket itself is an ISocket, picked per platform:
// ClientWebSocket on desktop, the browser's WebSocket through WebSocket.jslib on WebGL.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mesruiyet.Net
{
    [Serializable] public class WirePlayer
    {
        public string pid;
        public string name;
        public int seat = -1;
        public bool connected;
        public bool ready;
        public bool submitted;
    }

    [Serializable] class WireLobby
    {
        public string t;
        public string code;
        public int rev;
        public string phase;
        public double deadline;
        public WirePlayer[] players;
    }

    [Serializable] class WireProbe { public string t; }
    [Serializable] class WireFaz { public string t; public string name; public double deadline; public string reason; }

    public sealed class NetManager : MonoBehaviour
    {
        public static NetManager Instance;

        ISocket _socket;
        string _pid = "";

        public bool Connected => _socket != null && _socket.Open;
        public string Code { get; private set; } = "";
        public string Phase { get; private set; } = "";
        public double Deadline { get; private set; }
        public int MySeat { get; private set; } = -1;
        public readonly List<WirePlayer> Players = new List<WirePlayer>();
        public string LastError => _socket?.LastError ?? "";

        /// <summary>Raised on every lobby-state change, phase change included.</summary>
        public event Action Changed;

        /// <summary>Raw "rapor" lines, for the co-op turn controller on the governor's side.</summary>
        public event Action<string> ReportReceived;

        void Awake() { Instance = this; }

        public async void Connect(string baseUrl, string code, string playerName)
        {
            Code = code.ToUpperInvariant();
            // deviceUniqueIdentifier is unsupported in some browsers; a session GUID is fine —
            // reconnection to a seat goes by lobby code, not by pid.
            string device = SystemInfo.deviceUniqueIdentifier;
            _pid = "u-" + (device != null && device.Length >= 12 && device != SystemInfo.unsupportedIdentifier
                ? device.Substring(0, 12)
                : Guid.NewGuid().ToString("N").Substring(0, 12));
#if UNITY_WEBGL && !UNITY_EDITOR
            _socket = new WebGLSocket();
#else
            _socket = new DesktopSocket();
#endif

            string url = baseUrl.TrimEnd('/') + "/lobby/" + Code;
            bool ok = await _socket.Connect(url);
            if (!ok)
            {
                Debug.LogWarning($"[Net] bağlanamadı: {url} — {_socket.LastError}");
                return;
            }
            await _socket.Send("{\"t\":\"hello\",\"pid\":\"" + _pid + "\",\"name\":\"" +
                               (playerName ?? "vali") + "\"}");
        }

        public void Disconnect()
        {
            _socket?.Close();
            Phase = "";
            MySeat = -1;
            Players.Clear();
            Changed?.Invoke();
        }

        public void ClaimSeat(int n) => Send($"{{\"t\":\"koltuk\",\"n\":{n}}}");
        public void StartMatch() => Send("{\"t\":\"baslat\"}");
        public void NextPhase(string name) => Send($"{{\"t\":\"faz\",\"name\":\"{name}\"}}");

        /// <summary>A minister's report envelope. The data is built (and filtered) upstream.</summary>
        public void SendReport(string dataJson)
            => Send("{\"t\":\"rapor\",\"data\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) + "}");

        /// <summary>The governor's per-seat state, already cut by RoleView. Routed to one seat.</summary>
        public void SendSeatState(int seat, string dataJson)
            => Send("{\"t\":\"durum\",\"seat\":" + seat + ",\"data\":" + dataJson + "}");

        async void Send(string line)
        {
            if (_socket != null && _socket.Open) await _socket.Send(line);
        }

        void Update()
        {
            _socket?.Poll(HandleLine);
        }

        void HandleLine(string line)
        {
            WireProbe probe;
            try { probe = JsonUtility.FromJson<WireProbe>(line); }
            catch { return; }
            if (probe == null || string.IsNullOrEmpty(probe.t)) return;

            switch (probe.t)
            {
                case "durum-lobi":
                {
                    var s = JsonUtility.FromJson<WireLobby>(line);
                    Phase = s.phase;
                    Deadline = s.deadline;
                    Players.Clear();
                    MySeat = -1;
                    if (s.players != null)
                        foreach (var p in s.players)
                        {
                            Players.Add(p);
                            if (p.pid == _pid) MySeat = p.seat;
                        }
                    Changed?.Invoke();
                    break;
                }

                case "faz":
                {
                    var f = JsonUtility.FromJson<WireFaz>(line);
                    Phase = f.name;
                    Deadline = f.deadline;
                    Changed?.Invoke();
                    break;
                }

                case "rapor":
                    ReportReceived?.Invoke(line);
                    break;

                // telgraf / ozel / ferman / durum / anlik: minister-side consumption arrives
                // with the WebGL slice. Unknown types stay silent on purpose.
                default:
                    break;
            }
        }

        void OnDestroy()
        {
            _socket?.Close();
            if (Instance == this) Instance = null;
        }
    }
}
