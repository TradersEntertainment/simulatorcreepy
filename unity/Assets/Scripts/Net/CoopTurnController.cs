// CoopTurnController — where the network meets the deception engine.
//
// Runs on the governor's client. Three jobs, per COOP.md §3 and §7:
//   1. Seats: a connected human on a minister seat plugs a HumanSource into Reporting for
//      that desk; a seat that empties mid-match falls to the bot cabinet, and the match
//      never stops. All-formula single player stays untouched — this component is inert
//      until a lobby connection exists and a match starts.
//   2. Reports: a remote minister's "rapor" lands here, its claims go into the same
//      HumanSource the hot-seat screen fills, and the governor's ledger shows the lie —
//      the whole co-op idea, closed over a real wire.
//   3. After every resolved turn the governor broadcasts each seat its RoleView cut, so a
//      minister's client knows its own truth and nothing else.

#if !UNITY_WEBGL
using System;
using UnityEngine;
using Mesruiyet.Core;
using Mesruiyet.Sim;

namespace Mesruiyet.Net
{
    [Serializable] class WireClaim { public string satir; public float deger; }
    [Serializable] class WireReportData { public WireClaim[] satirlar; }
    [Serializable] class WireReport { public string t; public int seat; public WireReportData data; }

    public sealed class CoopTurnController : MonoBehaviour
    {
        public static CoopTurnController Instance;

        GameState _state;
        bool _hooked;

        /// <summary>The governor may not resolve a game turn while the minister phase runs.</summary>
        public static bool BlocksTurn
        {
            get
            {
                var net = NetManager.Instance;
                return net != null && net.Connected && net.MySeat == 0 && net.Phase == "bakan";
            }
        }

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
        }

        void Update()
        {
            if (_hooked) return;
            var net = NetManager.Instance;
            if (net == null || !net.Connected) return;
            net.ReportReceived += OnReport;
            net.Changed += OnLobbyChanged;
            TurnResolver.TurnCompleted += OnTurnCompleted;
            _hooked = true;
        }

        /// <summary>
        /// Mirror the lobby's seats into HotSeat. Only meaningful once a match has begun;
        /// in the lobby phase the single-player cabinet stays exactly as it was.
        /// </summary>
        void OnLobbyChanged()
        {
            var net = NetManager.Instance;
            if (net == null || net.MySeat != 0 || net.Phase == "" || net.Phase == "lobi") return;

            for (int seat = 1; seat <= 5; seat++)
            {
                var domain = (Domain)(seat - 1);
                bool humanHere = false;
                foreach (var p in net.Players)
                    if (p.seat == seat && p.connected) { humanHere = true; break; }

                var wanted = humanHere ? SeatKind.Insan : SeatKind.Bot;
                if (HotSeat.KindOf(domain) != wanted) HotSeat.SetSeat(domain, wanted);
            }
            UI.Hud.Instance?.Refresh();
        }

        /// <summary>A remote minister's report: claims into their desk's HumanSource.</summary>
        void OnReport(string line)
        {
            var net = NetManager.Instance;
            if (net == null || net.MySeat != 0) return;

            WireReport report;
            try { report = JsonUtility.FromJson<WireReport>(line); }
            catch { return; }
            if (report == null || report.seat < 1 || report.seat > 5) return;

            var domain = (Domain)(report.seat - 1);
            if (HotSeat.KindOf(domain) != SeatKind.Insan) HotSeat.SetSeat(domain, SeatKind.Insan);
            var human = HotSeat.HumanOf(domain);
            if (human == null) return;

            if (report.data?.satirlar != null)
                foreach (var claim in report.data.satirlar)
                {
                    var def = ReportLines.Find(domain, claim.satir);
                    if (def == null) continue;
                    human.SetClaim(def.Line, claim.deger, def.True(_state));
                }
            human.Submit();
            UI.Hud.Instance?.Refresh();
        }

        /// <summary>Every resolved turn, each seated minister gets their RoleView cut.</summary>
        void OnTurnCompleted()
        {
            var net = NetManager.Instance;
            if (net == null || !net.Connected || net.MySeat != 0) return;

            foreach (var p in net.Players)
                if (p.seat >= 1 && p.seat <= 5 && p.connected)
                    net.SendSeatState(p.seat, RoleView.SeatState(_state, p.seat));
        }

        void OnDestroy()
        {
            var net = NetManager.Instance;
            if (_hooked && net != null)
            {
                net.ReportReceived -= OnReport;
                net.Changed -= OnLobbyChanged;
            }
            if (_hooked) TurnResolver.TurnCompleted -= OnTurnCompleted;
            if (Instance == this) Instance = null;
        }
    }
}
#endif
