// The council: twenty-one seats, and the last honest channel the governor has left.
//
// This is the counterweight to the ministers, and the reason the information system is a fog
// rather than a blindfold. Ministers report on their domain and round in your favour; council
// members live in the districts and say what is happening on their own street. So the murmurs
// below read GameState directly — deliberately, and as the single exception to the rule that
// everything reaches the player through Reporting. A representative from LİMAN does not consult
// the Tarım ministry before telling you there is no bread.
//
// Which is exactly why "Meclisi Tatil Et" is the most expensive button in the game. It unlocks
// at axis_order ≥ 75, it makes every law free and instant, and it removes the only place the
// truth was still arriving. The tooltip states the trade plainly. It still gets pressed.

using System.Collections.Generic;
using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class Council
    {
        public const int Seats = 21;
        public const int Majority = 11;

        /// <summary>Seats per faction, index by <see cref="Faction"/>.</summary>
        public readonly int[] SeatsFor = new int[5];

        /// <summary>What the chamber is saying this turn. Always true, never distorted.</summary>
        public readonly List<string> Murmurs = new List<string>();

        /// <summary>Adjourned indefinitely. Laws pass free and instantly; nobody tells you anything.</summary>
        public bool Suspended;

        /// <summary>Council may only be suspended once you are this far towards OTORİTE.</summary>
        public const int SuspendThreshold = 75;

        // ---------------------------------------------------------------- composition

        /// <summary>
        /// Seats follow where people live and who they trust. A faction whose districts are
        /// populous and whose loyalty is high fills the chamber; one that has lost both is
        /// barely represented, and stops being able to block anything.
        /// </summary>
        public void Apportion(GameState g)
        {
            var weight = new float[5];
            float total = 0;

            foreach (var d in g.Districts)
            {
                int f = (int)d.Def.Affinity;
                float w = d.Population * (0.4f + g.FactionLoyalty[f] / 100f);
                weight[f] += w;
                total += w;
            }

            if (total <= 0.01f)
            {
                for (int i = 0; i < 5; i++) SeatsFor[i] = i == 0 ? Seats : 0;
                return;
            }

            // Largest remainder, so the seats always add to exactly twenty-one.
            int assigned = 0;
            var remainder = new float[5];
            for (int i = 0; i < 5; i++)
            {
                float exact = weight[i] / total * Seats;
                SeatsFor[i] = Mathf.FloorToInt(exact);
                remainder[i] = exact - SeatsFor[i];
                assigned += SeatsFor[i];
            }
            while (assigned < Seats)
            {
                int best = 0;
                for (int i = 1; i < 5; i++) if (remainder[i] > remainder[best]) best = i;
                SeatsFor[best]++;
                remainder[best] = -1f;
                assigned++;
            }
        }

        // ---------------------------------------------------------------- voting

        /// <summary>
        /// How a faction feels about a law: the axes it pushes, and what it does to their
        /// standing turn after turn. Simple, legible, and enough for the player to predict.
        /// </summary>
        public int SupportFor(LawDef law, GameState g)
        {
            if (Suspended) return Seats;

            int support = 0;
            for (int f = 0; f < 5; f++)
            {
                if (SeatsFor[f] == 0) continue;
                if (FactionApproves(law, (Faction)f, g)) support += SeatsFor[f];
            }
            return support;
        }

        public static bool FactionApproves(LawDef law, Faction f, GameState g)
        {
            int drift = DriftFor(law, f);
            float score = drift * 2f;

            // Each faction also reads the axes: merchants want capital, workers want equality,
            // the army and tradition want order, intellectuals want liberty.
            switch (f)
            {
                case Faction.Tuccarlar: score += law.Economy * 0.7f; break;
                case Faction.Isciler: score -= law.Economy * 0.7f; break;
                case Faction.Ordu: score += law.Order * 0.6f; break;
                case Faction.Gelenek: score += law.Order * 0.35f; break;
                case Faction.Aydinlar: score -= law.Order * 0.8f; break;
            }

            // A faction that already dislikes you votes against out of habit.
            score += (g.FactionLoyalty[(int)f] - 50f) * 0.06f;
            return score > 0f;
        }

        public static int DriftFor(LawDef law, Faction f)
        {
            switch (f)
            {
                case Faction.Tuccarlar: return law.TuccarPerTurn;
                case Faction.Isciler: return law.IsciPerTurn;
                case Faction.Ordu: return law.OrduPerTurn;
                case Faction.Gelenek: return law.GelenekPerTurn;
                default: return law.AydinPerTurn;
            }
        }

        /// <summary>Meşruiyet needed to force a law through a chamber that is short of a majority.</summary>
        public int ForceCost(LawDef law, GameState g)
        {
            int support = SupportFor(law, g);
            return support >= Majority ? 0 : (Majority - support) * 3;
        }

        // ---------------------------------------------------------------- murmurs

        /// <summary>
        /// What the chamber says this turn, read from the truth. Ordered worst first and capped,
        /// because a wall of complaints is as unreadable as silence.
        /// </summary>
        public void Hear(GameState g)
        {
            Murmurs.Clear();

            if (Suspended)
            {
                Murmurs.Add("Meclis tatilde. Kimse bir şey söylemiyor.");
                return;
            }

            var found = new List<(float weight, string line)>();

            // Hunger first, always. This is the complaint a loyalist Tarım minister is busy
            // not making, and hearing it here is the player's way back to reality.
            var food = g.FoodChain;
            float breadDemand = g.Population * 0.12f;
            if (food.Final.Stock < breadDemand * 0.95f)
            {
                var district = Poorest(g);
                float shortfall = breadDemand - food.Final.Stock;
                found.Add((100f, $"{district.Name} vekili: fırınlarda ekmek bitti. " +
                                 $"Bu tur {shortfall:0} kişilik eksik var, ambarda ne yazdığı umurumuzda değil."));
            }
            else if (food.Bottleneck() != null)
            {
                var b = food.Bottleneck();
                found.Add((70f, $"Ziraat komisyonu: {b.Name} zinciri tutuyor. " +
                                "Ambar dolu diye rahatlamayın, halk tahılı çiğ yiyemez."));
            }

            foreach (var d in g.Districts)
            {
                if (d.Grievance >= 70)
                    found.Add((d.Grievance, $"{d.Name} vekili: mahallede kimse hükümete inanmıyor. " +
                                            $"{d.AngryStreak + 1}. turdur böyle."));
                else if (d.Grievance >= 50)
                    found.Add((d.Grievance * 0.6f, $"{d.Name} vekili: sabır azalıyor, sebebi de belli."));
            }

            int idle = g.LabourPool - g.LabourUsed;
            if (g.LabourPool > 0 && idle > g.LabourPool * 0.28f)
                found.Add((45f, $"İşçi grubu: {idle} kişi boşta duruyor. İş yoksa öfke var."));

            var depot = g.MaterialChain;
            if (depot.Final.Stock < 40)
                found.Add((40f, $"İmar komisyonu: depoda {depot.Final.Stock:0} malzeme kaldı. " +
                                "İnşaat üç tura kalmaz durur."));

            if (g.Stock[(int)Res.Su] < g.Population * 0.2f)
                found.Add((60f, "Sıhhiye komisyonu: kuyular yetmiyor, su sırası uzuyor."));

            if (g.Legitimacy < 35)
                found.Add((55f, "Muhalefet: bu meclis size güvenoyu vermeye devam edemez."));

            // A quiet chamber is information too: it means nothing is visibly wrong yet.
            if (found.Count == 0)
            {
                Murmurs.Add("Meclis sakin. Bu tur şikâyet gelmedi.");
                return;
            }

            found.Sort((a, b) => b.weight.CompareTo(a.weight));
            for (int i = 0; i < found.Count && i < 4; i++) Murmurs.Add(found[i].line);
        }

        static DistrictState Poorest(GameState g)
        {
            var worst = g.Districts[0];
            foreach (var d in g.Districts)
                if (d.Def.Wealth < worst.Def.Wealth) worst = d;
            return worst;
        }
    }
}
