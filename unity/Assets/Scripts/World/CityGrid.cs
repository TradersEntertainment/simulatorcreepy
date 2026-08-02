// The 48 × 32 tile grid the city sits on: terrain, districts, roads and what occupies a tile.
//
// Generated from a fixed seed so every run of the agent loop looks at the same delta. Nothing
// here is authored in the editor — CLAUDE.md forbids hand-written scenes, and a grid this
// regular is far easier to reason about as code anyway.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class CityGrid
    {
        public const int Width = 48;
        public const int Height = 32;
        /// <summary>One tile is 4 metres, per the design brief.</summary>
        public const float TileSize = 4f;

        public readonly TileKind[] Tiles = new TileKind[Width * Height];
        /// <summary>Index into GameState.Buildings, or −1 for an empty tile.</summary>
        public readonly int[] Occupant = new int[Width * Height];

        public static CityGrid Current;

        /// <summary>Deterministic little PRNG so the city is identical on every launch.</summary>
        int _seed;
        float Rnd()
        {
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            return _seed / (float)0x7fffffff;
        }
        int RndInt(int a, int b) => a + Mathf.FloorToInt(Rnd() * (b - a + 1));

        public static int Index(int x, int y) => y * Width + x;
        public static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TileKind At(int x, int y) => InBounds(x, y) ? Tiles[Index(x, y)] : TileKind.Su;

        /// <summary>Tile centre in world space. The grid is centred on the origin.</summary>
        public static Vector3 World(int x, int y, float height = 0f)
            => new Vector3((x - Width * 0.5f + 0.5f) * TileSize, height, (y - Height * 0.5f + 0.5f) * TileSize);

        /// <summary>World point back to a tile. Returns false when the point is off the map.</summary>
        public static bool Tile(Vector3 world, out Vector2Int tile)
        {
            int x = Mathf.FloorToInt(world.x / TileSize + Width * 0.5f);
            int y = Mathf.FloorToInt(world.z / TileSize + Height * 0.5f);
            tile = new Vector2Int(x, y);
            return InBounds(x, y);
        }

        public static bool Buildable(TileKind t) => t == TileKind.Cayir || t == TileKind.Verimli || t == TileKind.Tepelik;

        public bool IsFree(int x, int y)
            => InBounds(x, y) && Occupant[Index(x, y)] < 0 && Buildable(Tiles[Index(x, y)]);

        // ---------------------------------------------------------------- generation

        public static CityGrid Generate(int seed = 20260802)
        {
            var g = new CityGrid { _seed = seed };

            for (int i = 0; i < g.Occupant.Length; i++) g.Occupant[i] = -1;

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                TileKind t = TileKind.Cayir;

                // The river runs along the south edge, wandering a little so the docks have
                // a coastline rather than a ruler's edge.
                float bank = 2.4f + 1.7f * Mathf.Sin(x * 0.19f) + 0.9f * Mathf.Sin(x * 0.47f + 1.3f);
                if (y < bank) t = TileKind.Su;
                else if (y < bank + 3.2f) t = TileKind.Verimli;      // silt, where the farms belong

                // Ore in the north-east hills. The wedge deliberately reaches down into TEPE:
                // quarries have to be buildable inside a district or the material chain has
                // nowhere to start, and TEPE having the ore is its own political problem.
                if (x > 32 && y > 17 && (x - 32) + (y - 17) > 9) t = TileKind.Tepelik;

                // Marsh in the north-west, which must be drained before it takes a building.
                if (x < 11 && y > 24 && (11 - x) + (y - 24) > 6) t = TileKind.Bataklik;

                g.Tiles[Index(x, y)] = t;
            }

            g.CarveRoads();
            return g;
        }

        /// <summary>
        /// A plain arterial grid. Roads are terrain rather than buildings at this stage; the
        /// player can still lay more of them, and the flood-fill service check reads this.
        /// </summary>
        void CarveRoads()
        {
            int[] avenues = { 5, 12, 17, 23, 29, 35, 41 };   // north-south
            int[] streets = { 6, 12, 18, 24, 29 };           // east-west

            foreach (int x in avenues)
                for (int y = 0; y < Height; y++)
                    if (At(x, y) != TileKind.Su && At(x, y) != TileKind.Bataklik)
                        Tiles[Index(x, y)] = TileKind.Yol;

            foreach (int y in streets)
                for (int x = 0; x < Width; x++)
                    if (At(x, y) != TileKind.Su && At(x, y) != TileKind.Bataklik)
                        Tiles[Index(x, y)] = TileKind.Yol;
        }

        /// <summary>
        /// Seeds the founding settlement so turn one is a city and not an empty field. Returns
        /// the buildings it placed; the caller registers them with GameState.
        /// </summary>
        public void SeedStartingCity(GameState state)
        {
            bool PlaceExactly(string id, int x, int y)
            {
                var def = Buildings.Get(id);
                if (def == null || !IsFree(x, y)) return false;
                if (def.Requires.HasValue && At(x, y) != def.Requires.Value) return false;

                var district = Districts.At(x, y);
                if (district == null) return false;

                var b = new PlacedBuilding
                {
                    Def = def,
                    Tile = new Vector2Int(x, y),
                    District = district.Id,
                    BuiltOnTurn = 0,
                };
                Occupant[Index(x, y)] = state.Buildings.Count;
                state.Buildings.Add(b);
                return true;
            }

            // Spiral out from the hint until a legal tile turns up. Hard-coded coordinates are
            // brittle against a terrain tweak — an earlier version silently dropped the quarry
            // and both river farms because the river had moved a tile, and the city started
            // with no material chain at all and nothing said so.
            void Place(string id, int x, int y, int radius = 6)
            {
                if (PlaceExactly(id, x, y)) return;

                for (int r = 1; r <= radius; r++)
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // ring only
                    if (PlaceExactly(id, x + dx, y + dy)) return;
                }

                Debug.LogWarning($"[CityGrid] kurucu yapı yerleştirilemedi: {id} ({x},{y}) çevresinde yer yok");
            }

            // Housing scattered through every district, denser where the population starts high.
            foreach (var d in Districts.All)
            {
                int wanted = Mathf.Max(2, d.StartPopulation / 34);
                int placed = 0, guard = 0;
                while (placed < wanted && guard++ < 400)
                {
                    int x = RndInt(d.Bounds.xMin, d.Bounds.xMax - 1);
                    int y = RndInt(d.Bounds.yMin, d.Bounds.yMax - 1);
                    if (!IsFree(x, y)) continue;
                    Place("konut", x, y);
                    placed++;
                }
            }

            // The founding works: enough to run, not enough to relax. Both chains start whole —
            // the player's first real lesson is watching population growth outpace the mills.
            Place("tarla", 7, 4); Place("tarla", 8, 4); Place("tarla", 20, 4); Place("tarla", 21, 5);
            Place("degirmen", 14, 8); Place("degirmen", 15, 8);
            Place("firin", 20, 15); Place("firin", 21, 15);
            Place("ambar", 15, 4);
            Place("kuyu", 9, 8); Place("kuyu", 26, 16); Place("kuyu", 37, 16);
            Place("santral", 25, 8);
            Place("ocak", 42, 23);
            Place("islik", 37, 19);
            Place("depo", 30, 16);
            Place("dokuma", 27, 7);
            Place("tapinak", 24, 19);
            Place("kisla", 22, 27);
            Place("park", 36, 17);

            // Trees and scrub on whatever is left, purely for silhouette.
        }

        /// <summary>
        /// Road tiles inside a rectangle. This is a district's road capacity, and the reason
        /// laying a road is worth doing: a quarter that grew without streets jams.
        /// </summary>
        public int RoadTilesIn(RectInt bounds)
        {
            int n = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                if (At(x, y) == TileKind.Yol) n++;
            return n;
        }

        /// <summary>
        /// The road neighbours of a tile, written into <paramref name="into"/>. Four entries at
        /// most; returns how many. This is the whole road graph — no adjacency list to keep in
        /// sync, because the grid already is one.
        /// </summary>
        public int RoadNeighbours(int x, int y, Vector2Int[] into)
        {
            int n = 0;
            if (At(x + 1, y) == TileKind.Yol) into[n++] = new Vector2Int(x + 1, y);
            if (At(x - 1, y) == TileKind.Yol) into[n++] = new Vector2Int(x - 1, y);
            if (At(x, y + 1) == TileKind.Yol) into[n++] = new Vector2Int(x, y + 1);
            if (At(x, y - 1) == TileKind.Yol) into[n++] = new Vector2Int(x, y - 1);
            return n;
        }

        /// <summary>Every road tile on the map, for spawning traffic.</summary>
        public System.Collections.Generic.List<Vector2Int> AllRoadTiles()
        {
            var list = new System.Collections.Generic.List<Vector2Int>(512);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Tiles[Index(x, y)] == TileKind.Yol) list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>Deterministic 0..1 hash for a tile, for scatter that must not change per frame.</summary>
        public static float Hash(int x, int y, int salt = 0)
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}



