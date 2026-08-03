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
            public string Shader = "";
        }

        readonly Dictionary<string, Template> _templates = new Dictionary<string, Template>();
        readonly List<GameObject> _spawned = new List<GameObject>();

        GameState _state;
        int _syncedVersion = -1;
        bool _loadTried;

        /// <summary>For the probe: which ids are backed by a real model right now.</summary>
        public bool Covers(string id) => _templates.ContainsKey(id);

        public int LoadedCount => _templates.Count;
        public string FirstShaderName
        {
            get
            {
                foreach (var t in _templates.Values) return t.Shader;
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

            await LoadOne("konut");
            await LoadOne("tapinak");

            // The city was baked with procedural stand-ins before the files finished loading.
            // Rebake once: the covered forms drop out of the bake and the instances go in.
            if (_templates.Count > 0 && CityRenderer.Instance != null)
                CityRenderer.Instance.Rebuild();
        }

        async System.Threading.Tasks.Task LoadOne(string id)
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath,
                                                 "Models", "buildings", id + ".glb");
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

            var root = new GameObject("tpl_" + id);
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

            float widest = Mathf.Max(bounds.size.x, bounds.size.z, 0.01f);
            var tpl = new Template
            {
                Root = root,
                LocalBounds = bounds,
                Scale = Footprint / widest,
                Shader = shaderName,
            };
            root.SetActive(false);
            _templates[id] = tpl;
            Debug.Log($"[BuildingModels] {id} hazır · ölçek {tpl.Scale:0.00} · shader {shaderName}");
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

            foreach (var b in _state.Buildings)
            {
                if (!_templates.TryGetValue(b.Def.Id, out var tpl)) continue;
                if (_state.District(b.District).Lost) continue;

                var ground = CityGrid.World(b.Tile.x, b.Tile.y);
                var go = Instantiate(tpl.Root, transform);
                go.SetActive(true);

                float s = tpl.Scale;
                go.transform.localScale = tpl.Root.transform.localScale * s;

                // Centre the measured bounds on the tile and set their base on the floor.
                var c = tpl.LocalBounds.center * s;
                float baseY = (tpl.LocalBounds.center.y - tpl.LocalBounds.extents.y) * s;
                go.transform.position = new Vector3(ground.x - c.x, ground.y - baseY, ground.z - c.z);

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

        /// <summary>The world bounds an instance would occupy on this tile, for the probe.</summary>
        public bool ProbeBounds(string id, Vector3 ground, out Vector3 min, out Vector3 max)
        {
            min = max = ground;
            if (!_templates.TryGetValue(id, out var tpl)) return false;
            float s = tpl.Scale;
            var half = tpl.LocalBounds.extents * s;
            min = new Vector3(ground.x - half.x, ground.y, ground.z - half.z);
            max = new Vector3(ground.x + half.x, ground.y + half.y * 2f, ground.z + half.z);
            return true;
        }
    }
}
