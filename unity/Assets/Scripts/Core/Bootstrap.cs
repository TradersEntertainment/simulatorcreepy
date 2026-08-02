// Bootstrap builds the entire game at runtime: camera, light, grid, city, simulation, HUD.
//
// The build settings hold exactly one empty scene and this component sits in it. CLAUDE.md
// forbids hand-authored scenes and prefabs — .unity files are GUID-referenced YAML and
// generating them as text is brittle — so everything the player sees is assembled here, in
// an order that is easy to read and easy to reorder.

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Mesruiyet.Sim;
using Mesruiyet.UI;
using Mesruiyet.World;

namespace Mesruiyet.Core
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent()
        {
            // Belt and braces: if the scene somehow ships without the component, put it back.
            if (FindFirstObjectByType<Bootstrap>() == null)
                new GameObject("Bootstrap").AddComponent<Bootstrap>();
        }

        void Awake()
        {
            // The agent drives an unfocused window; without this the player pauses the moment
            // the loop script takes focus and every bridge command times out.
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var state = GameState.NewGame();
            GameState.Current = state;

            var grid = CityGrid.Generate();
            CityGrid.Current = grid;
            grid.SeedStartingCity(state);

            var camera = BuildCamera();
            BuildLighting();

            var world = new GameObject("City");
            world.transform.SetParent(transform, false);
            var renderer = world.AddComponent<CityRenderer>();
            renderer.Build(grid, state);

            // The crowd shares the city material so it needs the renderer to exist first.
            var crowd = world.AddComponent<CrowdSystem>();
            crowd.Init(state, grid, renderer.Material);

            // Ministers before the resolver: TurnResolver.Init recomputes immediately, and
            // transparency has to exist before the first figure is reported.
            var ministers = gameObject.AddComponent<MinisterManager>();
            ministers.Init(state);

            // Governance before the resolver too: law modifiers scale the very first survey.
            var governance = gameObject.AddComponent<GovernanceManager>();
            governance.Init(state);

            var outside = gameObject.AddComponent<OutsideWorld>();
            outside.Init(state);

            var collapse = gameObject.AddComponent<CollapseWatcher>();
            collapse.Init(state);

            var resolver = gameObject.AddComponent<TurnResolver>();
            resolver.Init(state);

            var placement = gameObject.AddComponent<Placement>();
            placement.Init(grid, state, camera);

            var hud = BuildHud(state);

            // The agent plays through the same UI a human does — no private back door.
            Agent.AgentHooks.Bind(state, grid, hud.rootVisualElement);

            // The city has to keep looking like what it is: crowds re-read their districts and
            // the walls re-grow their politics after every tick.
            Sim.TurnResolver.TurnCompleted += () =>
            {
                crowd.Repopulate();
                crowd.SyncMoods();
                renderer.RefreshIdeology();
                SeasonLight(state);
            };
            crowd.SyncMoods();
            SeasonLight(state);

            Debug.Log($"[Bootstrap] şehir kuruldu · {state.Buildings.Count} yapı · {state.Population} nüfus");
            CheckChains(state);
        }

        /// <summary>
        /// A founding city with a dead chain stage is a bug, not a challenge. This used to fail
        /// silently — the quarry landed outside every district, so the material chain started
        /// stopped and nothing said so until a state dump was read by hand.
        /// </summary>
        static void CheckChains(GameState state)
        {
            foreach (var chain in state.Chains)
            {
                chain.Survey(state);
                foreach (var stage in chain.Stages)
                {
                    if (stage.Throughput > 0.01f) continue;
                    Debug.LogError($"[Bootstrap] {chain.Def.Name} zinciri kurulamadı: " +
                                   $"'{stage.Name}' aşamasını çalıştıran yapı yok " +
                                   $"({stage.Def.BuildingId}).");
                }
            }
        }

        static Light _sun;

        /// <summary>The light turns with the year: pale in winter, heavy and gold in autumn.</summary>
        static void SeasonLight(GameState state)
        {
            if (_sun == null) return;

            switch (state.Season)
            {
                case 0: _sun.color = UiKit.Hex("#FFE8C4"); _sun.intensity = 1.30f; break;   // ilkbahar
                case 1: _sun.color = UiKit.Hex("#FFF0CE"); _sun.intensity = 1.45f; break;   // yaz
                case 2: _sun.color = UiKit.Hex("#FFD79A"); _sun.intensity = 1.22f; break;   // sonbahar
                default: _sun.color = UiKit.Hex("#D8E4F2"); _sun.intensity = 1.02f; break;  // kış
            }
        }

        Camera BuildCamera()
        {
            var go = new GameObject("IsoCamera");
            go.transform.SetParent(transform, false);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Bg;
            cam.allowHDR = true;
            cam.tag = "MainCamera";

            go.AddComponent<IsoCamera>().Init(cam);
            BuildPostFx(cam);
            return cam;
        }

        /// <summary>
        /// The vignette the design asks for: corners pulled down so the city reads as an object
        /// floating in space rather than a texture filling a rectangle. Built as a runtime URP
        /// volume — no profile asset to author, nothing to keep in sync.
        ///
        /// Bloom is deliberately faint. The lit windows are emissive quads and a heavy bloom
        /// would smear the flat colours the whole art direction depends on.
        /// </summary>
        void BuildPostFx(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;

            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(UiKit.Hex("#06090E"));

            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.intensity.Override(0.38f);
            bloom.threshold.Override(0.92f);
            bloom.scatter.Override(0.62f);

            var go = new GameObject("PostFx");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.profile = profile;
        }

        void BuildLighting()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);

            var light = go.AddComponent<Light>();
            _sun = light;
            light.type = LightType.Directional;
            // Low and warm: long shadows across the grid are what give the toy city its depth.
            light.transform.rotation = Quaternion.Euler(38f, -128f, 0f);
            light.color = UiKit.Hex("#FFE2B8");
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.88f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = UiKit.Hex("#3E4B63");
            RenderSettings.ambientEquatorColor = UiKit.Hex("#252D3C");
            RenderSettings.ambientGroundColor = UiKit.Hex("#12161E");

            // No distance fog. Under an orthographic camera every pixel's depth is a linear
            // function of screen position, so linear fog paints a hard diagonal band straight
            // across the city instead of a haze. The dark apron and the vignette do that job.
            RenderSettings.fog = false;

            // The orthographic camera sits 260 units back, so URP's default 50 unit shadow
            // distance would put the entire city outside the shadow cascade and the skyline
            // would look flat and pasted-on. Shadows are most of the low-poly read.
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 480f;
                urp.shadowCascadeCount = 4;
            }
        }

        UIDocument BuildHud(GameState state)
        {
            // Built inactive so UIDocument.OnEnable sees its PanelSettings and has a live
            // rootVisualElement by the time Hud.Init runs.
            var go = new GameObject("Hud");
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            var doc = go.AddComponent<UIDocument>();
            var settings = Resources.Load<PanelSettings>("MesruiyetPanel");
            if (settings == null)
                Debug.LogError("[Bootstrap] Resources/MesruiyetPanel bulunamadı — " +
                               "Editor/ProjectSetup.Setup çalıştırılmamış.");
            doc.panelSettings = settings;
            doc.sortingOrder = 10;

            var hud = go.AddComponent<Hud>();
            go.SetActive(true);

            hud.Init(state, doc);
            return doc;
        }
    }
}





