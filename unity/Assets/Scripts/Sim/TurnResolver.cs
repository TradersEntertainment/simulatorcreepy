// TurnResolver — the tick. One season passes here and nothing else in the game advances time.
//
// Slice one resolves: staffing out of the shared İşgücü pool, production and consumption,
// population, district grievance weighted by local patience, the safety margin, and the axis
// trail. Supply chains, ministers, laws and events hang off the same tick in later slices, so
// the order of operations below is deliberate — labour first, because conscription will raid
// that same pool and starve the farms, which is the causal chain the whole game turns on.

using System.Collections;
using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class TurnResolver : MonoBehaviour
    {
        public static TurnResolver Instance;

        /// <summary>False while a turn is resolving. The agent bridge waits on this.</summary>
        public static bool Idle = true;

        /// <summary>Raised once a tick has fully resolved. The HUD repaints off this.</summary>
        public static System.Action TurnCompleted;

        GameState _state;

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
            Recompute();
        }

        public void BeginTurn()
        {
            if (!Idle) return;
            Idle = false;
            StartCoroutine(Resolve());
        }

        IEnumerator Resolve()
        {
            // A frame of breathing room so the HUD can show the button depressed and so the
            // agent's WaitUntil has something to wait on.
            yield return null;

            Tick();
            TurnCompleted?.Invoke();

            yield return null;
            Idle = true;
        }

        // ---------------------------------------------------------------- the tick

        void Tick()
        {
            var g = _state;
            g.Turn++;

            Staff();
            SurveyChains();
            TickChains();

            var flow = Produce();
            Consume(flow);
            ApplyFlow(flow);
            Grievance();
            Population();
            Indices();
            Factions();

            g.BufferTurns = ComputeBuffer();
            g.RecordTrail();

            // Governance before transparency: a law adopted this turn changes how clean the
            // numbers are, and the chamber has to hear the city as it stands now.
            GovernanceManager.Instance.Tick();

            // Transparency after that, so this turn's new printing press counts, and the
            // telegrams last, so ministers quote the figures the player is about to be shown.
            MinisterManager.Instance.Refresh();
            MinisterManager.Instance.RecordTerm();
            MinisterManager.Instance.WriteTelegrams();

            g.Legitimacy = Mathf.Clamp(
                g.Legitimacy + LegitimacyDelta(), 0, 100);
        }

        /// <summary>
        /// Hand out the shared İşgücü pool. Buildings that cannot be staffed stand there costing
        /// upkeep and producing nothing — the visible symptom of a labour shortage.
        /// </summary>
        void Staff()
        {
            var g = _state;

            int pool = 0;
            foreach (var b in g.Buildings)
            {
                float supplied = b.Def.Output[(int)Res.Isgucu];
                if (supplied > 0) pool += Mathf.RoundToInt(supplied);
            }
            // Nobody works who isn't housed, and the population caps what the housing offers.
            float participation = 0.68f + g.Modifiers.LabourParticipation;
            pool = Mathf.Min(pool, Mathf.RoundToInt(g.Population * Mathf.Clamp(participation, 0.3f, 0.9f)));
            // A mobilisation order puts more hands to work for a few turns; conscription
            // takes them away again. Both arrive here as the same signed number.
            pool += Mathf.RoundToInt(g.EffectMagnitude(DecreeEffect.LabourSurge));
            g.LabourPool = Mathf.Max(0, pool);

            int used = 0;
            foreach (var b in g.Buildings)
            {
                if (b.Disabled) { b.Staffed = false; continue; }        // strike, fire, sabotage
                if (b.Def.Workers <= 0) { b.Staffed = true; continue; }
                if (used + b.Def.Workers <= pool)
                {
                    b.Staffed = true;
                    used += b.Def.Workers;
                }
                else
                {
                    b.Staffed = false;
                }
            }
            g.LabourUsed = used;
        }

        float[] Produce()
        {
            var g = _state;
            var flow = new float[6];

            foreach (var b in g.Buildings)
            {
                if (!b.Staffed) { flow[(int)Res.Para] -= b.Def.Upkeep; continue; }

                for (int r = 0; r < 6; r++)
                {
                    if (r == (int)Res.Isgucu) continue;   // labour is a pool, not a stock
                    flow[r] += b.Def.Output[r];
                }
                flow[(int)Res.Para] -= b.Def.Upkeep;
            }

            // Tax. Wealthier districts yield more per head; the rate itself becomes a slider
            // when the budget panel lands.
            flow[(int)Res.Para] += Tax();

            return flow;
        }

        void SurveyChains()
        {
            foreach (var c in _state.Chains) c.Survey(_state);
        }

        void TickChains()
        {
            foreach (var c in _state.Chains) c.Tick();
        }

        /// <summary>
        /// The city eats bread, not "food". Grain sitting in a granary behind a stopped mill
        /// feeds nobody — but it still counts towards the Yiyecek line in the ledger, which is
        /// exactly the blind spot the design is built around.
        /// </summary>
        float _breadDemand, _breadServed;

        void Consume(float[] flow)
        {
            var g = _state;
            int pop = g.Population;

            _breadDemand = BreadDemand();
            _breadServed = g.FoodChain.Draw(_breadDemand);

            flow[(int)Res.Su] -= pop * 0.10f;
            flow[(int)Res.Enerji] -= pop * 0.045f;
        }

        void ApplyFlow(float[] flow)
        {
            var g = _state;

            for (int r = 0; r < 6; r++)
            {
                if (r == (int)Res.Isgucu) { g.Flow[r] = g.LabourPool - g.LabourUsed; continue; }
                if (r == (int)Res.Yiyecek || r == (int)Res.Malzeme) continue;   // chains own these

                g.Flow[r] = flow[r];
                g.Stock[r] = Mathf.Max(0, g.Stock[r] + flow[r]);
            }

            // Chain totals are written back into the ledger, so the headline figure is the sum
            // of every stage — the number that looks healthy while the middle is blocked.
            foreach (var c in g.Chains)
            {
                int r = (int)c.Def.Ledger;
                float before = g.Stock[r];
                g.Stock[r] = c.Total;
                g.Flow[r] = g.Stock[r] - before;
            }

            WriteLedgerNotes(flow);
        }

        /// <summary>
        /// Spell out where each line came from. §14: a player who cannot see why a number moved
        /// cannot learn the game, and this game is only worth playing if it can be learned.
        /// </summary>
        void WriteLedgerNotes(float[] flow)
        {
            var g = _state;
            foreach (var list in g.Ledger) list.Clear();

            int pop = g.Population;

            g.Ledger[(int)Res.Para].Add($"vergi · nüfus {pop} (+{Tax():0})");
            g.Ledger[(int)Res.Para].Add($"atölye ve borsa geliri (+{WorkshopIncome():0})");
            g.Ledger[(int)Res.Para].Add($"bakım giderleri (−{Upkeep():0})");

            var food = g.FoodChain;
            g.Ledger[(int)Res.Yiyecek].Add($"toplam = {string.Join(" + ", StageSummary(food))}");
            g.Ledger[(int)Res.Yiyecek].Add($"halk ekmek yer: −{_breadDemand:0} istendi, {_breadServed:0} verildi");
            g.Ledger[(int)Res.Yiyecek].Add($"zincir: {food.Diagnosis()}");

            var mat = g.MaterialChain;
            g.Ledger[(int)Res.Malzeme].Add($"toplam = {string.Join(" + ", StageSummary(mat))}");
            g.Ledger[(int)Res.Malzeme].Add($"inşaat yalnızca depodan çeker: {mat.Final.Stock:0}");
            g.Ledger[(int)Res.Malzeme].Add($"zincir: {mat.Diagnosis()}");

            g.Ledger[(int)Res.Su].Add($"kuyular (+{Produced(Res.Su):0})");
            g.Ledger[(int)Res.Su].Add($"nüfus {pop} (−{pop * 0.10f:0}), tarlalar (−{Consumed(Res.Su):0})");

            g.Ledger[(int)Res.Enerji].Add($"santraller (+{Produced(Res.Enerji):0})");
            g.Ledger[(int)Res.Enerji].Add($"nüfus {pop} (−{pop * 0.045f:0}), sanayi (−{Consumed(Res.Enerji):0})");

            g.Ledger[(int)Res.Isgucu].Add($"konutlardan {g.LabourPool} kişi");
            g.Ledger[(int)Res.Isgucu].Add($"çalışan {g.LabourUsed}, boşta {g.LabourPool - g.LabourUsed}");

            int unstaffed = 0;
            foreach (var b in g.Buildings) if (!b.Staffed) unstaffed++;
            if (unstaffed > 0)
                g.Ledger[(int)Res.Isgucu].Add($"{unstaffed} yapı işçisiz duruyor");
        }

        string[] StageSummary(Chain c)
        {
            var parts = new string[c.Stages.Length];
            for (int i = 0; i < c.Stages.Length; i++)
                parts[i] = $"{c.Stages[i].Def.Holds} {c.Stages[i].Stock:0}";
            return parts;
        }

        float Tax()
        {
            float t = 0;
            foreach (var d in _state.Districts)
                t += d.Population * 0.22f * (0.5f + d.Def.Wealth / 100f);
            return t * _state.Modifiers.Tax;
        }

        /// <summary>Rationing and the food laws both act here, on what the city asks for.</summary>
        float BreadDemand()
        {
            var g = _state;
            float rationing = 1f - Mathf.Clamp01(g.EffectMagnitude(DecreeEffect.Rationing));
            return g.Population * 0.12f * g.Modifiers.FoodDemand * rationing;
        }

        float WorkshopIncome()
        {
            float v = 0;
            foreach (var b in _state.Buildings)
                if (b.Staffed) v += Mathf.Max(0, b.Def.Output[(int)Res.Para]);
            return v;
        }

        float Upkeep()
        {
            float v = 0;
            foreach (var b in _state.Buildings) v += b.Def.Upkeep;
            return v;
        }

        float Produced(Res r)
        {
            float v = 0;
            foreach (var b in _state.Buildings)
                if (b.Staffed) v += Mathf.Max(0, b.Def.Output[(int)r]);
            return v;
        }

        float Consumed(Res r)
        {
            float v = 0;
            foreach (var b in _state.Buildings)
                if (b.Staffed) v += Mathf.Max(0, -b.Def.Output[(int)r]);
            return v;
        }

        /// <summary>
        /// Unmet needs raise grievance in the district that felt them, weighted by what that
        /// district is willing to put up with. TEPE tolerates less discomfort; LİMAN tolerates
        /// less injustice — that asymmetry is what makes placement political.
        /// </summary>
        void Grievance()
        {
            var g = _state;

            // Hunger is measured in bread handed out, not in the ledger's food total. A city
            // can be starving with a full granary, and the district feels the bread.
            float foodShare = _breadDemand <= 0.01f ? 1f : Mathf.Clamp01(_breadServed / _breadDemand);
            _ = foodShare;
            float waterShare = Satisfaction(Res.Su, g.Population * 0.10f);
            float energyShare = Satisfaction(Res.Enerji, g.Population * 0.045f);

            int jobless = Mathf.Max(0, g.LabourPool - g.LabourUsed);
            float joblessShare = g.LabourPool > 0 ? jobless / (float)g.LabourPool : 0f;

            foreach (var d in g.Districts)
            {
                // Grievance moves towards the level the district's conditions justify, rather
                // than accumulating a per-turn delta. Accumulation is the wrong model: a
                // permanent nuisance like a smokestack would ratchet a quarter to 100 and pin
                // it there, while a district with nothing wrong would sit at exactly 0. Both
                // are unreadable. A target the player can lower by fixing the cause is the
                // whole point of a grievance number.
                float intolerance = 1.4f - d.Def.Patience;

                float target = 8f;                                  // nobody is ever fully content
                target += (1f - foodShare) * 46f;
                target += (1f - waterShare) * 34f;
                target += (1f - energyShare) * 16f;

                int housingShort = Mathf.Max(0, d.Population - d.Housing);
                target += housingShort * 0.28f;

                // Unemployment stings hardest where work is the whole identity.
                float jobWeight = d.Def.Affinity == Faction.Isciler ? 26f : 12f;
                target += joblessShare * jobWeight;

                target += (LocalPollution(d) + g.Modifiers.Pollution) * 1.15f;
                // A curfew does not fix anything; it makes the street quiet for a few turns.
                target -= g.EffectMagnitude(DecreeEffect.CalmStreets);
                target -= LocalAmenity(d) * 0.55f;                  // parks, temples, clinics

                target = Mathf.Clamp(target * intolerance, 0f, 100f);

                // Trust is cheaper to lose than to earn back, so it climbs faster than it settles.
                // Gelenek districts forgive a little quicker; it is what they are for.
                float settle = (d.Def.Affinity == Faction.Gelenek ? 4.5f : 3.5f)
                             + g.Modifiers.GrievanceSettle;
                float step = target > d.Grievance ? 6f : settle;

                d.Grievance = Mathf.MoveTowards(d.Grievance, target, step);
                d.AngryStreak = d.Grievance > 70 ? d.AngryStreak + 1 : 0;
            }
        }

        /// <summary>Health and culture buildings standing in this district, staffed and working.</summary>
        float LocalAmenity(DistrictState d)
        {
            float a = 0;
            foreach (var b in _state.Buildings)
                if (b.District == d.Id && b.Staffed) a += b.Def.Health + b.Def.Culture;
            return a;
        }

        float Satisfaction(Res r, float demand)
        {
            if (demand <= 0.01f) return 1f;
            float available = _state.Stock[(int)r];
            return Mathf.Clamp01(available / demand);
        }

        float LocalPollution(DistrictState d)
        {
            float p = 0;
            foreach (var b in _state.Buildings)
                if (b.District == d.Id && b.Staffed) p += b.Def.Pollution;
            return p;
        }

        void Population()
        {
            var g = _state;

            foreach (var d in g.Districts)
            {
                d.Housing = 0;
                foreach (var b in g.Buildings)
                    if (b.District == d.Id) d.Housing += b.Def.Housing;

                bool fed = g.Stock[(int)Res.Yiyecek] > g.Population * 0.12f;
                int room = d.Housing - d.Population;

                // Slow on purpose: a 60-turn run should not double its population by turn ten,
                // or the whole safety-margin economy is swamped by growth it never chose.
                if (fed && room > 0 && d.Grievance < 60)
                    d.Population += Mathf.Clamp(Mathf.RoundToInt(room * 0.05f), 1, 7);
                else if (!fed || d.Grievance > 82)
                    d.Population = Mathf.Max(20, d.Population - Mathf.RoundToInt(d.Population * 0.04f) - 1);
            }
        }

        void Indices()
        {
            var g = _state;
            float health = 0, education = 0, security = 0, culture = 0, pollution = 0;

            foreach (var b in g.Buildings)
            {
                if (!b.Staffed) continue;
                health += b.Def.Health;
                education += b.Def.Education;
                security += b.Def.Security;
                culture += b.Def.Culture;
                pollution += b.Def.Pollution;
            }

            float perCapita = Mathf.Max(1f, g.Population / 100f);
            g.Saglik = Mathf.Clamp(30 + health / perCapita * 4f, 0, 100);
            g.Egitim = Mathf.Clamp(28 + education / perCapita * 4f, 0, 100);
            g.Guvenlik = Mathf.Clamp(34 + security / perCapita * 3.4f, 0, 100);
            g.Kultur = Mathf.Clamp(30 + culture / perCapita * 4f, 0, 100);
            g.Kirlilik = Mathf.Clamp(pollution / perCapita * 3.2f, 0, 100);
        }

        /// <summary>Loyalty follows the districts each faction speaks for, and drifts back to 50.</summary>
        void Factions()
        {
            var g = _state;

            for (int f = 0; f < g.FactionLoyalty.Length; f++)
            {
                float pressure = 0;
                int counted = 0;

                foreach (var d in g.Districts)
                {
                    if (d.Def.Affinity != (Faction)f) continue;
                    pressure += (45f - d.Grievance) * 0.06f;
                    counted++;
                }

                if (counted > 0) pressure /= counted;

                // Everything sags back towards indifference if you do nothing at all.
                float towardsMiddle = (50f - g.FactionLoyalty[f]) * 0.03f;
                g.FactionLoyalty[f] = Mathf.Clamp(g.FactionLoyalty[f] + pressure + towardsMiddle, 0, 100);
            }
        }

        /// <summary>
        /// TAMPON — how many turns of shock the city could absorb. Pillar 3, made a single number
        /// the player watches fall from 6 to 1 while they build.
        /// </summary>
        float ComputeBuffer()
        {
            var g = _state;
            float worst = 9f;

            // Bread on hand, not the food total. This is the single most important honesty in
            // the whole slice: the buffer must be computed from what people can actually eat,
            // so that when a minister later inflates the granary the buffer becomes a lie too.
            if (_breadDemand > 0.01f)
            {
                // Run the food chain forward rather than dividing a stock by a rate. A balanced
                // chain with empty bakeries still starves the city for the two turns it takes
                // grain to become bread, and a margin that cannot see that is worthless.
                float turns = g.FoodChain.TurnsUntilShortfall(_breadDemand);
                if (turns < worst) worst = turns;
            }

            Res[] pooled = { Res.Su, Res.Enerji, Res.Para };
            foreach (var r in pooled)
            {
                float flow = g.Flow[(int)r];
                if (flow >= -0.01f) continue;
                float turns = g.Stock[(int)r] / -flow;
                if (turns < worst) worst = turns;
            }

            return Mathf.Clamp(worst, 0f, 9f);
        }

        int LegitimacyDelta()
        {
            var g = _state;
            float avg = g.AverageGrievance;

            if (avg < 25) return 2;
            if (avg < 45) return 1;
            if (avg < 62) return 0;
            if (avg < 78) return -2;
            return -4;
        }

        /// <summary>Applied the moment a building lands, so the political cost is felt, not deferred.</summary>
        public void ApplyPlacement(PlacedBuilding b)
        {
            var g = _state;
            var e = b.Def.PoliticsIn(b.District);

            g.ShiftAxes(e.Order, e.Economy);
            g.ShiftFaction(Faction.Tuccarlar, e.Tuccar);
            g.ShiftFaction(Faction.Isciler, e.Isci);
            g.ShiftFaction(Faction.Ordu, e.Ordu);
            g.ShiftFaction(Faction.Gelenek, e.Gelenek);
            g.ShiftFaction(Faction.Aydinlar, e.Aydin);

            var d = g.District(b.District);
            d.Grievance = Mathf.Clamp(d.Grievance + e.Grievance, 0, 100);

            Recompute();
        }

        /// <summary>
        /// Refresh derived numbers without advancing time — after a build, say. Deliberately
        /// does not tick or draw from the chains: placing a mill must show its new throughput
        /// immediately without also feeding the city a free turn's worth of bread.
        /// </summary>
        public void Recompute()
        {
            var g = _state;
            int pop = g.Population;

            Staff();
            SurveyChains();

            var flow = Produce();
            flow[(int)Res.Su] -= pop * 0.10f;
            flow[(int)Res.Enerji] -= pop * 0.045f;

            for (int r = 0; r < 6; r++)
            {
                if (r == (int)Res.Isgucu) { g.Flow[r] = g.LabourPool - g.LabourUsed; continue; }
                if (r == (int)Res.Yiyecek || r == (int)Res.Malzeme) continue;
                g.Flow[r] = flow[r];
            }

            foreach (var c in g.Chains) g.Stock[(int)c.Def.Ledger] = c.Total;

            foreach (var d in g.Districts)
            {
                d.Housing = 0;
                foreach (var b in g.Buildings)
                    if (b.District == d.Id) d.Housing += b.Def.Housing;
            }

            _breadDemand = BreadDemand();
            g.BufferTurns = ComputeBuffer();
            WriteLedgerNotes(flow);

            if (GovernanceManager.Instance != null)
            {
                GovernanceManager.Instance.Recompute();
                _state.Council.Apportion(_state);
                _state.Council.Hear(_state);
            }

            if (MinisterManager.Instance != null)
            {
                MinisterManager.Instance.Refresh();
                MinisterManager.Instance.WriteTelegrams();
            }
        }
    }
}





