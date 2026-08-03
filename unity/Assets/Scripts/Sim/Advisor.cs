// Advisor — where should this building go?
//
// One scorer, two customers. When the player arms a building, Placement asks for the top
// tiles and blinks them on the ground; when a district is delegated to a minister, the
// minister asks the same scorer what the district needs and builds it. The advice being
// shared is the point: the hints teach the player exactly the judgement the cabinet will
// use when the player stops placing things by hand.

using System.Collections.Generic;
using UnityEngine;
using Mesruiyet.Core;
using Mesruiyet.World;

namespace Mesruiyet.Sim
{
    public static class Advisor
    {
        public struct Suggestion
        {
            public Vector2Int Tile;
            public float Score;
        }

        /// <summary>What feeds what, so a mill wants to stand near grain and not near nothing.</summary>
        static string InputOf(string id)
        {
            switch (id)
            {
                case "degirmen":  return "tarla";
                case "firin":     return "degirmen";
                case "ambar":     return "firin";
                case "tayinlama": return "firin";
                case "islik":     return "ocak";
                case "depo":      return "islik";
                default:          return null;
            }
        }

        /// <summary>
        /// The best tiles for this building right now, highest score first. Only tiles that
        /// CanBuild accepts are considered, so a hint is never a lie about affordability or
        /// terrain. Pass a district to restrict the search — that is the delegation case.
        /// </summary>
        public static List<Suggestion> Recommend(BuildingDef def, GameState g, CityGrid grid,
                                                 Placement p, int max, DistrictId? only = null)
        {
            var best = new List<Suggestion>(max + 1);

            for (int y = 0; y < CityGrid.Height; y++)
            for (int x = 0; x < CityGrid.Width; x++)
            {
                var dd = Districts.At(x, y);
                if (dd == null) continue;
                if (only.HasValue && dd.Id != only.Value) continue;
                if (!p.CanBuild(def, x, y, out _)) continue;

                float s = Score(def, x, y, dd, g, grid);

                int i = best.Count;
                while (i > 0 && best[i - 1].Score < s) i--;
                if (i < max)
                {
                    best.Insert(i, new Suggestion { Tile = new Vector2Int(x, y), Score = s });
                    if (best.Count > max) best.RemoveAt(best.Count - 1);
                }
            }
            return best;
        }

        /// <summary>The single best placement, or null score −∞ when nothing qualifies.</summary>
        public static bool BestTile(BuildingDef def, GameState g, CityGrid grid, Placement p,
                                    DistrictId district, out Suggestion sug)
        {
            var list = Recommend(def, g, grid, p, 1, district);
            sug = list.Count > 0 ? list[0] : default;
            return list.Count > 0;
        }

        static float Score(BuildingDef def, int x, int y, DistrictDef dd, GameState g, CityGrid grid)
        {
            var ds = g.District(dd.Id);
            float s = 0;

            // Almost everything wants a street; the street itself obviously doesn't.
            bool nearRoad = grid.NextTo(x, y, TileKind.Yol);
            if (def.Id != "yol") s += nearRoad ? 2.2f : -0.6f;

            switch (def.Category)
            {
                case "konut":
                    // Housing pressure is the whole reason to build homes; industry next door
                    // is the reason people regret them.
                    s += Mathf.Clamp((ds.Population + 24 - ds.Housing) * 0.12f, -2f, 5f);
                    s -= 0.8f * CountNear(g, x, y, 1, b => b.Def.Category == "sanayi");
                    if (ds.Congestion < 1f) s += 0.5f;
                    break;

                case "tarim":
                {
                    string input = InputOf(def.Id);
                    if (input == null) s += 2f;                        // tarla: terrain filtered it
                    else
                    {
                        float d = NearestBuilding(g, input, x, y);
                        s += d == float.MaxValue ? -3f : Mathf.Max(0f, 3.5f - d * 0.25f);
                    }
                    break;
                }

                case "sanayi":
                {
                    string input = InputOf(def.Id);
                    if (input != null)
                    {
                        float d = NearestBuilding(g, input, x, y);
                        s += d == float.MaxValue ? -2f : Mathf.Max(0f, 3f - d * 0.22f);
                    }
                    // Markets live off people; quarries and workshops should not.
                    int homes = CountNear(g, x, y, 3, b => b.Def.Category == "konut");
                    if (def.Id == "pazar" || def.Id == "borsa" || def.Id == "dokuma")
                        s += Mathf.Min(2.5f, homes * 0.35f);
                    else
                        s -= 0.6f * CountNear(g, x, y, 1, b => b.Def.Category == "konut");
                    break;
                }

                case "altyapi":
                    if (def.Id == "yol")
                    {
                        // A road tile is worth laying where it extends the network toward
                        // buildings that have no street of their own.
                        if (!grid.NextTo(x, y, TileKind.Yol)) return -5f;
                        s += 1.2f * CountNear(g, x, y, 2,
                            b => !grid.NextTo(b.Tile.x, b.Tile.y, TileKind.Yol));
                    }
                    else if (def.Id == "santral")
                    {
                        s += 0.4f * CountNear(g, x, y, 4, b => b.Def.Category == "sanayi");
                        s -= 0.5f * CountNear(g, x, y, 1, b => b.Def.Category == "konut");
                    }
                    else
                    {
                        // Wells, aqueducts, treatment: serve the thickest cluster, and do not
                        // stand two of them on the same corner.
                        s += 0.25f * CountNear(g, x, y, 4, b => true);
                        if (NearestBuilding(g, def.Id, x, y) < 6f) s -= 2f;
                    }
                    break;

                case "kamu":
                    // Coverage first: a district that already has one needs the next one less.
                    if (HasInDistrict(g, def.Id, dd.Id)) s -= 2.5f;
                    s += ds.Grievance * 0.05f;
                    s += Mathf.Min(2f, 0.3f * CountNear(g, x, y, 4, b => b.Def.Category == "konut"));
                    if (def.Id == "matbaa" && dd.Id == DistrictId.Universite) s += 1f;
                    break;

                case "ordu":
                    if (dd.Id == DistrictId.Kisla) s += 2.5f;
                    float dk = NearestBuilding(g, "kisla", x, y);
                    if (dk != float.MaxValue) s += Mathf.Max(0f, 2f - dk * 0.2f);
                    break;
            }

            // A deterministic dither so equal tiles do not all point at the same corner.
            s += ((x * 73856093 ^ y * 19349663) & 63) / 63f * 0.3f;
            return s;
        }

        static float NearestBuilding(GameState g, string id, int x, int y)
        {
            float best = float.MaxValue;
            foreach (var b in g.Buildings)
            {
                if (b.Def.Id != id || g.District(b.District).Lost) continue;
                best = Mathf.Min(best, Mathf.Abs(b.Tile.x - x) + Mathf.Abs(b.Tile.y - y));
            }
            return best;
        }

        static int CountNear(GameState g, int x, int y, int radius, System.Predicate<PlacedBuilding> pick)
        {
            int n = 0;
            foreach (var b in g.Buildings)
            {
                if (Mathf.Abs(b.Tile.x - x) > radius || Mathf.Abs(b.Tile.y - y) > radius) continue;
                if (pick(b)) n++;
            }
            return n;
        }

        static bool HasInDistrict(GameState g, string id, DistrictId d)
        {
            foreach (var b in g.Buildings)
                if (b.Def.Id == id && b.District == d) return true;
            return false;
        }
    }
}
