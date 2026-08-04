// Externally-made building models — the player's own 3D, standing in the city.
//
// Two glTF models live in StreamingAssets/Models/buildings: konut.glb and tapinak.glb.
// They load asynchronously at startup; until they are ready the procedural forms stand in,
// and the moment they arrive the city rebakes with those two forms skipped and one real
// instance parented here per building. Every instance is normalised the same way: scaled so
// its footprint fits the tile, centred, and dropped so its measured bounds touch the ground
// — the same "min.y = 0" bar the probe holds every procedural building to.

using System.Collections.Generic;
using UnityEngine;
using Mesruiyet.Core;
using GLTFast;

namespace Mesruiyet.World
{
    public sealed class BuildingModels : MonoBehaviour
    {
        public static BuildingModels Instance;

        /// <summary>Largest footprint a model may occupy of its 4 m tile.</summary>
        const float Footprint = 3.7f;

        sealed class Template
        {
            public GameObject Root;
            public Bounds LocalBounds;     // combined renderer bounds at scale 1
            public float Scale;
            public float YScale;           // 0 = no storey target, keep the model's proportions
            public string Shader = "";
        }

        // One id can own several variants — tapinak.glb, tapinak-2.glb, tapinak-3.glb are the
        // mosque, the church and the synagogue, and instances cycle through them in build
        // order. The list is sorted by suffix, so the plain file is always the first placed.
        readonly Dictionary<string, List<Template>> _templates = new Dictionary<string, List<Template>>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        GameState _state;
        int _syncedVersion = -1;
        bool _loadTried;

        /// <summary>For the probe: which ids are backed by a real model right now.</summary>
        public bool Covers(string id) => _templates.ContainsKey(id);

        public int LoadedCount
        {
            get
            {
                int n = 0;
                foreach (var list in _templates.Values) n += list.Count;
                return n;
            }
        }
        public IEnumerable<string> CoveredIds => _templates.Keys;
        public string FirstShaderName
        {
            get
            {
                foreach (var list in _templates.Values) return list[0].Shader;
                return "";
            }
        }

        /// <summary>Worst ground clearance across spawned instances — measured, not assumed.</summary>
        public float WorstMinY { get; private set; }

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
            _ = LoadAll();
        }

        async System.Threading.Tasks.Task LoadAll()
        {
            if (_loadTried) return;
            _loadTried = true;

            // The delivery contract from ASSETS.md: every .glb in the folder is named after
            // the building id it replaces — with an optional "-2", "-3" suffix for variants.
            // Drop a file in, it stands in the city — no code. Suffix order decides the cycle,
            // so the files are sorted by (id, variant) before loading, not by raw filename
            // ("tapinak-2" sorts before "tapinak" as a string and would steal the first slot).
            string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Models", "buildings");
            if (System.IO.Directory.Exists(dir))
            {
                var entries = new List<(string id, int variant, string file)>();
                foreach (var file in System.IO.Directory.GetFiles(dir, "*.glb"))
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    string id = name;
                    int variant = 1;
                    var m = System.Text.RegularExpressions.Regex.Match(name, @"^(.+)-(\d+)$");
                    if (m.Success)
                    {
                        id = m.Groups[1].Value;
                        variant = int.Parse(m.Groups[2].Value);
                    }
                    if (Buildings.Get(id) == null)
                    {
                        Debug.LogWarning($"[BuildingModels] '{name}' diye bir yapı yok, atlandı: {file}");
                        continue;
                    }
                    entries.Add((id, variant, file));
                }
                entries.Sort((a, b) => a.id != b.id
                    ? string.CompareOrdinal(a.id, b.id) : a.variant.CompareTo(b.variant));
                foreach (var e in entries)
                    await LoadOne(e.id, e.file);
            }

            // The city was baked with procedural stand-ins before the files finished loading.
            // Rebake once: the covered forms drop out of the bake and the instances go in.
            if (_templates.Count > 0 && CityRenderer.Instance != null)
                CityRenderer.Instance.Rebuild();
        }

        async System.Threading.Tasks.Task LoadOne(string id, string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[BuildingModels] yok: {path}");
                return;
            }

            var import = new GltfImport();
            if (!await import.Load(path))
            {
                Debug.LogWarning($"[BuildingModels] yüklenemedi: {id}");
                return;
            }

            var root = new GameObject("tpl_" + System.IO.Path.GetFileNameWithoutExtension(path));
            root.transform.SetParent(transform, false);
            var instantiator = new GameObjectInstantiator(import, root.transform);
            if (!await import.InstantiateMainSceneAsync(instantiator))
            {
                Debug.LogWarning($"[BuildingModels] kurulamadı: {id}");
                Destroy(root);
                return;
            }

            // Player builds strip glTFast's shaders unless they are in Always Included
            // Shaders (ProjectSetup adds them). If a slot still arrives empty, fall back to
            // the same URP/Lit the crowd skins use — grey beats magenta beats invisible.
            var fallback = Resources.Load<Material>("UnitLit");
            string shaderName = "";
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == null ||
                        mats[i].shader.name.Contains("InternalError"))
                        mats[i] = fallback;
                    else if (shaderName.Length == 0)
                        shaderName = mats[i].shader.name;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Measure once at scale 1; every instance derives from this.
            var bounds = new Bounds(root.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }

            // The base scale fits a 1×1 tile; a building with a bigger footprint scales up
            // per axis at spawn. Kept as one number so every model obeys the same rule.
            float widest = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            float scale = Footprint / widest;

            // Height obeys the data, not the generator's whim: the same storey rule the
            // procedural forms use, so a well stays squat and an apartment block towers.
            // Flat things (fields, the park) keep their own proportions, and the stretch is
            // clamped so no model becomes a chimney or a pancake.
            var def = Buildings.Get(id);
            float yScale = 0f;
            if (def != null && def.Storeys > 0)
            {
                // Homes run lower than the procedural 2.6 m because a model's measured height
                // includes its roof — at 2.6 the two-storey house stretched 39% and read as a
                // townhouse with an elongated door. 2.0 lands the house near its natural
                // proportions and still leaves the four-storey block towering at ~8 m.
                float storeyHeight = def.Category == "konut" ? 2.0f : 3.1f;
                float worldH = Mathf.Max(bounds.size.y, 0.01f) * scale;
                yScale = Mathf.Clamp(def.Storeys * storeyHeight / worldH, 0.55f, 1.8f);
            }

            var tpl = new Template
            {
                Root = root,
                LocalBounds = bounds,
                Scale = scale,
                YScale = yScale,
                Shader = shaderName,
            };
            root.SetActive(false);
            if (!_templates.TryGetValue(id, out var variants))
                _templates[id] = variants = new List<Template>();
            variants.Add(tpl);
            Debug.Log($"[BuildingModels] {root.name.Substring(4)} hazır · ölçek {tpl.Scale:0.00} · kat çarpanı {tpl.YScale:0.00} · shader {shaderName}");
        }

        void Update()
        {
            if (_templates.Count == 0 || CityRenderer.Instance == null) return;
            if (_syncedVersion == CityRenderer.Instance.RebuildVersion) return;
            _syncedVersion = CityRenderer.Instance.RebuildVersion;
            Sync();
        }

        /// <summary>One real instance per covered building, standing exactly on its tile.</summary>
        void Sync()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            WorstMinY = 0f;

            // Variants cycle in build order: the first temple placed is the mosque, the second
            // the church, the third the synagogue, then round again. state.Buildings appends,
            // so the assignment is stable across rebakes — a temple never changes faith.
            var placed = new Dictionary<string, int>();

            foreach (var b in _state.Buildings)
            {
                if (!_templates.TryGetValue(b.Def.Id, out var variants)) continue;
                placed.TryGetValue(b.Def.Id, out int nth);
                placed[b.Def.Id] = nth + 1;
                var tpl = variants[nth % variants.Count];
                if (_state.District(b.District).Lost) continue;

                // Multi-tile buildings stand at the centre of their whole footprint and scale
                // up with it: a 2×2 plant really is four parcels of machine.
                var size = b.Def.Size;
                var ground = CityGrid.World(b.Tile.x, b.Tile.y)
                           + new Vector3((size.x - 1) * CityGrid.TileSize * 0.5f, 0,
                                         (size.y - 1) * CityGrid.TileSize * 0.5f);
                var go = Instantiate(tpl.Root, transform);
                go.SetActive(true);

                // The footprint grows with the parcel; the height does not — a 2×2 plant is
                // four parcels of machine, not a machine twice as tall. Its storeys say how
                // high it stands, same as its procedural neighbours.
                float s = tpl.Scale * Mathf.Min(size.x, size.y);
                float sy = tpl.YScale > 0f ? tpl.Scale * tpl.YScale : s;
                go.transform.localScale = Vector3.Scale(
                    tpl.Root.transform.localScale, new Vector3(s, sy, s));

                // Centre the measured bounds on the tile and set their base on the floor.
                var c = tpl.LocalBounds.center;
                float baseY = (tpl.LocalBounds.center.y - tpl.LocalBounds.extents.y) * sy;
                go.transform.position = new Vector3(ground.x - c.x * s, ground.y - baseY, ground.z - c.z * s);

                // An unworked building goes cold like its procedural neighbours.
                if (!b.Staffed) Tint(go, new Color(0.55f, 0.58f, 0.62f, 1f));

                _spawned.Add(go);

                float dev = MeasuredMinY(go) - ground.y;
                if (Mathf.Abs(dev) > Mathf.Abs(WorstMinY)) WorstMinY = dev;
            }
        }

        static float MeasuredMinY(GameObject go)
        {
            float min = float.MaxValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                min = Mathf.Min(min, r.bounds.min.y);
            return min == float.MaxValue ? 0f : min;
        }

        static void Tint(GameObject go, Color c)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("baseColorFactor", c);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.SetPropertyBlock(block);
        }

        /// <summary>The world bounds an instance would occupy, centred on <paramref name="ground"/>.</summary>
        public bool ProbeBounds(string id, Vector3 ground, out Vector3 min, out Vector3 max)
        {
            min = max = ground;
            if (!_templates.TryGetValue(id, out var variants)) return false;
            var tpl = variants[0];
            var def = Buildings.Get(id);
            float s = tpl.Scale * (def != null ? Mathf.Min(def.Size.x, def.Size.y) : 1);
            float sy = tpl.YScale > 0f ? tpl.Scale * tpl.YScale : s;
            var half = tpl.LocalBounds.extents * s;
            min = new Vector3(ground.x - half.x, ground.y, ground.z - half.z);
            max = new Vector3(ground.x + half.x, ground.y + tpl.LocalBounds.size.y * sy, ground.z + half.z);
            return true;
        }
    }
}
