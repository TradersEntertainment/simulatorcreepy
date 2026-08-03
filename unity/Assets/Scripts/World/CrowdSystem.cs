// The crowd and the traffic — and the main readability channel in the whole game.
//
// This is not decoration. Density and behaviour encode the simulation, and the target the
// design sets is explicit: a player should be able to diagnose a district by watching it for
// five seconds without opening a panel. So:
//
//   · employment fills the streets — people walk, the district looks busy
//   · unemployment leaves clusters standing still
//   · grievance above 70 makes crowds converge on the square and carry banners
//   · a strike empties the industrial quarter completely
//
// Architecture, per CLAUDE.md: no MonoBehaviour per agent, ever. Everything lives in
// NativeArray<struct>, moves in a Burst-compiled IJobParallelFor, and draws through
// RenderMeshInstanced in a handful of colour buckets. Six hundred agents cost one job and
// about a dozen draw calls.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Mesruiyet.Core;

namespace Mesruiyet.World
{
    /// <summary>What a district is doing, as the crowd needs to see it.</summary>
    public struct DistrictMood
    {
        public float3 Centre;
        public float3 Extent;
        /// <summary>0..1 — how many hands are working.</summary>
        public float Employment;
        public float Grievance;
        /// <summary>1 when the quarter has stopped: no staffed workplaces at all.</summary>
        public byte Struck;
    }

    public struct CrowdAgent
    {
        public float3 Pos;
        public float3 Target;
        public float Speed;
        public int District;
        /// <summary>0 pedestrian · 1 car.</summary>
        public byte Kind;
        /// <summary>0 walking · 1 standing idle · 2 marching on the square.</summary>
        public byte Mood;
        public byte Bucket;
        public float Phase;

        // Cars only: the road tile they are on, the one they are driving to, and how long they
        // are held at a junction. Pedestrians ignore all three.
        public int TileX, TileY;
        public int NextX, NextY;
        public float Wait;
    }

    [BurstCompile]
    public struct CrowdJob : IJobParallelFor
    {
        public NativeArray<CrowdAgent> Agents;
        [ReadOnly] public NativeArray<DistrictMood> Moods;
        public float Dt;
        public uint Frame;

        static float Hash01(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xffffff) / 16777215f;
        }

        public void Execute(int i)
        {
            var a = Agents[i];
            if (a.District < 0 || a.District >= Moods.Length) return;
            if (a.Kind == 1) return;                    // cars follow the road graph, not this
            var mood = Moods[a.District];

            // Standing still is a behaviour, not an absence of one: idle clusters are how
            // unemployment looks from the air.
            if (a.Mood == 1)
            {
                a.Phase += Dt;
                Agents[i] = a;
                return;
            }

            float3 delta = a.Target - a.Pos;
            float distance = math.length(delta);

            if (distance < 1.2f)
            {
                uint seed = (uint)(i * 747796405) ^ (Frame * 2891336453u);
                float rx = Hash01(seed) - 0.5f;
                float rz = Hash01(seed * 16807u + 13u) - 0.5f;

                // An angry district converges: everyone heads for the same square, and the
                // crowd thickens there until it is the first thing you notice.
                float3 target = a.Mood == 2
                    ? mood.Centre + new float3(rx * 8f, 0, rz * 8f)
                    : mood.Centre + new float3(rx * 2f * mood.Extent.x, 0, rz * 2f * mood.Extent.z);

                a.Target = target;
            }
            else
            {
                a.Pos += delta / distance * a.Speed * Dt;
            }

            a.Phase += Dt * (a.Kind == 1 ? 0.4f : 1.6f);
            Agents[i] = a;
        }
    }

    public sealed class CrowdSystem : MonoBehaviour
    {
        public const int MaxAgents = 600;

        /// <summary>Six buckets, so the whole crowd is six instanced draw calls.</summary>
        static readonly string[] BucketHex =
        {
            "#E4544A", "#4E8FE0", "#F0C24A", "#EDEFF3", "#3FBF7A", "#C48CFF",
        };

        GameState _state;
        CityGrid _grid;

        NativeArray<CrowdAgent> _agents;
        NativeArray<DistrictMood> _moods;
        int _live;

        Mesh _personMesh, _bannerMesh;

        /// <summary>
        /// Three vehicles rather than one. A street where every car is the same object reads as a
        /// conveyor belt; a saloon, a van and a horse cart read as a city that has more than one
        /// kind of errand. Which one an agent is comes from its own hash, so it never changes.
        /// </summary>
        Mesh[] _carMeshes;
        const int CarKinds = 3;
        Material[] _bucketMats;
        Material _bannerMat;

        Matrix4x4[][] _batches;
        int[] _batchCount;
        // Cars are batched by [vehicle kind][colour bucket]: one draw call each, so three
        // silhouettes cost three times a handful rather than anything that shows up in a frame.
        Matrix4x4[][][] _carBatches;
        int[][] _carBatchCount;
        Matrix4x4[] _banners;
        int _bannerCount;

        // Smoke over the industrial quarter. Opaque low-poly puffs rather than a particle
        // system: it matches the toy-city look, needs no extra shader or material to survive
        // variant stripping, and rides the instanced draw path that already exists.
        struct Puff { public Vector3 Base; public float Height; public float Speed; public float Size; }
        Puff[] _puffs = new Puff[0];
        Matrix4x4[] _puffMatrices = new Matrix4x4[0];
        Mesh _puffMesh;
        Material _smokeMat;

        System.Collections.Generic.List<Vector2Int> _roads;

        JobHandle _handle;
        uint _frame;

        public int LiveAgents => _live;

        /// <summary>
        /// Agents currently represented by a skinned model instead of the instanced boxes.
        /// CrowdSkins claims the ones nearest the camera; the draw loop skips them so nobody is
        /// on screen twice. The simulation neither knows nor cares which representation runs.
        /// </summary>
        readonly bool[] _skinClaims = new bool[MaxAgents];

        public void SetSkinClaim(int index, bool claimed)
        {
            if (index >= 0 && index < _skinClaims.Length) _skinClaims[index] = claimed;
        }

        public CrowdAgent AgentAt(int index) => _agents[index];

        public void Init(GameState state, CityGrid grid, Material template)
        {
            _state = state;
            _grid = grid;

            _agents = new NativeArray<CrowdAgent>(MaxAgents, Allocator.Persistent);
            _moods = new NativeArray<DistrictMood>(state.Districts.Length, Allocator.Persistent);

            BuildMeshes();
            BuildMaterials(template);

            _batches = new Matrix4x4[BucketHex.Length][];
            _carBatches = new Matrix4x4[CarKinds][][];
            _carBatchCount = new int[CarKinds][];
            for (int k = 0; k < CarKinds; k++)
            {
                _carBatches[k] = new Matrix4x4[BucketHex.Length][];
                _carBatchCount[k] = new int[BucketHex.Length];
                for (int i = 0; i < BucketHex.Length; i++) _carBatches[k][i] = new Matrix4x4[MaxAgents];
            }
            _batchCount = new int[BucketHex.Length];

            for (int i = 0; i < _batches.Length; i++)
            {
                _batches[i] = new Matrix4x4[MaxAgents];

            }
            _banners = new Matrix4x4[MaxAgents];

            BuildSmoke(template);
            Repopulate();

            Debug.Log($"[Crowd] kişi {_personMesh.vertexCount}v · araba {_carMeshes[0].vertexCount}v · " +
                      $"pankart {_bannerMesh.vertexCount}v · shader {template.shader.name} · " +
                      $"instancing {_bucketMats[0].enableInstancing} · ajan {_live}");
        }

        void OnDestroy()
        {
            _handle.Complete();
            if (_agents.IsCreated) _agents.Dispose();
            if (_moods.IsCreated) _moods.Dispose();
        }

        // ---------------------------------------------------------------- geometry

        void BuildMeshes()
        {
            // Both are still built from the same primitives as everything else — there is not one
            // imported asset in this project — but a person was two stacked boxes and a car was
            // three, and at this camera distance that reads as gravel. The proportions below are
            // deliberately toy-like: a big head on a short body is what makes a two-centimetre
            // figure legible and, not incidentally, likeable.
            var white = Color.white;                       // tinted per colour bucket at draw time
            var skin = new Color(0.93f, 0.76f, 0.60f);
            var hair = new Color(0.24f, 0.18f, 0.15f);
            var shoe = new Color(0.16f, 0.14f, 0.13f);

            var b = new MeshBuilder();
            // Legs, kept dark so the figure has a base and does not float.
            b.AddBox(new Vector3(-0.13f, 0f, 0), new Vector3(0.19f, 0.34f, 0.22f), shoe);
            b.AddBox(new Vector3(0.13f, 0f, 0), new Vector3(0.19f, 0.34f, 0.22f), shoe);
            // Body, slightly tapered by stacking a narrower block on a wider one.
            b.AddBox(new Vector3(0, 0.34f, 0), new Vector3(0.52f, 0.52f, 0.40f), white);
            b.AddBox(new Vector3(0, 0.86f, 0), new Vector3(0.46f, 0.14f, 0.36f), white);
            // Arms, out at the sides so the silhouette is not a pillar.
            b.AddBox(new Vector3(-0.32f, 0.42f, 0), new Vector3(0.13f, 0.44f, 0.18f), white);
            b.AddBox(new Vector3(0.32f, 0.42f, 0), new Vector3(0.13f, 0.44f, 0.18f), white);
            // Head — oversized on purpose — with a cap of hair.
            b.AddBox(new Vector3(0, 1.0f, 0), new Vector3(0.44f, 0.40f, 0.40f), skin);
            b.AddBox(new Vector3(0, 1.33f, 0), new Vector3(0.47f, 0.11f, 0.43f), hair);
            _personMesh = b.ToMesh("Person");

            b.Clear();
            var glass = new Color(0.40f, 0.52f, 0.64f);
            var tyre = new Color(0.12f, 0.13f, 0.15f);
            var lamp = new Color(1f, 0.92f, 0.72f, 0f);     // alpha 0 → self-lit headlights
            var tail = new Color(1f, 0.36f, 0.28f, 0f);

            // Every vehicle is built with its NOSE ALONG +Z, because the draw loop orients with
            // LookRotation and LookRotation turns +Z towards the direction of travel. The first
            // versions were long along X, so every car on the map drove sideways — and the probe
            // now reads the nose off the mesh's own longest axis, so this convention is enforced
            // by measurement, not by this comment.

            // A bonnet, a cabin set back on it, and a boot: three stacked blocks instead of one,
            // which is all it takes for a box to read as a car from above.
            b.AddBox(new Vector3(0, 0.20f, 0), new Vector3(0.92f, 0.42f, 1.85f), white);
            b.AddBox(new Vector3(0, 0.14f, 0.62f), new Vector3(0.86f, 0.20f, 0.62f), white);   // bonnet
            b.AddBox(new Vector3(0, 0.16f, -0.72f), new Vector3(0.86f, 0.24f, 0.42f), white);  // boot
            b.AddBox(new Vector3(0, 0.62f, -0.08f), new Vector3(0.80f, 0.34f, 0.92f), white);  // cabin
            // Glass on both flanks and the windscreen, so it catches the light like a car.
            b.AddBox(new Vector3(0.41f, 0.70f, -0.08f), new Vector3(0.04f, 0.22f, 0.80f), glass);
            b.AddBox(new Vector3(-0.41f, 0.70f, -0.08f), new Vector3(0.04f, 0.22f, 0.80f), glass);
            b.AddBox(new Vector3(0, 0.70f, 0.39f), new Vector3(0.72f, 0.22f, 0.06f), glass);
            // Lights. Tiny, and the whole reason a night street reads as traffic.
            foreach (float dx in new[] { -0.28f, 0.28f })
            {
                b.AddBox(new Vector3(dx, 0.26f, 0.93f), new Vector3(0.20f, 0.14f, 0.10f), lamp);
                b.AddBox(new Vector3(dx, 0.26f, -0.93f), new Vector3(0.18f, 0.12f, 0.08f), tail);
            }
            foreach (float dz in new[] { -0.58f, 0.62f })
            foreach (float dx in new[] { -0.46f, 0.46f })
                b.AddBox(new Vector3(dx, 0, dz), new Vector3(0.16f, 0.30f, 0.34f), tyre);
            var saloon = b.ToMesh("Car");

            // ---- a van: same nose, a tall box behind it, and a load door at the back.
            b.Clear();
            b.AddBox(new Vector3(0, 0.20f, 0.55f), new Vector3(0.92f, 0.44f, 0.95f), white);
            b.AddBox(new Vector3(0, 0.64f, 0.62f), new Vector3(0.84f, 0.42f, 0.72f), white);
            b.AddBox(new Vector3(0, 0.72f, 0.95f), new Vector3(0.7f, 0.26f, 0.06f), glass);
            b.AddBox(new Vector3(0, 0.20f, -0.42f), new Vector3(0.96f, 1.05f, 1.5f), white);
            b.AddBox(new Vector3(0, 0.36f, -1.16f), new Vector3(0.78f, 0.62f, 0.05f), MeshBuilder.Shade(white, 0.7f));
            foreach (float dx in new[] { -0.28f, 0.28f })
            {
                b.AddBox(new Vector3(dx, 0.26f, 1.03f), new Vector3(0.20f, 0.14f, 0.09f), lamp);
                b.AddBox(new Vector3(dx, 0.26f, -1.18f), new Vector3(0.18f, 0.12f, 0.07f), tail);
            }
            foreach (float dz in new[] { -0.72f, 0.72f })
            foreach (float dx in new[] { -0.47f, 0.47f })
                b.AddBox(new Vector3(dx, 0, dz), new Vector3(0.17f, 0.32f, 0.36f), tyre);
            var van = b.ToMesh("Van");

            // ---- a horse cart: two big wheels, an open bed, a pole and a horse in front. Slow,
            // and the reason the delta reads as a place with more than one century in it.
            b.Clear();
            var timber = new Color(0.55f, 0.42f, 0.30f);
            var horse = new Color(0.42f, 0.31f, 0.24f);
            b.AddBox(new Vector3(0, 0.42f, -0.35f), new Vector3(0.86f, 0.16f, 1.35f), timber);
            foreach (float dx in new[] { -0.44f, 0.44f })
                b.AddBox(new Vector3(dx, 0.58f, -0.35f), new Vector3(0.08f, 0.34f, 1.35f), MeshBuilder.Shade(timber, 1.1f));
            b.AddBox(new Vector3(0, 0.58f, -0.98f), new Vector3(0.86f, 0.34f, 0.08f), MeshBuilder.Shade(timber, 0.9f));
            foreach (float dx in new[] { -0.47f, 0.47f })
                b.AddBox(new Vector3(dx, 0, -0.35f), new Vector3(0.12f, 0.62f, 0.62f), tyre);
            b.AddBox(new Vector3(0, 0.48f, 0.42f), new Vector3(0.10f, 0.08f, 0.85f), MeshBuilder.Shade(timber, 0.8f));
            b.AddBox(new Vector3(0, 0, 1.0f), new Vector3(0.45f, 0.95f, 0.85f), horse);
            b.AddBox(new Vector3(0, 0.95f, 1.42f), new Vector3(0.32f, 0.34f, 0.30f), horse);
            var cart = b.ToMesh("Cart");

            _carMeshes = new[] { saloon, van, cart };

            // A banner on a pole. Only marching crowds carry them, so seeing one at all is
            // the signal — you never have to read the number to know a quarter has had enough.
            b.Clear();
            b.AddBox(new Vector3(0, 1.5f, 0), new Vector3(0.09f, 1.5f, 0.09f), new Color(0.2f, 0.16f, 0.12f));
            b.AddBox(new Vector3(0.4f, 2.5f, 0), new Vector3(0.8f, 0.55f, 0.06f), Color.white);
            _bannerMesh = b.ToMesh("Banner");
        }

        void BuildMaterials(Material template)
        {
            _bucketMats = new Material[BucketHex.Length];
            for (int i = 0; i < BucketHex.Length; i++)
            {
                ColorUtility.TryParseHtmlString(BucketHex[i], out var c);
                _bucketMats[i] = new Material(template) { name = "Crowd " + i, enableInstancing = true };
                _bucketMats[i].SetColor("_Tint", c);
            }
            _bannerMat = new Material(template) { name = "Banner", enableInstancing = true };
            _bannerMat.SetColor("_Tint", new Color(0.88f, 0.25f, 0.18f));
        }

        // ---------------------------------------------------------------- smoke

        void BuildSmoke(Material template)
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0, 0, 0), new Vector3(1f, 1f, 1f), Color.white, includeBottom: true);
            _puffMesh = b.ToMesh("Puff");

            _smokeMat = new Material(template) { name = "Smoke", enableInstancing = true };
            _smokeMat.SetColor("_Tint", new Color(0.72f, 0.76f, 0.80f));

            RescanStacks();
        }

        /// <summary>Re-find the chimneys. Called whenever the city changes, so a new works smokes.</summary>
        void RescanStacks()
        {
            // Three puffs per chimney, small and low. The first version ran six per stack up to
            // 25 units in the air at sizes past 3, and from a 40° camera those projected right
            // across the map as soft warm-grey banks — measured off a screenshot as brown-grey
            // overlays on the grass, and read by everyone who looked as "muddy smears". Smoke
            // must read as a plume you can trace to its chimney, not as weather.
            var stacks = new System.Collections.Generic.List<Vector3>();
            foreach (var bld in _state.Buildings)
            {
                if (bld.Def.Pollution <= 0) continue;
                var at = CityGrid.World(bld.Tile.x, bld.Tile.y);
                stacks.Add(new Vector3(at.x + 1.3f, bld.Def.Storeys * 3.1f + 5.5f, at.z - 1.2f));
            }

            _puffs = new Puff[stacks.Count * 3];
            _puffMatrices = new Matrix4x4[_puffs.Length];
            uint seed = 0x51ED2701u;
            for (int i = 0; i < _puffs.Length; i++)
            {
                _puffs[i] = new Puff
                {
                    Base = stacks[i / 3],
                    Height = Rand(ref seed) * 7f,
                    Speed = 1.0f + Rand(ref seed) * 0.8f,
                    Size = 0.55f + Rand(ref seed) * 0.6f,
                };
            }
        }

        void DrawSmoke()
        {
            if (_puffs.Length == 0) return;

            // Emission scales with Kirlilik: a clean city has a thin wisp, a filthy one a plume
            // you can see from the far side of the map.
            float pollution = Mathf.Clamp01(_state.Kirlilik / 45f);
            int count = Mathf.RoundToInt(_puffs.Length * pollution);
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                var puff = _puffs[i];
                puff.Height += puff.Speed * Time.deltaTime;
                if (puff.Height > 8f) puff.Height = 0f;
                _puffs[i] = puff;

                float t = puff.Height / 8f;
                float size = puff.Size * (0.6f + t * 0.9f);
                var pos = puff.Base + new Vector3(t * 1.6f, puff.Height, t * 0.9f);
                _puffMatrices[i] = Matrix4x4.TRS(pos, Quaternion.Euler(0, t * 90f, 0), Vector3.one * size);
            }

            var rp = new RenderParams(_smokeMat)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
            };
            Graphics.RenderMeshInstanced(rp, _puffMesh, 0, _puffMatrices, count);
        }

        // ---------------------------------------------------------------- population

        static float Rand(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xffffff) / 16777215f;
        }

        /// <summary>
        /// Resize the crowd to the city. Fleet and footfall both scale with population, capped
        /// at six hundred — a bigger world is not more agents, it is more statistics.
        /// </summary>
        public void Repopulate()
        {
            RescanStacks();
            if (_roads == null || _roads.Count == 0) _roads = _grid.AllRoadTiles();

            int wanted = Mathf.Clamp(_state.Population / 3, 40, MaxAgents);
            uint seed = 0x9E3779B9u;

            // "Fleet size scales with commerce and population", §3. Population was already in
            // `wanted`; commerce was not, so a trading city and a subsistence one put the same
            // number of cars on the same streets. One in eight when nothing is moving, one in
            // three when the market quarters are full — visible from the air without a panel.
            int trade = 0;
            foreach (var b in _state.Buildings)
                if (b.Staffed && b.Def.Output[(int)Res.Para] > 0) trade++;
            int carEvery = Mathf.Clamp(8 - trade / 3, 3, 8);

            for (int i = 0; i < wanted; i++)
            {
                int district = i % _state.Districts.Length;
                var bounds = _state.Districts[district].Def.Bounds;

                float x = bounds.xMin + Rand(ref seed) * bounds.width;
                float y = bounds.yMin + Rand(ref seed) * bounds.height;
                Vector3 world = CityGrid.World(Mathf.RoundToInt(x), Mathf.RoundToInt(y));

                bool car = i % carEvery == 0;
                var agent = new CrowdAgent
                {
                    Pos = new float3(world.x, car ? 0.05f : 0f, world.z),
                    Target = new float3(world.x, 0, world.z),
                    Speed = car ? 7f + Rand(ref seed) * 4f : 2.2f + Rand(ref seed) * 1.4f,
                    District = district,
                    Kind = (byte)(car ? 1 : 0),
                    Bucket = (byte)(Rand(ref seed) * BucketHex.Length),
                    Phase = Rand(ref seed) * 10f,
                };

                // A car has to start on a road, or it has no graph to drive. Prefer a road in
                // its own district, so the traffic a quarter generates is the traffic it gets.
                if (car && _roads.Count > 0)
                {
                    Vector2Int tile = _roads[Mathf.FloorToInt(Rand(ref seed) * _roads.Count) % _roads.Count];
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        var candidate = _roads[Mathf.FloorToInt(Rand(ref seed) * _roads.Count) % _roads.Count];
                        if (bounds.Contains(candidate)) { tile = candidate; break; }
                    }

                    var at = CityGrid.World(tile.x, tile.y, 0.05f);
                    agent.Pos = new float3(at.x, 0.05f, at.z);
                    agent.TileX = tile.x; agent.TileY = tile.y;

                    int n = _grid.RoadNeighbours(tile.x, tile.y, _neighbours);
                    var next = n > 0 ? _neighbours[Mathf.FloorToInt(Rand(ref seed) * n) % n] : tile;
                    agent.NextX = next.x; agent.NextY = next.y;

                    // Facing, on the first frame as on every other one.
                    var aim = CityGrid.World(next.x, next.y, 0.05f);
                    agent.Target = new float3(aim.x, aim.y, aim.z);
                }

                _agents[i] = agent;
            }
            _live = wanted;
        }

        /// <summary>
        /// Read the districts and tell the crowd how to behave. Called on every tick and every
        /// build, because this is the moment the simulation becomes something you can look at.
        /// </summary>
        public void SyncMoods()
        {
            for (int d = 0; d < _state.Districts.Length; d++)
            {
                var district = _state.Districts[d];
                var bounds = district.Def.Bounds;

                Vector3 centre = CityGrid.World(bounds.xMin + bounds.width / 2,
                                                bounds.yMin + bounds.height / 2);

                int workplaces = 0, working = 0;
                foreach (var b in _state.Buildings)
                {
                    if (b.District != district.Id || b.Def.Workers <= 0) continue;
                    workplaces++;
                    if (b.Staffed) working++;
                }

                _moods[d] = new DistrictMood
                {
                    Centre = new float3(centre.x, 0, centre.z),
                    Extent = new float3(bounds.width * CityGrid.TileSize * 0.42f, 0,
                                        bounds.height * CityGrid.TileSize * 0.42f),
                    Employment = workplaces == 0 ? 1f : working / (float)workplaces,
                    Grievance = district.Grievance,
                    Struck = (byte)(workplaces > 0 && working == 0 ? 1 : 0),
                };
            }

            AssignBehaviour();
        }

        void AssignBehaviour()
        {
            uint seed = 0x2545F491u;

            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.District < 0) continue;
                var mood = _moods[a.District];

                // A struck quarter empties: its people are simply not on the street.
                if (mood.Struck == 1 && a.Kind == 0)
                {
                    a.Mood = 1;
                    a.Pos = new float3(a.Pos.x, -50f, a.Pos.z);      // parked out of sight
                    _agents[i] = a;
                    continue;
                }
                if (a.Pos.y < -1f) a.Pos = new float3(a.Pos.x, a.Kind == 1 ? 0.05f : 0f, a.Pos.z);

                if (mood.Grievance >= 70f && a.Kind == 0) a.Mood = 2;          // converge, carry banners
                else if (Rand(ref seed) > mood.Employment && a.Kind == 0) a.Mood = 1;   // idle
                else a.Mood = 0;

                _agents[i] = a;
            }
        }

        // ---------------------------------------------------------------- traffic
        //
        // Cars drive the road graph one tile at a time: straight on where they can, a turn
        // where they must, never an immediate reversal unless the street is a dead end. They
        // pause at junctions and they queue behind each other, so a district with more traffic
        // than street jams visibly — which is exactly what the simulation says is happening.

        readonly Vector2Int[] _neighbours = new Vector2Int[4];
        int[] _occupancy;

        void DriveCars(float dt)
        {
            if (_occupancy == null) _occupancy = new int[CityGrid.Width * CityGrid.Height];
            System.Array.Clear(_occupancy, 0, _occupancy.Length);

            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Kind != 1) continue;
                if (CityGrid.InBounds(a.NextX, a.NextY)) _occupancy[CityGrid.Index(a.NextX, a.NextY)]++;
            }

            uint seed = _frame * 2654435761u + 17u;

            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Kind != 1) continue;

                if (a.Wait > 0f)
                {
                    a.Wait -= dt;
                    _agents[i] = a;
                    continue;
                }

                var from = CityGrid.World(a.TileX, a.TileY, 0.05f);
                var to = CityGrid.World(a.NextX, a.NextY, 0.05f);

                // Queue: a car whose target tile already holds others crawls instead of
                // driving. Enough of them on one street and the street stops.
                int ahead = CityGrid.InBounds(a.NextX, a.NextY)
                    ? _occupancy[CityGrid.Index(a.NextX, a.NextY)] : 1;
                float speed = a.Speed / Mathf.Max(1f, ahead * 0.8f);

                float3 delta = new float3(to.x, to.y, to.z) - a.Pos;
                float distance = math.length(delta);

                if (distance < 0.6f)
                {
                    // Arrived. Snap to the centre of the tile before choosing the next one.
                    //
                    // Without this the car turned from wherever it happened to be when it came
                    // within 0.6 of the target — still short of the junction — so a right angle
                    // was driven as a diagonal cut across the corner, and the error carried into
                    // the next leg and the next. Roads are four-connected, so a leg that starts
                    // at a tile centre is always exactly along one axis.
                    a.Pos = new float3(to.x, to.y, to.z);

                    int dx = a.NextX - a.TileX, dy = a.NextY - a.TileY;
                    a.TileX = a.NextX; a.TileY = a.NextY;

                    int n = _grid.RoadNeighbours(a.TileX, a.TileY, _neighbours);
                    if (n == 0) { _agents[i] = a; continue; }

                    int straightX = a.TileX + dx, straightY = a.TileY + dy;
                    int chosen = -1;
                    for (int k = 0; k < n; k++)
                        if (_neighbours[k].x == straightX && _neighbours[k].y == straightY) chosen = k;

                    // A junction is a decision and a pause; a straight run is neither.
                    bool junction = n >= 3;
                    if (junction || chosen < 0)
                    {
                        int tries = 0;
                        do
                        {
                            chosen = Mathf.FloorToInt(Rand(ref seed) * n) % n;
                            tries++;
                        }
                        while (tries < 6 && n > 1 &&
                               _neighbours[chosen].x == a.TileX - dx && _neighbours[chosen].y == a.TileY - dy);

                        if (junction) a.Wait = 0.35f + Rand(ref seed) * 0.5f;
                    }

                    a.NextX = _neighbours[chosen].x;
                    a.NextY = _neighbours[chosen].y;

                    // Face where it is actually going. The draw loop takes its rotation from
                    // Target, and Target is only ever written by the Burst job — which skips
                    // cars. So every car pointed at a wander destination it was not driving to,
                    // and slid down the street crabwise.
                    var aim = CityGrid.World(a.NextX, a.NextY, 0.05f);
                    a.Target = new float3(aim.x, aim.y, aim.z);
                }
                else
                {
                    a.Pos += delta / distance * speed * dt;
                }

                a.Phase += dt;
                _agents[i] = a;
            }
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (_live == 0) return;

            _handle.Complete();
            _frame++;

            var job = new CrowdJob
            {
                Agents = _agents,
                Moods = _moods,
                Dt = Time.deltaTime,
                Frame = _frame,
            };
            _handle = job.Schedule(_live, 64);
            _handle.Complete();

            DriveCars(Time.deltaTime);
            Draw();
            DrawSmoke();
        }

        void Draw()
        {
            // Thin the crowd out as the camera pulls back: at full zoom the individual figures
            // are below a pixel and only the density reads anyway.
            int stride = 1;
            var cam = IsoCamera.Instance;
            if (cam != null && cam.Cam != null)
            {
                float size = cam.Cam.orthographicSize;
                stride = size > 90f ? 4 : size > 68f ? 2 : 1;
            }

            for (int i = 0; i < _batchCount.Length; i++) _batchCount[i] = 0;
            for (int k = 0; k < CarKinds; k++)
                for (int i = 0; i < _carBatchCount[k].Length; i++) _carBatchCount[k][i] = 0;
            _bannerCount = 0;

            for (int i = 0; i < _live; i += stride)
            {
                var a = _agents[i];
                if (a.Pos.y < -1f) continue;
                if (a.Kind == 0 && _skinClaims[i]) continue;   // drawn as a skinned model instead

                // A walking figure sways; a standing one does not. This used to be a vertical
                // bob, which lifted every walker up to 18 cm off the street — feet in the air,
                // measured by the probe. A slight roll around the walk axis reads as the same
                // life without ever breaking contact with the ground.
                var pos = new Vector3(a.Pos.x, a.Pos.y, a.Pos.z);

                float3 face = a.Target - a.Pos;
                Quaternion rot = math.lengthsq(face) > 0.01f
                    ? Quaternion.LookRotation(new Vector3(face.x, 0, face.z))
                    : Quaternion.identity;
                if (a.Kind == 0 && a.Mood == 0)
                    rot *= Quaternion.Euler(0, 0, Mathf.Sin(a.Phase * 6f) * 4f);

                int bucket = a.Bucket % _batches.Length;
                var trs = Matrix4x4.TRS(pos, rot, Vector3.one);
                if (a.Kind == 1)
                {
                    // Which vehicle. From the agent's index, which does not change while it
                    // lives — Phase was the obvious choice and the wrong one, because DriveCars
                    // advances it every frame and the van kept turning into a cart.
                    int kind = i % CarKinds;
                    _carBatches[kind][bucket][_carBatchCount[kind][bucket]++] = trs;
                }
                else _batches[bucket][_batchCount[bucket]++] = trs;

                if (a.Mood == 2 && a.Kind == 0 && (i & 3) == 0 && _bannerCount < _banners.Length)
                    _banners[_bannerCount++] = Matrix4x4.TRS(pos, rot, Vector3.one);
            }

            // Two calls per colour bucket — people and cars — so the entire population of the
            // city costs about a dozen draw calls however many of them there are.
            for (int b = 0; b < _batches.Length; b++)
            {
                bool anyCar = false;
                for (int k = 0; k < CarKinds; k++) if (_carBatchCount[k][b] > 0) anyCar = true;
                if (_batchCount[b] == 0 && !anyCar) continue;

                var rp = new RenderParams(_bucketMats[b])
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };

                if (_batchCount[b] > 0)
                    Graphics.RenderMeshInstanced(rp, _personMesh, 0, _batches[b], _batchCount[b]);
                for (int k = 0; k < CarKinds; k++)
                    if (_carBatchCount[k][b] > 0)
                        Graphics.RenderMeshInstanced(rp, _carMeshes[k], 0, _carBatches[k][b], _carBatchCount[k][b]);
            }


            if (_bannerCount > 0)
            {
                var rp = new RenderParams(_bannerMat)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                Graphics.RenderMeshInstanced(rp, _bannerMesh, 0, _banners, _bannerCount);
            }
        }

        /// <summary>One car, as the probe measures it: where it is, where it points, where the road goes.</summary>
        public struct CarProbe
        {
            public Vector3 Pos;
            /// <summary>The facing the draw loop actually uses, flattened to the ground plane.</summary>
            public Vector3 Forward;
            /// <summary>Direction of the road segment it is driving, from its tile to its next.</summary>
            public Vector3 RoadDir;
            /// <summary>Forward · RoadDir. 1 means it points where it is going.</summary>
            public float Dot;
        }

        /// <summary>One pedestrian: position, the drawn lowest point, and speed.</summary>
        public struct PedProbe
        {
            public Vector3 Pos;
            /// <summary>World y of the figure's lowest vertex as it is drawn this frame.</summary>
            public float MinY;
            public float Speed;
        }

        /// <summary>
        /// Measure the cars. Fills up to <paramref name="into"/>.Length entries, returns the count
        /// and the worst (lowest) dot across ALL cars — the pass condition is about every car,
        /// not the sample the report prints.
        /// </summary>
        public int ProbeCars(CarProbe[] into, out float worstDot)
        {
            int n = 0;
            worstDot = 1f;
            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Kind != 1 || a.Pos.y < -1f) continue;

                // The rotation, exactly as the draw loop builds it...
                float3 face = a.Target - a.Pos;
                var aim = new Vector3(face.x, 0, face.z);
                aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;
                var rot = Quaternion.LookRotation(aim);

                // ...applied to the NOSE OF THE MESH, not to the rotation's own forward. The
                // first version of this probe measured LookRotation's input and reported dot 1.0
                // while every car on screen slid sideways: the meshes were built long along X and
                // the rotation turns +Z. The nose is wherever the mesh is longest, so the probe
                // reads that off the mesh itself and cannot agree with a wrong axis again.
                var mesh = _carMeshes != null ? _carMeshes[i % CarKinds] : null;
                Vector3 noseLocal = mesh != null && mesh.bounds.extents.x > mesh.bounds.extents.z
                    ? Vector3.right : Vector3.forward;
                var fwd = rot * noseLocal;

                var road = new Vector3(a.NextX - a.TileX, 0, a.NextY - a.TileY);
                road = road.sqrMagnitude > 0.0001f ? road.normalized : fwd;

                float dot = Vector3.Dot(fwd, road);
                if (dot < worstDot) worstDot = dot;

                if (n < into.Length)
                    into[n] = new CarProbe { Pos = a.Pos, Forward = fwd, RoadDir = road, Dot = dot };
                n++;
            }
            return Mathf.Min(n, into.Length);
        }

        /// <summary>
        /// Measure the pedestrians: the drawn lowest point of each figure, bob included, because
        /// the claim under test is about what is on screen rather than about the data.
        /// </summary>
        public int ProbePedestrians(PedProbe[] into, out float worstMinY)
        {
            float meshMinY = _personMesh != null ? _personMesh.bounds.min.y : 0f;
            int n = 0;
            worstMinY = 0f;
            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Kind != 0 || a.Pos.y < -1f) continue;

                // The draw applies no vertical offset any more — the sway is a roll, which tilts
                // the figure without lifting it. So the drawn lowest point is just this.
                float minY = a.Pos.y + meshMinY;
                if (Mathf.Abs(minY) > Mathf.Abs(worstMinY)) worstMinY = minY;

                if (n < into.Length)
                    into[n] = new PedProbe { Pos = a.Pos, MinY = minY, Speed = a.Speed };
                n++;
            }
            return Mathf.Min(n, into.Length);
        }

        /// <summary>
        /// Cars standing on something that is not a road. Should always be zero: they are driven
        /// tile to tile along a four-connected graph. Any other answer means one of them left the
        /// carriageway — which is what cutting a corner diagonally looks like, measured.
        /// </summary>
        public int CarsOffRoad()
        {
            int off = 0;
            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Kind != 1 || a.Pos.y < -1f) continue;
                if (!CityGrid.Tile(new Vector3(a.Pos.x, 0, a.Pos.z), out var tile)) { off++; continue; }
                if (_grid.At(tile.x, tile.y) != TileKind.Yol) off++;
            }
            return off;
        }

        /// <summary>How the crowd reads right now, for the agent loop to assert against.</summary>
        public void Census(out int walking, out int idle, out int marching, out int hidden)
        {
            walking = idle = marching = hidden = 0;
            for (int i = 0; i < _live; i++)
            {
                var a = _agents[i];
                if (a.Pos.y < -1f) { hidden++; continue; }
                if (a.Mood == 2) marching++;
                else if (a.Mood == 1) idle++;
                else walking++;
            }
        }
    }
}








