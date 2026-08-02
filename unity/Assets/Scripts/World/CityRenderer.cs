// CityRenderer turns the grid and the building list into geometry.
//
// Three meshes, three renderers: the ground never changes, the buildings mesh is rebuilt when
// something is placed, and the props mesh (trees, streetlights, scatter) is rebuilt with it.
// That is a few draw calls for the entire city, which is the point — the frame budget belongs
// to the simulation, not to the skyline.

using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.World
{
    public sealed class CityRenderer : MonoBehaviour
    {
        public static CityRenderer Instance;

        CityGrid _grid;
        GameState _state;
        Material _material;

        Mesh _groundMesh, _buildingMesh, _propMesh;
        MeshFilter _buildingFilter, _propFilter;

        readonly MeshBuilder _builder = new MeshBuilder();

        // Terrain palette, straight off the reference mockup.
        static readonly Color ColWater = Hex("#2F4F63");
        static readonly Color ColGrass = Hex("#6F8F5C");
        static readonly Color ColFertile = Hex("#87984F");
        static readonly Color ColHill = Hex("#6C6858");
        static readonly Color ColMarsh = Hex("#4B5A4A");
        static readonly Color ColRoad = Hex("#41454E");
        static readonly Color ColKerb = Hex("#4E535E");
        static readonly Color ColVoid = Hex("#0B0E13");
        static readonly Color ColLine = Hex("#C9CEDA");

        static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }

        public void Build(CityGrid grid, GameState state)
        {
            Instance = this;
            _grid = grid;
            _state = state;

            var shader = Resources.Load<Shader>("Shaders/CityLit");
            if (shader == null)
            {
                Debug.LogError("[CityRenderer] Shaders/CityLit bulunamadı — Resources klasörü eksik.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            _material = new Material(shader) { name = "CityLit (runtime)" };

            BuildGround();
            _buildingFilter = NewLayer("Buildings", out _buildingMesh);
            _propFilter = NewLayer("Props", out _propMesh);
            Rebuild();
        }

        MeshFilter NewLayer(string name, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            mesh = new Mesh { name = name };
            filter.sharedMesh = mesh;
            return filter;
        }

        // ---------------------------------------------------------------- ground

        void BuildGround()
        {
            _builder.Clear();

            // A dark apron under everything so the city reads as an object floating in space.
            // Darker than the background colour on purpose: this slab is lit like everything
            // else, so painting it #0B0E13 makes it come out *lighter* than the empty frame.
            float w = CityGrid.Width * CityGrid.TileSize;
            float h = CityGrid.Height * CityGrid.TileSize;
            _builder.AddTile(new Vector3(0, -0.6f, 0), Mathf.Max(w, h) * 2.2f, Hex("#04060A"));

            for (int y = 0; y < CityGrid.Height; y++)
            for (int x = 0; x < CityGrid.Width; x++)
            {
                var t = _grid.At(x, y);
                Color c = TerrainColour(t);

                // A little per-tile variation keeps the grass from reading as a bedsheet.
                float v = 0.93f + 0.14f * CityGrid.Hash(x, y, 7);
                c = MeshBuilder.Shade(c, v);

                float level = t == TileKind.Su ? -0.55f : 0f;
                Vector3 centre = CityGrid.World(x, y, level);
                _builder.AddTile(centre, CityGrid.TileSize, c);

                // Road tiles get a kerb lip and a centre dash, which is what makes a grid of
                // grey squares read as a street from three hundred metres up.
                if (t == TileKind.Yol)
                {
                    bool vertical = _grid.At(x, y - 1) == TileKind.Yol || _grid.At(x, y + 1) == TileKind.Yol;
                    bool horizontal = _grid.At(x - 1, y) == TileKind.Yol || _grid.At(x + 1, y) == TileKind.Yol;

                    if (vertical ^ horizontal)
                    {
                        Vector3 d = vertical ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
                        Vector3 s = vertical ? new Vector3(1, 0, 0) : new Vector3(0, 0, 1);
                        Vector3 mid = centre + Vector3.up * 0.02f;
                        float half = CityGrid.TileSize * 0.28f;
                        float wide = 0.16f;
                        _builder.AddQuad(
                            mid - d * half - s * wide,
                            mid + d * half - s * wide,
                            mid + d * half + s * wide,
                            mid - d * half + s * wide,
                            MeshBuilder.Shade(ColLine, 0.55f));
                    }
                }
                else if (t != TileKind.Su && HasRoadNeighbour(x, y))
                {
                    // Kerb where a plot meets the street.
                    _builder.AddTile(centre + Vector3.up * 0.01f, CityGrid.TileSize * 0.98f, MeshBuilder.Shade(c, 0.97f));
                }
            }

            var go = new GameObject("Ground");
            go.transform.SetParent(transform, false);
            _groundMesh = _builder.ToMesh("Ground");
            go.AddComponent<MeshFilter>().sharedMesh = _groundMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;

            // A flat collider so mouse picking has something to hit; the mesh itself is not
            // needed for physics and a plane is far cheaper to raycast.
            var plane = new GameObject("GroundCollider");
            plane.transform.SetParent(transform, false);
            var box = plane.AddComponent<BoxCollider>();
            box.size = new Vector3(w, 0.1f, h);
            box.center = Vector3.zero;
        }

        bool HasRoadNeighbour(int x, int y)
            => _grid.At(x - 1, y) == TileKind.Yol || _grid.At(x + 1, y) == TileKind.Yol
            || _grid.At(x, y - 1) == TileKind.Yol || _grid.At(x, y + 1) == TileKind.Yol;

        static Color TerrainColour(TileKind t)
        {
            switch (t)
            {
                case TileKind.Su: return ColWater;
                case TileKind.Verimli: return ColFertile;
                case TileKind.Tepelik: return ColHill;
                case TileKind.Bataklik: return ColMarsh;
                case TileKind.Yol: return ColRoad;
                default: return ColGrass;
            }
        }

        // ---------------------------------------------------------------- buildings

        /// <summary>Re-bake the building and prop meshes. Called once at start and on every placement.</summary>
        public void Rebuild()
        {
            BuildBuildings();
            BuildProps();
        }

        void BuildBuildings()
        {
            _builder.Clear();

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                var b = _state.Buildings[i];
                var def = b.Def;
                var district = Districts.Get(b.District);

                Vector3 ground = CityGrid.World(b.Tile.x, b.Tile.y);
                float hash = CityGrid.Hash(b.Tile.x, b.Tile.y, 11);
                float hash2 = CityGrid.Hash(b.Tile.x, b.Tile.y, 23);

                if (def.Storeys <= 0)
                {
                    // Flat works — roads, fields, parks. A thin slab reads better than nothing.
                    Color flat = def.Id == "tarla"
                        ? MeshBuilder.Shade(def.Tint, 0.92f + 0.16f * hash)
                        : def.Tint;
                    _builder.AddBox(ground, new Vector3(CityGrid.TileSize * 0.92f, 0.14f, CityGrid.TileSize * 0.92f), flat);

                    if (def.Id == "tarla") AddFurrows(ground, hash);
                    if (def.Id == "park") AddTree(ground + new Vector3((hash - 0.5f) * 2f, 0.14f, (hash2 - 0.5f) * 2f), 1f);
                    continue;
                }

                // Housing borrows its district's palette, so each quarter reads differently from
                // the air. Civic buildings keep their own colour — they are meant to stand out.
                bool residential = def.Category == "konut";
                Color tint = residential
                    ? district.Palette[Mathf.FloorToInt(hash * district.Palette.Length) % district.Palette.Length]
                    : def.Tint;

                int storeys = def.Storeys;
                if (residential)
                    storeys = Mathf.Clamp(district.Storeys.x + Mathf.FloorToInt(hash2 * (district.Storeys.y - district.Storeys.x + 1)),
                                          1, 6);

                float footprint = CityGrid.TileSize * (0.56f + 0.2f * hash);
                float depth = CityGrid.TileSize * (0.56f + 0.2f * hash2);
                float height = storeys * 3.1f;

                if (!b.Staffed) tint = Color.Lerp(tint, ColVoid, 0.45f);

                _builder.AddBox(ground, new Vector3(footprint, height, depth), tint);
                AddWindows(ground, footprint, depth, height, storeys, b.Tile);

                // A rooftop box on the taller blocks so the skyline is not a row of dominoes.
                if (storeys >= 2 && hash > 0.4f)
                {
                    _builder.AddBox(
                        ground + new Vector3((hash - 0.5f) * footprint * 0.3f, height, (hash2 - 0.5f) * depth * 0.3f),
                        new Vector3(footprint * 0.34f, 0.9f, depth * 0.34f),
                        MeshBuilder.Shade(tint, 0.7f));
                }

                if (def.Id == "anit")
                    _builder.AddPyramid(ground + Vector3.up * height, footprint * 0.5f, 4.2f, MeshBuilder.Shade(tint, 1.1f));
                if (def.Id == "tapinak")
                    _builder.AddPyramid(ground + Vector3.up * height, footprint * 0.62f, 3.4f, Hex("#B5654A"));
                if (def.Id == "santral" || def.Id == "dokuma")
                    _builder.AddBox(ground + new Vector3(footprint * 0.32f, 0, -depth * 0.3f),
                                    new Vector3(1.1f, height + 5.5f, 1.1f), MeshBuilder.Shade(tint, 0.62f));
            }

            _builder.Into(_buildingMesh);
        }

        void AddFurrows(Vector3 ground, float hash)
        {
            Color furrow = Hex("#6C7C42");
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 0.5f) / 4f - 0.5f;
                _builder.AddBox(
                    ground + new Vector3(t * CityGrid.TileSize * 0.86f, 0.14f, 0),
                    new Vector3(0.35f, 0.12f, CityGrid.TileSize * 0.86f),
                    MeshBuilder.Shade(furrow, 0.94f + 0.1f * hash));
            }
        }

        void AddWindows(Vector3 ground, float w, float d, float height, int storeys, Vector2Int tile)
        {
            Color warm = Hex("#FFD79A");
            Color cold = Hex("#8FA6C0");

            for (int floor = 0; floor < storeys; floor++)
            {
                float y = ground.y + (floor + 0.55f) / storeys * height;
                for (int col = 0; col < 3; col++)
                {
                    float u = (col + 0.5f) / 3f - 0.5f;
                    int salt = tile.x * 31 + tile.y * 17 + floor * 7 + col;
                    if (CityGrid.Hash(tile.x, tile.y, salt) < 0.24f) continue;

                    bool lit = CityGrid.Hash(tile.x, tile.y, salt + 5000) < 0.55f;
                    Color glow = lit ? warm : MeshBuilder.Shade(cold, 0.45f);

                    _builder.AddWindow(new Vector3(ground.x + u * w, y, ground.z + d * 0.5f),
                                       Vector3.forward, w * 0.2f, 1.05f, glow);
                    _builder.AddWindow(new Vector3(ground.x + w * 0.5f, y, ground.z + u * d),
                                       Vector3.right, d * 0.2f, 1.05f, MeshBuilder.Shade(glow, 0.85f));
                }
            }
        }

        // ---------------------------------------------------------------- props

        void BuildProps()
        {
            _builder.Clear();

            for (int y = 0; y < CityGrid.Height; y++)
            for (int x = 0; x < CityGrid.Width; x++)
            {
                if (_grid.Occupant[CityGrid.Index(x, y)] >= 0) continue;
                var t = _grid.At(x, y);
                float r = CityGrid.Hash(x, y, 3);

                if (t == TileKind.Cayir || t == TileKind.Verimli)
                {
                    if (r < 0.22f)
                        AddTree(CityGrid.World(x, y) + new Vector3((r - 0.11f) * 6f, 0, (CityGrid.Hash(x, y, 4) - 0.5f) * 3f),
                                0.85f + r);
                }
                else if (t == TileKind.Bataklik && r < 0.3f)
                {
                    _builder.AddBox(CityGrid.World(x, y), new Vector3(1.4f, 0.5f, 1.4f), Hex("#3E4C3C"));
                }
                else if (t == TileKind.Tepelik && r < 0.35f)
                {
                    _builder.AddPyramid(CityGrid.World(x, y), 1.5f + r, 2f + r * 2.5f, Hex("#5E5A4E"));
                }
                else if (t == TileKind.Yol && r < 0.13f)
                {
                    // Streetlight: a post and a lit head.
                    Vector3 p = CityGrid.World(x, y) + new Vector3(1.5f, 0, 1.5f);
                    _builder.AddBox(p, new Vector3(0.22f, 4.2f, 0.22f), Hex("#2C3138"));
                    _builder.AddBox(p + Vector3.up * 4.2f, new Vector3(0.7f, 0.28f, 0.7f),
                                    new Color(1f, 0.84f, 0.6f, 0f));
                }
            }

            _builder.Into(_propMesh);
        }

        void AddTree(Vector3 at, float scale)
        {
            _builder.AddBox(at, new Vector3(0.4f * scale, 1.6f * scale, 0.4f * scale), Hex("#5A4433"));
            _builder.AddPyramid(at + Vector3.up * 1.4f * scale, 1.25f * scale, 3.1f * scale,
                                MeshBuilder.Shade(Hex("#4E7A44"), 0.9f + 0.25f * scale));
        }
    }
}


