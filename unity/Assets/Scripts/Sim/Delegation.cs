// Delegation — "şu bölge sende."
//
// A district handed to a minister builds itself: each turn the minister surveys their
// district with the same Advisor the placement hints use, picks the single most-needed
// building from their own domain's palette, pays for it from the same treasury and depot
// the player pays from, and reports what they did in their telegram. The governor stops
// being a mason and goes back to being a governor.
//
// The trap stays honest here too: a loyalist minister has a signature building they reach
// for first — the security man wants another karakol whether the district needs one or not.

using UnityEngine;
using Mesruiyet.Core;
using Mesruiyet.World;

namespace Mesruiyet.Sim
{
    public static class Delegation
    {
        /// <summary>What each desk knows how to build. A minister never leaves their lane.</summary>
        static readonly string[][] Palette =
        {
            /* Maliye   */ new[] { "pazar", "borsa" },
            /* Tarim    */ new[] { "tarla", "degirmen", "firin", "ambar" },
            /* Guvenlik */ new[] { "karakol", "kontrol" },
            /* Imar     */ new[] { "konut", "toplukonut", "yol", "kuyu", "santral", "depo" },
            /* Halk     */ new[] { "klinik", "okul", "hamam", "park", "kutuphane", "hastane" },
        };

        /// <summary>The loyalist's pet project, preferred whenever it is remotely defensible.</summary>
        static readonly string[] Signature = { "pazar", "ambar", "karakol", "toplukonut", "park" };

        // The cabinet never spends the city to the bone: a floor on the treasury and the
        // depot so an eager minister cannot leave the governor unable to answer a crisis.
        const float MoneyReserve = 150f;
        const float DepotReserve = 40f;

        /// <summary>A build only happens above this need; idle districts stay untouched.</summary>
        const float NeedFloor = 0.3f;

        /// <summary>Run every delegated district once. Called at the top of the turn tick.</summary>
        public static void Run(GameState g, Placement p, CityGrid grid)
        {
            g.DelegationNotes.Clear();
            if (p == null || grid == null) return;

            for (int i = 0; i < g.Delegation.Length; i++)
            {
                int domain = g.Delegation[i];
                if (domain < 0) continue;

                var district = g.Districts[i];
                if (district.Lost) continue;

                var minister = g.Cabinet.Of((Domain)domain);
                if (minister == null) continue;

                BuildingDef bestDef = null;
                Advisor.Suggestion bestSug = default;
                float bestScore = NeedFloor;

                // Money idling in the treasury is money the minister feels licensed to spend.
                float comfort = g.Stock[(int)Res.Para] > 600 && g.DepotStock > 120 ? 0.8f : 0f;

                foreach (string id in Palette[domain])
                {
                    var def = Buildings.Get(id);
                    if (def == null) continue;
                    if (g.Stock[(int)Res.Para] - p.MoneyCost(def) < MoneyReserve) continue;
                    if (def.Id != "yol" && g.DepotStock - p.MaterialCost(def) < DepotReserve) continue;
                    if (!Advisor.BestTile(def, g, grid, p, (DistrictId)i, out var sug)) continue;

                    float score = sug.Score + comfort;
                    // The loyalist reaches for their signature first; the expert weighs need.
                    if (minister.Loyalist && id == Signature[domain]) score += 1.2f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDef = def;
                        bestSug = sug;
                    }
                }

                if (bestDef == null) continue;

                int money = p.MoneyCost(bestDef);
                int material = p.MaterialCost(bestDef);
                if (!p.TryBuild(bestDef, bestSug.Tile.x, bestSug.Tile.y, quiet: true)) continue;

                g.DelegationNotes.Add(
                    $"{Ministers.DomainNames[domain]}|{minister.Name}|" +
                    $"{district.Name} bende: bu tur {bestDef.Name.ToLowerInvariant()} kurdum " +
                    $"({money} ₺ · {material} malzeme).");
            }
        }
    }
}
