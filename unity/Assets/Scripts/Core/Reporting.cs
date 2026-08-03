// Reporting — the only door between the simulation and the player's eyes.
//
// Every UI read goes through here. Nothing in Assets/Scripts/UI is permitted to touch
// GameState directly; if you find yourself wanting to, add a method here instead. That single
// rule is what makes the information system real rather than cosmetic.
//
// The shape, from PROMPT-CITY §5:
//     displayed = true × (1 + bias)
//     bias = minister.style_bias × (1 − transparency) × authority_pressure
// and in co-op the same figures arrive from a human minister instead. The plumbing is
// deliberately indifferent to where the numbers came from.

using UnityEngine;

namespace Mesruiyet.Core
{
    public static class Reporting
    {
        static GameState G => GameState.Current;

        /// <summary>Which minister owns a resource line.</summary>
        public static Domain DomainOf(Res r) => Ministers.DomainOf(r);

        /// <summary>
        /// The single funnel every minister report passes through. In co-op this same value
        /// will arrive from a human minister's submitted report instead of from a formula, and
        /// nothing downstream needs to know the difference.
        /// </summary>
        public static float BiasFor(Domain domain)
            => Distortion.Bias(G.Cabinet?.Of(domain), G);

        /// <summary>
        /// Apply a minister's bias. <paramref name="flatteringSign"/> is +1 when a bigger number
        /// is the comfortable one — money, food, materials — and −1 when a smaller one is, as
        /// with grievance. A loyalist always leans towards comfortable.
        /// </summary>
        static Reported Distort(float trueValue, Domain domain, int flatteringSign = 1)
        {
            float bias = BiasFor(domain) * flatteringSign;
            if (Mathf.Abs(bias) < 0.0001f) return new Reported(trueValue);

            float shown = trueValue * (1f + bias);

            // Past a certain amount of noise the ministry stops pretending to a precise figure
            // and reports a range instead. The player can see they are being fogged; they just
            // cannot see which way.
            if (Mathf.Abs(bias) < Distortion.NoiseThreshold)
                return new Reported(shown, false);

            // Floored at a whole unit. A proportional spread on a small figure comes out under
            // half a unit, and both ends of the range then round to the same number — a range
            // that says nothing is worse than no range.
            float spread = Mathf.Max(Mathf.Abs(shown) * Mathf.Abs(bias) * 0.7f, 1f);
            return new Reported(shown, false, spread);
        }

        // ---------------------------------------------------------------- the ledger
        public static Reported Stock(Res r) => Distort(G.Stock[(int)r], DomainOf(r));
        public static Reported Flow(Res r) => Distort(G.Flow[(int)r], DomainOf(r));

        public static Reported Labour() => Distort(G.LabourUsed, Domain.Halk);
        public static Reported LabourPool() => Distort(G.LabourPool, Domain.Halk);

        // ---------------------------------------------------------------- supply chains
        //
        // Two reads, and the difference between them is the whole design. Stock(Yiyecek) is the
        // sum of every stage — grain, flour and bread added together — and it is what a minister
        // will happily quote. Product() is what the city can actually eat this turn. A city can
        // sit on a full granary and starve, and only one of these two numbers will say so.

        public static Chain ChainOf(string id) => G.GetChain(id);

        /// <summary>What the last stage holds: bread, dressed material.</summary>
        public static Reported Product(string chainId)
        {
            var chain = G.GetChain(chainId);
            return Distort(chain.Final.Stock, chain.Def.Domain);
        }

        public static Reported StageStock(Chain chain, int index)
            => Distort(chain.Stages[index].Stock, chain.Def.Domain);

        /// <summary>
        /// Where the chain is stuck, in words — or nothing at all, because a loyalist simply
        /// declines to mention it. This is the interlock the design cares about most: the total
        /// still reads fine, so the player spends their safety margin without knowing.
        /// </summary>
        public static string Diagnosis(string chainId)
        {
            var chain = G.GetChain(chainId);
            return Mathf.Abs(BiasFor(chain.Def.Domain)) < 0.0001f ? chain.Diagnosis() : "bildirilmedi";
        }

        /// <summary>The line-by-line breakdown behind a ledger figure, for its tooltip.</summary>
        public static System.Collections.Generic.List<string> Explain(Res r) => G.Ledger[(int)r];

        // ---------------------------------------------------------------- the city
        public static Reported Grievance(DistrictId id) => Distort(G.District(id).Grievance, Domain.Halk, -1);
        public static Reported Population() => Distort(G.Population, Domain.Halk);

        /// <summary>
        /// TAMPON — how many turns of shock the city can absorb, and the single most dangerous
        /// number on the screen.
        ///
        /// It is derived, not measured, so distorting it by multiplication is wrong: a city
        /// already at zero stays at zero however hard the ministry rounds, and the lie that
        /// matters most would be the one lie the system could not tell. What a minister actually
        /// does is quote the granary total, and the buffer the governor infers from that total
        /// is the buffer they plan around. So it is recomputed from *their* number.
        ///
        /// The result is the interlock the design is built on: the mills stop, the true margin
        /// falls to zero, and a loyalist Tarım still reports a comfortable nine turns — because
        /// the grain really is piling up. The player spends a margin they no longer have, and
        /// arrives at the next crisis with nothing but the fast option left.
        /// </summary>
        public static Reported Buffer()
        {
            float bias = BiasFor(Domain.Tarim);
            if (Mathf.Abs(bias) < 0.0001f) return new Reported(G.BufferTurns);

            float claimedFood = G.Stock[(int)Res.Yiyecek] * (1f + bias);
            float demand = G.Population * 0.12f;
            float claimed = demand <= 0.01f ? 9f : Mathf.Clamp(claimedFood / demand, 0f, 9f);

            // A minister rounds in your favour; they do not invent a crisis you do not have.
            float shown = Mathf.Max(G.BufferTurns, claimed);

            return Mathf.Abs(bias) < Distortion.NoiseThreshold
                ? new Reported(shown, false)
                : new Reported(shown, false, Mathf.Max(shown * Mathf.Abs(bias) * 0.5f, 1f));
        }

        // ---------------------------------------------------------------- politics
        // Axes and Meşruiyet are your own position, not a report. Nobody stands between you
        // and them, so they are never distorted.
        public static int AxisOrder => G.AxisOrder;
        public static int AxisEconomy => G.AxisEconomy;
        public static int Legitimacy => G.Legitimacy;

        /// <summary>
        /// Faction mood, always honest — you meet these people face to face. ORDU is the one
        /// exception: the army reaches you only through your Güvenlik minister, so a loyalist
        /// there reports "ordu memnun" until the morning of the coup.
        /// </summary>
        public static Mood FactionMood(Faction f)
        {
            float loyalty = G.FactionLoyalty[(int)f];
            if (f == Faction.Ordu) loyalty *= 1f + BiasFor(Domain.Guvenlik);

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
            if (f == Faction.Ordu) loyalty *= 1f + BiasFor(Domain.Guvenlik);
            return Mathf.Clamp01(loyalty / 100f);
        }

        public static bool FactionReliable(Faction f) => f != Faction.Ordu || Mathf.Abs(BiasFor(Domain.Guvenlik)) < 0.0001f;
    }
}


