// The four failure mechanics, the point of no return, and how a term ends.
//
// Each extreme fails through its own mechanic rather than a lose screen, and each is legible in
// the city ten turns before it lands:
//
//   OTORİTE   information degrades until you govern a fiction; each purge buys compliance with
//             competence, and the army you built to save you ends you
//   ÖZGÜRLÜK  everything needs consent — building slows, then deadlocks, and districts pass to
//             local strongmen one at a time until you govern one
//   SERMAYE   land prices the workers out; LİMAN and SANAYİ empty and the works stand idle
//             while the treasury swells
//   EŞİTLİK   everyone is fed and nothing accumulates; the material chain decays and the
//             educated leave
//
// Bands scale it: RADİKAL runs the mechanic at partial strength, DÖNÜŞSÜZ at full, and entering
// DÖNÜŞSÜZ starts a visible eight-turn GERİ DÖNÜŞ countdown. Pulling back below 80 stops it and
// costs something real, which is the whole of the DAYANIKLI ending.

using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class CollapseWatcher : MonoBehaviour
    {
        public static CollapseWatcher Instance;
        public static System.Action Changed;

        GameState _state;

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
        }

        /// <summary>0 in PRAGMATİK/KARARLI, ramping to 1 across RADİKAL and DÖNÜŞSÜZ.</summary>
        static float Severity(int axis)
        {
            int magnitude = Mathf.Abs(axis);
            if (magnitude < 70) return 0f;
            return Mathf.Clamp01((magnitude - 70f) / 25f);
        }

        public void Tick()
        {
            var g = _state;
            if (g.Ending != Ending.None) return;

            PointOfNoReturn();
            Authority(Severity(g.AxisOrder) * (g.AxisOrder > 0 ? 1 : 0));
            Liberty(Severity(g.AxisOrder) * (g.AxisOrder < 0 ? 1 : 0));
            Capital(Severity(g.AxisEconomy) * (g.AxisEconomy > 0 ? 1 : 0));
            Equality(Severity(g.AxisEconomy) * (g.AxisEconomy < 0 ? 1 : 0));

            CheckEnding();
        }

        // ---------------------------------------------------------------- the countdown

        void PointOfNoReturn()
        {
            var g = _state;

            bool order = Mathf.Abs(g.AxisOrder) >= 90;
            bool economy = Mathf.Abs(g.AxisEconomy) >= 90;

            if (!order && !economy)
            {
                if (g.NoReturnCountdown > 0)
                {
                    // Pulling back is possible and it is meant to cost. The sacrifice is taken
                    // where it hurts a governor most: standing, and a faction's goodwill.
                    g.NoReturnCountdown = 0;
                    g.PulledBack = true;
                    g.Legitimacy = Mathf.Clamp(g.Legitimacy - 30, 0, 100);
                    g.Council.Murmurs.Insert(0,
                        "Meclis: geri adım atıldı. Bedeli ödendi ve bu tutanağa geçti.");
                }
                return;
            }

            if (g.NoReturnCountdown == 0)
            {
                g.NoReturnCountdown = 8;
                g.Council.Murmurs.Insert(0,
                    "Meclis başkanı: bu noktadan sonrası dönüşsüzdür. Sekiz tur içinde " +
                    "geri adım atılmazsa bu meclis bir daha toplanmayacak.");
            }
            else
            {
                g.NoReturnCountdown--;
            }
        }

        // ---------------------------------------------------------------- the four mechanics

        /// <summary>
        /// Each purge trades competence for compliance. It is permanent, it is never announced,
        /// and it is why the city stops working long before anybody names the problem.
        /// </summary>
        void Authority(float severity)
        {
            var g = _state;
            if (severity <= 0f) return;

            g.Egitim = Mathf.Clamp(g.Egitim - 1.5f * severity, 0, 100);
            g.Guvenlik = Mathf.Clamp(g.Guvenlik + 1.2f * severity, 0, 100);

            // Compliance costs output: the competent were replaced by the obedient.
            g.Competence = Mathf.Max(0.45f, g.Competence - 0.012f * severity);
        }

        /// <summary>
        /// Consent for everything. Building slows, then deadlocks, and one by one the districts
        /// stop remitting and pass to whoever is actually running that street.
        /// </summary>
        void Liberty(float severity)
        {
            var g = _state;
            if (severity <= 0f) return;

            g.Consent = Mathf.Min(2.6f, g.Consent + 0.05f * severity);

            // A district slips away roughly every four turns at full severity, worst first.
            g.SecessionPressure += severity * 0.28f;
            if (g.SecessionPressure < 1f) return;
            g.SecessionPressure = 0f;

            DistrictState worst = null;
            foreach (var d in g.Districts)
                if (!d.Lost && (worst == null || d.Grievance > worst.Grievance)) worst = d;
            if (worst == null) return;

            worst.Lost = true;
            g.Council.Murmurs.Insert(0,
                $"{worst.Name} artık valiliğe vergi göndermiyor. Mahalleyi kimin yönettiğini " +
                "orada herkes biliyor, burada kimse bilmiyor.");
            Changed?.Invoke();
        }

        /// <summary>
        /// Land value prices the workers out. The docks and the industrial quarter drain, the
        /// works stand unstaffed, and the treasury goes on filling.
        /// </summary>
        void Capital(float severity)
        {
            var g = _state;
            if (severity <= 0f) return;

            foreach (var d in g.Districts)
            {
                if (d.Lost || d.Def.Affinity != Faction.Isciler) continue;
                d.Population = Mathf.Max(15, d.Population - Mathf.RoundToInt(d.Population * 0.05f * severity) - 1);
            }
        }

        /// <summary>
        /// Everyone housed and fed, and nothing accumulates. Capital never forms, so the
        /// material chain wears out faster than it is replaced and the educated leave.
        /// </summary>
        void Equality(float severity)
        {
            var g = _state;
            if (severity <= 0f) return;

            g.Competence = Mathf.Max(0.45f, g.Competence - 0.008f * severity);
            g.Egitim = Mathf.Clamp(g.Egitim - 1.1f * severity, 0, 100);

            var depot = g.MaterialChain.Final;
            depot.Stock = Mathf.Max(0, depot.Stock - depot.Stock * 0.06f * severity);
        }

        // ---------------------------------------------------------------- endings

        void CheckEnding()
        {
            var g = _state;

            // Your own garrison, first: it is the fastest and the most deserved.
            if (g.CoupCountdown == 1) { Finish(Ending.Darbe); return; }
            if (g.Threat >= 100f && g.Garrison < 25f) { Finish(Ending.Isgal); return; }

            if (g.LawBook.Count > 0 && g.SealedSlots >= g.LawSlots) { Finish(Ending.BorcluSehir); return; }
            if (g.Population < 150) { Finish(Ending.TerkEdilmis); return; }

            if (g.Stock[(int)Res.Para] <= 0 && g.Flow[(int)Res.Para] < 0)
            {
                g.BankruptTurns++;
                if (g.BankruptTurns >= 4) { Finish(Ending.Iflas); return; }
            }
            else g.BankruptTurns = 0;

            int lost = 0;
            foreach (var d in g.Districts) if (d.Lost) lost++;
            if (lost >= g.Districts.Length - 1) { Finish(Ending.Anarsi); return; }

            // The point of no return runs out.
            if (g.NoReturnCountdown == 1)
            {
                if (g.AxisOrder >= 90) { Finish(Ending.Diktatorluk); return; }
                if (g.AxisOrder <= -90) { Finish(Ending.Anarsi); return; }
                if (g.AxisEconomy >= 90) { Finish(Ending.Plutokrasi); return; }
                if (g.AxisEconomy <= -90) { Finish(Ending.Kolektif); return; }
            }

            // Fifteen years. Whatever you have become, you face it.
            if (g.Turn >= GameState.FinalTurn)
            {
                bool clean = Mathf.Abs(g.AxisOrder) < 70 && Mathf.Abs(g.AxisEconomy) < 70 && lost == 0;
                Finish(g.PulledBack && clean ? Ending.Dayanikli
                     : clean ? Ending.Surdurulebilir
                     : g.AxisOrder >= 70 ? Ending.Diktatorluk
                     : g.AxisOrder <= -70 ? Ending.Anarsi
                     : g.AxisEconomy >= 70 ? Ending.Plutokrasi
                     : Ending.Kolektif);
            }
        }

        void Finish(Ending ending)
        {
            _state.Ending = ending;
            Debug.Log($"[Son] {Core.Endings.Get(ending).Title} · {_state.Turn}. tur");
            Changed?.Invoke();
        }
    }
}
