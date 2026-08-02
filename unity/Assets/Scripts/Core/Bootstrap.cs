// Bootstrap builds the entire game at runtime: camera, light, grid, city, simulation, HUD.
//
// The build settings hold exactly one empty scene and this component sits in it. CLAUDE.md
// forbids hand-authored scenes and prefabs — .unity files are GUID-referenced YAML and
// generating them as text is brittle — so everything the player sees is assembled here, in
// an order that is easy to read and easy to reorder.

using UnityEngine;
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
            world.AddComponent<CityRenderer>().Build(grid, state);

            // Ministers before the resolver: TurnResolver.Init recomputes immediately, and
            // transparency has to exist before the first figure is reported.
            var ministers = gameObject.AddComponent<MinisterManager>();
            ministers.Init(state);

            var resolver = gameObject.AddComponent<TurnResolver>();
            resolver.Init(state);

            var placement = gameObject.AddComponent<Placement>();
            placement.Init(grid, state, camera);

            var hud = BuildHud(state);

            // The agent plays through the same UI a human does — no private back door.
            Agent.AgentHooks.Bind(state, grid, hud.rootVisualElement);

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
            return cam;
        }

        void BuildLighting()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);

            var light = go.AddComponent<Light>();
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
