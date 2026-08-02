// What the agent can see and do. Debug and editor builds only.
//
// AgentState dumps the TRUE simulation state — never Reporting. The agent has to be able to
// see reality in order to tell whether the lies are working, and once ministers exist the gap
// between this dump and the HUD is the thing under test.

#if DEBUG || UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Mesruiyet.Core;
using Mesruiyet.Sim;
using Mesruiyet.World;

namespace Mesruiyet.Agent
{
    public static class AgentState
    {
        static GameState _state;
        static CityGrid _grid;

        public static void Bind(GameState state, CityGrid grid)
        {
            _state = state;
            _grid = grid;
        }

        /// <summary>False while a turn is resolving; TurnResolver owns this.</summary>
        public static bool TurnIdle => TurnResolver.Idle;

        public static string DumpJson()
        {
            if (_state == null) return "{\"error\":\"state not bound\"}";

            var g = _state;
            var sb = new StringBuilder(2048);
            sb.Append('{');

            Num(sb, "turn", g.Turn); sb.Append(',');
            Str(sb, "season", g.SeasonName); sb.Append(',');
            Num(sb, "year", g.Year); sb.Append(',');
            Num(sb, "turnsToElection", g.TurnsToElection); sb.Append(',');
            Num(sb, "legitimacy", g.Legitimacy); sb.Append(',');
            Num(sb, "axisOrder", g.AxisOrder); sb.Append(',');
            Num(sb, "axisEconomy", g.AxisEconomy); sb.Append(',');
            Str(sb, "bandOrder", Naming.Band(g.AxisOrder)); sb.Append(',');
            Str(sb, "bandEconomy", Naming.Band(g.AxisEconomy)); sb.Append(',');
            Num(sb, "bufferTurns", g.BufferTurns); sb.Append(',');
            Num(sb, "population", g.Population); sb.Append(',');
            Num(sb, "buildings", g.Buildings.Count); sb.Append(',');
            Num(sb, "labourPool", g.LabourPool); sb.Append(',');
            Num(sb, "labourUsed", g.LabourUsed); sb.Append(',');
            Bool(sb, "turnIdle", TurnIdle); sb.Append(',');

            sb.Append("\"stock\":{");
            for (int r = 0; r < 6; r++)
            {
                if (r > 0) sb.Append(',');
                Num(sb, Key(r), g.Stock[r]);
            }
            sb.Append("},");

            sb.Append("\"flow\":{");
            for (int r = 0; r < 6; r++)
            {
                if (r > 0) sb.Append(',');
                Num(sb, Key(r), g.Flow[r]);
            }
            sb.Append("},");

            sb.Append("\"indices\":{");
            Num(sb, "saglik", g.Saglik); sb.Append(',');
            Num(sb, "egitim", g.Egitim); sb.Append(',');
            Num(sb, "guvenlik", g.Guvenlik); sb.Append(',');
            Num(sb, "kultur", g.Kultur); sb.Append(',');
            Num(sb, "kirlilik", g.Kirlilik);
            sb.Append("},");

            sb.Append("\"districts\":[");
            for (int i = 0; i < g.Districts.Length; i++)
            {
                var d = g.Districts[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Str(sb, "name", d.Name); sb.Append(',');
                Num(sb, "grievance", d.Grievance); sb.Append(',');
                Num(sb, "population", d.Population); sb.Append(',');
                Num(sb, "housing", d.Housing); sb.Append(',');
                Num(sb, "angryStreak", d.AngryStreak);
                sb.Append('}');
            }
            sb.Append("],");

            // The chains in full. The agent has to be able to compare the ledger total against
            // what the last stage actually holds — that gap is the thing under test.
            sb.Append("\"chains\":[");
            for (int i = 0; i < g.Chains.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(g.Chains[i].ToJson());
            }
            sb.Append("],");

            Num(sb, "depot", g.DepotStock); sb.Append(',');
            Num(sb, "bread", g.FoodChain.Final.Stock); sb.Append(',');

            sb.Append("\"factions\":{");
            for (int f = 0; f < 5; f++)
            {
                if (f > 0) sb.Append(',');
                // True loyalty, not the mood the player is shown.
                Num(sb, Naming.FactionNames[f], g.FactionLoyalty[f]);
            }
            sb.Append("},");

            // What the HUD is currently claiming, so a test can compare the two directly.
            sb.Append("\"reported\":{");
            Num(sb, "yiyecek", Reporting.Stock(Res.Yiyecek).Value); sb.Append(',');
            Num(sb, "bufferTurns", Reporting.Buffer().Value); sb.Append(',');
            Bool(sb, "yiyecekReliable", Reporting.Stock(Res.Yiyecek).Reliable);
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        static string Key(int r) => Naming.ResourceNames[r]
            .Replace("İ", "I").Replace("ş", "s").Replace("ç", "c").Replace("ü", "u").ToLowerInvariant();

        static void Num(StringBuilder sb, string key, float v)
            => sb.Append('"').Append(key).Append("\":")
                 .Append(v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

        static void Str(StringBuilder sb, string key, string v)
            => sb.Append('"').Append(key).Append("\":\"").Append(v.Replace("\"", "'")).Append('"');

        static void Bool(StringBuilder sb, string key, bool v)
            => sb.Append('"').Append(key).Append("\":").Append(v ? "true" : "false");
    }

    /// <summary>Synthetic input so the agent can actually play, not just observe.</summary>
    public static class AgentInput
    {
        /// <summary>Root of the UI Toolkit document; Bootstrap sets this.</summary>
        public static VisualElement UiRoot;

        public static void Press(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var cam = IsoCamera.Instance;
            if (cam == null) return;

            switch (key.ToLowerInvariant())
            {
                case "w": cam.Pan(Vector2.up); break;
                case "s": cam.Pan(Vector2.down); break;
                case "a": cam.Pan(Vector2.left); break;
                case "d": cam.Pan(Vector2.right); break;
                case "q": cam.Rotate(-90f); break;
                case "e": cam.Rotate(90f); break;
                case "zoomin": cam.Zoom(-0.5f); break;
                case "zoomout": cam.Zoom(0.5f); break;
                case "escape": Placement.Instance?.Disarm(); break;
                default: Debug.Log($"[AgentInput] bilinmeyen tuş: {key}"); break;
            }
        }

        /// <summary>
        /// Click a UI element by its `name`. Buttons are invoked through their own clickable
        /// rather than through a synthetic event, so this exercises exactly the code path a
        /// human click takes and cannot silently no-op.
        /// </summary>
        public static bool Click(string id)
        {
            if (UiRoot == null) { Debug.LogWarning("[AgentInput] UiRoot not set"); return false; }

            var element = UiRoot.Q(id);
            if (element == null) return false;

            if (element is Button button)
            {
                if (!button.enabledInHierarchy) return false;
                using (var e = new NavigationSubmitEvent { target = button })
                    button.SendEvent(e);
                Debug.Log($"[AgentInput] click {id}");
                return true;
            }

            using (var e = new NavigationSubmitEvent { target = element })
                element.SendEvent(e);
            return true;
        }

        /// <summary>
        /// Shut down every building of a type, or start them again. This is how the loop
        /// reproduces a supply blockage without waiting for an event card to roll one: stop the
        /// mills and watch the granary fill while the bakeries empty.
        /// </summary>
        public static int Block(string buildingId, bool on, out string message)
        {
            message = "";
            var state = GameState.Current;
            if (state == null) { message = "state not bound"; return 0; }

            int touched = 0;
            foreach (var b in state.Buildings)
            {
                if (b.Def.Id != buildingId) continue;
                b.Disabled = on;
                touched++;
            }

            if (touched == 0) { message = $"'{buildingId}' türünde yapı yok"; return 0; }

            TurnResolver.Instance.Recompute();
            Debug.Log($"[AgentInput] {(on ? "block" : "unblock")} {buildingId} ×{touched}");
            return touched;
        }

        /// <summary>Place a building by id at a tile — the agent's version of a mouse click on the map.</summary>
        public static bool Build(string buildingId, int x, int y, out string message)
        {
            message = "";
            var def = Buildings.Get(buildingId);
            if (def == null) { message = $"bilinmeyen yapı '{buildingId}'"; return false; }
            if (Placement.Instance == null) { message = "placement not ready"; return false; }

            bool ok = Placement.Instance.TryBuild(def, x, y);
            if (!ok) message = Placement.Instance.LastRefusal;
            return ok;
        }
    }
}
#endif
