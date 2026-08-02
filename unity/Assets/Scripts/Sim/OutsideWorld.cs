// Mersa, the garrison, the creditors, and the event deck.
//
// Three pressures, one squeeze. The causal loop the design cares about most runs straight
// through this file and is worth stating in full, because every number below is chosen to make
// it reachable in an ordinary run:
//
//     Mersa'nın tehdidi ↑ → askere alma → tarlalarda işgücü ↓ → yiyecek açığı
//     → yiyecek krizi → hızlı çözüm: el koyma → axis_order ↑ → bilgi bozulur
//     → sonraki kriz körlemesine karşılanır
//
// Arming against an external threat is what makes you authoritarian, and nothing in the UI
// ever says so. The garrison large enough to save you is the garrison large enough to depose
// you, and whether you get any warning about that depends on a minister you appointed.

using System.Collections.Generic;
using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class OutsideWorld : MonoBehaviour
    {
        public static OutsideWorld Instance;
        public static System.Action Changed;

        GameState _state;
        int _seed = 90210;

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
            RecomputeGarrison();
        }

        float Rnd()
        {
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            return _seed / (float)0x7fffffff;
        }

        // ---------------------------------------------------------------- the tick

        public void Tick()
        {
            var g = _state;

            RaiseThreat();
            RecomputeGarrison();
            ChargeInterest();
            CoupClock();
            Telegraph();
            DrawEvent();
        }

        /// <summary>
        /// Mersa grows on its own schedule and on your weakness. A city with no garrison, no
        /// standing and an angry population is an opportunity, and Mersa reads the same numbers
        /// you do — except Mersa's are not rounded in your favour.
        /// </summary>
        void RaiseThreat()
        {
            var g = _state;
            if (g.Turn < 8) return;                       // the first years are quiet

            float rise = 1.1f;
            rise += Mathf.Max(0f, (40f - g.Garrison) / 40f) * 1.4f;
            rise += Mathf.Max(0f, (50f - g.Legitimacy) / 50f) * 1.2f;
            rise += g.AverageGrievance / 100f;

            g.Threat = Mathf.Clamp(g.Threat + rise, 0f, 100f);
        }

        /// <summary>
        /// Garrison strength from barracks and conscripts. Conscripts are people: they come out
        /// of the same İşgücü pool the farms and mills draw on, which is the whole point.
        /// </summary>
        public void RecomputeGarrison()
        {
            var g = _state;

            int barracks = 0;
            foreach (var b in g.Buildings)
                if (b.Def.Id == "kisla" && b.Staffed) barracks++;

            g.Garrison = barracks * 22f + g.Conscripts * 0.55f;
        }

        void ChargeInterest()
        {
            var g = _state;
            foreach (var loan in g.Loans)
            {
                float interest = loan.Def.Principal * loan.Rate;
                loan.Owed += interest;
                g.Stock[(int)Res.Para] -= interest;
            }
            if (g.Stock[(int)Res.Para] < 0) g.Stock[(int)Res.Para] = 0;
        }

        /// <summary>
        /// The other end of the garrison. An army that is large, unpaid and unhappy, serving a
        /// governor nobody believes in, starts counting days — and you are told about it only if
        /// the minister who speaks for the army happens to be telling you the truth.
        /// </summary>
        void CoupClock()
        {
            var g = _state;
            float ordu = g.FactionLoyalty[(int)Faction.Ordu];

            bool danger = ordu <= 25f && g.Legitimacy <= 35f && g.Garrison >= 20f;

            if (!danger)
            {
                if (g.CoupCountdown > 0)
                {
                    g.CoupCountdown = 0;
                    g.Council.Murmurs.Insert(0, "Ordu grubu: garnizonda huzursuzluk yatıştı.");
                }
                return;
            }

            g.CoupCountdown = g.CoupCountdown == 0 ? 5 : g.CoupCountdown - 1;

            // Announced only if your information is reliable. With a loyalist Güvenlik minister
            // it arrives with no warning at all — which is exactly the deal you made.
            if (Reporting.FactionReliable(Faction.Ordu))
                g.Council.Murmurs.Insert(0,
                    $"Ordu grubu: garnizonda toplantılar oluyor. {g.CoupCountdown} tur içinde " +
                    "bir şey olacak ve bunu size söyleyen ben oldum.");
        }

        /// <summary>
        /// Kadra's collapse is telegraphed five turns ahead, through the council rather than
        /// through a banner. A player with reliable information and a buffer can prepare; a
        /// player with a loyalist cabinet will not see it coming at all.
        /// </summary>
        void Telegraph()
        {
            var g = _state;
            int away = Events.KadraTurn - g.Turn;
            if (away <= 0 || away > 5) return;

            if (away == 5)
                g.Council.Murmurs.Add("Dışişleri: Kadra'dan gelen telgraflar kesildi.");
            else if (away == 3)
                g.Council.Murmurs.Add("Ticaret grubu: Kadra'nın tahıl alımları durdu. " +
                                      "Oradan gelen kervanlar dönmüyor.");
            else if (away == 1)
                g.Council.Murmurs.Add("Kapı zabiti: kuzey yolunda kalabalık görülüyor. " +
                                      "Ambarı ve suyu bugün sayın.");
        }

        // ---------------------------------------------------------------- events

        void DrawEvent()
        {
            var g = _state;
            if (g.PendingEvent != null) return;              // one at a time
            if (g.Turn < Events.FirstCrisisTurn) return;     // turns 1–10 teach, they do not test

            // Scheduled cards fire on their turn regardless of the pool.
            foreach (var def in Events.All)
            {
                if (def.ScheduledTurn != g.Turn) continue;
                if (g.FiredEvents.Contains(def.Id)) continue;
                Fire(def);
                return;
            }

            // 31–50 is where the compound crises live, so draw more often as the run goes on.
            float chance = g.Turn < 20 ? 0.45f : g.Turn < 31 ? 0.6f : 0.75f;
            if (Rnd() > chance) return;

            var pool = new List<EventDef>();
            float total = 0;
            foreach (var def in Events.All)
            {
                if (def.ScheduledTurn >= 0) continue;
                if (g.Turn < def.MinTurn || g.Turn > def.MaxTurn) continue;
                if (def.Once && g.FiredEvents.Contains(def.Id)) continue;
                if (g.Threat < def.MinThreat) continue;
                if (g.AverageGrievance < def.MinGrievance) continue;

                // Was declared and never read, so cards written to land during a shortage were
                // arriving in the middle of a surplus and reading as nonsense.
                if (def.NeedsFoodShortage && g.BufferTurns > 3f) continue;

                // A card that just fired is not news. Without this the deck reruns the same
                // three crises whenever their conditions stay true, which is exactly when the
                // conditions are most interesting.
                if (_recent.Contains(def.Id)) continue;

                pool.Add(def);
                total += def.Weight;
            }
            if (pool.Count == 0) return;

            float pick = Rnd() * total;
            foreach (var def in pool)
            {
                pick -= def.Weight;
                if (pick <= 0) { Fire(def); return; }
            }
            Fire(pool[pool.Count - 1]);
        }

        /// <summary>Ids drawn lately, oldest first. Keeps the deck from repeating itself.</summary>
        readonly List<string> _recent = new List<string>();
        const int RecentMemory = 8;

        void Fire(EventDef def)
        {
            _state.PendingEvent = def;
            _state.FiredEvents.Add(def.Id);

            _recent.Add(def.Id);
            if (_recent.Count > RecentMemory) _recent.RemoveAt(0);

            Changed?.Invoke();
        }

        /// <summary>Answer the card in front of you. Returns what happened, in the player's words.</summary>
        public string Resolve(int optionIndex)
        {
            var g = _state;
            var def = g.PendingEvent;
            if (def == null) return "bekleyen olay yok";

            optionIndex = Mathf.Clamp(optionIndex, 0, def.Options.Length - 1);
            var opt = def.Options[optionIndex];

            g.PendingEvent = null;

            g.Stock[(int)Res.Para] = Mathf.Max(0, g.Stock[(int)Res.Para] - opt.CostMoney);
            g.Legitimacy = Mathf.Clamp(g.Legitimacy - opt.CostLegitimacy, 0, 100);

            g.ShiftAxes(opt.Order, opt.Economy);
            g.ShiftFaction(Faction.Tuccarlar, opt.Tuccar);
            g.ShiftFaction(Faction.Isciler, opt.Isci);
            g.ShiftFaction(Faction.Ordu, opt.Ordu);
            g.ShiftFaction(Faction.Gelenek, opt.Gelenek);
            g.ShiftFaction(Faction.Aydinlar, opt.Aydin);

            if (opt.Grievance != 0)
                foreach (var d in g.Districts)
                    d.Grievance = Mathf.Clamp(d.Grievance + opt.Grievance, 0, 100);

            g.Threat = Mathf.Clamp(g.Threat + opt.Threat, 0, 100);
            if (opt.Garrison != 0) g.Conscripts += opt.Garrison;

            string outcome = ApplyEffect(opt);

            RecomputeGarrison();
            TurnResolver.Instance.Recompute();
            Changed?.Invoke();
            return string.IsNullOrEmpty(outcome) ? opt.Blurb : outcome;
        }

        string ApplyEffect(EventOption opt)
        {
            var g = _state;
            var food = g.FoodChain;

            switch (opt.Effect)
            {
                case EventEffect.Grain:
                case EventEffect.Requisition:
                    food.Stages[0].Stock = Mathf.Clamp(food.Stages[0].Stock + opt.Magnitude,
                                                       0, food.Stages[0].Capacity);
                    return null;

                case EventEffect.Refugees:
                case EventEffect.KadraCollapse:
                {
                    if (opt.Magnitude <= 0) return "Kol geri çevrildi.";
                    // Spread arrivals across the districts that will actually take them.
                    int each = Mathf.RoundToInt(opt.Magnitude / g.Districts.Length);
                    foreach (var d in g.Districts) d.Population += each;

                    if (opt.Effect == EventEffect.KadraCollapse)
                    {
                        // Three consequences at once: the column, Mersa's freed hand, and
                        // creditors repricing risk on a city that just got poorer.
                        foreach (var loan in g.Loans) loan.Rate *= 1.5f;
                        return $"{opt.Magnitude:0} kişi içeri alındı. Mersa bir komşusundan " +
                               "kurtuldu ve alacaklılar faizi yükseltti.";
                    }
                    return $"{opt.Magnitude:0} kişi şehre katıldı.";
                }

                case EventEffect.Conscript:
                    g.Conscripts += Mathf.RoundToInt(opt.Magnitude);
                    return $"{opt.Magnitude:0} kişi garnizona yazıldı. Tarlalarda o kadar eksildiler.";

                case EventEffect.Raid:
                {
                    // No war map: it resolves against the garrison, and losing costs people.
                    bool held = g.Garrison >= opt.Magnitude;
                    if (held)
                    {
                        g.ShiftFaction(Faction.Ordu, 4);
                        return "Garnizon sınırı tuttu.";
                    }
                    g.Conscripts = Mathf.Max(0, g.Conscripts - 20);
                    g.Legitimacy = Mathf.Clamp(g.Legitimacy - 8, 0, 100);
                    foreach (var d in g.Districts) d.Grievance = Mathf.Clamp(d.Grievance + 6, 0, 100);
                    return "Garnizon yetmedi. Sınırda kayıp verildi ve şehir bunu duydu.";
                }

                case EventEffect.Tribute:
                    return "Haraç ödendi. Elçi memnun ayrıldı; memnuniyeti bir yıl sürer.";

                case EventEffect.Plague:
                {
                    // Taken out of the districts rather than off a total, because the labour pool
                    // is rebuilt from district populations and the shortfall has to be felt there.
                    int each = Mathf.RoundToInt(opt.Magnitude / g.Districts.Length);
                    int lost = 0;
                    foreach (var d in g.Districts)
                    {
                        int take = Mathf.Min(each, Mathf.Max(0, d.Population - 10));
                        d.Population -= take;
                        lost += take;
                        d.Grievance = Mathf.Clamp(d.Grievance + 5, 0, 100);
                    }
                    return $"{lost} kişi kaybedildi. Mahalleler bunu tek tek saydı.";
                }

                case EventEffect.Material:
                {
                    var mat = g.MaterialChain;
                    var last = mat.Stages[mat.Stages.Length - 1];
                    float before = last.Stock;
                    last.Stock = Mathf.Clamp(last.Stock + opt.Magnitude, 0, last.Capacity);
                    float moved = last.Stock - before;
                    return moved >= 0
                        ? $"Depoya {moved:0} malzeme girdi."
                        : $"Depodan {-moved:0} malzeme çıktı. İnşaat bunu üç tur sonra hisseder.";
                }

                default:
                    if (opt.Magnitude > 0) g.Stock[(int)Res.Para] += opt.Magnitude;
                    return null;
            }
        }

        // ---------------------------------------------------------------- debt

        public bool CanBorrow(CreditorDef def, out string reason)
        {
            var g = _state;
            reason = "";

            foreach (var loan in g.Loans)
                if (loan.Def == def) { reason = "Bu alacaklıya zaten borçlusunuz."; return false; }

            var law = Laws.Get(def.DemandedLaw);
            if (law == null) { reason = "Talep edilen kanun bulunamadı."; return false; }

            if (g.LawBook.Contains(law)) return true;       // they will simply seal the one you have
            if (g.LawBook.Count >= g.LawSlots)
            {
                reason = $"Yasa yuvası dolu. {law.Name} için bir yuva açmanız gerekiyor — " +
                         "kredinin teminatı para değil, yuvanın kendisidir.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sign the credit line. The money lands today; the law slot is theirs until the debt is
        /// settled, and while it is theirs the law cannot be repealed by you, the council, or a
        /// lost election.
        /// </summary>
        public bool Borrow(CreditorDef def, out string message)
        {
            if (!CanBorrow(def, out message)) return false;

            var g = _state;
            var law = Laws.Get(def.DemandedLaw);

            if (!g.LawBook.Contains(law))
            {
                g.LawBook.Add(law);
                g.ShiftAxes(law.Order, law.Economy);
            }

            g.Loans.Add(new Loan { Def = def, Owed = def.Principal, Rate = def.InterestRate });
            g.Stock[(int)Res.Para] += def.Principal;
            g.ShiftAxes(0, 4);                              // foreign money pulls towards SERMAYE
            g.ShiftFaction(Faction.Tuccarlar, 4);
            g.ShiftFaction(Faction.Aydinlar, -3);

            GovernanceManager.Instance.Recompute();
            TurnResolver.Instance.Recompute();
            Changed?.Invoke();

            message = $"{def.Name} ile anlaşıldı. {def.Principal} ₺ hazineye girdi; " +
                      $"{law.Name} borç kapanana kadar yuvada mühürlü kalır.";
            return true;
        }

        public bool Settle(CreditorDef def, out string message)
        {
            var g = _state;
            message = "";

            Loan found = null;
            foreach (var loan in g.Loans)
                if (loan.Def == def) { found = loan; break; }

            if (found == null) { message = "Bu alacaklıya borcunuz yok."; return false; }
            if (g.Stock[(int)Res.Para] < found.Owed)
            {
                message = $"Borcu kapatmak {found.Owed:0} ₺ tutuyor.";
                return false;
            }

            g.Stock[(int)Res.Para] -= found.Owed;
            g.Loans.Remove(found);
            Changed?.Invoke();

            message = $"{def.Name} borcu kapandı. {Laws.Get(def.DemandedLaw).Name} artık " +
                      "kaldırılabilir.";
            return true;
        }

        /// <summary>True while a creditor holds this law. Repeal is refused for exactly this reason.</summary>
        public static bool IsSealed(GameState g, LawDef law)
        {
            foreach (var loan in g.Loans)
                if (loan.Def.DemandedLaw == law.Id) return true;
            return false;
        }
    }
}
