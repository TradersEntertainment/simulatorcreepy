// RoleView — the security boundary, per COOP.md §7: filtering happens when the message is
// BUILT, never in the UI. Every payload the governor's client sends to a minister seat is
// assembled here from that seat's own domain and nothing else — a minister's client can be
// debugged, dumped and decompiled and still contain no other desk's truth, because the
// bytes never crossed the wire.

#if !UNITY_WEBGL
using System.Text;
using Mesruiyet.Core;

namespace Mesruiyet.Net
{
    public static class RoleView
    {
        /// <summary>
        /// What seat N (1..5) is allowed to see after a tick: the turn, their own hidden
        /// objective, and the TRUE values of their own desk's lines. No treasury unless it is
        /// their desk, no axes, no other ministry — by construction, not by filtering.
        /// </summary>
        public static string SeatState(GameState g, int seat)
        {
            var domain = (Domain)(seat - 1);
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"koltuk\":").Append(seat).Append(',');
            sb.Append("\"tur\":").Append(g.Turn).Append(',');
            sb.Append("\"hedef\":\"").Append(g.ObjectiveOf[(int)domain] ?? "").Append("\",");
            sb.Append("\"satirlar\":[");
            var lines = ReportLines.For(domain);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"satir\":\"").Append(lines[i].Key).Append("\",\"gercek\":")
                  .Append(lines[i].True(g).ToString("0.##",
                      System.Globalization.CultureInfo.InvariantCulture))
                  .Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// A minister's outbound report: only their own desk's lines, as claims. Used by the
        /// minister-side client; the governor never calls this.
        /// </summary>
        public static string Report(Domain domain, System.Collections.Generic.IReadOnlyDictionary<string, float> claims)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"satirlar\":[");
            bool first = true;
            foreach (var line in ReportLines.For(domain))
            {
                if (!claims.TryGetValue(line.Key, out float value)) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"satir\":\"").Append(line.Key).Append("\",\"deger\":")
                  .Append(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                  .Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
#endif
