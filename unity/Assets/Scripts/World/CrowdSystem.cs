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

        Mesh _personMesh, _carMesh, _bannerMesh;
        Material[] _bucketMats;
        Material _bannerMat;

        Matrix4x4[][] _batches, _carBatches;
        int[] _batchCount, _carBatchCount;
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

        public void Init(GameState state, CityGrid grid, Material template)
        {
            _state = state;
            _grid = grid;

            _agents = new NativeArray<CrowdAgent>(MaxAgents, Allocator.Persistent);
            _moods = new NativeArray<DistrictMood>(state.Districts.Length, Allocator.Persistent);

            BuildMeshes();
            BuildMaterials(template);

            _batches = new Matrix4x4[BucketHex.Length][];
            _carBatches = new Matrix4x4[BucketHex.Length][];
            _batchCount = new int[BucketHex.Length];
            _carBatchCount = new int[BucketHex.Length];
            for (int i = 0; i < _batches.Length; i++)
            {
                _batches[i] = new Matrix4x4[MaxAgents];
                _carBatches[i] = new Matrix4x4[MaxAgents];
            }
            _banners = new Matrix4x4[MaxAgents];

            BuildSmoke(template);
            Repopulate();

            Debug.Log($"[Crowd] kişi {_personMesh.vertexCount}v · araba {_carMesh.vertexCount}v · " +
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

            // A bonnet, a cabin set back on it, and a boot: three stacked blocks instead of one,
            // which is all it takes for a box to read as a car from above.
            b.AddBox(new Vector3(0, 0.20f, 0), new Vector3(1.85f, 0.42f, 0.92f), white);
            b.AddBox(new Vector3(0.62f, 0.14f, 0), new Vector3(0.62f, 0.20f, 0.86f), white);   // bonnet
            b.AddBox(new Vector3(-0.72f, 0.16f, 0), new Vector3(0.42f, 0.24f, 0.86f), white);  // boot
            b.AddBox(new Vector3(-0.08f, 0.62f, 0), new Vector3(0.92f, 0.34f, 0.80f), white);  // cabin
            // Glass on both flanks and the windscreen, so it catches the light like a car.
            b.AddBox(new Vector3(-0.08f, 0.70f, 0.41f), new Vector3(0.80f, 0.22f, 0.04f), glass);
            b.AddBox(new Vector3(-0.08f, 0.70f, -0.41f), new Vector3(0.80f, 0.22f, 0.04f), glass);
            b.AddBox(new Vector3(0.39f, 0.70f, 0), new Vector3(0.06f, 0.22f, 0.72f), glass);
            // Lights. Tiny, and the whole reason a night street reads as traffic.
            foreach (float dz in new[] { -0.28f, 0.28f })
            {
                b.AddBox(new Vector3(0.93f, 0.26f, dz), new Vector3(0.10f, 0.14f, 0.20f), lamp);
                b.AddBox(new Vector3(-0.93f, 0.26f, dz), new Vector3(0.08f, 0.12f, 0.18f), tail);
            }
            foreach (float dx in new[] { -0.58f, 0.62f })
            foreach (float dz in new[] { -0.46f, 0.46f })
                b.AddBox(new Vector3(dx, 0, dz), new Vector3(0.34f, 0.30f, 0.16f), tyre);
            _carMesh = b.ToMesh("Car");

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
            // Six puffs per chimney, staggered up the column.
            var stacks = new System.Collections.Generic.List<Vector3>();
            foreach (var bld in _state.Buildings)
            {
                if (bld.Def.Pollution <= 0) continue;
                var at = CityGrid.World(bld.Tile.x, bld.Tile.y);
                stacks.Add(new Vector3(at.x + 1.3f, bld.Def.Storeys * 3.1f + 5.5f, at.z - 1.2f));
            }

            _puffs = new Puff[stacks.Count * 6];
            _puffMatrices = new Matrix4x4[_puffs.Length];
            uint seed = 0x51ED2701u;
            for (int i = 0; i < _puffs.Length; i++)
            {
                _puffs[i] = new Puff
                {
                    Base = stacks[i / 6],
                    Height = Rand(ref seed) * 14f,
                    Speed = 1.4f + Rand(ref seed) * 1.1f,
                    Size = 1.1f + Rand(ref seed) * 1.3f,
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
                if (puff.Height > 16f) puff.Height = 0f;
                _puffs[i] = puff;

                float t = puff.Height / 16f;
                float size = puff.Size * (0.5f + t * 1.9f);
                var pos = puff.Base + new Vector3(t * 3.5f, puff.Height, t * 2.2f);
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

            for (int i = 0; i < wanted; i++)
            {
                int district = i % _state.Districts.Length;
                var bounds = _state.Districts[district].Def.Bounds;

                float x = bounds.xMin + Rand(ref seed) * bounds.width;
                float y = bounds.yMin + Rand(ref seed) * bounds.height;
                Vector3 world = CityGrid.World(Mathf.RoundToInt(x), Mathf.RoundToInt(y));

                bool car = i % 5 == 0;
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
                    // Arrived. Pick the next tile, preferring to carry straight on.
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

            for (int i = 0; i < _batchCount.Length; i++) { _batchCount[i] = 0; _carBatchCount[i] = 0; }
            _bannerCount = 0;

            for (int i = 0; i < _live; i += stride)
            {
                var a = _agents[i];
                if (a.Pos.y < -1f) continue;

                // A walking figure bobs; a standing one does not. It is two lines of code and
                // it is the difference between a crowd and a scatter of boxes.
                float bob = a.Mood == 0 && a.Kind == 0 ? Mathf.Abs(Mathf.Sin(a.Phase * 3f)) * 0.18f : 0f;
                var pos = new Vector3(a.Pos.x, a.Pos.y + bob, a.Pos.z);

                float3 face = a.Target - a.Pos;
                Quaternion rot = math.lengthsq(face) > 0.01f
                    ? Quaternion.LookRotation(new Vector3(face.x, 0, face.z))
                    : Quaternion.identity;

                int bucket = a.Bucket % _batches.Length;
                var trs = Matrix4x4.TRS(pos, rot, Vector3.one);
                if (a.Kind == 1) _carBatches[bucket][_carBatchCount[bucket]++] = trs;
                else _batches[bucket][_batchCount[bucket]++] = trs;

                if (a.Mood == 2 && a.Kind == 0 && (i & 3) == 0 && _bannerCount < _banners.Length)
                    _banners[_bannerCount++] = Matrix4x4.TRS(pos, rot, Vector3.one);
            }

            // Two calls per colour bucket — people and cars — so the entire population of the
            // city costs about a dozen draw calls however many of them there are.
            for (int b = 0; b < _batches.Length; b++)
            {
                if (_batchCount[b] == 0 && _carBatchCount[b] == 0) continue;

                var rp = new RenderParams(_bucketMats[b])
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };

                if (_batchCount[b] > 0)
                    Graphics.RenderMeshInstanced(rp, _personMesh, 0, _batches[b], _batchCount[b]);
                if (_carBatchCount[b] > 0)
                    Graphics.RenderMeshInstanced(rp, _carMesh, 0, _carBatches[b], _carBatchCount[b]);
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








