// Reporting — the only door between the simulation and the player's eyes.
//
// Every UI read goes through here. Nothing in Assets/Scripts/UI is permitted to touch
// GameState directly; if you find yourself wanting to, add a method here instead. Slice one
// returns the truth for everything, because there are no ministers yet — but the seam is
// already load-bearing, and later slices only change what happens *inside* these methods.
//
// The eventual shape, from PROMPT-CITY §5:
//     displayed = true * (1 + bias)
//     bias = minister.style_bias * (1 - transparency) * authority_pressure
// and in co-op, the same dictionary arrives from a human minister instead. The plumbing is
// deliberately indifferent to where the numbers came from.

using UnityEngine;

namespace Mesruiyet.Core
{
    public static class Reporting
    {
        static GameState G => GameState.Current;

        /// <summary>Which domain owns a resource line, once ministers exist.</summary>
        public static string DomainOf(Res r)
        {
            switch (r)
            {
                case Res.Para: return "MALİYE";
                case Res.Yiyecek: return "TARIM";
                case Res.Malzeme: return "İMAR";
                case Res.Isgucu: return "HALK";
                default: return "İMAR";
            }
        }

        /// <summary>
        /// The single funnel every minister report will pass through. Today the bias is zero
        /// and the reliability flag is always true; the signature is what matters.
        /// </summary>
        public static float BiasFor(string domain)
        {
            // No ministers appointed yet — nobody is between you and the ledger.
            return 0f;
        }

        static Reported Distort(float trueValue, string domain)
        {
            float bias = BiasFor(domain);
            if (Mathf.Abs(bias) < 0.0001f) return new Reported(trueValue);

            float shown = trueValue * (1f + bias);
            // Above a noise threshold we stop pretending to know and show a range instead.
            float spread = Mathf.Abs(shown) * Mathf.Abs(bias) * 0.8f;
            return new Reported(shown, false, spread);
        }

        // ---------------------------------------------------------------- the ledger
        public static Reported Stock(Res r) => Distort(G.Stock[(int)r], DomainOf(r));
        public static Reported Flow(Res r) => Distort(G.Flow[(int)r], DomainOf(r));

        public static Reported Labour() => Distort(G.LabourUsed, "HALK");
        public static Reported LabourPool() => Distort(G.LabourPool, "HALK");

        // ---------------------------------------------------------------- the city
        public static Reported Grievance(DistrictId id) => Distort(G.District(id).Grievance, "HALK");
        public static Reported Population() => Distort(G.Population, "HALK");

        /// <summary>
        /// TAMPON — how many turns of shock the city can absorb. A lying Tarım minister makes
        /// this number a lie, and that is precisely the point: the buffer is destroyed silently.
        /// </summary>
        public static Reported Buffer() => Distort(G.BufferTurns, "TARIM");

        // ---------------------------------------------------------------- politics
        // Axes and Meşruiyet are your own position, not a report. Nobody stands between you
        // and them, so they are never distorted.
        public static int AxisOrder => G.AxisOrder;
        public static int AxisEconomy => G.AxisEconomy;
        public static int Legitimacy => G.Legitimacy;

        /// <summary>
        /// Faction mood, always honest — you meet these people face to face. ORDU is the one
        /// exception: it reaches you through the Güvenlik minister, so once ministers exist
        /// this call will run it through their distortion instead.
        /// </summary>
        public static Mood FactionMood(Faction f)
        {
            float loyalty = G.FactionLoyalty[(int)f];
            if (f == Faction.Ordu) loyalty *= 1f + BiasFor("GÜVENLİK");

            if (loyalty >= 65) return Mood.Hosnut;
            if (loyalty >= 45) return Mood.Temkinli;
            if (loyalty >= 30) return Mood.Kaygili;
            if (loyalty >= 15) return Mood.Ofkeli;
            return Mood.Dusman;
        }

        /// <summary>Only for the meter width — the number itself is never printed.</summary>
        public static float FactionBar(Faction f)
        {
            float loyalty = G.FactionLoyalty[(int)f];
            if (f == Faction.Ordu) loyalty *= 1f + BiasFor("GÜVENLİK");
            return Mathf.Clamp01(loyalty / 100f);
        }

        public static bool FactionReliable(Faction f) => f != Faction.Ordu || Mathf.Abs(BiasFor("GÜVENLİK")) < 0.0001f;
    }
}
