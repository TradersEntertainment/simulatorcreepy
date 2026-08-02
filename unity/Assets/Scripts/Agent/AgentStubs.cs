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
                Num(sb, "congestion", d.Congestion);
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
                Num(sb, "hidden", hidden);
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









