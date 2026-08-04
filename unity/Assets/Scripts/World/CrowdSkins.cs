// The skinned characters: real glTF models for the people nearest the camera.
//
// The instanced box-figures stay — they are what lets six hundred agents cost a dozen draw
// calls — but skinned, animated characters cannot be drawn that way, so the budget is explicit:
// at most SkinnedCap civilians near the camera focus get a real model, everyone further keeps
// the cheap representation, and the hand-off is a pool reassignment, not a spawn.
//
// Models load at runtime with glTFast from StreamingAssets. Nothing here is an imported asset
// in the Unity sense: the .glb ships as a raw file, the loader builds the hierarchy in play
// mode, and the only editor-made piece is the UnitLit material in Resources — because glTFast's
// own shaders get stripped from a build whose scene references nothing.

using System.Collections.Generic;
using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.World
{
    public sealed class CrowdSkins : MonoBehaviour
    {
        public static CrowdSkins Instance { get; private set; }

        /// <summary>The whole performance contract: never more skinned civilians than this.</summary>
        public const int SkinnedCap = 60;
        /// <summary>Garrison guards at the barracks, likewise capped.</summary>
        public const int SoldierCap = 8;

        /// <summary>
        /// glTF is authored in metres and a human is ~1.8 of them; our figures are toy-scaled.
        /// This is the ONE place the conversion lives — every instance is normalised so its
        /// renderer stands exactly this tall, whatever the source file says.
        /// </summary>
        public const float TargetHeight = 1.55f;

        // Two rigs, two clip vocabularies: the KayKit adventurers ("Walking_A") and the older
        // infantry family ("CharacterArmature|Walk"). Each action tries its candidates in
        // order at play time, so swapping the .glb files never needs a code change here.
        static readonly string[] ClipWalk = { "Walking_A", "CharacterArmature|Walk" };
        static readonly string[] ClipIdle = { "Idle", "CharacterArmature|Idle_Neutral" };
        static readonly string[] ClipWave = { "Cheer", "CharacterArmature|Wave" };
        static readonly string[] ClipInteract = { "Interact", "CharacterArmature|Interact" };
        static readonly string[] ClipGuard = { "Blocking", "CharacterArmature|Idle_Gun" };

        /// <summary>First candidate the rig actually has — the seam between the two families.</summary>
        static string Resolve(Animation anim, string[] candidates)
        {
            foreach (var name in candidates)
                if (anim.GetClip(name) != null) return name;
            return null;
        }

        GameState _state;
        CrowdSystem _crowd;

        GameObject _civilianTemplate, _soldierTemplate;
        float _scale = 1f;
        // The KayKit rigs carry their pivot above the soles, so a clone placed at ground level
        // sinks to the ankles. Measured once on the template, added to every placement.
        float _footLift;
        /// <summary>True when the source materials were lost and the figures are plain UnitLit.</summary>
        bool _untextured;

        /// <summary>The crowd's colour buckets, for tinting untextured figures per clone.</summary>
        static readonly string[] TintHex =
        {
            "#E4544A", "#4E8FE0", "#F0C24A", "#EDEFF3", "#3FBF7A", "#C48CFF",
        };

        // Pool slot: one instantiated model and which agent it is standing in for.
        struct Slot
        {
            public GameObject Go;
            public Animation Anim;
            public int Agent;       // −1 when parked
            public byte MoodShown;  // last mood we picked a clip for
        }
        Slot[] _civilians;
        readonly List<GameObject> _soldiers = new List<GameObject>();

        float _retarget;
        public bool Ready { get; private set; }
        public int ActiveSkins { get; private set; }
        public float Scale => _scale;
        /// <summary>Worst |bounds.min.y| across active skins — the "feet on the ground" number.</summary>
        public float WorstMinY { get; private set; }

        /// <summary>
        /// The shader actually on the first model's renderer. Magenta figures mean a stripped
        /// shader, and the only way to know *which* material survived onto the mesh is to ask it.
        /// </summary>
        public string FirstShaderName { get; private set; } = "";
        public int SoldierCount => _soldiers.Count;

        public void Init(GameState state, CrowdSystem crowd)
        {
            Instance = this;
            _state = state;
            _crowd = crowd;
            LoadModels();
        }

        async void LoadModels()
        {
            // The cute chunky KayKit figures, by request — the rogue reads as a hooded townsman,
            // the knight as the garrison. The infantry pair stays on disk as a fallback.
            _civilianTemplate = await LoadTemplate("kaykit_rogue.glb");
            _soldierTemplate = await LoadTemplate("kaykit_knight.glb");
            if (_civilianTemplate == null) _civilianTemplate = await LoadTemplate("infantry_male_b.glb");
            if (_soldierTemplate == null) _soldierTemplate = await LoadTemplate("infantry_male_a.glb");
            if (_civilianTemplate == null) return;   // logged inside; instanced crowd carries on

            _civilians = new Slot[SkinnedCap];
            for (int i = 0; i < SkinnedCap; i++)
            {
                var go = Instantiate(_civilianTemplate, transform);
                go.SetActive(false);

                // Untextured figures get a crowd-bucket tint per clone, so the street keeps the
                // same colour language whether a person is a box or a model.
                if (_untextured)
                {
                    ColorUtility.TryParseHtmlString(TintHex[i % TintHex.Length], out var tint);
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", tint);
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                        r.SetPropertyBlock(block);
                }

                _civilians[i] = new Slot { Go = go, Anim = go.GetComponentInChildren<Animation>(), Agent = -1 };
            }
            Ready = true;
        }

        async System.Threading.Tasks.Task<GameObject> LoadTemplate(string file)
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Models/units", file);
            var gltf = new GLTFast.GltfImport();
            var settings = new GLTFast.ImportSettings
            {
                // Legacy clips are the whole point: they play by name through an Animation
                // component with no controller asset, which is the only animation route open to
                // a project that builds everything at runtime.
                AnimationMethod = GLTFast.AnimationMethod.Legacy,
            };

            bool ok = await gltf.Load(path, settings);
            if (!ok) { Debug.LogError($"[CrowdSkins] yüklenemedi: {path}"); return null; }

            var root = new GameObject(file);
            root.transform.SetParent(transform, false);
            var instantiator = new GLTFast.GameObjectInstantiator(gltf, root.transform);
            ok = await gltf.InstantiateMainSceneAsync(instantiator);
            if (!ok) { Debug.LogError($"[CrowdSkins] kurulamadı: {path}"); Destroy(root); return null; }

            // The clips, wired to an Animation component if the instantiator did not already.
            var anim = root.GetComponentInChildren<Animation>();
            if (anim == null) anim = root.AddComponent<Animation>();
            var clips = gltf.GetAnimationClips();
            if (clips != null)
                foreach (var clip in clips)
                {
                    clip.legacy = true;
                    clip.wrapMode = WrapMode.Loop;
                    if (anim.GetClip(clip.name) == null) anim.AddClip(clip, clip.name);
                }

            // Materials: swap EVERY slot onto UnitLit (URP/Lit from Resources). glTFast's own
            // shaders are stripped from the build — in the player its material generator fails
            // and leaves the slots null, which renders as flat magenta. So null is not skipped,
            // null is exactly the case this swap exists for: those slots get a plain UnitLit and
            // the clone later gets a crowd-bucket tint, keeping the colour coding the instanced
            // figures already had. A slot that did survive keeps its texture and tint.
            var unitLit = Resources.Load<Material>("UnitLit");
            if (unitLit != null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var src = mats[i];
                        var m = new Material(unitLit) { name = (src != null ? src.name : "boş") + " (unit)" };
                        if (src != null)
                        {
                            if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null)
                                m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                            else if (src.mainTexture != null)
                                m.SetTexture("_BaseMap", src.mainTexture);
                            if (src.HasProperty("_BaseColor"))
                                m.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                        }
                        else
                        {
                            _untextured = true;
                        }
                        mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                }
            }

            // Read back AFTER the swap, so the report shows what is actually on the mesh.
            var firstR = root.GetComponentInChildren<Renderer>();
            FirstShaderName = firstR == null ? "(renderer yok)"
                : firstR.sharedMaterial == null ? "(malzeme yok)"
                : firstR.sharedMaterial.shader.name;

            // Normalise the height once, on the template; every clone inherits it. The foot
            // lift compensates a pivot that sits above the soles — without it the whole crowd
            // stands ankle-deep in the pavement (measured −0.22 on the KayKit rig).
            var bounds = MeasureBounds(root);
            if (bounds.size.y > 0.01f)
            {
                _scale = TargetHeight / bounds.size.y;
                root.transform.localScale = Vector3.one * _scale;
                _footLift = -(bounds.min.y - root.transform.position.y) * _scale;
            }

            root.SetActive(false);
            return root;
        }

        static Bounds MeasureBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        void Update()
        {
            if (!Ready || _crowd == null) return;

            _retarget -= Time.deltaTime;
            if (_retarget <= 0f)
            {
                _retarget = 0.5f;    // reassignment cadence — hysteresis against popping
                Retarget();
                PlaceSoldiers();
            }

            // Every frame: the claimed agents move, so their models move with them.
            ActiveSkins = 0;
            float worst = 0f;
            for (int s = 0; s < _civilians.Length; s++)
            {
                ref var slot = ref _civilians[s];
                if (slot.Agent < 0) continue;

                var a = _crowd.AgentAt(slot.Agent);
                if (a.Kind != 0 || a.Pos.y < -1f) { Park(ref slot); continue; }

                var pos = new Vector3(a.Pos.x, a.Pos.y + _footLift, a.Pos.z);
                slot.Go.transform.position = pos;

                var face = new Vector3(a.Target.x - a.Pos.x, 0, a.Target.z - a.Pos.z);
                if (face.sqrMagnitude > 0.01f)
                    slot.Go.transform.rotation = Quaternion.LookRotation(face);

                if (slot.MoodShown != a.Mood && slot.Anim != null)
                {
                    slot.MoodShown = a.Mood;
                    // Walking walks; marching also walks (with the banner beside it); idle
                    // figures mostly stand, but one in four waves or chats, which is the small
                    // life the design wants on a street corner.
                    string clip = Resolve(slot.Anim, a.Mood == 1
                        ? (slot.Agent % 4 == 0 ? ClipWave : slot.Agent % 4 == 1 ? ClipInteract : ClipIdle)
                        : ClipWalk);
                    if (clip != null) slot.Anim.CrossFade(clip, 0.2f);
                }

                ActiveSkins++;
                float minY = MeasureBounds(slot.Go).min.y;
                if (Mathf.Abs(minY) > Mathf.Abs(worst)) worst = minY;
            }
            WorstMinY = worst;
        }

        void Park(ref Slot slot)
        {
            if (slot.Agent >= 0) _crowd.SetSkinClaim(slot.Agent, false);
            slot.Agent = -1;
            slot.MoodShown = 255;
            slot.Go.SetActive(false);
        }

        /// <summary>
        /// Give the models to the pedestrians nearest the camera. Everything else keeps the
        /// instanced figure. Claims are released before they are retaken, so an agent is never
        /// drawn twice and never not at all.
        /// </summary>
        void Retarget()
        {
            var focus = IsoCamera.Instance != null ? IsoCamera.Instance.Focus : Vector3.zero;

            // All visible pedestrians by distance to the focus point.
            var candidates = new List<(float d, int i)>(256);
            for (int i = 0; i < _crowd.LiveAgents; i++)
            {
                var a = _crowd.AgentAt(i);
                if (a.Kind != 0 || a.Pos.y < -1f) continue;
                float dx = a.Pos.x - focus.x, dz = a.Pos.z - focus.z;
                candidates.Add((dx * dx + dz * dz, i));
            }
            candidates.Sort((x, y) => x.d.CompareTo(y.d));

            int want = Mathf.Min(SkinnedCap, candidates.Count);
            var chosen = new HashSet<int>();
            for (int k = 0; k < want; k++) chosen.Add(candidates[k].i);

            // Keep slots whose agent is still chosen; park the rest; hand free slots to the new.
            for (int s = 0; s < _civilians.Length; s++)
                if (_civilians[s].Agent >= 0 && !chosen.Remove(_civilians[s].Agent))
                    Park(ref _civilians[s]);

            foreach (int agent in chosen)
            {
                for (int s = 0; s < _civilians.Length; s++)
                {
                    if (_civilians[s].Agent >= 0) continue;
                    _civilians[s].Agent = agent;
                    _civilians[s].MoodShown = 255;
                    _civilians[s].Go.SetActive(true);
                    _crowd.SetSkinClaim(agent, true);
                    break;
                }
            }
        }

        /// <summary>
        /// Garrison guards: soldier models standing at the barracks, count following garrison
        /// strength. Pure presentation — the garrison number is the simulation, these are what
        /// it looks like from the air.
        /// </summary>
        void PlaceSoldiers()
        {
            if (_soldierTemplate == null || _state == null) return;

            var posts = new List<Vector3>(4);
            foreach (var b in _state.Buildings)
                if (b.Def.Id == "kisla")
                    posts.Add(CityGrid.World(b.Tile.x, b.Tile.y));
            if (posts.Count == 0) { foreach (var s in _soldiers) s.SetActive(false); return; }

            int want = Mathf.Clamp(Mathf.RoundToInt(_state.Garrison / 8f), 2, SoldierCap);
            while (_soldiers.Count < want)
            {
                var go = Instantiate(_soldierTemplate, transform);
                if (_untextured)
                {
                    // A garrison is a uniform; khaki, not a crowd colour.
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", new Color(0.45f, 0.48f, 0.38f));
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                        r.SetPropertyBlock(block);
                }
                var anim = go.GetComponentInChildren<Animation>();
                string guard = anim != null ? Resolve(anim, ClipGuard) : null;
                if (guard != null) { go.SetActive(true); anim.Play(guard); }
                else go.SetActive(true);
                _soldiers.Add(go);
            }

            for (int i = 0; i < _soldiers.Count; i++)
            {
                bool on = i < want;
                _soldiers[i].SetActive(on);
                if (!on) continue;

                var post = posts[i % posts.Count];
                float angle = (i / (float)SoldierCap) * Mathf.PI * 2f;
                _soldiers[i].transform.position = post + new Vector3(Mathf.Cos(angle) * 2.4f, _footLift, Mathf.Sin(angle) * 2.4f);
                _soldiers[i].transform.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg + 180f, 0);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
