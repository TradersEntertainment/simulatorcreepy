// Decrees, laws and elections — the instruments, and what using them costs.
//
// Nothing here labels a choice. A decree is described by what it does and priced by what it
// costs; the axis it moves is shown as a number and never as a verdict. The drift the design
// is about happens because the fast option is cheap and you are in a hurry, not because anyone
// offered you a tyranny button.

using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class GovernanceManager : MonoBehaviour
    {
        public static GovernanceManager Instance;
        public static System.Action Changed;

        GameState _state;

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
            Recompute();
            state.Council.Apportion(state);
            state.Council.Hear(state);
        }

        /// <summary>Rebuild the law modifiers and the chamber. Cheap, and always exact.</summary>
        public void Recompute()
        {
            _state.Modifiers = LawModifiers.From(_state.LawBook, _state.Charter);
        }

        // ---------------------------------------------------------------- the tick

        public void Tick()
        {
            var g = _state;

            // Laws make enemies slowly, which is how a book you assembled for good reasons
            // turns into a coalition against you without a single dramatic moment.
            foreach (var law in g.LawBook)
            {
                g.ShiftFaction(Faction.Tuccarlar, law.TuccarPerTurn);
                g.ShiftFaction(Faction.Isciler, law.IsciPerTurn);
                g.ShiftFaction(Faction.Ordu, law.OrduPerTurn);
                g.ShiftFaction(Faction.Gelenek, law.GelenekPerTurn);
                g.ShiftFaction(Faction.Aydinlar, law.AydinPerTurn);
            }

            for (int i = g.Effects.Count - 1; i >= 0; i--)
            {
                g.Effects[i].TurnsLeft--;
                if (g.Effects[i].TurnsLeft <= 0) g.Effects.RemoveAt(i);
            }

            g.DecreesLeft = g.DecreeAllowance;

            Recompute();
            g.Council.Apportion(g);
            g.Council.Hear(g);

            SurfaceScandal();

            if (g.IsElectionTurn) g.ElectionPending = true;
            if (g.Turn == Constitution.Turn && g.Charter.Count == 0) g.CharterPending = true;
        }

        /// <summary>
        /// A rigged election is free at the time and expensive later — but only if anybody is
        /// left who can find out. Muzzle the press and the bill never arrives, which is the
        /// point: rigging feels free precisely because the only witness was already silenced.
        /// </summary>
        void SurfaceScandal()
        {
            var g = _state;
            if (g.ScandalTurn < 0 || g.Turn < g.ScandalTurn) return;

            g.ScandalTurn = -1;
            if (g.Transparency < 0.45f) return;      // nobody is looking

            g.Legitimacy = Mathf.Clamp(g.Legitimacy - 35, 0, 100);
            g.ShiftFaction(Faction.Aydinlar, -14);
            g.ShiftFaction(Faction.Isciler, -10);
            g.Council.Murmurs.Insert(0,
                "Muhalefet: sandık tutanakları ile ilan edilen sonuç birbirini tutmuyor. " +
                "Bu meclis açıklama bekliyor.");
        }

        // ---------------------------------------------------------------- decrees

        public bool CanIssue(DecreeDef d, out string reason)
        {
            reason = "";
            var g = _state;

            if (g.DecreesLeft <= 0) { reason = "Bu tur kararname hakkınız kalmadı."; return false; }
            if (g.Stock[(int)Res.Para] < d.CostMoney)
            { reason = $"Para yetmiyor: {d.CostMoney} ₺ gerek."; return false; }
            if (g.Legitimacy < d.CostLegitimacy)
            { reason = "Meşruiyet yetmiyor."; return false; }
            return true;
        }

        public bool Issue(DecreeDef d, out string message)
        {
            message = "";
            if (!CanIssue(d, out message)) return false;

            var g = _state;
            g.DecreesLeft--;
            g.Stock[(int)Res.Para] -= d.CostMoney;
            g.Legitimacy = Mathf.Clamp(g.Legitimacy - d.CostLegitimacy, 0, 100);

            g.ShiftAxes(d.Order, d.Economy);
            g.ShiftFaction(Faction.Tuccarlar, d.Tuccar);
            g.ShiftFaction(Faction.Isciler, d.Isci);
            g.ShiftFaction(Faction.Ordu, d.Ordu);
            g.ShiftFaction(Faction.Gelenek, d.Gelenek);
            g.ShiftFaction(Faction.Aydinlar, d.Aydin);

            if (d.Grievance != 0)
                foreach (var district in g.Districts)
                    district.Grievance = Mathf.Clamp(district.Grievance + d.Grievance, 0, 100);

            ApplyEffect(d);

            TurnResolver.Instance.Recompute();
            Changed?.Invoke();
            message = d.Name + " yürürlükte.";
            return true;
        }

        void ApplyEffect(DecreeDef d)
        {
            var g = _state;
            var food = g.FoodChain;

            switch (d.Effect)
            {
                case DecreeEffect.BuyGrain:
                case DecreeEffect.SeizeGrain:
                    food.Stages[0].Stock = Mathf.Min(food.Stages[0].Capacity,
                                                     food.Stages[0].Stock + d.Magnitude);
                    break;

                case DecreeEffect.ImportMaterial:
                {
                    var mat = g.MaterialChain;
                    mat.Final.Stock = Mathf.Min(mat.Final.Capacity, mat.Final.Stock + d.Magnitude);
                    break;
                }

                case DecreeEffect.None:
                    // A few decrees simply hand over money; Magnitude is the amount.
                    if (d.Magnitude > 0) g.Stock[(int)Res.Para] += d.Magnitude;
                    break;

                default:
                {
                    // Repeating an order refreshes it; it does not stack. Six standing press
                    // directives are not six times as effective as one, and letting them add up
                    // meant a player could drive transparency to zero in four turns by pressing
                    // the same button — which is a bug wearing the costume of a mechanic.
                    ActiveEffect existing = null;
                    foreach (var e in g.Effects)
                        if (e.Kind == d.Effect) { existing = e; break; }

                    if (existing != null)
                    {
                        existing.Magnitude = Mathf.Max(existing.Magnitude, d.Magnitude);
                        existing.TurnsLeft = Mathf.Max(existing.TurnsLeft, Mathf.Max(1, d.Duration));
                    }
                    else
                    {
                        g.Effects.Add(new ActiveEffect
                        {
                            Kind = d.Effect,
                            Magnitude = d.Magnitude,
                            TurnsLeft = Mathf.Max(1, d.Duration),
                            Name = d.Name,
                        });
                    }
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- laws

        public bool CanAdopt(LawDef law, out string reason)
        {
            reason = "";
            var g = _state;

            if (g.LawBook.Contains(law)) { reason = "Bu kanun zaten yürürlükte."; return false; }
            if (g.LawBook.Count >= g.LawSlots)
            { reason = $"Yasa yuvası dolu ({g.LawSlots}). Önce bir kanunu kaldırın."; return false; }

            if (g.AxisOrder < law.RequiresOrderAtLeast)
            { reason = "Meclis bu kanunu şu hâliyle gündeme almaz."; return false; }
            if (g.AxisOrder > law.RequiresOrderAtMost)
            { reason = "Meclis bu kanunu şu hâliyle gündeme almaz."; return false; }

            int cost = g.Council.ForceCost(law, g);
            if (cost > 0 && g.Legitimacy < cost)
            {
                int support = g.Council.SupportFor(law, g);
                reason = $"Mecliste {support}/{Council.Seats} oy var. " +
                         $"Zorlamak {cost} meşruiyete mal olur, o kadarı yok.";
                return false;
            }
            return true;
        }

        public bool Adopt(LawDef law, out string message)
        {
            message = "";
            if (!CanAdopt(law, out message)) return false;

            var g = _state;
            int support = g.Council.SupportFor(law, g);
            int cost = g.Council.ForceCost(law, g);

            g.Legitimacy = Mathf.Clamp(g.Legitimacy - cost, 0, 100);
            g.LawBook.Add(law);
            g.ShiftAxes(law.Order, law.Economy);

            Recompute();
            TurnResolver.Instance.Recompute();
            Changed?.Invoke();

            message = cost > 0
                ? $"{law.Name} {support}/{Council.Seats} oyla geçmedi; {cost} meşruiyetle yürürlüğe kondu."
                : $"{law.Name} {support}/{Council.Seats} oyla kabul edildi.";
            return true;
        }

        public bool Repeal(LawDef law, out string message)
        {
            var g = _state;

            // The rule that makes foreign debt matter. A creditor law is not yours to repeal:
            // not by decree, not by a council majority, not by losing an election over it.
            if (OutsideWorld.IsSealed(g, law))
            {
                message = $"{law.Name} bir alacaklının teminatı. Borç kapanmadan kaldırılamaz.";
                return false;
            }

            if (!g.LawBook.Remove(law)) { message = "Bu kanun yürürlükte değil."; return false; }

            g.ShiftAxes(-law.Order, -law.Economy);
            Recompute();
            TurnResolver.Instance.Recompute();
            Changed?.Invoke();
            message = $"{law.Name} kaldırıldı.";
            return true;
        }

        // ---------------------------------------------------------------- the council itself

        public bool CanSuspend => _state.AxisOrder >= Council.SuspendThreshold;

        public void SetSuspended(bool on)
        {
            var g = _state;
            if (on && !CanSuspend) return;

            g.Council.Suspended = on;
            if (on)
            {
                g.ShiftAxes(6, 0);
                g.ShiftFaction(Faction.Aydinlar, -12);
                g.ShiftFaction(Faction.Gelenek, -6);
            }
            g.Council.Hear(g);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- the charter

        /// <summary>
        /// Write a founding clause into the charter. Three of these, on turn five, and they are
        /// never revisited — they are walls, not laws.
        /// </summary>
        public bool AdoptClause(ClauseDef clause, out string message)
        {
            var g = _state;
            message = "";

            if (!g.CharterPending) { message = "Anayasa yazım süresi geçti."; return false; }
            if (g.Charter.Contains(clause)) { message = "Bu madde zaten yazıldı."; return false; }
            if (g.Charter.Count >= Constitution.Picks) { message = "Üç madde doldu."; return false; }

            g.AddClause(clause);
            if (g.Charter.Count >= Constitution.Picks) g.CharterPending = false;

            Recompute();
            TurnResolver.Instance.Recompute();
            Changed?.Invoke();
            message = $"{clause.Name} anayasaya yazıldı.";
            return true;
        }

        // ---------------------------------------------------------------- elections

        /// <summary>
        /// What the city actually thinks, out of 100. Computed from TRUE district grievance and
        /// nothing else — no minister touches this, which makes an election the one moment the
        /// player sees their real city. That is the entire reason it exists.
        /// </summary>
        public float TrueSupport()
        {
            var g = _state;
            float weighted = 0, population = 0;
            foreach (var d in g.Districts)
            {
                weighted += (100f - d.Grievance) * d.Population;
                population += d.Population;
            }
            float mood = population > 0 ? weighted / population : 50f;
            return Mathf.Clamp(mood * 0.7f + g.Legitimacy * 0.3f, 0, 100);
        }

        /// <summary>choice: "yap" · "ertele" · "hile".</summary>
        public string ResolveElection(string choice)
        {
            var g = _state;
            float support = TrueSupport();
            bool won = support >= 50f;

            var record = new ElectionRecord { Turn = g.Turn, TrueSupport = support, Choice = choice };
            string result;

            // Cleared first: losing can force a repeal, and a repeal raises Changed, and a HUD
            // that still saw a pending election would reopen the ballot mid-resolution.
            g.ElectionPending = false;

            switch (choice)
            {
                case "ertele":
                    // Cheap the first time and ruinous by the third, exactly as designed.
                    g.ShiftAxes(12, 0);
                    g.Legitimacy = Mathf.Clamp(g.Legitimacy - 15, 0, 100);
                    for (int f = 0; f < 5; f++) g.ShiftFaction((Faction)f, -8);
                    record.Won = false;
                    record.Note = "ertelendi";
                    result = "Seçim ertelendi. Kimse itiraz etmedi; herkes not aldı.";
                    break;

                case "hile":
                    // No cost today. The true result is recorded, and if anyone is still allowed
                    // to look, it surfaces within a few turns.
                    g.ShiftAxes(9, 0);
                    record.Won = true;
                    record.Note = $"hile · gerçek destek %{support:0}";
                    g.ScandalTurn = g.Turn + Random.Range(1, 5);
                    result = $"Sonuç ilan edildi: kazandınız. (Sandıkta çıkan: %{support:0})";
                    break;

                default:
                    record.Won = won;
                    if (won)
                    {
                        g.Legitimacy = Mathf.Clamp(g.Legitimacy + 10, 0, 100);
                        for (int f = 0; f < 5; f++) g.ShiftFaction((Faction)f, 4);
                        result = $"Seçimi %{support:0} ile kazandınız.";
                    }
                    else
                    {
                        g.Legitimacy = Mathf.Clamp(g.Legitimacy - 12, 0, 100);
                        // A heavy loss forces a concession. With nothing else to give, the
                        // council takes a law off the book.
                        // The concession can only touch a law that is actually yours. If every
                        // slot is collateral there is nothing left to give, and the council
                        // takes it out of your standing instead.
                        LawDef forced = null;
                        for (int i = g.LawBook.Count - 1; i >= 0; i--)
                            if (!OutsideWorld.IsSealed(g, g.LawBook[i])) { forced = g.LawBook[i]; break; }

                        if (forced != null)
                        {
                            Repeal(forced, out _);
                            record.Note = $"tâviz: {forced.Name} kaldırıldı";
                            result = $"Seçimi %{support:0} ile kaybettiniz. " +
                                     $"Meclis {forced.Name} kanununun kaldırılmasını şart koştu.";
                        }
                        else if (g.LawBook.Count > 0)
                        {
                            g.Legitimacy = Mathf.Clamp(g.Legitimacy - 8, 0, 100);
                            record.Note = "tâviz verilemedi: yasa kitabı alacaklıların";
                            result = $"Seçimi %{support:0} ile kaybettiniz. Meclis bir kanunun " +
                                     "kaldırılmasını istedi; kaldırabileceğiniz kanun kalmamış.";
                        }
                        else
                        {
                            g.ShiftFaction(Faction.Aydinlar, -6);
                            result = $"Seçimi %{support:0} ile kaybettiniz.";
                        }
                    }
                    break;
            }

            g.Elections.Add(record);
            g.Council.Apportion(g);
            g.Council.Hear(g);
            Changed?.Invoke();
            return result;
        }
    }
}



