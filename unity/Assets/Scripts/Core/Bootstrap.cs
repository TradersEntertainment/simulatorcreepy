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
            // This attribute fires once per application start, not once per scene load. Starting
            // a new term reloads the scene, and the scene in Build Settings is deliberately empty
            // — the world is assembled here — so without the sceneLoaded hook a restart would
            // come back to an empty frame.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                  UnityEngine.SceneManagement.LoadSceneMode mode) => Spawn();

        static void Spawn()
        {
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

            // Synthesized at startup; no audio file ships with the project. Bound to the true
            // state, because the drone and the crowd are not reports.
            var audio = gameObject.AddComponent<AudioBus>();
            audio.Bind(state);

            // Skinned characters for whoever is nearest the camera; the instanced crowd for
            // everyone else. Loads its models asynchronously and degrades to nothing if it cannot.
            var skins = world.AddComponent<CrowdSkins>();
            skins.Init(state, crowd);

            // The player's own 3D: real models for konut and tapınak, loaded from
            // StreamingAssets, replacing their procedural forms the moment they are ready.
            var models = world.AddComponent<BuildingModels>();
            models.Init(state);

#if !UNITY_WEBGL
            // The lobby door, and the governor-side co-op wiring. Both idle until a
            // connection exists; single player never notices them.
            gameObject.AddComponent<Net.NetManager>();
            var coop = gameObject.AddComponent<Net.CoopTurnController>();
            coop.Init(state);
#endif

            var hud = BuildHud(state);

            // The agent plays through the same UI a human does — no private back door.
            Agent.AgentHooks.Bind(state, grid, hud.rootVisualElement);

            // The city has to keep looking like what it is: crowds re-read their districts and
            // the walls re-grow their politics after every tick.
            //
            // Held in a field rather than written inline, because TurnCompleted is static and
            // outlives the scene: restarting a term used to leave this closure subscribed to the
            // previous city, and the first tick of the new one wrote into a NativeArray that had
            // already been disposed. The unsubscribe in OnDestroy is the whole point.
            _afterTick = () =>
            {
                if (crowd == null || renderer == null) return;
                crowd.Repopulate();
                crowd.SyncMoods();
                renderer.RefreshIdeology();
                SeasonLight(state);
            };
            Sim.TurnResolver.TurnCompleted += _afterTick;
            crowd.SyncMoods();
            SeasonLight(state);

            Debug.Log($"[Bootstrap] şehir kuruldu · {state.Buildings.Count} yapı · {state.Population} nüfus");
            CheckChains(state);
        }

        System.Action _afterTick;

        void OnDestroy()
        {
            if (_afterTick != null) Sim.TurnResolver.TurnCompleted -= _afterTick;
            _afterTick = null;
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

        /// <summary>
        /// The light turns with the year. Colour and intensity were already doing something; the
        /// sun's *angle* was not, and it is the one that carries. A low winter sun rakes shadows
        /// half a district long across the grid and a high summer one pulls them in under the
        /// eaves — from an orthographic camera that shadow length is most of what tells you how
        /// tall a building is, so the season now changes the shape of the city, not just its tint.
        /// </summary>
        static void SeasonLight(GameState state)
        {
            if (_sun == null) return;

            Color sky, ground;
            float elevation, azimuth;

            switch (state.Season)
            {
                case 0:  // ilkbahar — clean and high-ish
                    _sun.color = UiKit.Hex("#FFE8C4"); _sun.intensity = 1.30f;
                    elevation = 50f; azimuth = -128f;
                    sky = UiKit.Hex("#44536E"); ground = UiKit.Hex("#141A24");
                    break;
                case 1:  // yaz — highest sun, shortest shadows, palest shade
                    _sun.color = UiKit.Hex("#FFF0CE"); _sun.intensity = 1.48f;
                    elevation = 58f; azimuth = -120f;
                    sky = UiKit.Hex("#4C5E7C"); ground = UiKit.Hex("#171E29");
                    break;
                case 2:  // sonbahar — low and gold, the long-shadow season
                    _sun.color = UiKit.Hex("#FFC983"); _sun.intensity = 1.24f;
                    elevation = 44f; azimuth = -138f;
                    sky = UiKit.Hex("#54483F"); ground = UiKit.Hex("#1C1712");
                    break;
                default: // kış — lowest and coldest; the city leans on its own lit windows
                    _sun.color = UiKit.Hex("#CFE0F4"); _sun.intensity = 0.92f;
                    elevation = 40f; azimuth = -146f;
                    sky = UiKit.Hex("#38465F"); ground = UiKit.Hex("#0E1219");
                    break;
            }

            _sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, ground, 0.55f);
            RenderSettings.ambientGroundColor = ground;
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

            // 0.42 was the "muddy smears". At gameplay zoom the grass reaches the screen
            // corners, and a vignette that strong reads not as framing but as irregular dark
            // masses lying on the map — chased for a whole day as shadows, smoke and cascade
            // boundaries before a pixel scan pinned it to the screen's corners. Kept, but as a
            // whisper: the dark apron around the city already does the framing job.
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.75f);
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
            light.shadowStrength = 0.5f;

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
                // The camera sits 260 units back, so the ground plane lives at roughly 260–420
                // of camera depth and the distance has to reach past it — but 480 spread over
                // four cascades put the city in a coarse far cascade, and the muddy dark smears
                // all over the screenshots were tree shadows at a fraction of the resolution
                // they needed. Two cascades split at 60% puts everything the player sees in the
                // sharp near half, and the shadowmap itself is raised to 4096.
                // ONE cascade. With two, the split fell across the middle of the map — the near
                // half drew crisp tree shadows and the far half drew the same shadows as soft
                // dark masses, which is exactly the "sharp here, muddy there" gradient in the
                // screenshots. One cascade at 4096 over 460 units is ~0.22 units per texel,
                // sharp everywhere, and there is no boundary to fall across.
                urp.shadowDistance = 460f;
                urp.shadowCascadeCount = 1;
                var res = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
                    .GetProperty("mainLightShadowmapResolution");
                if (res != null && res.CanWrite) res.SetValue(urp, 4096);
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





