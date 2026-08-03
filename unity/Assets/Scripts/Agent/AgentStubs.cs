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
                Num(sb, "angryStreak", d.AngryStreak); sb.Append(',');
                Num(sb, "roadTiles", d.RoadTiles); sb.Append(',');
                Num(sb, "congestion", d.Congestion); sb.Append(',');
                // How well surfaced the quarter's streets are. Colour is the only place this
                // shows in game, and colour is not something an unattended run can assert on.
                Num(sb, "paving", CityRenderer.PavingOf(d.Def, g));
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
            Num(sb, "transparency", g.Transparency); sb.Append(',');

            // Ministers with their TRUE bias, so a test can measure the gap between what the
            // HUD shows and what is actually happening. The player never sees this number.
            sb.Append("\"ministers\":[");
            for (int i = 0; i < 5; i++)
            {
                var m = g.Cabinet.Of((Domain)i);
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Str(sb, "domain", Ministers.DomainNames[i]); sb.Append(',');
                Str(sb, "name", m.Name); sb.Append(',');
                Bool(sb, "loyalist", m.Loyalist); sb.Append(',');
                Num(sb, "bias", Distortion.Bias(m, g)); sb.Append(',');
                Str(sb, "telegram", m.Telegram);
                sb.Append('}');
            }
            sb.Append("],");

            // Governance: the chamber, the book, and what the city actually thinks. Murmurs are
            // included verbatim so a test can assert that the truth still reaches the governor
            // through the council even while every ministry is rounding it away.
            sb.Append("\"council\":{");
            Num(sb, "seats", Sim.Council.Seats); sb.Append(',');
            Bool(sb, "suspended", g.Council.Suspended); sb.Append(',');
            Num(sb, "support", Sim.GovernanceManager.Instance != null
                               ? Sim.GovernanceManager.Instance.TrueSupport() : 0f); sb.Append(',');
            sb.Append("\"murmurs\":[");
            for (int i = 0; i < g.Council.Murmurs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(g.Council.Murmurs[i].Replace("\"", "'")).Append('"');
            }
            sb.Append("]},");

            sb.Append("\"laws\":[");
            for (int i = 0; i < g.LawBook.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(g.LawBook[i].Id).Append('"');
            }
            sb.Append("],");

            Num(sb, "lawSlots", g.LawSlots); sb.Append(',');
            Num(sb, "decreesLeft", g.DecreesLeft); sb.Append(',');
            Bool(sb, "electionPending", g.ElectionPending); sb.Append(',');

            // The outside world, with the true garrison and the true coup clock — the player may
            // be looking at a number their Güvenlik minister invented.
            Num(sb, "threat", g.Threat); sb.Append(',');
            Num(sb, "garrison", g.Garrison); sb.Append(',');
            Num(sb, "conscripts", g.Conscripts); sb.Append(',');
            Num(sb, "coupCountdown", g.CoupCountdown); sb.Append(',');
            Num(sb, "owed", g.TotalOwed); sb.Append(',');
            Num(sb, "sealedSlots", g.SealedSlots); sb.Append(',');
            Str(sb, "pendingEvent", g.PendingEvent != null ? g.PendingEvent.Id : ""); sb.Append(',');
            Str(sb, "lastEvent", g.LastEventOutcome); sb.Append(',');

            // The skinned-character budget, as numbers: how many real models are up, against the
            // hard cap the frame rate depends on, plus the one scale constant and whether their
            // feet are actually on the ground.
            var skins = World.CrowdSkins.Instance;
            sb.Append("\"iskelet\":{");
            Bool(sb, "hazir", skins != null && skins.Ready); sb.Append(',');
            Num(sb, "aktif", skins != null ? skins.ActiveSkins : 0); sb.Append(',');
            Num(sb, "tavan", World.CrowdSkins.SkinnedCap); sb.Append(',');
            Num(sb, "asker", skins != null ? skins.SoldierCount : 0); sb.Append(',');
            Num(sb, "olcek", skins != null ? skins.Scale : 0f); sb.Append(',');
            Num(sb, "enKotuMinY", skins != null ? skins.WorstMinY : 0f); sb.Append(',');
            Str(sb, "shader", skins != null ? skins.FirstShaderName : "");
            sb.Append("},");

            // Does every building rest on the ground? Measured, not judged: the renderer records
            // the lowest vertex each building contributed and compares it to the tile it stands
            // on. Anything above the floor is a part standing on air.
            var cr = World.CityRenderer.Instance;
            sb.Append("\"zemin\":{");
            Num(sb, "havada", cr != null ? cr.FloatingBuildings : 0); sb.Append(',');
            Num(sb, "enKotuKalkis", cr != null ? cr.WorstLift : 0f); sb.Append(',');
            Str(sb, "enKotuYapi", cr != null ? cr.WorstLiftId : "");
            sb.Append("},");

            // Which menu, if any, is covering the game. The agent opens a term through the title
            // screen the same way a player does, so it has to be able to see one.
            var hud = UI.Hud.Instance;
            Str(sb, "menu", hud == null ? "" : hud.MenuName); sb.Append(',');

            // The dock's category tabs and the armed building, so a scenario can prove the
            // click path works: open a tab, arm a tile, escape, and watch these two fields.
            var placement = World.Placement.Instance;
            Str(sb, "acikKategori", hud == null ? "" : hud.OpenCategory); sb.Append(',');
            Str(sb, "secili", placement != null && placement.Selected != null ? placement.Selected.Id : ""); sb.Append(',');
            Str(sb, "disKarti", hud != null && hud.IsFoldOpen("dis") ? "acik" : "kapali"); sb.Append(',');

            // Audio cannot be verified by listening in an unattended run, so it reports itself:
            // how many clips were synthesized, and what the two ambient voices are currently
            // doing. A drone that never responds to grievance is a dead system, silently.
            var audio = AudioBus.Instance;
            sb.Append("\"audio\":{");
            Num(sb, "clips", audio != null ? audio.ClipCount : 0); sb.Append(',');
            Num(sb, "drone", audio != null ? audio.DroneLevel : 0f); sb.Append(',');
            Num(sb, "murmur", audio != null ? audio.MurmurLevel : 0f); sb.Append(',');
            sb.Append("\"muted\":").Append(audio != null && audio.Muted ? "true" : "false");
            sb.Append("},");

            // Which cards this run has actually drawn. A deck that reads well on paper can still
            // deal the same four crises all game once its conditions narrow, and that is only
            // visible from the outside.
            Num(sb, "deckSize", Events.All.Length); sb.Append(',');
            sb.Append("\"firedEvents\":[");
            bool firstFired = true;
            foreach (var id in g.FiredEvents)
            {
                if (!firstFired) sb.Append(',');
                firstFired = false;
                sb.Append('"').Append(id).Append('"');
            }
            sb.Append("],");

            sb.Append("\"loans\":[");
            for (int i = 0; i < g.Loans.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{");
                Str(sb, "id", g.Loans[i].Def.Id); sb.Append(',');
                Str(sb, "law", g.Loans[i].Def.DemandedLaw); sb.Append(',');
                Num(sb, "owed", g.Loans[i].Owed);
                sb.Append("}");
            }
            sb.Append("],");

            // The crowd as the player would read it off the map. This is the readability
            // channel under test: a district should be diagnosable by looking at it.
            var crowd = Object.FindFirstObjectByType<CrowdSystem>();
            if (crowd != null)
            {
                crowd.Census(out int walking, out int idle, out int marching, out int hidden);
                sb.Append("\"crowd\":{");
                Num(sb, "live", crowd.LiveAgents); sb.Append(',');
                Num(sb, "walking", walking); sb.Append(',');
                Num(sb, "idle", idle); sb.Append(',');
                Num(sb, "marching", marching); sb.Append(',');
                Num(sb, "hidden", hidden); sb.Append(',');
                // A car that cut a corner ends up on grass, so this is the diagonal-driving bug
                // expressed as a number rather than as something to squint at.
                Num(sb, "yoldisi", crowd.CarsOffRoad());
                sb.Append("},");
            }

            // How the term is going, and how it ended if it has.
            Str(sb, "ending", g.Ending == Ending.None ? "" : Endings.Get(g.Ending).Title); sb.Append(',');
            Bool(sb, "isOver", g.IsOver); sb.Append(',');
            Num(sb, "noReturnCountdown", g.NoReturnCountdown); sb.Append(',');
            Bool(sb, "pulledBack", g.PulledBack); sb.Append(',');
            Num(sb, "competence", g.Competence); sb.Append(',');
            Num(sb, "consent", g.Consent); sb.Append(',');
            Num(sb, "lostDistricts", g.LostDistricts); sb.Append(',');
            Num(sb, "historyRows", g.History.Count); sb.Append(',');
            Num(sb, "taxRate", g.TaxRate); sb.Append(',');
            Num(sb, "fundingCost", g.FundingCost); sb.Append(',');
            Bool(sb, "charterPending", g.CharterPending); sb.Append(',');
            Num(sb, "orderCeiling", g.OrderCeiling); sb.Append(',');
            Num(sb, "orderFloor", g.OrderFloor); sb.Append(',');
            sb.Append("\"charter\":[");
            for (int i = 0; i < g.Charter.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(g.Charter[i].Id).Append('"');
            }
            sb.Append("],");

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
            Num(sb, "para", Reporting.Stock(Res.Para).Value); sb.Append(',');
            Num(sb, "malzeme", Reporting.Stock(Res.Malzeme).Value); sb.Append(',');
            Str(sb, "yiyecekTeshis", Reporting.Diagnosis("yiyecek")); sb.Append(',');

            // The loudest district as the governor is told it, which is what decides whether a
            // crisis ring appears on the map at all.
            float loudest = 0;
            foreach (var d in g.Districts)
            {
                float v = Reporting.Grievance(d.Id).Value;
                if (v > loudest) loudest = v;
            }
            Num(sb, "enYuksekHosnutsuzluk", loudest); sb.Append(',');
            Bool(sb, "yiyecekReliable", Reporting.Stock(Res.Yiyecek).Reliable); sb.Append(',');

            // The uncertainty system, as numbers. A range whose two ends are equal is the bug
            // this exists to catch, and it cannot be seen in a screenshot of a four-digit figure.
            var money = Reporting.Stock(Res.Para);
            Num(sb, "paraAlt", money.Ranged ? money.Value - money.Spread : money.Value); sb.Append(',');
            Num(sb, "paraUst", money.Ranged ? money.Value + money.Spread : money.Value); sb.Append(',');
            Num(sb, "paraYayilim", money.Spread); sb.Append(',');
            Bool(sb, "paraAralikli", money.Ranged);
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        static string Key(int r) => Naming.ResourceNames[r]
            .Replace("İ", "I").Replace("ş", "s").Replace("ç", "c").Replace("ü", "u").ToLowerInvariant();

        internal static void Num(StringBuilder sb, string key, float v)
            => sb.Append('"').Append(key).Append("\":")
                 .Append(v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

        internal static void Str(StringBuilder sb, string key, string v)
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

        /// <summary>Answer the event card on the desk by option index.</summary>
        public static bool Event(int option, out string message)
        {
            var state = GameState.Current;
            if (state == null || state.PendingEvent == null) { message = "bekleyen olay yok"; return false; }
            state.LastEventOutcome = Sim.OutsideWorld.Instance.Resolve(option);
            message = state.LastEventOutcome;
            return true;
        }

        /// <summary>Sign a credit line (on) or settle it (off).</summary>
        public static bool Loan(string id, bool borrow, out string message)
        {
            var def = Creditors.Get(id);
            if (def == null) { message = $"bilinmeyen alacaklı '{id}'"; return false; }
            if (Sim.OutsideWorld.Instance == null) { message = "outside world not ready"; return false; }
            return borrow
                ? Sim.OutsideWorld.Instance.Borrow(def, out message)
                : Sim.OutsideWorld.Instance.Settle(def, out message);
        }

        /// <summary>Set Mersa's threat directly. Test affordance only — no player can do this.</summary>
        public static bool SetThreat(int value, out string message)
        {
            var state = GameState.Current;
            if (state == null) { message = "state not bound"; return false; }
            state.Threat = Mathf.Clamp(value, 0, 100);
            message = $"tehdit {state.Threat:0}";
            return true;
        }

        /// <summary>
        /// Measure, don't judge. Three probes, one per visual claim that has burned us:
        /// `bina` — world bounds of the first 20 buildings plus the worst lift over all of them;
        /// `araba` — position, drawn forward, road direction and their dot for the first 20 cars,
        /// plus the worst dot over all of them; `yaya` — drawn lowest point of the first 20
        /// pedestrians plus the worst over all. The aggregates are the pass conditions; the
        /// samples are what a human reads when an aggregate fails.
        /// </summary>
        // The JSON helpers live on AgentState; these shims keep the probe code readable.
        static void Num(StringBuilder sb, string key, float v) => AgentState.Num(sb, key, v);
        static void Str(StringBuilder sb, string key, string v) => AgentState.Str(sb, key, v);

        public static string Probe(string id)
        {
            var sb = new StringBuilder(4096);
            sb.Append('{');

            switch (id)
            {
                case "bina":
                {
                    var cr = World.CityRenderer.Instance;
                    if (cr == null) return "{\"hata\":\"renderer yok\"}";

                    float worst = 0f; string worstId = "";
                    foreach (var p in cr.Probes)
                    {
                        float lift = p.Min.y - p.FloorY;
                        if (Mathf.Abs(lift) > Mathf.Abs(worst)) { worst = lift; worstId = p.Id; }
                    }
                    Num(sb, "toplam", cr.Probes.Count); sb.Append(',');
                    Num(sb, "enKotuMinY", worst); sb.Append(',');
                    Str(sb, "enKotuYapi", worstId); sb.Append(',');

                    sb.Append("\"binalar\":[");
                    int count = Mathf.Min(20, cr.Probes.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var p = cr.Probes[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('{');
                        Str(sb, "id", p.Id); sb.Append(',');
                        Num(sb, "minY", p.Min.y - p.FloorY); sb.Append(',');
                        Num(sb, "maxY", p.Max.y - p.FloorY); sb.Append(',');
                        sb.Append($"\"min\":[{p.Min.x:0.00},{p.Min.y:0.00},{p.Min.z:0.00}],");
                        sb.Append($"\"max\":[{p.Max.x:0.00},{p.Max.y:0.00},{p.Max.z:0.00}]");
                        sb.Append('}');
                    }
                    sb.Append(']');
                    break;
                }

                case "araba":
                {
                    var crowd = Object.FindFirstObjectByType<CrowdSystem>();
                    if (crowd == null) return "{\"hata\":\"kalabalık yok\"}";

                    var cars = new CrowdSystem.CarProbe[20];
                    int n = crowd.ProbeCars(cars, out float worstDot);
                    Num(sb, "enKotuDot", worstDot); sb.Append(',');
                    sb.Append("\"arabalar\":[");
                    for (int i = 0; i < n; i++)
                    {
                        var c = cars[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('{');
                        sb.Append($"\"poz\":[{c.Pos.x:0.00},{c.Pos.y:0.00},{c.Pos.z:0.00}],");
                        sb.Append($"\"ileri\":[{c.Forward.x:0.00},{c.Forward.z:0.00}],");
                        sb.Append($"\"yol\":[{c.RoadDir.x:0.00},{c.RoadDir.z:0.00}],");
                        Num(sb, "dot", c.Dot);
                        sb.Append('}');
                    }
                    sb.Append(']');
                    break;
                }

                case "yaya":
                {
                    var crowd = Object.FindFirstObjectByType<CrowdSystem>();
                    if (crowd == null) return "{\"hata\":\"kalabalık yok\"}";

                    var peds = new CrowdSystem.PedProbe[20];
                    int n = crowd.ProbePedestrians(peds, out float worstMinY);
                    Num(sb, "enKotuMinY", worstMinY); sb.Append(',');
                    sb.Append("\"yayalar\":[");
                    for (int i = 0; i < n; i++)
                    {
                        var p = peds[i];
                        if (i > 0) sb.Append(',');
                        sb.Append('{');
                        sb.Append($"\"poz\":[{p.Pos.x:0.00},{p.Pos.y:0.00},{p.Pos.z:0.00}],");
                        Num(sb, "minY", p.MinY); sb.Append(',');
                        Num(sb, "hiz", p.Speed);
                        sb.Append('}');
                    }
                    sb.Append(']');
                    break;
                }

                default:
                    return "{\"hata\":\"bilinmeyen probe: bina, araba veya yaya olmalı\"}";
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Walk the visual tree and report every visible element large enough to be the thing
        /// darkening the map. Staring at a screenshot cannot tell you which element is on top of
        /// the city; the layout tree can, and it knows its own bounds.
        /// </summary>
        public static string UiOverlay()
        {
            var hud = UI.Hud.Instance;
            if (hud == null || hud.Root == null) return "arayüz yok";

            var sb = new StringBuilder();
            float screen = Screen.width * (float)Screen.height;
            Walk(hud.Root, 0);
            return sb.Length == 0 ? "(ekranın %10'undan büyük görünür öğe yok)" : sb.ToString();

            void Walk(VisualElement e, int depth)
            {
                if (e.resolvedStyle.display == DisplayStyle.None) return;

                var r = e.worldBound;
                float area = r.width * r.height;
                var bg = e.resolvedStyle.backgroundColor;

                if (area > screen * 0.10f && bg.a > 0.01f)
                    sb.Append(new string(' ', depth * 2))
                      .Append(string.IsNullOrEmpty(e.name) ? e.GetType().Name : e.name)
                      .Append("  ").Append($"{r.x:0},{r.y:0} {r.width:0}x{r.height:0}")
                      .Append($"  alpha {bg.a:0.00}")
                      .Append('\n');

                foreach (var child in e.Children()) Walk(child, depth + 1);
            }
        }

        /// <summary>
        /// Hide or show the whole UI layer. Diagnostic: an artefact that survives both the
        /// shadows being off and the interface being gone is in the render, not on top of it.
        /// </summary>
        public static bool ShowUi(bool on, out string message)
        {
            var hud = UI.Hud.Instance;
            if (hud == null || hud.Root == null) { message = "arayüz yok"; return false; }
            hud.Root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            message = on ? "arayüz açık" : "arayüz gizli";
            return true;
        }

        /// <summary>
        /// Turn the sun's shadows on or off. Purely diagnostic: a dark artefact on the ground is
        /// either something the light is doing or something in the scene, and there is no way to
        /// tell those apart from a screenshot without removing one of them.
        /// </summary>
        public static bool Shadows(bool on, out string message)
        {
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun == null) { message = "ışık yok"; return false; }
            sun.shadows = on ? LightShadows.Soft : LightShadows.None;
            message = on ? "gölgeler açık" : "gölgeler kapalı";
            return true;
        }

        /// <summary>
        /// Top up the treasury and the construction depot. Test affordance: whether a building
        /// can be placed on a given parcel and whether the city can currently afford it are two
        /// different questions, and a scenario checking the first must not be answering the second.
        /// </summary>
        public static bool Grant(int value, out string message)
        {
            var state = GameState.Current;
            if (state == null) { message = "state not bound"; return false; }

            int amount = value > 0 ? value : 20000;
            state.Stock[(int)Res.Para] += amount;

            // Overfill the depot rather than raising its capacity: capacity is derived from the
            // workshops standing, so writing it here would be undone by the next tick anyway.
            var depot = state.MaterialChain.Final;
            depot.Stock += amount;

            message = $"hazine {state.Stock[(int)Res.Para]:0} · depo {depot.Stock:0}";
            return true;
        }

        /// <summary>
        /// Deal a named card. Test affordance: the draw is weighted and random by design, which
        /// is right for play and useless for asserting that a specific causal chain works.
        /// </summary>
        public static bool Card(string id, out string message)
        {
            message = "";
            var def = Events.Get(id);
            if (def == null) { message = $"bilinmeyen olay '{id}'"; return false; }

            var state = GameState.Current;
            if (state == null) { message = "state not bound"; return false; }

            state.PendingEvent = def;
            state.FiredEvents.Add(def.Id);
            return true;
        }

        /// <summary>Set the tax rate, 0..0.60, as a percentage.</summary>
        public static bool Tax(int percent, out string message)
        {
            var state = GameState.Current;
            if (state == null) { message = "state not bound"; return false; }
            state.TaxRate = Mathf.Clamp(percent / 100f, 0f, 0.60f);
            Sim.TurnResolver.Instance.Recompute();
            message = $"vergi %{state.TaxRate * 100:0}";
            return true;
        }

        /// <summary>Write a founding clause into the charter.</summary>
        public static bool Clause(string id, out string message)
        {
            var def = Constitution.Get(id);
            if (def == null) { message = $"bilinmeyen madde '{id}'"; return false; }
            if (Sim.GovernanceManager.Instance == null) { message = "governance not ready"; return false; }
            return Sim.GovernanceManager.Instance.AdoptClause(def, out message);
        }

        /// <summary>Issue a decree by id — the same two-a-turn allowance the player has.</summary>
        public static bool Decree(string id, out string message)
        {
            var def = Decrees.Get(id);
            if (def == null) { message = $"bilinmeyen kararname '{id}'"; return false; }
            if (Sim.GovernanceManager.Instance == null) { message = "governance not ready"; return false; }
            return Sim.GovernanceManager.Instance.Issue(def, out message);
        }

        /// <summary>Adopt a law (on) or repeal it (off).</summary>
        public static bool Law(string id, bool adopt, out string message)
        {
            var def = Laws.Get(id);
            if (def == null) { message = $"bilinmeyen kanun '{id}'"; return false; }
            if (Sim.GovernanceManager.Instance == null) { message = "governance not ready"; return false; }
            return adopt
                ? Sim.GovernanceManager.Instance.Adopt(def, out message)
                : Sim.GovernanceManager.Instance.Repeal(def, out message);
        }

        /// <summary>Answer a pending election: "yap", "ertele" or "hile".</summary>
        public static bool Election(string choice, out string message)
        {
            var state = GameState.Current;
            if (state == null || !state.ElectionPending) { message = "bekleyen seçim yok"; return false; }
            message = Sim.GovernanceManager.Instance.ResolveElection(choice);
            return true;
        }

        /// <summary>Adjourn the chamber, or call it back.</summary>
        public static bool Council(bool suspend, out string message)
        {
            message = "";
            var g = Sim.GovernanceManager.Instance;
            if (g == null) { message = "governance not ready"; return false; }
            if (suspend && !g.CanSuspend)
            { message = "meclis ancak otorite 75'e ulaşınca tatil edilebilir"; return false; }
            g.SetSuspended(suspend);
            return true;
        }

        /// <summary>
        /// Appoint a minister: id is the domain (maliye/tarim/guvenlik/imar/halk), loyalist
        /// picks SADIK over UZMAN. The agent takes the same choice the player does.
        /// </summary>
        public static bool Appoint(string domain, bool loyalist, out string message)
        {
            message = "";
            if (!System.Enum.TryParse(domain, true, out Domain d))
            {
                message = $"bilinmeyen bakanlık '{domain}'";
                return false;
            }
            if (Sim.MinisterManager.Instance == null) { message = "cabinet not ready"; return false; }

            var m = Sim.MinisterManager.Instance.Appoint(d, loyalist);
            Debug.Log($"[AgentInput] appoint {d} -> {m.Name} ({(loyalist ? "sadık" : "uzman")})");
            return true;
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









