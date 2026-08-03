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

        // Terrain palette, straight off the reference mockup — which is noticeably more
        // saturated than the first pass was. Washed olive read as fog, not as grass.
        static readonly Color ColWater = Hex("#3E6E82");
        static readonly Color ColGrass = Hex("#69A054");
        static readonly Color ColFertile = Hex("#8FA648");
        static readonly Color ColHill = Hex("#6C6858");
        static readonly Color ColMarsh = Hex("#4B5A4A");
        static readonly Color ColRoad = Hex("#41454E");
        static readonly Color ColKerb = Hex("#4E535E");
        static readonly Color ColVoid = Hex("#0B0E13");
        static readonly Color ColLine = Hex("#C9CEDA");
        /// <summary>Where an unworked building's colours are dragged towards: cold, not absent.</summary>
        static readonly Color ColShuttered = Hex("#2A2F38");

        static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }

        /// <summary>The shared CityLit material, so the crowd can tint copies of it.</summary>
        public Material Material => _material;

        /// <summary>Axis values the props were last baked at, so we only rebake when it shows.</summary>
        int _bakedOrder, _bakedEconomy, _bakedSeason = -1;

        public void Build(CityGrid grid, GameState state)
        {
            Instance = this;
            _grid = grid;
            _state = state;

            // Load the shipped material rather than building one from the shader: the asset is
            // what keeps the GPU instancing variant from being stripped out of the build, and
            // the crowd draws through instancing.
            var template = Resources.Load<Material>("CityLit");
            if (template == null)
            {
                Debug.LogError("[CityRenderer] Resources/CityLit.mat bulunamadı — " +
                               "Editor/ProjectSetup.Ensure çalıştırılmamış.");
                var shader = Resources.Load<Shader>("Shaders/CityLit")
                             ?? Shader.Find("Universal Render Pipeline/Lit");
                _material = new Material(shader) { name = "CityLit (fallback)" };
            }
            else
            {
                _material = new Material(template) { name = "CityLit (runtime)" };
            }
            _material.enableInstancing = true;

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

        GameObject _groundObject;

        /// <summary>Re-bake the terrain. Called when a road is laid and the graph grows.</summary>
        public void RebuildGround()
        {
            if (_groundObject != null) Destroy(_groundObject);
            BuildGround();
        }

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

                // "Roads repave as land value rises", §11. Land value is not a stat in this game
                // and inventing one would be a lie; what a quarter is actually worth shows in
                // what stands there, so paving is its founding wealth plus what has been built
                // since. A poor quarter keeps a rutted dirt lane and no lane markings at all —
                // which means the player can see where the money went without opening a panel.
                float paving = 0f;
                if (t == TileKind.Yol)
                {
                    paving = Paving(x, y);
                    c = Color.Lerp(ColRoadRough, ColRoad, paving);
                }

                // A little per-tile variation keeps the grass from reading as a bedsheet, and a
                // faint checker on the open land is the reference's signature texture.
                float v = 0.95f + 0.10f * CityGrid.Hash(x, y, 7);
                if ((t == TileKind.Cayir || t == TileKind.Verimli || t == TileKind.Su) && ((x + y) & 1) == 0)
                    v *= 0.955f;
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

                    // No markings on an unpaved lane. Nobody paints a line on gravel.
                    if ((vertical ^ horizontal) && paving > 0.45f)
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
            _groundObject = go;
            go.transform.SetParent(transform, false);
            _groundMesh = _builder.ToMesh("Ground");
            go.AddComponent<MeshFilter>().sharedMesh = _groundMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;

            // A flat collider so mouse picking has something to hit; the mesh itself is not
            // needed for physics and a plane is far cheaper to raycast. Parented to the ground
            // object so a rebuild does not leave a stack of colliders behind.
            var plane = new GameObject("GroundCollider");
            plane.transform.SetParent(go.transform, false);
            var box = plane.AddComponent<BoxCollider>();
            box.size = new Vector3(w, 0.1f, h);
            box.center = Vector3.zero;
        }

        /// <summary>
        /// How well surfaced this stretch of street is, 0..1. Half of it is the quarter's founding
        /// wealth — TEPE was always going to have better roads than LİMAN — and half is what the
        /// player has since built there, so investing in a district visibly repaves it.
        /// </summary>
        float Paving(int x, int y)
        {
            var def = Districts.At(x, y);
            return def == null ? 0.25f : PavingOf(def, _state);
        }

        /// <summary>
        /// The same number, addressable by district. Static so the agent dump can report it
        /// without a second copy of the formula going quietly out of step with this one.
        /// </summary>
        public static float PavingOf(DistrictDef def, GameState state)
        {
            int built = 0;
            foreach (var b in state.Buildings)
                if (b.District == def.Id && b.Def.Storeys > 0) built++;

            // Floored well above zero: a founding city already has streets, and starting every
            // quarter on bare dirt turned the whole map tan and threw away the lane markings that
            // make a grid of grey squares read as a road. The range wants to be a difference in
            // upkeep between quarters, not a difference in century.
            float wealth = Mathf.Clamp01(def.Wealth / 80f);
            float density = Mathf.Clamp01(built / 14f);
            return Mathf.Clamp01(0.35f + wealth * 0.35f + density * 0.30f);
        }

        static readonly Color ColRoadRough = Hex("#585045");

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

        /// <summary>Bumps on every rebake, so model instances know when to re-sync.</summary>
        public int RebuildVersion { get; private set; }

        /// <summary>Re-bake the building and prop meshes. Called once at start and on every placement.</summary>
        public void Rebuild()
        {
            _bakedOrder = _state.AxisOrder;
            _bakedEconomy = _state.AxisEconomy;
            _bakedSeason = _state.Season;
            BuildBuildings();
            BuildProps();
            // The ground carries the road surface, and the road surface depends on how built-up
            // the quarter is — so it has to be re-baked when the quarter changes, not only when
            // somebody lays a road.
            RebuildGround();
            RebuildVersion++;
        }

        /// <summary>
        /// Rebake only when the drift has moved enough to change what is on the walls, or when
        /// the year has turned and the windows should be lit differently. Baking the whole
        /// skyline every turn would be wasteful, and baking it never would mean the city stopped
        /// telling the truth about its own politics.
        /// </summary>
        public void RefreshIdeology()
        {
            if (_state.Season == _bakedSeason &&
                Mathf.Abs(_state.AxisOrder - _bakedOrder) < 6 &&
                Mathf.Abs(_state.AxisEconomy - _bakedEconomy) < 6) return;
            Rebuild();
        }

        void BuildBuildings()
        {
            _builder.Clear();

            _floating = 0;
            _worstLift = 0f;
            _worstLiftId = "";
            _probes.Clear();

            for (int i = 0; i < _state.Buildings.Count; i++)
            {
                var b = _state.Buildings[i];
                var def = b.Def;
                var district = Districts.Get(b.District);

                // A multi-tile building is centred on its whole footprint, not on its anchor
                // tile — a 2×2 plant sits in the middle of its four tiles.
                Vector3 ground = CityGrid.World(b.Tile.x, b.Tile.y)
                               + new Vector3((def.Size.x - 1) * CityGrid.TileSize * 0.5f, 0,
                                             (def.Size.y - 1) * CityGrid.TileSize * 0.5f);
                float hash = CityGrid.Hash(b.Tile.x, b.Tile.y, 11);
                float hash2 = CityGrid.Hash(b.Tile.x, b.Tile.y, 23);

                // A building backed by a real external model contributes no procedural
                // geometry — the instance stands on the tile instead. Its probe entry still
                // exists, measured from the model's own bounds, so the ground bar holds.
                if (BuildingModels.Instance != null && BuildingModels.Instance.Covers(def.Id))
                {
                    BuildingModels.Instance.ProbeBounds(def.Id, ground, out var mMin, out var mMax);
                    _probes.Add(new BuildingProbe
                    {
                        Id = def.Id, Tile = b.Tile, Min = mMin, Max = mMax, FloorY = ground.y,
                    });
                    continue;
                }

                // Everything a building contributes is measured against the ground it stands on,
                // so "does every part of this rest on something" stops being a matter of opinion.
                _builder.MarkFloor();

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

                // Homes are wider and their floors are lower than a civic building's. At the old
                // uniform ratio a three-storey house came out nine metres tall and two wide —
                // a chimney with windows, not somewhere a family lives.
                float spread = residential ? 0.66f : 0.56f;
                float vary = residential ? 0.24f : 0.2f;
                float storeyHeight = residential ? 2.6f : 3.1f;

                float footprint = CityGrid.TileSize * (spread + vary * hash) * def.Size.x;
                float depth = CityGrid.TileSize * (spread + vary * hash2) * def.Size.y;
                float height = storeys * storeyHeight;

                // A building nobody works reads as cold and shuttered. It used to be dragged 45%
                // of the way to the background colour, which under a low winter sun left the
                // walls all but invisible — and since the roof took its colour from its own
                // palette and never saw this line, a dead house showed a bright terracotta roof
                // hanging in the air over nothing. Softer, towards slate rather than towards the
                // void, and everything the house is made of now follows it.
                bool dead = !b.Staffed;
                if (dead) tint = Color.Lerp(tint, ColShuttered, 0.38f);

                var s = new Shape
                {
                    Ground = ground, W = footprint, D = depth, H = height,
                    Tint = tint, Tile = b.Tile, Dead = dead,
                    Hash = hash, Hash2 = hash2, Storeys = storeys,
                };

                switch (def.Form)
                {
                    case Form.Duz:      FormDuz(s, def); break;
                    case Form.Ev:       FormEv(s); break;
                    case Form.Salon:    FormSalon(s); break;
                    case Form.Atolye:   FormAtolye(s); break;
                    case Form.Ocak:     FormOcak(s); break;
                    case Form.Ambar:    FormAmbar(s); break;
                    case Form.Degirmen: FormDegirmen(s); break;
                    case Form.Tapinak:  FormTapinak(s); break;
                    case Form.Anit:     FormAnit(s); break;
                    case Form.Baca:     FormBaca(s); break;
                    case Form.Kemer:    FormKemer(s); break;
                    case Form.Kuyu:     FormKuyu(s); break;
                    case Form.Karakol:  FormKarakol(s); break;
                    case Form.Pazar:    FormPazar(s); break;
                    default:            FormBlok(s); break;
                }

                if (def.Form != Form.Duz)
                    AddIdeologyProps(ground, footprint, depth, height, hash, hash2);

                // How far off the floor this building's lowest corner ended up. Zero is right;
                // anything else means a part of it is standing on air.
                float lift = _builder.LowestY - ground.y;
                if (lift > _worstLift) { _worstLift = lift; _worstLiftId = def.Id; }
                if (lift > 0.01f) _floating++;

                _probes.Add(new BuildingProbe
                {
                    Id = def.Id,
                    Tile = b.Tile,
                    Min = _builder.MinSince,
                    Max = _builder.MaxSince,
                    FloorY = ground.y,
                });
            }

            _builder.Into(_buildingMesh);
        }

        // ---------------------------------------------------------------- ideology
        //
        // The highest-payoff feature in the design, and the cheapest: as the axes move, the
        // buildings themselves grow the evidence. Red banners and shuttered windows towards
        // OTORİTE, awnings and improvised extensions towards ÖZGÜRLÜK, billboards and private
        // walls towards SERMAYE, murals and laundry lines towards EŞİTLİK.
        //
        // A player who never once reads the meters should still be able to look out of the
        // window and see what they have become. Nothing labels it. It is simply there.

        static readonly Color PropBanner = Hex("#C43A2A");
        static readonly Color PropShutter = Hex("#4A4238");
        static readonly Color PropAwning = Hex("#3E8C6E");
        static readonly Color PropBillboard = Hex("#E8E2D2");
        static readonly Color PropWall = Hex("#8E8578");
        static readonly Color PropMural = Hex("#C97B3C");
        static readonly Color PropLaundry = Hex("#DCE3EA");

        void AddIdeologyProps(Vector3 ground, float w, float d, float height, float hash, float hash2)
        {
            int order = _state.AxisOrder;
            int economy = _state.AxisEconomy;

            // Each band puts props on a fraction of the buildings, so the change creeps across
            // the city rather than switching on. At RADİKAL it is on most of them.
            // Capped below 1 on purpose: even at DÖNÜŞSÜZ some buildings stay bare, so the
            // city reads as a place that drifted rather than a set that was dressed.
            float orderDensity = Mathf.Clamp01((Mathf.Abs(order) - 25f) / 60f) * 0.8f;
            float economyDensity = Mathf.Clamp01((Mathf.Abs(economy) - 25f) / 60f) * 0.8f;

            if (order > 0 && hash < orderDensity)
            {
                // A banner down the face, and windows boarded on the ground floor.
                _builder.AddBox(ground + new Vector3(0, height * 0.32f, d * 0.5f + 0.04f),
                                new Vector3(w * 0.2f, height * 0.55f, 0.09f), PropBanner);
                if (hash2 < 0.55f)
                    _builder.AddBox(ground + new Vector3(w * 0.2f, 0.9f, d * 0.5f + 0.03f),
                                    new Vector3(w * 0.32f, 1.1f, 0.08f), PropShutter);
            }
            else if (order < 0 && hash < orderDensity)
            {
                // An awning, and a room somebody added without asking anyone. The brackets are
                // two slivers and they are what stop the awning reading as a plate hanging in
                // the air beside the building.
                float ay = height * 0.45f;
                _builder.AddBox(ground + new Vector3(0, ay, d * 0.5f + 0.4f),
                                new Vector3(w * 0.7f, 0.12f, 0.9f), PropAwning);
                foreach (float bx in new[] { -w * 0.3f, w * 0.3f })
                    _builder.AddBox(ground + new Vector3(bx, ay - 0.45f, d * 0.5f + 0.32f),
                                    new Vector3(0.07f, 0.5f, 0.5f),
                                    MeshBuilder.Shade(PropAwning, 0.6f));
                if (hash2 < 0.5f)
                    _builder.AddBox(ground + new Vector3(w * 0.5f, height * 0.4f, 0),
                                    new Vector3(w * 0.28f, height * 0.26f, d * 0.45f),
                                    MeshBuilder.Shade(PropAwning, 0.7f));
            }

            if (economy > 0 && hash2 < economyDensity)
            {
                // A billboard standing on the roof edge, and a low wall around the plot. Both
                // deliberately modest: a hoarding the size of the building it stands on reads
                // as a bug, not as commerce.
                _builder.AddBox(ground + new Vector3(0, height, -d * 0.34f),
                                new Vector3(w * 0.7f, 1.3f, 0.09f), PropBillboard);
                _builder.AddBox(ground + new Vector3(0, height, -d * 0.34f),
                                new Vector3(0.12f, 0.5f, 0.12f), MeshBuilder.Shade(PropWall, 0.7f));
                _builder.AddBox(ground + new Vector3(0, 0, d * 0.66f),
                                new Vector3(w * 1.1f, 0.85f, 0.12f), PropWall);
            }
            else if (economy < 0 && hash2 < economyDensity)
            {
                // A mural across the ground floor, and a line of washing between the blocks.
                _builder.AddBox(ground + new Vector3(0, 0.9f, d * 0.5f + 0.03f),
                                new Vector3(w * 0.8f, 1.8f, 0.07f), PropMural);
                // The line ran out of the wall and stopped in mid-air. It now ends on a post,
                // because a washing line with one end attached to nothing is the sort of thing
                // that reads as a rendering fault rather than as a neighbourhood.
                float y = height * 0.7f;
                _builder.AddBox(ground + new Vector3(0, y, d * 0.5f + 1.1f),
                                new Vector3(w * 0.05f, 0.05f, 2.2f), PropLaundry);
                _builder.AddBox(ground + new Vector3(0, 0, d * 0.5f + 2.1f),
                                new Vector3(0.12f, y + 0.05f, 0.12f), MeshBuilder.Shade(PropLaundry, 0.55f));
                for (int i = 0; i < 3; i++)
                    _builder.AddBox(ground + new Vector3(0, y - 0.35f, d * 0.5f + 0.45f + i * 0.62f),
                                    new Vector3(0.36f, 0.5f, 0.04f),
                                    MeshBuilder.Shade(PropLaundry, 0.85f + i * 0.06f));
            }
        }

        // ---------------------------------------------------------------- silhouettes
        //
        // One method per Form. The house style rule for all of them: a part is placed either at
        // `g.Ground` or at the top of a part that itself reaches the ground. Nothing is offset
        // upward "to sit on" something — AddBox measures from its base, so an upward offset is a
        // gap, and a gap is what made the last round of these look like they were floating.

        /// <summary>Everything a form needs to draw itself.</summary>
        struct Shape
        {
            public Vector3 Ground;
            public float W, D, H;
            public Color Tint;
            public Vector2Int Tile;
            public bool Dead;
            public float Hash, Hash2;
            public int Storeys;
        }

        int _floating;
        float _worstLift;
        string _worstLiftId = "";

        /// <summary>One building as the probe reports it: what it is, and where its box really is.</summary>
        public struct BuildingProbe
        {
            public string Id;
            public Vector2Int Tile;
            public Vector3 Min, Max;
            /// <summary>World y of the tile it stands on — the floor its Min.y is judged against.</summary>
            public float FloorY;
        }

        readonly System.Collections.Generic.List<BuildingProbe> _probes =
            new System.Collections.Generic.List<BuildingProbe>(64);

        /// <summary>World-space bounds of every building, recorded at bake time.</summary>
        public System.Collections.Generic.IReadOnlyList<BuildingProbe> Probes => _probes;

        /// <summary>Buildings whose lowest corner is off the ground. Should always be zero.</summary>
        public int FloatingBuildings => _floating;
        public float WorstLift => _worstLift;
        public string WorstLiftId => _worstLiftId;

        Color Shade(Shape g, float k) => MeshBuilder.Shade(g.Tint, k);

        /// <summary>
        /// A foundation plinth: a skirt slightly wider than the body, darker than it. The single
        /// biggest "is it standing on the ground?" cue there is — buildings without one read as
        /// placed, buildings with one read as built.
        /// </summary>
        void AddPlinth(Shape g, float w, float d)
        {
            _builder.AddBox(g.Ground, new Vector3(w + 0.4f, 0.3f, d + 0.4f), Shade(g, 0.6f));
        }

        /// <summary>
        /// Floor lines across the walls, one per storey. The reference buildings are striped
        /// with them, and they are most of what separates "a building" from "a tall box".
        /// </summary>
        void AddFloorBands(Shape g, float w, float d, float storeyHeight, int storeys)
        {
            for (int f = 1; f < storeys; f++)
                _builder.AddBox(g.Ground + Vector3.up * (f * storeyHeight - 0.09f),
                                new Vector3(w + 0.08f, 0.18f, d + 0.08f), Shade(g, 0.78f));
        }

        /// <summary>A slab: road, field, park. The only form with no height at all.</summary>
        void FormDuz(Shape g, BuildingDef def)
        {
            Color flat = def.Id == "tarla" ? MeshBuilder.Shade(def.Tint, 0.92f + 0.16f * g.Hash) : def.Tint;
            _builder.AddBox(g.Ground, new Vector3(CityGrid.TileSize * 0.92f, 0.14f, CityGrid.TileSize * 0.92f), flat);

            if (def.Id == "tarla") AddFurrows(g.Ground, g.Hash);
            if (def.Id == "park")
            {
                AddTree(g.Ground + new Vector3((g.Hash - 0.5f) * 2f, 0.14f, (g.Hash2 - 0.5f) * 2f), 1f);
                // A bench and a path, so a park is not a green rectangle.
                _builder.AddBox(g.Ground + new Vector3(1.1f, 0.14f, -0.8f),
                                new Vector3(1.4f, 0.44f, 0.4f), Hex("#7A6244"));
            }
        }

        /// <summary>A house: plinth, banded walls, pitched roof, chimney, door.</summary>
        void FormEv(Shape g)
        {
            AddPlinth(g, g.W, g.D);
            _builder.AddBox(g.Ground, new Vector3(g.W, g.H, g.D), g.Tint);
            AddFloorBands(g, g.W, g.D, g.H / Mathf.Max(1, g.Storeys), g.Storeys);
            AddWindows(g.Ground, g.W, g.D, g.H, g.Storeys, g.Tile, g.Dead);
            AddCottageTop(g.Ground, g.W, g.D, g.H, g.Tint, g.Tile, g.Dead);
            AddDoor(g.Ground, g.W, g.D, g.Tile, g.Dead);
        }

        /// <summary>Dense housing and the exchange: banded walls, a parapet, a coloured roof.</summary>
        void FormBlok(Shape g)
        {
            AddPlinth(g, g.W, g.D);
            _builder.AddBox(g.Ground, new Vector3(g.W, g.H, g.D), g.Tint);
            AddFloorBands(g, g.W, g.D, g.H / Mathf.Max(1, g.Storeys), g.Storeys);
            AddWindows(g.Ground, g.W, g.D, g.H, g.Storeys, g.Tile, g.Dead);

            // A coloured roof slab inside a parapet lip — the reference's rooftops are one of
            // its most recognisable features from the air.
            Color roof = RoofTiles[Mathf.FloorToInt(g.Hash * RoofTiles.Length) % RoofTiles.Length];
            if (g.Dead) roof = Color.Lerp(roof, ColShuttered, 0.38f);
            _builder.AddBox(g.Ground + Vector3.up * g.H, new Vector3(g.W + 0.14f, 0.22f, g.D + 0.14f), Shade(g, 0.72f));
            _builder.AddBox(g.Ground + Vector3.up * (g.H + 0.22f), new Vector3(g.W - 0.3f, 0.06f, g.D - 0.3f),
                            MeshBuilder.Shade(roof, 0.9f));

            AddRoofClutter(g.Ground, g.W, g.D, g.H + 0.22f, g.Tint, g.Tile);
            AddDoor(g.Ground, g.W, g.D, g.Tile, g.Dead);
        }

        /// <summary>
        /// A civic hall: a long low body, a shallow roof, and a porch of columns across the front.
        /// The columns are the tell — nothing else in the city has a row of verticals at its face.
        /// </summary>
        void FormSalon(Shape g)
        {
            float h = g.H * 0.8f;
            AddPlinth(g, g.W, g.D * 0.86f);
            _builder.AddBox(g.Ground, new Vector3(g.W, h, g.D * 0.86f), g.Tint);
            AddFloorBands(g, g.W, g.D * 0.86f, h / Mathf.Max(1, g.Storeys), g.Storeys);
            AddWindows(g.Ground, g.W, g.D * 0.86f, h, Mathf.Max(1, g.Storeys), g.Tile, g.Dead);
            _builder.AddGable(g.Ground + Vector3.up * h, g.W, g.D * 0.86f, 0.9f,
                              Shade(g, 0.7f), g.W >= g.D);

            // Porch: a floor slab at ground level, four columns standing on it, and a lintel.
            float pz = g.D * 0.43f + 0.55f;
            Vector3 porch = g.Ground + new Vector3(0, 0, pz);
            _builder.AddBox(porch, new Vector3(g.W * 0.92f, 0.22f, 1.3f), Shade(g, 0.82f));
            float colH = h * 0.72f;
            for (int i = 0; i < 4; i++)
            {
                float u = (i / 3f - 0.5f) * g.W * 0.72f;
                _builder.AddBox(porch + new Vector3(u, 0, 0.2f),
                                new Vector3(0.24f, colH, 0.24f), Shade(g, 1.08f));
            }
            _builder.AddBox(porch + new Vector3(0, colH, 0.2f),
                            new Vector3(g.W * 0.86f, 0.34f, 0.62f), Shade(g, 0.9f));
        }

        /// <summary>A workshop: a wide shed with a saw-tooth roof and a stack at one corner.</summary>
        void FormAtolye(Shape g)
        {
            float h = g.H * 0.66f;
            AddPlinth(g, g.W * 1.05f, g.D);
            _builder.AddBox(g.Ground, new Vector3(g.W * 1.05f, h, g.D), Shade(g, 0.95f));

            // Saw-tooth: three short slopes, each standing on the roof deck below it.
            Vector3 deck = g.Ground + Vector3.up * h;
            for (int i = 0; i < 3; i++)
            {
                float u = (i - 1) * g.W * 0.33f;
                _builder.AddBox(deck + new Vector3(u, 0, 0), new Vector3(g.W * 0.28f, 0.5f, g.D * 0.9f),
                                Shade(g, 0.8f));
                _builder.AddWindow(deck + new Vector3(u, 0.9f, -g.D * 0.2f), Vector3.forward,
                                   g.W * 0.24f, 0.7f, g.Dead ? Hex("#39414E") : Hex("#BFD4E6"));
                _builder.AddGable(deck + new Vector3(u, 0.5f, 0), g.W * 0.28f, g.D * 0.9f, 0.7f,
                                  Shade(g, 0.68f), false, 0.04f);
            }

            // The stack stands on the ground beside the shed, not on its roof.
            _builder.AddBox(g.Ground + new Vector3(g.W * 0.62f, 0, -g.D * 0.34f),
                            new Vector3(1.05f, h + 6.5f, 1.05f), Shade(g, 0.6f));
        }

        /// <summary>A kiln: a squat drum, a domed cap, a flue. Bakeries and quarry ovens.</summary>
        void FormOcak(Shape g)
        {
            float h = g.H * 0.62f;
            AddPlinth(g, g.W * 0.9f, g.D * 0.9f);
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.9f, h, g.D * 0.9f), g.Tint);
            _builder.AddPyramid(g.Ground + Vector3.up * h, g.W * 0.45f, 1.6f, Shade(g, 0.78f));

            // Flue from the ground up past the dome, and a mouth glowing at the base.
            _builder.AddBox(g.Ground + new Vector3(g.W * 0.34f, 0, g.D * 0.28f),
                            new Vector3(0.7f, h + 3.4f, 0.7f), Shade(g, 0.62f));
            _builder.AddWindow(g.Ground + new Vector3(0, 0.7f, g.D * 0.45f + 0.02f), Vector3.forward,
                               g.W * 0.34f, 1.1f, g.Dead ? Hex("#39414E") : Hex("#FF9B4A"));
        }

        /// <summary>A barn with a silo. Granary, depot, ration store.</summary>
        void FormAmbar(Shape g)
        {
            float h = g.H * 0.72f;
            AddPlinth(g, g.W * 0.86f, g.D);
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.86f, h, g.D), g.Tint);
            _builder.AddGable(g.Ground + Vector3.up * h, g.W * 0.86f, g.D, 1.5f, Shade(g, 0.72f), false);

            // The silo stands on its own footing on the ground, capped with a cone.
            Vector3 silo = g.Ground + new Vector3(g.W * 0.6f, 0, g.D * 0.2f);
            _builder.AddBox(silo, new Vector3(1.5f, h + 2.2f, 1.5f), Shade(g, 1.06f));
            _builder.AddPyramid(silo + Vector3.up * (h + 2.2f), 0.9f, 1.1f, Shade(g, 0.7f));

            // Big doors, because that is what a barn is.
            _builder.AddBox(g.Ground + new Vector3(0, 0, g.D * 0.5f + 0.02f),
                            new Vector3(g.W * 0.42f, h * 0.7f, 0.1f), Shade(g, 0.6f));
        }

        /// <summary>A mill: a tapered tower, a cap, and four sails on a hub.</summary>
        void FormDegirmen(Shape g)
        {
            float h = g.H * 1.15f;
            AddPlinth(g, g.W * 0.8f, g.D * 0.8f);
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.8f, h * 0.55f, g.D * 0.8f), g.Tint);
            _builder.AddBox(g.Ground + Vector3.up * (h * 0.55f),
                            new Vector3(g.W * 0.62f, h * 0.45f, g.D * 0.62f), Shade(g, 1.04f));
            _builder.AddPyramid(g.Ground + Vector3.up * h, g.W * 0.36f, 1.4f, Shade(g, 0.68f));

            // Sails: a hub on the face with four arms crossing it. Read from any angle.
            Vector3 hub = g.Ground + new Vector3(0, h * 0.78f, g.D * 0.42f);
            _builder.AddBox(hub, new Vector3(0.4f, 0.4f, 0.3f), Shade(g, 0.55f));
            _builder.AddBox(hub + new Vector3(0, -2.4f, 0.1f), new Vector3(0.22f, 5.2f, 0.12f), Hex("#8C7A5E"));
            _builder.AddBox(hub + new Vector3(-2.6f, -0.11f, 0.1f), new Vector3(5.2f, 0.22f, 0.12f), Hex("#8C7A5E"));
        }

        /// <summary>A temple: a stepped base, a deep roof, a ridge ornament.</summary>
        void FormTapinak(Shape g)
        {
            _builder.AddBox(g.Ground, new Vector3(g.W * 1.1f, 0.5f, g.D * 1.1f), Shade(g, 0.86f));
            _builder.AddBox(g.Ground + Vector3.up * 0.5f, new Vector3(g.W * 0.95f, 0.4f, g.D * 0.95f), Shade(g, 0.94f));

            float bodyBase = 0.9f;
            _builder.AddBox(g.Ground + Vector3.up * bodyBase, new Vector3(g.W * 0.8f, g.H, g.D * 0.8f), g.Tint);
            _builder.AddGable(g.Ground + Vector3.up * (bodyBase + g.H), g.W * 1.0f, g.D * 1.0f, 2.2f,
                              Hex("#B5654A"), g.W >= g.D, 0.35f);
            _builder.AddBox(g.Ground + Vector3.up * (bodyBase + g.H + 2.2f),
                            new Vector3(0.3f, 1.2f, 0.3f), Hex("#D8C48A"));
        }

        /// <summary>A monument: a plinth and an obelisk, and nothing else.</summary>
        void FormAnit(Shape g)
        {
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.9f, 0.9f, g.D * 0.9f), Shade(g, 0.82f));
            _builder.AddBox(g.Ground + Vector3.up * 0.9f, new Vector3(g.W * 0.55f, 0.6f, g.D * 0.55f), Shade(g, 0.92f));
            float shaft = g.H + 3.5f;
            _builder.AddBox(g.Ground + Vector3.up * 1.5f, new Vector3(g.W * 0.3f, shaft, g.D * 0.3f), Shade(g, 1.08f));
            _builder.AddPyramid(g.Ground + Vector3.up * (1.5f + shaft), g.W * 0.17f, 1.6f, Shade(g, 1.15f));
        }

        /// <summary>A power house: a low plant with one very tall stack rising from the ground.</summary>
        void FormBaca(Shape g)
        {
            float h = g.H * 0.7f;
            AddPlinth(g, g.W, g.D * 0.9f);
            _builder.AddBox(g.Ground, new Vector3(g.W, h, g.D * 0.9f), g.Tint);
            AddRoofClutter(g.Ground, g.W, g.D * 0.9f, h, g.Tint, g.Tile);

            Vector3 stack = g.Ground + new Vector3(-g.W * 0.3f, 0, -g.D * 0.3f);
            _builder.AddBox(stack, new Vector3(1.5f, h + 9f, 1.5f), Shade(g, 0.66f));
            _builder.AddBox(stack + Vector3.up * (h + 9f), new Vector3(1.8f, 0.5f, 1.8f), Shade(g, 0.5f));
        }

        /// <summary>An aqueduct: piers standing on the ground with a channel across their tops.</summary>
        void FormKemer(Shape g)
        {
            float h = g.H * 1.1f;
            for (int i = 0; i < 3; i++)
            {
                float u = (i - 1) * g.W * 0.42f;
                _builder.AddBox(g.Ground + new Vector3(u, 0, 0), new Vector3(0.75f, h, g.D * 0.55f), g.Tint);
            }
            _builder.AddBox(g.Ground + Vector3.up * h, new Vector3(g.W * 1.15f, 0.55f, g.D * 0.7f), Shade(g, 1.05f));
            _builder.AddBox(g.Ground + Vector3.up * (h + 0.55f), new Vector3(g.W * 1.15f, 0.4f, g.D * 0.22f), Shade(g, 0.72f));
        }

        /// <summary>A wellhead: a low ring and a headframe with a winch over it.</summary>
        void FormKuyu(Shape g)
        {
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.7f, 0.85f, g.D * 0.7f), g.Tint);
            _builder.AddBox(g.Ground + Vector3.up * 0.85f, new Vector3(g.W * 0.5f, 0.14f, g.D * 0.5f), Shade(g, 0.6f));

            float legH = 2.6f;
            foreach (float sx in new[] { -1f, 1f })
                _builder.AddBox(g.Ground + new Vector3(sx * g.W * 0.32f, 0, 0),
                                new Vector3(0.18f, legH, 0.18f), Shade(g, 0.7f));
            _builder.AddBox(g.Ground + Vector3.up * legH, new Vector3(g.W * 0.78f, 0.2f, 0.24f), Shade(g, 0.62f));
        }

        /// <summary>A guard post: a small house with a watch tower rising beside it.</summary>
        void FormKarakol(Shape g)
        {
            float h = g.H * 0.75f;
            AddPlinth(g, g.W * 0.85f, g.D * 0.85f);
            _builder.AddBox(g.Ground, new Vector3(g.W * 0.85f, h, g.D * 0.85f), g.Tint);
            AddWindows(g.Ground, g.W * 0.85f, g.D * 0.85f, h, Mathf.Max(1, g.Storeys), g.Tile, g.Dead);
            _builder.AddGable(g.Ground + Vector3.up * h, g.W * 0.85f, g.D * 0.85f, 0.8f, Shade(g, 0.7f), g.W >= g.D);

            Vector3 tower = g.Ground + new Vector3(g.W * 0.5f, 0, -g.D * 0.34f);
            float th = h + 3.4f;
            _builder.AddBox(tower, new Vector3(1.25f, th, 1.25f), Shade(g, 0.9f));
            _builder.AddBox(tower + Vector3.up * th, new Vector3(1.8f, 0.7f, 1.8f), Shade(g, 0.72f));
            _builder.AddPyramid(tower + Vector3.up * (th + 0.7f), 0.95f, 0.9f, Shade(g, 0.6f));
        }

        /// <summary>A market: an open canopy on posts, with crates under it. Barely a building.</summary>
        void FormPazar(Shape g)
        {
            float postH = 2.6f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1 : 1;
                float sz = (i & 2) == 0 ? -1 : 1;
                _builder.AddBox(g.Ground + new Vector3(sx * g.W * 0.38f, 0, sz * g.D * 0.38f),
                                new Vector3(0.22f, postH, 0.22f), Shade(g, 0.62f));
            }
            _builder.AddBox(g.Ground + Vector3.up * postH, new Vector3(g.W * 0.95f, 0.2f, g.D * 0.95f), g.Tint);
            _builder.AddGable(g.Ground + Vector3.up * (postH + 0.2f), g.W * 0.95f, g.D * 0.95f, 0.8f,
                              Shade(g, 1.06f), g.W >= g.D, 0.2f);

            // Goods on the ground under it.
            _builder.AddBox(g.Ground + new Vector3(-0.4f, 0, 0.3f), new Vector3(0.8f, 0.7f, 0.8f), Hex("#7A6647"));
            _builder.AddBox(g.Ground + new Vector3(0.7f, 0, -0.4f), new Vector3(0.6f, 0.5f, 0.6f), Hex("#8A6E44"));
        }

        static readonly Color[] RoofTiles =
        {
            Hex("#8C4A38"), Hex("#A45B3E"), Hex("#6E4436"), Hex("#B0704A"), Hex("#5E4A44"),
        };

        /// <summary>
        /// A pitched roof, a chimney and — on about half of them — a dormer poking out of the
        /// slope. The roof colour comes from a small warm palette rather than the wall tint, so
        /// a quarter reads as a roofscape from above instead of a field of coloured lids.
        /// </summary>
        void AddCottageTop(Vector3 ground, float w, float d, float height, Color tint, Vector2Int tile,
                           bool dead)
        {
            float h1 = CityGrid.Hash(tile.x, tile.y, 61);
            float h2 = CityGrid.Hash(tile.x, tile.y, 67);
            float h3 = CityGrid.Hash(tile.x, tile.y, 71);

            Color roof = RoofTiles[Mathf.FloorToInt(h1 * RoofTiles.Length) % RoofTiles.Length];
            // The roof is the largest surface on a house. If it does not go dark with the walls,
            // an empty house is a bright lid floating over a shadow.
            if (dead) roof = Color.Lerp(roof, ColShuttered, 0.38f);
            bool alongX = w >= d;                       // ridge runs the long way, as roofs do
            // Kept under half the wall height. A steeper roof looks better in isolation and, from
            // a camera pitched at forty degrees, swallows the walls it is supposed to sit on.
            float pitch = Mathf.Min(1.1f + h2 * 1.0f, height * 0.45f);

            Vector3 eaves = ground + Vector3.up * height;
            _builder.AddGable(eaves, w, d, pitch, roof, alongX);

            // A chimney, offset from the ridge so it does not look like a spine.
            Vector3 stack = eaves + new Vector3(
                alongX ? (h2 - 0.5f) * w * 0.55f : w * 0.26f,
                0,
                alongX ? d * 0.26f : (h3 - 0.5f) * d * 0.55f);
            _builder.AddBox(stack, new Vector3(0.42f, pitch + 0.7f, 0.42f), MeshBuilder.Shade(tint, 0.72f));
            _builder.AddBox(stack + Vector3.up * (pitch + 0.7f), new Vector3(0.55f, 0.16f, 0.55f),
                            MeshBuilder.Shade(roof, 0.7f));

            // A dormer: a little box in the slope with its own lit window. This is the piece
            // that makes a roof read as somewhere people sleep.
            if (h3 > 0.45f)
            {
                Vector3 side = alongX ? new Vector3(0, 0, d * 0.30f) : new Vector3(w * 0.30f, 0, 0);
                Vector3 at = eaves + side + Vector3.up * (pitch * 0.34f);
                _builder.AddBox(at, new Vector3(0.85f, 0.8f, 0.85f), MeshBuilder.Shade(tint, 1.04f));
                _builder.AddWindow(at + (alongX ? new Vector3(0, 0.45f, 0.44f) : new Vector3(0.44f, 0.45f, 0)),
                                   alongX ? Vector3.forward : Vector3.right,
                                   0.5f, 0.5f, dead ? Hex("#39414E") : Hex("#FFD79A"));
            }
        }

        /// <summary>A door at the foot of the wall, with a step and a lamp beside it.</summary>
        void AddDoor(Vector3 ground, float w, float d, Vector2Int tile, bool dead)
        {
            float h = CityGrid.Hash(tile.x, tile.y, 73);
            Vector3 face = ground + new Vector3((h - 0.5f) * w * 0.4f, 0, d * 0.5f);

            _builder.AddBox(face + new Vector3(0, 0, 0.02f), new Vector3(0.62f, 1.35f, 0.1f), Hex("#4A3527"));
            _builder.AddBox(face + new Vector3(0, 0, 0.16f), new Vector3(0.9f, 0.12f, 0.35f), Hex("#7C7062"));
            // A porch lamp, self-lit. Small, warm, and the reason a street looks occupied — so
            // an empty house does not get to keep its light on.
            _builder.AddBox(face + new Vector3(0.5f, 1.5f, 0.1f), new Vector3(0.16f, 0.16f, 0.16f),
                            dead ? Hex("#3A3F48") : new Color(1f, 0.86f, 0.62f, 0f));
        }

        /// <summary>
        /// What sits on a roof. The design asks for rooftop units placed procedurally so no two
        /// blocks look identical, and one grey box on every tall building was the opposite of
        /// that — a row of dominoes wearing the same hat. Four pieces, each on its own coin
        /// flip, gives sixteen silhouettes for the price of one.
        /// </summary>
        void AddRoofClutter(Vector3 ground, float w, float d, float height, Color tint, Vector2Int tile)
        {
            Vector3 roof = ground + Vector3.up * height;
            Color dark = MeshBuilder.Shade(tint, 0.66f);

            float a = CityGrid.Hash(tile.x, tile.y, 41);
            float b = CityGrid.Hash(tile.x, tile.y, 43);
            float c = CityGrid.Hash(tile.x, tile.y, 47);
            float e = CityGrid.Hash(tile.x, tile.y, 53);

            // A stair head — the box that was always there, but now it moves around.
            if (a > 0.35f)
                _builder.AddBox(roof + new Vector3((a - 0.5f) * w * 0.4f, 0, (b - 0.5f) * d * 0.4f),
                                new Vector3(w * 0.3f, 0.85f, d * 0.3f), dark);

            // A water tank on four legs. The legs are what make it read as a tank rather than
            // another box, and they cost four thin boxes.
            if (b > 0.55f)
            {
                Vector3 at = roof + new Vector3((b - 0.5f) * w * 0.5f, 0, (c - 0.5f) * d * 0.5f);
                float leg = 0.7f;
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) == 0 ? -1 : 1;
                    float sz = (i & 2) == 0 ? -1 : 1;
                    _builder.AddBox(at + new Vector3(sx * 0.34f, leg * 0.5f, sz * 0.34f),
                                    new Vector3(0.14f, leg, 0.14f), MeshBuilder.Shade(dark, 0.8f));
                }
                _builder.AddBox(at + Vector3.up * (leg + 0.45f), new Vector3(1.1f, 0.9f, 1.1f),
                                Hex("#6E6357"));
            }

            // Vents, in a little row. AddBox measures from the base of the box, so these take no
            // Y offset at all — 0.18 here used to lift the whole row off the roof it sits on.
            if (c > 0.5f)
                for (int i = 0; i < 3; i++)
                    _builder.AddBox(roof + new Vector3(w * (-0.22f + i * 0.22f), 0f, -d * 0.3f),
                                    new Vector3(0.3f, 0.36f, 0.3f), MeshBuilder.Shade(dark, 0.9f));

            // An aerial. Thin, tall, and the only thing up here that breaks the skyline — which
            // it was doing from a metre and a half above the roof, standing on nothing.
            if (e > 0.7f)
                _builder.AddBox(roof + new Vector3((e - 0.5f) * w * 0.5f, 0f, (a - 0.5f) * d * 0.5f),
                                new Vector3(0.1f, 4.5f, 0.1f), MeshBuilder.Shade(dark, 0.75f));
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

        void AddWindows(Vector3 ground, float w, float d, float height, int storeys, Vector2Int tile,
                        bool dead)
        {
            Color warm = Hex("#FFD79A");
            Color cold = Hex("#8FA6C0");

            // Short winter days mean lamps lit while you are still looking at the city; high
            // summer means most windows are just glass. Same buildings, different hour.
            float litShare = _state.Season == 3 ? 0.80f      // kış
                           : _state.Season == 2 ? 0.62f      // sonbahar
                           : _state.Season == 0 ? 0.48f      // ilkbahar
                           : 0.34f;                          // yaz

            for (int floor = 0; floor < storeys; floor++)
            {
                float y = ground.y + (floor + 0.55f) / storeys * height;
                for (int col = 0; col < 3; col++)
                {
                    float u = (col + 0.5f) / 3f - 0.5f;
                    int salt = tile.x * 31 + tile.y * 17 + floor * 7 + col;
                    if (CityGrid.Hash(tile.x, tile.y, salt) < 0.24f) continue;

                    // Nobody is home to light a lamp in a building nobody works.
                    bool lit = !dead && CityGrid.Hash(tile.x, tile.y, salt + 5000) < litShare;
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
                    // 0.22 buried the city in forest, and under a low autumn sun that many
                    // canopies melt into one muddy shadow mass across the map. The reference
                    // city is sparse: trees punctuate blocks, they do not carpet them.
                    if (r < 0.10f)
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
                else if (t == TileKind.Su && r < 0.05f)
                {
                    AddBoat(CityGrid.World(x, y, -0.5f), CityGrid.Hash(x, y, 9));
                }

                // Market stalls cluster where people already gather: an empty plot on the street
                // in the old town or the docks. The design names them as one of the things that
                // stops two blocks looking alike, and a canopy is three boxes.
                if ((t == TileKind.Cayir || t == TileKind.Verimli) && HasRoadNeighbour(x, y))
                {
                    var d = Districts.At(x, y);
                    bool market = d != null && (d.Id == DistrictId.EskiSehir || d.Id == DistrictId.Liman);
                    if (market && CityGrid.Hash(x, y, 13) < 0.14f)
                        AddStall(CityGrid.World(x, y), CityGrid.Hash(x, y, 17));
                }

                // And the quarter's own furniture. §3 asks that a player be able to diagnose a
                // district by watching it for five seconds; palette and storey height alone gave
                // six quarters that differed by tint, which is not the same as differing by
                // character. A crane says docks before you have read the label.
                if (t == TileKind.Cayir || t == TileKind.Verimli || t == TileKind.Tepelik)
                {
                    var quarter = Districts.At(x, y);
                    if (quarter != null) AddDistrictProp(quarter.Id, x, y);
                }
            }

            _builder.Into(_propMesh);
        }

        /// <summary>
        /// One piece of quarter-specific furniture on an empty plot. Each is a handful of boxes,
        /// each is unmistakable from the air, and each says what the district is *for* — which is
        /// the readability the design asks for and which colour alone was never going to carry.
        /// </summary>
        void AddDistrictProp(DistrictId id, int x, int y)
        {
            float r = CityGrid.Hash(x, y, 91);
            float s = CityGrid.Hash(x, y, 97);
            Vector3 at = CityGrid.World(x, y) + new Vector3((s - 0.5f) * 2.2f, 0, (r - 0.5f) * 2.2f);

            switch (id)
            {
                case DistrictId.Liman when r < 0.05f:
                {
                    // A quayside crane: a mast, a counterweight and a jib reaching out over the
                    // water side. Nothing else on this map has a horizontal arm in the air.
                    _builder.AddBox(at, new Vector3(0.9f, 0.35f, 0.9f), Hex("#4A4F57"));
                    _builder.AddBox(at + Vector3.up * 0.35f, new Vector3(0.34f, 5.2f, 0.34f), Hex("#B8703A"));
                    _builder.AddBox(at + new Vector3(1.5f, 5.2f, 0), new Vector3(3.6f, 0.26f, 0.26f), Hex("#B8703A"));
                    _builder.AddBox(at + new Vector3(-0.7f, 5.2f, 0), new Vector3(0.8f, 0.5f, 0.5f), Hex("#4A4F57"));
                    _builder.AddBox(at + new Vector3(2.9f, 4.3f, 0), new Vector3(0.1f, 1.6f, 0.1f), Hex("#3A3E44"));
                    break;
                }
                case DistrictId.Liman when r < 0.13f:
                {
                    // Stacked cargo. Three crates, never the same three.
                    for (int i = 0; i < 3; i++)
                    {
                        float k = CityGrid.Hash(x, y, 101 + i);
                        if (k < 0.3f) continue;
                        _builder.AddBox(at + new Vector3((k - 0.5f) * 1.3f, i * 0.62f, (i - 1) * 0.4f),
                                        new Vector3(0.75f, 0.6f, 0.75f),
                                        k > 0.6f ? Hex("#8A6E44") : Hex("#6E5B3C"));
                    }
                    break;
                }

                case DistrictId.Tepe when r < 0.10f:
                {
                    // A garden wall with a gate post at each end. Wealth, expressed as a boundary.
                    _builder.AddBox(at, new Vector3(4.2f, 1.1f, 0.24f), Hex("#B9AE97"));
                    _builder.AddBox(at + new Vector3(-2.1f, 0, 0), new Vector3(0.42f, 1.7f, 0.42f), Hex("#A3987F"));
                    _builder.AddBox(at + new Vector3(2.1f, 0, 0), new Vector3(0.42f, 1.7f, 0.42f), Hex("#A3987F"));
                    break;
                }
                case DistrictId.Tepe when r < 0.20f:
                {
                    // A clipped ornamental hedge — a ball on a stem, which reads as money.
                    _builder.AddBox(at, new Vector3(0.22f, 0.9f, 0.22f), Hex("#5A4433"));
                    _builder.AddBox(at + Vector3.up * 0.9f, new Vector3(1.15f, 1.0f, 1.15f), Hex("#4B6F49"));
                    break;
                }

                case DistrictId.EskiSehir when r < 0.10f:
                {
                    // A courtyard wall with an arched opening, faked as two piers and a lintel.
                    _builder.AddBox(at + new Vector3(-1.3f, 0, 0), new Vector3(0.9f, 2.3f, 0.7f), Hex("#B49E7E"));
                    _builder.AddBox(at + new Vector3(1.3f, 0, 0), new Vector3(0.9f, 2.3f, 0.7f), Hex("#B49E7E"));
                    _builder.AddBox(at + Vector3.up * 2.3f, new Vector3(3.5f, 0.5f, 0.7f), Hex("#C0AB8B"));
                    break;
                }
                case DistrictId.EskiSehir when r < 0.19f:
                {
                    // A well head. Old towns are organised around water.
                    _builder.AddBox(at, new Vector3(1.3f, 0.7f, 1.3f), Hex("#9A9086"));
                    _builder.AddBox(at + new Vector3(-0.55f, 0.7f, 0), new Vector3(0.12f, 1.5f, 0.12f), Hex("#5A4433"));
                    _builder.AddBox(at + new Vector3(0.55f, 0.7f, 0), new Vector3(0.12f, 1.5f, 0.12f), Hex("#5A4433"));
                    _builder.AddBox(at + Vector3.up * 2.2f, new Vector3(1.5f, 0.14f, 0.5f), Hex("#6E5B3C"));
                    break;
                }

                case DistrictId.Sanayi when r < 0.08f:
                {
                    // A pipe rack straddling the plot. Industry, in one silhouette.
                    _builder.AddBox(at + new Vector3(-1.6f, 0, 0), new Vector3(0.3f, 3.2f, 0.3f), Hex("#5B5F66"));
                    _builder.AddBox(at + new Vector3(1.6f, 0, 0), new Vector3(0.3f, 3.2f, 0.3f), Hex("#5B5F66"));
                    _builder.AddBox(at + Vector3.up * 3.0f, new Vector3(3.8f, 0.34f, 0.34f), Hex("#7A6A52"));
                    _builder.AddBox(at + Vector3.up * 2.55f, new Vector3(3.8f, 0.26f, 0.26f), Hex("#6A5C48"));
                    break;
                }
                case DistrictId.Sanayi when r < 0.18f:
                {
                    // A spoil heap and a drum or two.
                    _builder.AddPyramid(at, 1.4f, 1.5f, Hex("#57514A"));
                    _builder.AddBox(at + new Vector3(1.5f, 0, 0.7f), new Vector3(0.6f, 0.85f, 0.6f), Hex("#7A5B3C"));
                    break;
                }

                case DistrictId.Universite when r < 0.09f:
                {
                    // A bench under a lamp. Students, sitting about.
                    _builder.AddBox(at + Vector3.up * 0.36f, new Vector3(1.7f, 0.12f, 0.5f), Hex("#7A6244"));
                    _builder.AddBox(at + new Vector3(-0.7f, 0, 0), new Vector3(0.12f, 0.36f, 0.42f), Hex("#4A4034"));
                    _builder.AddBox(at + new Vector3(0.7f, 0, 0), new Vector3(0.12f, 0.36f, 0.42f), Hex("#4A4034"));
                    _builder.AddBox(at + new Vector3(0, 0.48f, -0.22f), new Vector3(1.7f, 0.55f, 0.1f), Hex("#8A7050"));
                    break;
                }
                case DistrictId.Universite when r < 0.16f:
                {
                    // A noticeboard, papered over. Where an argument starts.
                    _builder.AddBox(at + new Vector3(-0.55f, 0, 0), new Vector3(0.12f, 1.5f, 0.12f), Hex("#4A4034"));
                    _builder.AddBox(at + new Vector3(0.55f, 0, 0), new Vector3(0.12f, 1.5f, 0.12f), Hex("#4A4034"));
                    _builder.AddBox(at + Vector3.up * 1.05f, new Vector3(1.1f, 0.75f, 0.1f), Hex("#A79E8C"));
                    break;
                }

                case DistrictId.Kisla when r < 0.06f:
                {
                    // A flagpole. Nothing says garrison faster.
                    _builder.AddBox(at, new Vector3(0.7f, 0.3f, 0.7f), Hex("#6E7A6A"));
                    _builder.AddBox(at + Vector3.up * 0.3f, new Vector3(0.14f, 6.0f, 0.14f), Hex("#C8C2B4"));
                    _builder.AddBox(at + new Vector3(0.55f, 5.4f, 0), new Vector3(1.1f, 0.7f, 0.06f), Hex("#C43A2A"));
                    break;
                }
                case DistrictId.Kisla when r < 0.16f:
                {
                    // Sandbags and a stack of crates against the wall.
                    for (int i = 0; i < 3; i++)
                        _builder.AddBox(at + new Vector3((i - 1) * 0.62f, 0, 0),
                                        new Vector3(0.68f, 0.4f, 0.5f), Hex("#8C8468"));
                    _builder.AddBox(at + new Vector3(0, 0.4f, 0), new Vector3(0.68f, 0.4f, 0.5f), Hex("#7E7660"));
                    break;
                }
            }
        }

        static readonly Color[] StallCanopy =
        {
            Hex("#C4553F"), Hex("#3E8C6E"), Hex("#D0A24A"), Hex("#8B6BA8"),
        };

        /// <summary>
        /// A market stall: a table, two posts and a coloured canopy over them.
        ///
        /// Sized against a house, not against a real market stall. A tile is four metres and a
        /// house is only two and a bit wide, so a life-sized canopy came out as broad as the
        /// building behind it and the quarter read as a car park full of parasols. Two thirds
        /// of that is small enough to be furniture and big enough to see.
        /// </summary>
        void AddStall(Vector3 at, float hash)
        {
            Vector3 p = at + new Vector3((hash - 0.5f) * 2.4f, 0, (hash - 0.5f) * 2.4f);
            Color canopy = StallCanopy[Mathf.FloorToInt(hash * StallCanopy.Length) % StallCanopy.Length];

            // The posts run from the ground, not from the table top: they were starting at 0.62
            // and the stall stood on nothing, a canopy and a plank hanging in the air.
            _builder.AddBox(p + Vector3.up * 0.42f, new Vector3(1.25f, 0.12f, 0.75f), Hex("#6B5A45"));
            _builder.AddBox(p + new Vector3(-0.52f, 0f, 0), new Vector3(0.09f, 1.4f, 0.09f), Hex("#4A4034"));
            _builder.AddBox(p + new Vector3(0.52f, 0f, 0), new Vector3(0.09f, 1.4f, 0.09f), Hex("#4A4034"));
            // Legs under the table too, so the plank is not levitating either.
            _builder.AddBox(p + new Vector3(-0.5f, 0f, 0.28f), new Vector3(0.08f, 0.42f, 0.08f), Hex("#4A4034"));
            _builder.AddBox(p + new Vector3(0.5f, 0f, 0.28f), new Vector3(0.08f, 0.42f, 0.08f), Hex("#4A4034"));
            _builder.AddBox(p + Vector3.up * 1.28f, new Vector3(1.45f, 0.11f, 0.95f), canopy);
            // Crates under the table, so it reads as a stall with something to sell.
            _builder.AddBox(p + new Vector3(0.3f, 0, 0.28f), new Vector3(0.3f, 0.3f, 0.3f), Hex("#7A6647"));
        }

        /// <summary>
        /// A moored boat. The river was the one part of the map with nothing on it, which made
        /// a delta city read as a city that happened to be next to water rather than one that
        /// lives off it.
        /// </summary>
        void AddBoat(Vector3 at, float hash)
        {
            bool along = hash > 0.5f;
            Vector3 hull = along ? new Vector3(3.4f, 0.55f, 1.3f) : new Vector3(1.3f, 0.55f, 3.4f);

            _builder.AddBox(at + Vector3.up * 0.3f, hull, Hex("#6E5A46"));
            _builder.AddBox(at + Vector3.up * 0.62f, hull * 0.55f, Hex("#8A7458"));
            // A mast, on about half of them, so the moorings are not a row of identical hulls.
            // Stepped on the deck at 0.6, not hung at 2.0 — it used to float clear of the boat.
            if (hash > 0.62f)
                _builder.AddBox(at + Vector3.up * 0.6f, new Vector3(0.12f, 3.6f, 0.12f), Hex("#4A4034"));
        }

        static readonly Color[] Foliage =
        {
            Hex("#4E7A44"), Hex("#5C8A4C"), Hex("#41693C"), Hex("#6B9455"), Hex("#3C5F3A"),
        };

        /// <summary>
        /// A tree: a trunk and two tiers of canopy rather than one cone. Trees cover more of this
        /// map than buildings do, so a single cone repeated four hundred times was the strongest
        /// thing on screen and the least interesting — two tiers and five greens fix both.
        /// </summary>
        void AddTree(Vector3 at, float scale)
        {
            float k = Mathf.Repeat(at.x * 0.31f + at.z * 0.17f, 1f);
            Color leaf = Foliage[Mathf.FloorToInt(k * Foliage.Length) % Foliage.Length];

            _builder.AddBox(at, new Vector3(0.38f * scale, 1.5f * scale, 0.38f * scale), Hex("#5A4433"));
            _builder.AddPyramid(at + Vector3.up * 1.15f * scale, 1.3f * scale, 2.1f * scale,
                                MeshBuilder.Shade(leaf, 0.82f));
            _builder.AddPyramid(at + Vector3.up * 2.05f * scale, 0.95f * scale, 2.0f * scale,
                                MeshBuilder.Shade(leaf, 1.06f));
        }
    }
}






